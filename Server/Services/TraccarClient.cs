using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using StallmedManager.Shared.Models;

namespace StallmedManager.Server.Services
{
    // Ο Traccar δεν απάντησε (κάτω, timeout, λάθος credentials). Ο controller το
    // γυρίζει σε 503 ώστε ο client να δείξει "Traccar μη διαθέσιμος" -- όχι κενή λίστα.
    public class TraccarUnavailableException : Exception
    {
        public TraccarUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
    }

    // ---- Client του υπάρχοντος Traccar (130.162.251.146:8082) ----
    // ΜΟΝΟ ανάγνωση μέσω του REST API του Traccar: δεν αλλάζει συσκευές,
    // listeners ή ρυθμίσεις. Τα credentials ζουν στο appsettings.Tracking.json
    // του server και δεν φτάνουν ποτέ στον browser ούτε στα logs.
    public class TraccarClient
    {
        private readonly HttpClient _http;
        private readonly IMemoryCache _cache;
        private readonly ILogger<TraccarClient> _logger;
        private readonly bool _configured;

        public TraccarClient(HttpClient http, IConfiguration config, IMemoryCache cache, ILogger<TraccarClient> logger)
        {
            _http = http;
            _cache = cache;
            _logger = logger;

            var section = config.GetSection("Traccar");
            var baseUrl = section["BaseUrl"];
            var token = section["Token"];
            var username = section["Username"];
            var password = section["Password"];
            _configured = !string.IsNullOrWhiteSpace(baseUrl) &&
                (!string.IsNullOrWhiteSpace(token) || !string.IsNullOrWhiteSpace(username));
            if (!_configured) return;

            _http.BaseAddress = new Uri(baseUrl!.TrimEnd('/') + "/");
            _http.Timeout = TimeSpan.FromSeconds(int.TryParse(section["TimeoutSeconds"], out var t) && t > 0 ? t : 15);
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            // Token (Traccar: Settings → Token) προτιμάται· αλλιώς Basic με χρήστη Traccar.
            _http.DefaultRequestHeaders.Authorization = !string.IsNullOrWhiteSpace(token)
                ? new AuthenticationHeaderValue("Bearer", token)
                : new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));
        }

        // Συσκευές: αλλάζουν σπάνια -- cache 30''.
        public Task<List<JsonElement>> GetDevicesAsync() =>
            GetCachedAsync("traccar:devices", TimeSpan.FromSeconds(30), "api/devices");

        // Τελευταία θέση κάθε συσκευής: cache 5'' ώστε όλοι οι χρήστες που κάνουν
        // polling την ίδια στιγμή να μοιράζονται μία κλήση προς τον Traccar.
        public Task<List<JsonElement>> GetLatestPositionsAsync() =>
            GetCachedAsync("traccar:latest", TimeSpan.FromSeconds(5), "api/positions");

        public Task<List<JsonElement>> GetPositionsAsync(int deviceId, DateTimeOffset from, DateTimeOffset to) =>
            GetListAsync($"api/positions?deviceId={deviceId}&from={Iso(from)}&to={Iso(to)}");

        public Task<List<JsonElement>> GetTripsAsync(int deviceId, DateTimeOffset from, DateTimeOffset to) =>
            GetListAsync($"api/reports/trips?deviceId={deviceId}&from={Iso(from)}&to={Iso(to)}");

        public Task<List<JsonElement>> GetStopsAsync(int deviceId, DateTimeOffset from, DateTimeOffset to) =>
            GetListAsync($"api/reports/stops?deviceId={deviceId}&from={Iso(from)}&to={Iso(to)}");

        public Task<List<JsonElement>> GetEventsAsync(int deviceId, DateTimeOffset from, DateTimeOffset to) =>
            GetListAsync($"api/reports/events?deviceId={deviceId}&from={Iso(from)}&to={Iso(to)}");

        private static string Iso(DateTimeOffset d) =>
            Uri.EscapeDataString(d.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));

        private async Task<List<JsonElement>> GetCachedAsync(string key, TimeSpan ttl, string path)
        {
            if (_cache.TryGetValue(key, out List<JsonElement>? cached) && cached != null)
                return cached;
            var list = await GetListAsync(path);
            _cache.Set(key, list, ttl);
            return list;
        }

        private async Task<List<JsonElement>> GetListAsync(string path)
        {
            if (!_configured)
                throw new TraccarUnavailableException("Ο Traccar δεν έχει ρυθμιστεί (appsettings.Tracking.json).");

            // Στο log μόνο το path (χωρίς query/credentials).
            var logPath = path.Split('?')[0];
            var sw = Stopwatch.StartNew();
            try
            {
                using var response = await _http.GetAsync(path);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Traccar {Path} απάντησε {Status} σε {Ms} ms", logPath, (int)response.StatusCode, sw.ElapsedMilliseconds);
                    if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                        throw new TraccarUnavailableException("Ο Traccar απέρριψε τα στοιχεία σύνδεσης του site.");
                    throw new TraccarUnavailableException($"Ο Traccar απάντησε με σφάλμα {(int)response.StatusCode}.");
                }

                await using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogWarning("Traccar {Path}: απάντηση που δεν είναι λίστα", logPath);
                    return new List<JsonElement>();
                }
                // Clone: τα στοιχεία ζουν μετά το Dispose του JsonDocument (και στην cache).
                return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
            }
            catch (TraccarUnavailableException) { throw; }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning("Traccar {Path}: timeout μετά από {Ms} ms", logPath, sw.ElapsedMilliseconds);
                throw new TraccarUnavailableException("Ο Traccar δεν απάντησε εγκαίρως.", ex);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("Traccar {Path}: αποτυχία σύνδεσης ({Error})", logPath, ex.Message);
                throw new TraccarUnavailableException("Δεν ήταν δυνατή η σύνδεση με τον Traccar.", ex);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning("Traccar {Path}: μη έγκυρο JSON ({Error})", logPath, ex.Message);
                throw new TraccarUnavailableException("Ο Traccar έστειλε μη αναγνώσιμα δεδομένα.", ex);
            }
        }
    }

    // ---- Μετατροπή JSON του Traccar σε DTOs του site ----
    // Κανόνας: ό,τι λείπει ή δεν διαβάζεται γίνεται null (άγνωστο), όχι 0.
    public static class TraccarMapper
    {
        private const double KnotsToKmh = 1.852;

        // Αισθητήρες θερμοκρασίας φορτίου (Teltonika temp1-4 / BLE, κ.ά.).
        // Το deviceTemp (εσωτερική θερμοκρασία tracker) δεν είναι cold-chain.
        private static readonly Regex TempKey = new(@"^((temp|bleTemp)\d+|temperature\d*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool IsTempKey(string key) => TempKey.IsMatch(key);

        // Εκτός φυσικών ορίων = σφάλμα αισθητήρα (π.χ. Teltonika 850 / -1000 / 3000).
        public static bool IsPlausibleTemp(double v) => v >= -60 && v <= 125;

        public static int? Int(JsonElement e, string name) =>
            e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var v) ? v : null;

        public static long? Long(JsonElement e, string name) =>
            e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var v) ? v : null;

        public static double? Double(JsonElement e, string name)
        {
            if (!e.TryGetProperty(name, out var p)) return null;
            if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var v)) return v;
            if (p.ValueKind == JsonValueKind.String &&
                double.TryParse(p.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s)) return s;
            return null;
        }

        public static bool? Bool(JsonElement e, string name)
        {
            if (!e.TryGetProperty(name, out var p)) return null;
            return p.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(p.GetString(), out var b) => b,
                _ => null
            };
        }

        public static string? Str(JsonElement e, string name) =>
            e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

        public static DateTimeOffset? Time(JsonElement e, string name)
        {
            var s = Str(e, name);
            return s != null && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d)
                ? d.ToUniversalTime() : null;
        }

        public static Dictionary<string, string> Attributes(JsonElement e, bool includeRaw = false)
        {
            var dict = new Dictionary<string, string>();
            if (!e.TryGetProperty("attributes", out var attrs) || attrs.ValueKind != JsonValueKind.Object)
                return dict;
            foreach (var p in attrs.EnumerateObject())
            {
                if (!includeRaw && p.Name == "raw") continue;
                dict[p.Name] = p.Value.ValueKind switch
                {
                    JsonValueKind.String => p.Value.GetString() ?? "",
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => p.Value.GetRawText()
                };
            }
            return dict;
        }

        public static TrackingPositionDto Position(JsonElement e)
        {
            var attrs = e.TryGetProperty("attributes", out var a) && a.ValueKind == JsonValueKind.Object ? a : default;
            bool hasAttrs = attrs.ValueKind == JsonValueKind.Object;
            var dto = new TrackingPositionDto
            {
                Id = Long(e, "id") ?? 0,
                DeviceId = Int(e, "deviceId") ?? 0,
                Protocol = Str(e, "protocol"),
                ServerTime = Time(e, "serverTime"),
                DeviceTime = Time(e, "deviceTime"),
                FixTime = Time(e, "fixTime"),
                Valid = Bool(e, "valid") ?? false,
                Outdated = Bool(e, "outdated") ?? false,
                Latitude = Double(e, "latitude") ?? 0,
                Longitude = Double(e, "longitude") ?? 0,
                Altitude = Double(e, "altitude"),
                SpeedKmh = Double(e, "speed") is double kn ? Math.Round(kn * KnotsToKmh, 1) : null,
                Course = Double(e, "course"),
                Address = Str(e, "address"),
                Accuracy = Double(e, "accuracy"),
                Attributes = Attributes(e)
            };
            if (!hasAttrs) return dto;

            dto.Ignition = Bool(attrs, "ignition");
            dto.Motion = Bool(attrs, "motion");
            dto.PowerV = Double(attrs, "power");
            dto.BatteryV = Double(attrs, "battery");
            dto.BatteryLevel = Double(attrs, "batteryLevel");
            dto.Rssi = Double(attrs, "rssi") is double r ? (int)r : null;
            dto.Satellites = Double(attrs, "sat") is double s ? (int)s : null;
            dto.DeviceTemp = Double(attrs, "deviceTemp");
            dto.OdometerKm = Double(attrs, "totalDistance") is double m ? Math.Round(m / 1000, 1) : null;
            dto.Alarm = Str(attrs, "alarm");

            foreach (var p in attrs.EnumerateObject().Where(p => IsTempKey(p.Name)))
            {
                var value = Double(attrs, p.Name);
                var reading = new TrackingTempReading { Key = p.Name, RawValue = p.Value.ToString() };
                if (value is double v && IsPlausibleTemp(v)) reading.Value = Math.Round(v, 2);
                else reading.Malformed = true;
                dto.Temperatures.Add(reading);
            }
            return dto;
        }

        public static TrackingDeviceDto Device(JsonElement e) => new()
        {
            Id = Int(e, "id") ?? 0,
            Name = Str(e, "name") ?? "",
            UniqueId = Str(e, "uniqueId") ?? "",
            Model = Str(e, "model"),
            Category = Str(e, "category"),
            Phone = Str(e, "phone"),
            Status = Str(e, "status") ?? "unknown",
            LastUpdate = Time(e, "lastUpdate"),
            Disabled = Bool(e, "disabled") ?? false
        };

        public static TrackingTripDto Trip(JsonElement e) => new()
        {
            StartTime = Time(e, "startTime"),
            EndTime = Time(e, "endTime"),
            StartAddress = Str(e, "startAddress"),
            EndAddress = Str(e, "endAddress"),
            StartLat = Double(e, "startLat"),
            StartLon = Double(e, "startLon"),
            EndLat = Double(e, "endLat"),
            EndLon = Double(e, "endLon"),
            DurationMs = Long(e, "duration"),
            DistanceKm = Double(e, "distance") is double d ? Math.Round(d / 1000, 2) : null,
            MaxSpeedKmh = Double(e, "maxSpeed") is double mx ? Math.Round(mx * KnotsToKmh, 1) : null,
            AverageSpeedKmh = Double(e, "averageSpeed") is double av ? Math.Round(av * KnotsToKmh, 1) : null,
            DriverName = Str(e, "driverName")
        };

        public static TrackingStopDto Stop(JsonElement e) => new()
        {
            StartTime = Time(e, "startTime"),
            EndTime = Time(e, "endTime"),
            DurationMs = Long(e, "duration"),
            Latitude = Double(e, "latitude"),
            Longitude = Double(e, "longitude"),
            Address = Str(e, "address")
        };

        public static TrackingEventDto Event(JsonElement e)
        {
            var attrs = Attributes(e);
            return new TrackingEventDto
            {
                Id = Long(e, "id") ?? 0,
                Type = Str(e, "type") ?? "",
                EventTime = Time(e, "eventTime"),
                DeviceId = Int(e, "deviceId") ?? 0,
                PositionId = Long(e, "positionId") is long pid && pid > 0 ? pid : null,
                GeofenceId = Long(e, "geofenceId") is long gid && gid > 0 ? gid : null,
                Alarm = attrs.TryGetValue("alarm", out var al) ? al : null,
                Attributes = attrs
            };
        }
    }
}
