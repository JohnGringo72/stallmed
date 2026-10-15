using System.Globalization;
using StallmedManager.Shared.Models;

namespace StallmedManager.Client
{
    // ---- Παρακολούθηση Συσκευών: κλήσεις προς /api/tracking (ο server μιλά με τον Traccar) ----
    public class TrackingService
    {
        private readonly DataService dataService;

        public TrackingService(DataService dataService)
        {
            this.dataService = dataService;
        }

        private static string Q(DateTimeOffset from, DateTimeOffset to) =>
            $"from={Uri.EscapeDataString(from.ToString("o", CultureInfo.InvariantCulture))}&to={Uri.EscapeDataString(to.ToString("o", CultureInfo.InvariantCulture))}";

        public Task<TrackingMeDto?> Me() => dataService.Get<TrackingMeDto?>("api/tracking/me");

        public Task<(TrackingOverviewDto? Data, string? Error)> Overview() =>
            dataService.GetWithError<TrackingOverviewDto>("api/tracking/overview");

        public Task<(TrackingDeviceDto? Data, string? Error)> Device(int id) =>
            dataService.GetWithError<TrackingDeviceDto>($"api/tracking/device/{id}");

        public Task<(TrackingRouteDto? Data, string? Error)> Route(int id, DateTimeOffset from, DateTimeOffset to) =>
            dataService.GetWithError<TrackingRouteDto>($"api/tracking/device/{id}/route?{Q(from, to)}");

        public Task<(TrackingTripsDto? Data, string? Error)> Trips(int id, DateTimeOffset from, DateTimeOffset to) =>
            dataService.GetWithError<TrackingTripsDto>($"api/tracking/device/{id}/trips?{Q(from, to)}");

        public Task<(List<TrackingEventDto>? Data, string? Error)> Events(int id, DateTimeOffset from, DateTimeOffset to) =>
            dataService.GetWithError<List<TrackingEventDto>>($"api/tracking/device/{id}/events?{Q(from, to)}");

        public Task<(TrackingTemperatureDto? Data, string? Error)> Temperature(int id, DateTimeOffset from, DateTimeOffset to) =>
            dataService.GetWithError<TrackingTemperatureDto>($"api/tracking/device/{id}/temperature?{Q(from, to)}");

        public Task<(List<TrackingRawRowDto>? Data, string? Error)> Raw(int id, DateTimeOffset from, DateTimeOffset to) =>
            dataService.GetWithError<List<TrackingRawRowDto>>($"api/tracking/device/{id}/raw?{Q(from, to)}");

        // ---- Διαχείριση (admin) ----
        public Task<(TrackingAdminDto? Data, string? Error)> Admin() =>
            dataService.GetWithError<TrackingAdminDto>("api/tracking/admin");

        public Task<TrackingResult?> SaveAsset(SaveTrackingAssetRequest req) =>
            dataService.Post<SaveTrackingAssetRequest, TrackingResult?>("api/tracking/admin/asset", req);

        public Task<TrackingResult?> Link(LinkTrackingDeviceRequest req) =>
            dataService.Post<LinkTrackingDeviceRequest, TrackingResult?>("api/tracking/admin/link", req);

        public Task<TrackingResult?> Unlink(int linkId) =>
            dataService.Post<object, TrackingResult?>($"api/tracking/admin/unlink/{linkId}", new { });

        public Task<TrackingResult?> SaveAccess(SaveTrackingAccessRequest req) =>
            dataService.Post<SaveTrackingAccessRequest, TrackingResult?>("api/tracking/admin/access", req);

        public Task<TrackingResult?> DeleteAccess(int accessId) =>
            dataService.Post<object, TrackingResult?>($"api/tracking/admin/access/delete/{accessId}", new { });

        public Task<TrackingResult?> SaveDefaultRange(TrackingTempRangeDto req) =>
            dataService.Post<TrackingTempRangeDto, TrackingResult?>("api/tracking/admin/default-range", req);
    }

    // ---- Εμφάνιση: ώρα Ελλάδας, μονάδες, ελληνικά ονόματα ----
    public static class TrackingFormat
    {
        // Ώρα Ελλάδας με σωστή θερινή/χειμερινή ώρα (IANA tz του .NET -- όχι χειροκίνητο +2/+3).
        private static readonly TimeZoneInfo Athens = FindAthens();

        private static TimeZoneInfo FindAthens()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Athens"); }
            catch (Exception) { return TimeZoneInfo.Local; }   // ο browser είναι ούτως ή άλλως στην Ελλάδα
        }

        public static DateTime ToLocal(DateTimeOffset utc) => TimeZoneInfo.ConvertTime(utc, Athens).DateTime;

        // Ώρα Ελλάδας (από datetime-local input) → UTC για τον server.
        public static DateTimeOffset FromLocal(DateTime local)
        {
            var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            return new DateTimeOffset(unspecified, Athens.GetUtcOffset(unspecified)).ToUniversalTime();
        }

        public static DateTime NowLocal() => ToLocal(DateTimeOffset.UtcNow);

        public static string Time(DateTimeOffset? utc) =>
            utc.HasValue ? ToLocal(utc.Value).ToString("dd/MM/yyyy HH:mm:ss") : "—";

        public static string ShortTime(DateTimeOffset? utc) =>
            utc.HasValue ? ToLocal(utc.Value).ToString("dd/MM HH:mm") : "—";

        // «πριν από 5'» -- για την τελευταία επικοινωνία.
        public static string Ago(DateTimeOffset? utc)
        {
            if (!utc.HasValue) return "ποτέ";
            var span = DateTimeOffset.UtcNow - utc.Value;
            if (span.TotalSeconds < 60) return "τώρα";
            if (span.TotalMinutes < 60) return $"πριν {(int)span.TotalMinutes}'";
            if (span.TotalHours < 24) return $"πριν {(int)span.TotalHours} ώρ.";
            return $"πριν {(int)span.TotalDays} ημ.";
        }

        public static string Duration(long? ms)
        {
            if (!ms.HasValue) return "—";
            var t = TimeSpan.FromMilliseconds(ms.Value);
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours}ώ {t.Minutes:00}'" : $"{t.Minutes}' {t.Seconds:00}''";
        }

        public static string Num(double? v, string unit, int decimals = 1) =>
            v.HasValue ? v.Value.ToString("N" + decimals, CultureInfo.GetCultureInfo("el-GR")) + unit : "—";

        public static string StatusLabel(string? status) => status switch
        {
            "online" => "Online",
            "offline" => "Offline",
            _ => "Άγνωστο"
        };

        public static string StatusBadge(string? status) => status switch
        {
            "online" => "bg-success",
            "offline" => "bg-secondary",
            _ => "bg-warning text-dark"
        };

        // Κατάσταση για το χρώμα του marker.
        public static string MarkerStatus(TrackingDeviceDto d)
        {
            if (!string.IsNullOrEmpty(d.Position?.Alarm)) return "alarm";
            if (d.Position != null && !d.Position.Valid) return "nofix";
            return d.Status == "online" ? "online" : "offline";
        }

        public static string Ignition(bool? v) => v switch { true => "ON", false => "OFF", _ => "—" };

        // Εκτός ορίων με βάση το εύρος της συσκευής/οχήματος (όχι hardcoded).
        public static bool IsExcursion(double? value, TrackingTempRangeDto? range) =>
            value.HasValue && range != null && (value.Value < (double)range.MinC || value.Value > (double)range.MaxC);

        public static string EventLabel(string type) => type switch
        {
            "ignitionOn" => "Μίζα ON",
            "ignitionOff" => "Μίζα OFF",
            "deviceOnline" => "Συσκευή online",
            "deviceOffline" => "Συσκευή offline",
            "deviceUnknown" => "Κατάσταση άγνωστη",
            "deviceMoving" => "Κίνηση",
            "deviceStopped" => "Στάση",
            "deviceOverspeed" => "Υπερβολική ταχύτητα",
            "geofenceEnter" => "Είσοδος σε geofence",
            "geofenceExit" => "Έξοδος από geofence",
            "alarm" => "Alarm",
            "deviceFuelDrop" => "Πτώση καυσίμου",
            "deviceFuelIncrease" => "Αύξηση καυσίμου",
            "maintenance" => "Συντήρηση",
            "driverChanged" => "Αλλαγή οδηγού",
            "media" => "Media",
            "commandResult" => "Αποτέλεσμα εντολής",
            "textMessage" => "Μήνυμα",
            "queuedCommandSent" => "Εντολή εστάλη",
            "deviceInactive" => "Ανενεργή συσκευή",
            _ => type
        };

        public static string AlarmLabel(string? alarm)
        {
            if (string.IsNullOrEmpty(alarm)) return "";
            // Ο Traccar μπορεί να στείλει πολλά χωρισμένα με κόμμα.
            return string.Join(", ", alarm.Split(',').Select(a => a.Trim() switch
            {
                "powerCut" => "Διακοπή τροφοδοσίας",
                "powerOff" => "Power off",
                "powerRestored" => "Επαναφορά τροφοδοσίας",
                "lowBattery" => "Χαμηλή μπαταρία",
                "lowPower" => "Χαμηλή τάση",
                "overspeed" => "Υπερβολική ταχύτητα",
                "sos" => "SOS",
                "door" => "Πόρτα",
                "temperature" => "Θερμοκρασία",
                "movement" => "Κίνηση",
                "vibration" => "Δόνηση",
                "tampering" => "Παραβίαση",
                "geofenceEnter" => "Είσοδος geofence",
                "geofenceExit" => "Έξοδος geofence",
                "gpsAntennaCut" => "Αποκοπή κεραίας GPS",
                "jamming" => "Jamming",
                "accident" => "Ατύχημα",
                "parking" => "Στάθμευση",
                "removing" => "Αφαίρεση συσκευής",
                var other => other
            }));
        }

        // Φιλικά ονόματα για γνωστά attributes του Traccar· τα άγνωστα εμφανίζονται ως έχουν.
        public static string AttributeLabel(string key) => key switch
        {
            "ignition" => "Μίζα",
            "motion" => "Κίνηση",
            "power" => "Τάση τροφοδοσίας (V)",
            "battery" => "Μπαταρία (V)",
            "batteryLevel" => "Μπαταρία (%)",
            "rssi" => "Σήμα GSM",
            "sat" => "Δορυφόροι",
            "hdop" => "HDOP",
            "deviceTemp" => "Θερμοκρασία συσκευής",
            "totalDistance" => "Συνολική απόσταση (m)",
            "distance" => "Απόσταση από προηγ. (m)",
            "odometer" => "Οδόμετρο",
            "hours" => "Ώρες κινητήρα",
            "alarm" => "Alarm",
            "event" => "Event code",
            "status" => "Status",
            "input" => "Είσοδοι",
            "output" => "Έξοδοι",
            "in1" => "Είσοδος 1",
            "in2" => "Είσοδος 2",
            "in3" => "Είσοδος 3",
            "in4" => "Είσοδος 4",
            "out1" => "Έξοδος 1",
            "out2" => "Έξοδος 2",
            "out3" => "Έξοδος 3",
            "door" => "Πόρτα",
            "fuel" => "Καύσιμο",
            "operator" => "Πάροχος",
            "iccid" => "ICCID",
            "versionFw" => "Firmware",
            "versionHw" => "Hardware",
            "io239" => "Μίζα (io239)",
            "io240" => "Κίνηση (io240)",
            _ => key
        };
    }
}
