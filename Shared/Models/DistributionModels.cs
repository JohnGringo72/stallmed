using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StallmedManager.Shared.Models
{
    // ---- Διανομές σε γιατρούς (ημερολόγια, δώρα, υλικό) ----
    // Πίνακας: sql/distributions.sql (χωρίς EF migrations, όπως όλο το project).
    [Table("Distributions")]
    public class Distribution
    {
        [Key]
        public long DistributionID { get; set; }
        public string Item { get; set; } = "";
        public int? DoctorID { get; set; }
        public string DoctorName { get; set; } = "";
        public int Quantity { get; set; }
        public DateTime GivenDate { get; set; }
        public int GivenByUserID { get; set; }
        public string? GivenByName { get; set; }
        public string? Notes { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public static class DistributionItems
    {
        // Προεπιλογή της φόρμας -- αλλάζει ελεύθερα για άλλα είδη.
        public const string Default = "Ημερολόγιο 2027";
    }

    public class SaveDistributionRequest
    {
        public int UserID { get; set; }              // ποιος καταχωρεί
        public string Item { get; set; } = DistributionItems.Default;
        public int? DoctorID { get; set; }
        public string DoctorName { get; set; } = "";
        public int Quantity { get; set; } = 1;
        public DateTime GivenDate { get; set; } = DateTime.Today;
        public int GivenByUserID { get; set; }
        public string? Notes { get; set; }
    }

    public class DeleteDistributionRequest
    {
        public long DistributionID { get; set; }
        public int UserID { get; set; }
    }

    public class DistributionSaveResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
