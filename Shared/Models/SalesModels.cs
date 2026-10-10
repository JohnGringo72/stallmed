using System.ComponentModel.DataAnnotations;

namespace StallmedManager.Shared.Models
{
    // ============================================================
    // Sales Module: Επισκέψεις πωλητών + Εξοδολόγιο
    // Πίνακες: sql/sales_module.sql (χωρίς FK, ίδιο pattern με issues)
    // ============================================================

    public class SalesVisit
    {
        [Key]
        public long VisitID { get; set; }
        public int UserID { get; set; }
        public int? DoctorID { get; set; }
        public string DoctorName { get; set; } = "";
        public DateTime VisitDate { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SalesExpense
    {
        [Key]
        public long ExpenseID { get; set; }
        public int UserID { get; set; }
        public DateTime ExpenseDate { get; set; }
        public string? Destination { get; set; }
        public decimal Fuel { get; set; }
        public decimal Tolls { get; set; }
        public decimal Lodging { get; set; }
        public decimal Meals { get; set; }
        public decimal Parking { get; set; }
        public decimal Tickets { get; set; }
        public decimal Misc { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class SalesExpenseMonth
    {
        [Key]
        public int MonthID { get; set; }
        public int UserID { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public string? VehiclePlate { get; set; }
        public int? Kilometers { get; set; }
    }

    // ---- DTOs Επισκέψεων ----

    public class SalesVisitViewDto
    {
        public long VisitID { get; set; }
        public int UserID { get; set; }
        public string UserName { get; set; } = "";
        public int? DoctorID { get; set; }
        public string DoctorName { get; set; } = "";
        public DateTime VisitDate { get; set; }
        public string? Notes { get; set; }
    }

    public class SaveSalesVisitRequest
    {
        public long? VisitID { get; set; }
        public int UserID { get; set; }
        public int? DoctorID { get; set; }
        public string DoctorName { get; set; } = "";
        public DateTime VisitDate { get; set; }
        public string? Notes { get; set; }
    }

    public class DeleteSalesVisitRequest
    {
        public long VisitID { get; set; }
        public int UserID { get; set; }
    }

    // Επιλογή γιατρού για το autocomplete: ελληνικό όνομα όπου υπάρχει.
    public class SalesDoctorOptionDto
    {
        public int DoctorID { get; set; }
        public string DisplayName { get; set; } = "";
        public string? City { get; set; }
    }

    public class SalesSaveResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    // ---- DTOs Εξοδολογίου ----

    public class SalesExpenseDayDto
    {
        public DateTime ExpenseDate { get; set; }
        public string? Destination { get; set; }
        public decimal Fuel { get; set; }
        public decimal Tolls { get; set; }
        public decimal Lodging { get; set; }
        public decimal Meals { get; set; }
        public decimal Parking { get; set; }
        public decimal Tickets { get; set; }
        public decimal Misc { get; set; }
    }

    public class SalesExpenseMonthDto
    {
        public int UserID { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public string? VehiclePlate { get; set; }
        public int? Kilometers { get; set; }
        public List<SalesExpenseDayDto> Days { get; set; } = new();
    }

    // Γρήγορη καταχώρηση ΕΝΟΣ εξόδου: κατηγορία + ποσό + ημερομηνία.
    // Το ποσό ΠΡΟΣΤΙΘΕΤΑΙ στη γραμμή της ημέρας (τη δημιουργεί αν δεν υπάρχει).
    public class AddSalesExpenseRequest
    {
        public int ActingUserID { get; set; }
        public int UserID { get; set; }
        public DateTime ExpenseDate { get; set; }
        public string Category { get; set; } = "";   // Fuel/Tolls/Lodging/Meals/Parking/Tickets/Misc
        public decimal Amount { get; set; }
        public string? Destination { get; set; }
    }

    public class SaveSalesExpenseMonthRequest
    {
        public int ActingUserID { get; set; }
        public int UserID { get; set; }          // σε ποιον ανήκει το εξοδολόγιο
        public int Year { get; set; }
        public int Month { get; set; }
        public string? VehiclePlate { get; set; }
        public int? Kilometers { get; set; }
        public List<SalesExpenseDayDto> Days { get; set; } = new();
    }
}
