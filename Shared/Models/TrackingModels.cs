using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StallmedManager.Shared.Models
{
    // ============================================================
    // Tracking Module (Παρακολούθηση Συσκευών)
    // Πίνακες: sql/tracking_module.sql -- ΜΟΝΟ metadata (οχήματα,
    // συνδέσεις, πρόσβαση, όρια). Συσκευές/θέσεις/events ζουν στον
    // Traccar και διαβάζονται live μέσω TrackingController.
    // ============================================================

    [Table("TrackingAssets")]
    public class TrackingAsset
    {
        [Key]
        public int AssetID { get; set; }
        public string Name { get; set; } = "";
        public string AssetType { get; set; } = "Vehicle";
        public string? PlateNumber { get; set; }
        public string? Team { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }

    // Όχημα ↔ συσκευή Traccar για συγκεκριμένο διάστημα (UTC). Όχημα ≠ tracker.
    [Table("TrackingAssetDevices")]
    public class TrackingAssetDevice
    {
        [Key]
        public int LinkID { get; set; }
        public int AssetID { get; set; }
        public int TraccarDeviceId { get; set; }
        public string DeviceRole { get; set; } = "GPS";
        public DateTime ValidFromUtc { get; set; }
        public DateTime? ValidToUtc { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    [Table("TrackingUserAccess")]
    public class TrackingUserAccess
    {
        [Key]
        public int AccessID { get; set; }
        public int UserID { get; set; }
        public string AccessLevel { get; set; } = TrackingAccessLevels.User;
        public string? Team { get; set; }
        public int? AssetID { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    [Table("TrackingTempRanges")]
    public class TrackingTempRange
    {
        [Key]
        public int RangeID { get; set; }
        public string Scope { get; set; } = "Default";
        public int? AssetID { get; set; }
        public int? TraccarDeviceId { get; set; }
        public int? ShipmentID { get; set; }
        public decimal MinC { get; set; }
        public decimal MaxC { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public static class TrackingAccessLevels
    {
        public const string Technical = "Technical";
        public const string CompanyAdmin = "CompanyAdmin";
        public const string User = "User";
        public static readonly string[] All = { Technical, CompanyAdmin, User };
    }

    // ---------------- DTOs ----------------
    // Όλες οι μετρήσεις είναι nullable: null = άγνωστο/δεν το στέλνει η συσκευή,
    // ΠΟΤΕ 0. Οι χρόνοι είναι UTC (DateTimeOffset)· η μετατροπή σε ώρα Ελλάδας
    // γίνεται στον client με TimeZoneInfo (σωστή θερινή/χειμερινή ώρα).

    public class TrackingMeDto
    {
        public bool CanView { get; set; }
        public bool IsAdmin { get; set; }
        public bool CanSeeRaw { get; set; }
    }

    public class TrackingTempRangeDto
    {
        public string Scope { get; set; } = "Default";
        public decimal MinC { get; set; }
        public decimal MaxC { get; set; }
    }

    public class TrackingTempReading
    {
        public string Key { get; set; } = "";
        public double? Value { get; set; }
        // Η συσκευή έστειλε τιμή εκτός φυσικών ορίων (π.χ. 850 = αποσυνδεδεμένος αισθητήρας).
        public bool Malformed { get; set; }
        public string? RawValue { get; set; }
    }

    public class TrackingPositionDto
    {
        public long Id { get; set; }
        public int DeviceId { get; set; }
        public string? Protocol { get; set; }
        public DateTimeOffset? ServerTime { get; set; }
        public DateTimeOffset? DeviceTime { get; set; }
        public DateTimeOffset? FixTime { get; set; }
        public bool Valid { get; set; }
        public bool Outdated { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? Altitude { get; set; }
        public double? SpeedKmh { get; set; }
        public double? Course { get; set; }
        public string? Address { get; set; }
        public double? Accuracy { get; set; }
        public bool? Ignition { get; set; }
        public bool? Motion { get; set; }
        public double? PowerV { get; set; }
        public double? BatteryV { get; set; }
        public double? BatteryLevel { get; set; }
        public int? Rssi { get; set; }
        public int? Satellites { get; set; }
        public double? DeviceTemp { get; set; }
        public double? OdometerKm { get; set; }
        public string? Alarm { get; set; }
        public List<TrackingTempReading> Temperatures { get; set; } = new();
        // Όλα τα attributes όπως τα δίνει ο Traccar (για Live/Raw).
        public Dictionary<string, string> Attributes { get; set; } = new();
    }

    public class TrackingDeviceDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string UniqueId { get; set; } = "";
        public string? Model { get; set; }
        public string? Category { get; set; }
        public string? Phone { get; set; }
        public string Status { get; set; } = "unknown";   // online / offline / unknown
        public DateTimeOffset? LastUpdate { get; set; }
        public bool Disabled { get; set; }
        public int? AssetId { get; set; }
        public string? AssetName { get; set; }
        public string? AssetType { get; set; }
        public string? DeviceRole { get; set; }
        public TrackingPositionDto? Position { get; set; }
        public TrackingTempRangeDto? TempRange { get; set; }
        public DateTimeOffset? LastAlarmTime { get; set; }
    }

    public class TrackingOverviewDto
    {
        public List<TrackingDeviceDto> Devices { get; set; } = new();
        public DateTimeOffset FetchedAt { get; set; }
    }

    public class TrackingRouteDto
    {
        public List<TrackingPositionDto> Positions { get; set; } = new();
        public bool Truncated { get; set; }
        public DateTimeOffset From { get; set; }
        public DateTimeOffset To { get; set; }
    }

    public class TrackingTripDto
    {
        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
        public string? StartAddress { get; set; }
        public string? EndAddress { get; set; }
        public double? StartLat { get; set; }
        public double? StartLon { get; set; }
        public double? EndLat { get; set; }
        public double? EndLon { get; set; }
        public long? DurationMs { get; set; }
        public double? DistanceKm { get; set; }
        public double? MaxSpeedKmh { get; set; }
        public double? AverageSpeedKmh { get; set; }
        public string? DriverName { get; set; }
    }

    public class TrackingStopDto
    {
        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
        public long? DurationMs { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Address { get; set; }
    }

    public class TrackingTripsDto
    {
        public List<TrackingTripDto> Trips { get; set; } = new();
        public List<TrackingStopDto> Stops { get; set; } = new();
    }

    public class TrackingEventDto
    {
        public long Id { get; set; }
        public string Type { get; set; } = "";
        public DateTimeOffset? EventTime { get; set; }
        public int DeviceId { get; set; }
        public long? PositionId { get; set; }
        public long? GeofenceId { get; set; }
        public string? Alarm { get; set; }
        public Dictionary<string, string> Attributes { get; set; } = new();
    }

    public class TrackingTempPoint
    {
        public DateTimeOffset Time { get; set; }
        public double Value { get; set; }
    }

    public class TrackingTempExcursion
    {
        public DateTimeOffset Start { get; set; }
        public DateTimeOffset End { get; set; }
        public string Kind { get; set; } = "";   // low / high
        public double Peak { get; set; }
    }

    public class TrackingTempSeries
    {
        public string Key { get; set; } = "";
        public List<TrackingTempPoint> Points { get; set; } = new();
        public double? Min { get; set; }
        public double? Max { get; set; }
        public double? Avg { get; set; }
        public int MalformedCount { get; set; }
        public List<TrackingTempExcursion> Excursions { get; set; } = new();
    }

    public class TrackingTemperatureDto
    {
        public TrackingTempRangeDto? Range { get; set; }
        public List<TrackingTempSeries> Series { get; set; } = new();
        public bool Truncated { get; set; }
    }

    public class TrackingRawRowDto
    {
        public long PositionId { get; set; }
        public string? Protocol { get; set; }
        public DateTimeOffset? ServerTime { get; set; }
        public DateTimeOffset? DeviceTime { get; set; }
        public DateTimeOffset? FixTime { get; set; }
        public bool Valid { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? Raw { get; set; }      // μόνο αν ο Traccar έχει database.saveOriginal
        public Dictionary<string, string> Attributes { get; set; } = new();
    }

    // ---- Διαχείριση (admin) ----

    public class TrackingAssetDto
    {
        public int AssetID { get; set; }
        public string Name { get; set; } = "";
        public string AssetType { get; set; } = "Vehicle";
        public string? PlateNumber { get; set; }
        public string? Team { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public decimal? MinC { get; set; }
        public decimal? MaxC { get; set; }
        public List<TrackingLinkDto> Links { get; set; } = new();
    }

    public class TrackingLinkDto
    {
        public int LinkID { get; set; }
        public int TraccarDeviceId { get; set; }
        public string? DeviceName { get; set; }
        public string DeviceRole { get; set; } = "GPS";
        public DateTimeOffset ValidFrom { get; set; }
        public DateTimeOffset? ValidTo { get; set; }
    }

    public class SaveTrackingAssetRequest
    {
        public int? AssetID { get; set; }
        public string Name { get; set; } = "";
        public string AssetType { get; set; } = "Vehicle";
        public string? PlateNumber { get; set; }
        public string? Team { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;
        // null = ισχύει το προεπιλεγμένο εύρος
        public decimal? MinC { get; set; }
        public decimal? MaxC { get; set; }
    }

    public class LinkTrackingDeviceRequest
    {
        public int AssetID { get; set; }
        public int TraccarDeviceId { get; set; }
        public string DeviceRole { get; set; } = "GPS";
    }

    public class TrackingAccessDto
    {
        public int AccessID { get; set; }
        public int UserID { get; set; }
        public string UserName { get; set; } = "";
        public string AccessLevel { get; set; } = "";
        public string? Team { get; set; }
        public int? AssetID { get; set; }
        public string? AssetName { get; set; }
    }

    public class SaveTrackingAccessRequest
    {
        public int UserID { get; set; }
        public string AccessLevel { get; set; } = TrackingAccessLevels.User;
        public string? Team { get; set; }
        public int? AssetID { get; set; }
    }

    public class TrackingAdminDto
    {
        public List<TrackingAssetDto> Assets { get; set; } = new();
        public List<TrackingAccessDto> Access { get; set; } = new();
        public TrackingTempRangeDto DefaultRange { get; set; } = new();
        public List<TrackingDeviceOptionDto> Devices { get; set; } = new();
        public List<TrackingUserOptionDto> Users { get; set; } = new();
    }

    public class TrackingDeviceOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string UniqueId { get; set; } = "";
    }

    public class TrackingUserOptionDto
    {
        public int IdUser { get; set; }
        public string Name { get; set; } = "";
        public string? Team { get; set; }
    }

    public class TrackingResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
