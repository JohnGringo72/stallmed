using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StallmedManager.Server.Models;
using StallmedManager.Server.Services;
using StallmedManager.Shared.Models;

namespace StallmedManager.Server.Controllers
{
    // ---- Παρακολούθηση Συσκευών ----
    // Browser → αυτός ο controller → υπάρχων Traccar (TraccarClient).
    // Δεν αποθηκεύει GPS δεδομένα: ό,τι αφορά θέσεις/events/trips διαβάζεται
    // live από τον Traccar. Από τη MySQL (sql/tracking_module.sql) έρχονται μόνο
    // οχήματα, συνδέσεις όχημα↔συσκευή, πρόσβαση χρηστών και όρια θερμοκρασίας.
    // Πρόσβαση: admin του site = όλα· αλλιώς ό,τι ορίζει ο TrackingUserAccess.
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TrackingController : ControllerBase
    {
        // Όρια εύρους ώστε να μην κατεβαίνει ποτέ όλο το ιστορικό.
        private static readonly TimeSpan MaxRouteRange = TimeSpan.FromDays(7);
        private static readonly TimeSpan MaxReportRange = TimeSpan.FromDays(31);
        private static readonly TimeSpan MaxTempRange = TimeSpan.FromDays(14);
        private static readonly TimeSpan MaxRawRange = TimeSpan.FromDays(1);
        private const int MaxRoutePoints = 20000;
        private const int MaxRawRows = 500;

        private readonly StallmedContext _context;
        private readonly TraccarClient _traccar;
        private readonly ILogger<TrackingController> _logger;

        public TrackingController(StallmedContext context, TraccarClient traccar, ILogger<TrackingController> logger)
        {
            _context = context;
            _traccar = traccar;
            _logger = logger;
        }

        // ================= ΠΡΟΣΒΑΣΗ =================

        private class Access
        {
            public bool IsAdmin;
            public bool Technical;
            public HashSet<string> Teams = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<int> AssetIds = new();
            public bool AllDevices => IsAdmin || Technical;
            public bool CanSeeRaw => IsAdmin || Technical;
            public bool CanView => AllDevices || Teams.Count > 0 || AssetIds.Count > 0;
        }

        private async Task<Access> GetAccessAsync()
        {
            var access = new Access
            {
                IsAdmin = User.Claims.Any(c => c.Type == ClaimTypes.Role &&
                    string.Equals(c.Value?.Trim(), "admin", StringComparison.OrdinalIgnoreCase))
            };
            if (access.IsAdmin) return access;

            // Ο χρήστης βρίσκεται από το email του token -- όχι από ό,τι στείλει ο client.
            var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email)) return access;
            var userId = await _context.Users
                .Where(u => u.Email == email && u.Active)
                .Select(u => (int?)u.IdUser)
                .FirstOrDefaultAsync();
            if (userId == null) return access;

            var rows = await _context.TrackingUserAccess.Where(a => a.UserID == userId).ToListAsync();
            foreach (var r in rows)
            {
                if (r.AccessLevel == TrackingAccessLevels.Technical) access.Technical = true;
                else if (r.AccessLevel == TrackingAccessLevels.CompanyAdmin && !string.IsNullOrWhiteSpace(r.Team)) access.Teams.Add(r.Team.Trim());
                else if (r.AccessLevel == TrackingAccessLevels.User && r.AssetID.HasValue) access.AssetIds.Add(r.AssetID.Value);
            }
            if (access.Teams.Count > 0)
            {
                var teamAssets = await _context.TrackingAssets
                    .Where(a => a.IsActive && a.Team != null && access.Teams.Contains(a.Team))
                    .Select(a => a.AssetID).ToListAsync();
                access.AssetIds.UnionWith(teamAssets);
            }
            return access;
        }

        // Ενεργές τώρα συνδέσεις όχημα↔συσκευή (για overview και φιλτράρισμα).
        private async Task<List<TrackingAssetDevice>> CurrentLinksAsync()
        {
            var now = DateTime.UtcNow;
            return await _context.TrackingAssetDevices
                .Where(l => l.ValidFromUtc <= now && (l.ValidToUtc == null || l.ValidToUtc > now))
                .ToListAsync();
        }

        // Χρονικά παράθυρα στα οποία ο χρήστης δικαιούται να δει τη συσκευή:
        // μόνο όσο ήταν συνδεδεμένη σε όχημα που βλέπει. null = χωρίς περιορισμό.
        private async Task<List<(DateTime From, DateTime To)>?> AllowedWindowsAsync(Access access, int deviceId)
        {
            if (access.AllDevices) return null;
            var links = await _context.TrackingAssetDevices
                .Where(l => l.TraccarDeviceId == deviceId && access.AssetIds.Contains(l.AssetID))
                .ToListAsync();
            return links.Select(l => (l.ValidFromUtc, l.ValidToUtc ?? DateTime.MaxValue)).ToList();
        }

        // Ελέγχει δικαίωμα + εύρος. Επιστρέφει το (κομμένο) εύρος ή μήνυμα λάθους.
        private async Task<(DateTimeOffset From, DateTimeOffset To, ActionResult? Error)> ResolveRangeAsync(
            Access access, int deviceId, DateTimeOffset from, DateTimeOffset to, TimeSpan max)
        {
            if (!access.CanView) return (from, to, Forbid());
            if (to <= from) return (from, to, BadRequest("Το «έως» πρέπει να είναι μετά το «από»."));
            if (to - from > max) return (from, to, BadRequest($"Μέγιστο διάστημα: {max.TotalDays:0} ημέρες."));

            var windows = await AllowedWindowsAsync(access, deviceId);
            if (windows == null) return (from, to, null);
            if (windows.Count == 0) return (from, to, NotFound());

            // Κόβουμε στο εύρος των συνδέσεων που επιτρέπονται.
            var minFrom = windows.Min(w => w.From);
            var maxTo = windows.Max(w => w.To);
            var f = from.UtcDateTime < minFrom ? new DateTimeOffset(minFrom, TimeSpan.Zero) : from;
            var t = maxTo != DateTime.MaxValue && to.UtcDateTime > maxTo ? new DateTimeOffset(maxTo, TimeSpan.Zero) : to;
            if (t <= f) return (from, to, NotFound());
            return (f, t, null);
        }

        private ActionResult Unavailable(TraccarUnavailableException ex) =>
            StatusCode(503, ex.Message);

        // ================= ENDPOINTS =================

        [HttpGet("me")]
        public async Task<ActionResult<TrackingMeDto>> Me()
        {
            var access = await GetAccessAsync();
            return Ok(new TrackingMeDto { CanView = access.CanView, IsAdmin = access.IsAdmin, CanSeeRaw = access.CanSeeRaw });
        }

        // Πρώτη οθόνη: όλες οι ορατές συσκευές + τελευταία θέση. Καλείται με polling.
        [HttpGet("overview")]
        public async Task<ActionResult<TrackingOverviewDto>> Overview()
        {
            var access = await GetAccessAsync();
            if (!access.CanView) return Forbid();
            try
            {
                return Ok(new TrackingOverviewDto
                {
                    Devices = await BuildDevicesAsync(access, null),
                    FetchedAt = DateTimeOffset.UtcNow
                });
            }
            catch (TraccarUnavailableException ex) { return Unavailable(ex); }
        }

        [HttpGet("device/{id:int}")]
        public async Task<ActionResult<TrackingDeviceDto>> Device(int id)
        {
            var access = await GetAccessAsync();
            if (!access.CanView) return Forbid();
            try
            {
                var device = (await BuildDevicesAsync(access, id)).FirstOrDefault();
                return device == null ? NotFound() : Ok(device);
            }
            catch (TraccarUnavailableException ex) { return Unavailable(ex); }
        }

        private async Task<List<TrackingDeviceDto>> BuildDevicesAsync(Access access, int? onlyDeviceId)
        {
            var devicesJson = await _traccar.GetDevicesAsync();
            var positionsJson = await _traccar.GetLatestPositionsAsync();
            var links = await CurrentLinksAsync();
            var assets = await _context.TrackingAssets.ToDictionaryAsync(a => a.AssetID);
            var ranges = await _context.TrackingTempRanges.ToListAsync();

            var positions = new Dictionary<int, TrackingPositionDto>();
            foreach (var p in positionsJson)
            {
                try
                {
                    var dto = TraccarMapper.Position(p);
                    positions[dto.DeviceId] = dto;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Tracking: θέση που δεν διαβάστηκε ({Error})", ex.Message);
                }
            }

            var result = new List<TrackingDeviceDto>();
            foreach (var d in devicesJson)
            {
                TrackingDeviceDto device;
                try { device = TraccarMapper.Device(d); }
                catch (Exception ex)
                {
                    _logger.LogWarning("Tracking: συσκευή που δεν διαβάστηκε ({Error})", ex.Message);
                    continue;
                }
                if (onlyDeviceId.HasValue && device.Id != onlyDeviceId) continue;

                var link = links.Where(l => l.TraccarDeviceId == device.Id)
                    .OrderByDescending(l => l.ValidFromUtc).FirstOrDefault();
                TrackingAsset? asset = null;
                if (link != null) assets.TryGetValue(link.AssetID, out asset);

                // Φιλτράρισμα: εκτός admin/technical, μόνο συσκευές σε ορατό όχημα.
                if (!access.AllDevices && (asset == null || !access.AssetIds.Contains(asset.AssetID)))
                    continue;

                device.AssetId = asset?.AssetID;
                device.AssetName = asset?.Name;
                device.AssetType = asset?.AssetType;
                device.DeviceRole = link?.DeviceRole;
                device.Position = positions.TryGetValue(device.Id, out var pos) ? pos : null;
                device.TempRange = ResolveRange(ranges, device.Id, asset?.AssetID);
                if (!string.IsNullOrEmpty(device.Position?.Alarm))
                    device.LastAlarmTime = device.Position!.FixTime ?? device.Position.ServerTime;
                result.Add(device);
            }
            return result.OrderBy(d => d.AssetName ?? d.Name).ThenBy(d => d.Name).ToList();
        }

        // Το πιο συγκεκριμένο όριο: Device > Asset > Default.
        private static TrackingTempRangeDto? ResolveRange(List<TrackingTempRange> ranges, int deviceId, int? assetId)
        {
            var r = ranges.FirstOrDefault(x => x.Scope == "Device" && x.TraccarDeviceId == deviceId)
                 ?? (assetId.HasValue ? ranges.FirstOrDefault(x => x.Scope == "Asset" && x.AssetID == assetId) : null)
                 ?? ranges.FirstOrDefault(x => x.Scope == "Default");
            return r == null ? null : new TrackingTempRangeDto { Scope = r.Scope, MinC = r.MinC, MaxC = r.MaxC };
        }

        // Ιστορική διαδρομή (μέγιστο 7 ημέρες).
        [HttpGet("device/{id:int}/route")]
        public async Task<ActionResult<TrackingRouteDto>> Route(int id, [FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to)
        {
            var access = await GetAccessAsync();
            var (f, t, error) = await ResolveRangeAsync(access, id, from, to, MaxRouteRange);
            if (error != null) return error;
            try
            {
                var json = await _traccar.GetPositionsAsync(id, f, t);
                var positions = MapPositions(json);
                var truncated = positions.Count > MaxRoutePoints;
                if (truncated) positions = Thin(positions, MaxRoutePoints);
                return Ok(new TrackingRouteDto { Positions = positions, Truncated = truncated, From = f, To = t });
            }
            catch (TraccarUnavailableException ex) { return Unavailable(ex); }
        }

        // Trips + στάσεις από τα έτοιμα reports του Traccar (μέγιστο 31 ημέρες).
        [HttpGet("device/{id:int}/trips")]
        public async Task<ActionResult<TrackingTripsDto>> Trips(int id, [FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to)
        {
            var access = await GetAccessAsync();
            var (f, t, error) = await ResolveRangeAsync(access, id, from, to, MaxReportRange);
            if (error != null) return error;
            try
            {
                var trips = await _traccar.GetTripsAsync(id, f, t);
                var stops = await _traccar.GetStopsAsync(id, f, t);
                return Ok(new TrackingTripsDto
                {
                    Trips = trips.Select(TraccarMapper.Trip).OrderBy(x => x.StartTime).ToList(),
                    Stops = stops.Select(TraccarMapper.Stop).OrderBy(x => x.StartTime).ToList()
                });
            }
            catch (TraccarUnavailableException ex) { return Unavailable(ex); }
        }

        // Events/alarms όπως τα παράγει ο Traccar (μέγιστο 31 ημέρες).
        [HttpGet("device/{id:int}/events")]
        public async Task<ActionResult<List<TrackingEventDto>>> Events(int id, [FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to)
        {
            var access = await GetAccessAsync();
            var (f, t, error) = await ResolveRangeAsync(access, id, from, to, MaxReportRange);
            if (error != null) return error;
            try
            {
                var events = await _traccar.GetEventsAsync(id, f, t);
                return Ok(events.Select(TraccarMapper.Event).OrderByDescending(e => e.EventTime).ToList());
            }
            catch (TraccarUnavailableException ex) { return Unavailable(ex); }
        }

        // Θερμοκρασίες: γράφημα, min/max/avg και excursions εκτός ορίων (μέγιστο 14 ημέρες).
        [HttpGet("device/{id:int}/temperature")]
        public async Task<ActionResult<TrackingTemperatureDto>> Temperature(int id, [FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to)
        {
            var access = await GetAccessAsync();
            var (f, t, error) = await ResolveRangeAsync(access, id, from, to, MaxTempRange);
            if (error != null) return error;
            try
            {
                var positions = MapPositions(await _traccar.GetPositionsAsync(id, f, t));
                var link = (await CurrentLinksAsync()).FirstOrDefault(l => l.TraccarDeviceId == id);
                var range = ResolveRange(await _context.TrackingTempRanges.ToListAsync(), id, link?.AssetID);

                var result = new TrackingTemperatureDto { Range = range };
                var keys = positions.SelectMany(p => p.Temperatures.Select(x => x.Key)).Distinct().OrderBy(k => k);
                foreach (var key in keys)
                {
                    var series = new TrackingTempSeries { Key = key };
                    foreach (var p in positions)
                    {
                        var reading = p.Temperatures.FirstOrDefault(x => x.Key == key);
                        if (reading == null) continue;
                        var time = p.FixTime ?? p.DeviceTime ?? p.ServerTime;
                        if (reading.Malformed || reading.Value == null || time == null) { series.MalformedCount++; continue; }
                        series.Points.Add(new TrackingTempPoint { Time = time.Value, Value = reading.Value.Value });
                    }
                    if (series.Points.Count > 0)
                    {
                        series.Min = series.Points.Min(x => x.Value);
                        series.Max = series.Points.Max(x => x.Value);
                        series.Avg = Math.Round(series.Points.Average(x => x.Value), 2);
                    }
                    if (range != null) series.Excursions = Excursions(series.Points, (double)range.MinC, (double)range.MaxC);
                    if (series.Points.Count > 5000)
                    {
                        series.Points = Thin(series.Points, 5000);
                        result.Truncated = true;
                    }
                    result.Series.Add(series);
                }
                return Ok(result);
            }
            catch (TraccarUnavailableException ex) { return Unavailable(ex); }
        }

        // Συνεχόμενα διαστήματα εκτός ορίων (κάτω από Min ή πάνω από Max).
        private static List<TrackingTempExcursion> Excursions(List<TrackingTempPoint> points, double min, double max)
        {
            var list = new List<TrackingTempExcursion>();
            TrackingTempExcursion? current = null;
            foreach (var p in points.OrderBy(x => x.Time))
            {
                var kind = p.Value < min ? "low" : p.Value > max ? "high" : null;
                if (kind != null && current != null && current.Kind == kind)
                {
                    current.End = p.Time;
                    current.Peak = kind == "low" ? Math.Min(current.Peak, p.Value) : Math.Max(current.Peak, p.Value);
                }
                else
                {
                    if (current != null) list.Add(current);
                    current = kind == null ? null : new TrackingTempExcursion { Start = p.Time, End = p.Time, Kind = kind, Peak = p.Value };
                }
            }
            if (current != null) list.Add(current);
            return list;
        }

        // Raw Data για troubleshooting: μόνο admin/technical (μέγιστο 1 ημέρα, 500 γραμμές).
        [HttpGet("device/{id:int}/raw")]
        public async Task<ActionResult<List<TrackingRawRowDto>>> Raw(int id, [FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to)
        {
            var access = await GetAccessAsync();
            if (!access.CanSeeRaw) return Forbid();
            var (f, t, error) = await ResolveRangeAsync(access, id, from, to, MaxRawRange);
            if (error != null) return error;
            try
            {
                var json = await _traccar.GetPositionsAsync(id, f, t);
                return Ok(json.TakeLast(MaxRawRows).Reverse().Select(p => new TrackingRawRowDto
                {
                    PositionId = TraccarMapper.Long(p, "id") ?? 0,
                    Protocol = TraccarMapper.Str(p, "protocol"),
                    ServerTime = TraccarMapper.Time(p, "serverTime"),
                    DeviceTime = TraccarMapper.Time(p, "deviceTime"),
                    FixTime = TraccarMapper.Time(p, "fixTime"),
                    Valid = TraccarMapper.Bool(p, "valid") ?? false,
                    Latitude = TraccarMapper.Double(p, "latitude") ?? 0,
                    Longitude = TraccarMapper.Double(p, "longitude") ?? 0,
                    Raw = p.TryGetProperty("attributes", out var a) && a.ValueKind == System.Text.Json.JsonValueKind.Object
                        ? TraccarMapper.Str(a, "raw") : null,
                    Attributes = TraccarMapper.Attributes(p)
                }).ToList());
            }
            catch (TraccarUnavailableException ex) { return Unavailable(ex); }
        }

        private List<TrackingPositionDto> MapPositions(List<System.Text.Json.JsonElement> json)
        {
            var list = new List<TrackingPositionDto>(json.Count);
            var failed = 0;
            foreach (var p in json)
            {
                try { list.Add(TraccarMapper.Position(p)); }
                catch (Exception) { failed++; }
            }
            if (failed > 0) _logger.LogWarning("Tracking: {Count} θέσεις δεν διαβάστηκαν", failed);
            return list;
        }

        // Αραίωση κρατώντας πρώτο/τελευταίο σημείο (για χάρτη/γράφημα).
        private static List<T> Thin<T>(List<T> items, int max)
        {
            if (items.Count <= max) return items;
            var step = (double)(items.Count - 1) / (max - 1);
            return Enumerable.Range(0, max).Select(i => items[(int)Math.Round(i * step)]).ToList();
        }

        // ================= ΔΙΑΧΕΙΡΙΣΗ (admin) =================

        [HttpGet("admin")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<TrackingAdminDto>> Admin()
        {
            var assets = await _context.TrackingAssets.OrderBy(a => a.Name).ToListAsync();
            var links = await _context.TrackingAssetDevices.OrderByDescending(l => l.ValidFromUtc).ToListAsync();
            var ranges = await _context.TrackingTempRanges.ToListAsync();
            var access = await _context.TrackingUserAccess.ToListAsync();
            var users = await _context.Users.Where(u => u.Active)
                .OrderBy(u => u.Lastname).ThenBy(u => u.Firstname)
                .Select(u => new TrackingUserOptionDto { IdUser = u.IdUser, Name = (u.Firstname + " " + u.Lastname).Trim(), Team = u.Team })
                .ToListAsync();

            // Οι συσκευές έρχονται από τον Traccar· αν δεν απαντά, η διαχείριση δουλεύει χωρίς ονόματα.
            var devices = new List<TrackingDeviceOptionDto>();
            try
            {
                devices = (await _traccar.GetDevicesAsync()).Select(TraccarMapper.Device)
                    .Select(d => new TrackingDeviceOptionDto { Id = d.Id, Name = d.Name, UniqueId = d.UniqueId })
                    .OrderBy(d => d.Name).ToList();
            }
            catch (TraccarUnavailableException) { }
            var deviceNames = devices.ToDictionary(d => d.Id, d => d.Name);

            var def = ranges.FirstOrDefault(r => r.Scope == "Default");
            return Ok(new TrackingAdminDto
            {
                DefaultRange = new TrackingTempRangeDto { Scope = "Default", MinC = def?.MinC ?? 2, MaxC = def?.MaxC ?? 8 },
                Devices = devices,
                Users = users,
                Assets = assets.Select(a =>
                {
                    var r = ranges.FirstOrDefault(x => x.Scope == "Asset" && x.AssetID == a.AssetID);
                    return new TrackingAssetDto
                    {
                        AssetID = a.AssetID, Name = a.Name, AssetType = a.AssetType, PlateNumber = a.PlateNumber,
                        Team = a.Team, Notes = a.Notes, IsActive = a.IsActive, MinC = r?.MinC, MaxC = r?.MaxC,
                        Links = links.Where(l => l.AssetID == a.AssetID).Select(l => new TrackingLinkDto
                        {
                            LinkID = l.LinkID,
                            TraccarDeviceId = l.TraccarDeviceId,
                            DeviceName = deviceNames.TryGetValue(l.TraccarDeviceId, out var n) ? n : null,
                            DeviceRole = l.DeviceRole,
                            ValidFrom = new DateTimeOffset(DateTime.SpecifyKind(l.ValidFromUtc, DateTimeKind.Utc)),
                            ValidTo = l.ValidToUtc.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(l.ValidToUtc.Value, DateTimeKind.Utc)) : null
                        }).ToList()
                    };
                }).ToList(),
                Access = access.Select(x => new TrackingAccessDto
                {
                    AccessID = x.AccessID, UserID = x.UserID, AccessLevel = x.AccessLevel, Team = x.Team, AssetID = x.AssetID,
                    UserName = users.FirstOrDefault(u => u.IdUser == x.UserID)?.Name ?? $"user #{x.UserID}",
                    AssetName = assets.FirstOrDefault(a => a.AssetID == x.AssetID)?.Name
                }).OrderBy(x => x.UserName).ToList()
            });
        }

        [HttpPost("admin/asset")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<TrackingResult>> SaveAsset([FromBody] SaveTrackingAssetRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name)) return Ok(new TrackingResult { Message = "Κενό όνομα." });
            if (req.MinC.HasValue != req.MaxC.HasValue || (req.MinC.HasValue && req.MinC >= req.MaxC))
                return Ok(new TrackingResult { Message = "Μη έγκυρο εύρος θερμοκρασίας." });

            TrackingAsset asset;
            if (req.AssetID.HasValue)
            {
                asset = await _context.TrackingAssets.FindAsync(req.AssetID.Value);
                if (asset == null) return NotFound();
            }
            else
            {
                asset = new TrackingAsset { CreatedAt = DateTime.Now };
                _context.TrackingAssets.Add(asset);
            }
            asset.Name = req.Name.Trim();
            asset.AssetType = string.IsNullOrWhiteSpace(req.AssetType) ? "Vehicle" : req.AssetType.Trim();
            asset.PlateNumber = string.IsNullOrWhiteSpace(req.PlateNumber) ? null : req.PlateNumber.Trim();
            asset.Team = string.IsNullOrWhiteSpace(req.Team) ? null : req.Team.Trim();
            asset.Notes = req.Notes;
            asset.IsActive = req.IsActive;
            await _context.SaveChangesAsync();

            var range = await _context.TrackingTempRanges.FirstOrDefaultAsync(r => r.Scope == "Asset" && r.AssetID == asset.AssetID);
            if (req.MinC.HasValue)
            {
                if (range == null)
                {
                    range = new TrackingTempRange { Scope = "Asset", AssetID = asset.AssetID, CreatedAt = DateTime.Now };
                    _context.TrackingTempRanges.Add(range);
                }
                range.MinC = req.MinC.Value;
                range.MaxC = req.MaxC!.Value;
            }
            else if (range != null) _context.TrackingTempRanges.Remove(range);
            await _context.SaveChangesAsync();
            return Ok(new TrackingResult { Success = true });
        }

        // Νέα σύνδεση: κλείνει όποια ενεργή σύνδεση έχει ήδη η συσκευή (το ιστορικό μένει).
        [HttpPost("admin/link")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<TrackingResult>> Link([FromBody] LinkTrackingDeviceRequest req)
        {
            if (await _context.TrackingAssets.FindAsync(req.AssetID) == null) return NotFound();
            var now = DateTime.UtcNow;
            var open = await _context.TrackingAssetDevices
                .Where(l => l.TraccarDeviceId == req.TraccarDeviceId && l.ValidToUtc == null).ToListAsync();
            foreach (var l in open) l.ValidToUtc = now;
            _context.TrackingAssetDevices.Add(new TrackingAssetDevice
            {
                AssetID = req.AssetID,
                TraccarDeviceId = req.TraccarDeviceId,
                DeviceRole = string.IsNullOrWhiteSpace(req.DeviceRole) ? "GPS" : req.DeviceRole,
                ValidFromUtc = now,
                CreatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();
            return Ok(new TrackingResult { Success = true });
        }

        // Αποσύνδεση = κλείσιμο διαστήματος, όχι διαγραφή (το ιστορικό του οχήματος μένει).
        [HttpPost("admin/unlink/{linkId:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<TrackingResult>> Unlink(int linkId)
        {
            var link = await _context.TrackingAssetDevices.FindAsync(linkId);
            if (link == null) return NotFound();
            link.ValidToUtc ??= DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new TrackingResult { Success = true });
        }

        [HttpPost("admin/access")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<TrackingResult>> SaveAccess([FromBody] SaveTrackingAccessRequest req)
        {
            if (!TrackingAccessLevels.All.Contains(req.AccessLevel))
                return Ok(new TrackingResult { Message = "Άγνωστο επίπεδο πρόσβασης." });
            if (req.AccessLevel == TrackingAccessLevels.CompanyAdmin && string.IsNullOrWhiteSpace(req.Team))
                return Ok(new TrackingResult { Message = "Επίλεξε εταιρεία." });
            if (req.AccessLevel == TrackingAccessLevels.User && !req.AssetID.HasValue)
                return Ok(new TrackingResult { Message = "Επίλεξε όχημα." });

            _context.TrackingUserAccess.Add(new TrackingUserAccess
            {
                UserID = req.UserID,
                AccessLevel = req.AccessLevel,
                Team = req.AccessLevel == TrackingAccessLevels.CompanyAdmin ? req.Team!.Trim() : null,
                AssetID = req.AccessLevel == TrackingAccessLevels.User ? req.AssetID : null,
                CreatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();
            return Ok(new TrackingResult { Success = true });
        }

        [HttpPost("admin/access/delete/{accessId:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<TrackingResult>> DeleteAccess(int accessId)
        {
            var row = await _context.TrackingUserAccess.FindAsync(accessId);
            if (row == null) return NotFound();
            _context.TrackingUserAccess.Remove(row);
            await _context.SaveChangesAsync();
            return Ok(new TrackingResult { Success = true });
        }

        [HttpPost("admin/default-range")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<TrackingResult>> SaveDefaultRange([FromBody] TrackingTempRangeDto req)
        {
            if (req.MinC >= req.MaxC) return Ok(new TrackingResult { Message = "Μη έγκυρο εύρος θερμοκρασίας." });
            var range = await _context.TrackingTempRanges.FirstOrDefaultAsync(r => r.Scope == "Default");
            if (range == null)
            {
                range = new TrackingTempRange { Scope = "Default", CreatedAt = DateTime.Now };
                _context.TrackingTempRanges.Add(range);
            }
            range.MinC = req.MinC;
            range.MaxC = req.MaxC;
            await _context.SaveChangesAsync();
            return Ok(new TrackingResult { Success = true });
        }
    }
}
