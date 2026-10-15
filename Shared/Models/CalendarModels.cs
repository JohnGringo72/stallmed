using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StallmedManager.Shared.Models
{
    // ---- Ημερολόγιο (καρτέλα μέσα στο Todo) ----
    // Πίνακας: sql/calendar_events.sql (χωρίς EF migrations, όπως όλο το project).
    [Table("CalendarEvents")]
    public class CalendarEvent
    {
        [Key]
        public long EventID { get; set; }
        public string Title { get; set; } = "";
        public string? Notes { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Company { get; set; }
        public string VisibleTo { get; set; } = CalendarVisibility.Me;
        public string? VisibleTeam { get; set; }
        public int? VisibleUserID { get; set; }
        public int CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // Ποιος βλέπει ένα γεγονός
    public static class CalendarVisibility
    {
        public const string Me = "Me";
        public const string All = "All";
        public const string Team = "Team";
        public const string User = "User";
    }

    // Εταιρεία που αφορά (φαίνεται μόνο σε admin). Χρώματα: CompanyColors.
    public static class CalendarCompanies
    {
        public static readonly (string Code, string Label)[] All =
        {
            ("SM",   "StallMedicals"),
            ("BM",   "BeltaMed"),
            ("TECO", "Teco"),
            ("VF",   "VFpro"),
            ("SMBM", "SM + BM")
        };

        public static string? Normalize(string? code) =>
            All.Any(c => string.Equals(c.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase))
                ? code!.Trim().ToUpperInvariant()
                : null;

        public static string Short(string? code) => code switch
        {
            "SM" => "SM",
            "BM" => "BM",
            "TECO" => "Teco",
            "VF" => "VF",
            "SMBM" => "SM+BM",
            _ => ""
        };
    }

    public class CalendarEventDto
    {
        public long EventID { get; set; }
        public string Title { get; set; } = "";
        public string? Notes { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Company { get; set; }          // null για όσους δεν είναι admin
        public string VisibleTo { get; set; } = CalendarVisibility.Me;
        public string? VisibleTeam { get; set; }
        public int? VisibleUserID { get; set; }
        public string? VisibleLabel { get; set; }     // π.χ. «Όλοι», «Sales», «Μαρία Π.»
        public int CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public bool CanEdit { get; set; }
    }

    public class SaveCalendarEventRequest
    {
        public long? EventID { get; set; }            // null = νέο
        public int UserID { get; set; }               // ποιος κάνει την ενέργεια
        public string? UserName { get; set; }
        public string Title { get; set; } = "";
        public string? Notes { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Company { get; set; }
        public string VisibleTo { get; set; } = CalendarVisibility.Me;
        public string? VisibleTeam { get; set; }
        public int? VisibleUserID { get; set; }
    }

    public class CalendarSaveResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public long EventID { get; set; }
    }
}
