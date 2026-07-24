namespace StallmedManager.Shared.Models
{
    // Γραμμή του Dashboard Αποθέματος (§8β). Τα OnHand/Committed/OnOrder
    // υπολογίζονται server-side από τα transactional δεδομένα:
    //   OnHand    = ελεύθερο υπόλοιπο παραλαβών + δεσμευμένα (allocated) τεμάχια
    //               μη απεσταλμένων παραγγελιών (βρίσκονται ακόμα στην αποθήκη)
    //   Committed = ζήτηση ανοιχτών (μη απεσταλμένων/ακυρωμένων) παραγγελιών γιατρών
    //   OnOrder   = υπόλοιπο προς παραλαβή από ανοιχτές παραγγελίες παραγωγής
    // Τα Available/ToOrder είναι παράγωγα (get-only) -- ίδιος τύπος και στον
    // client, οπότε η φόρμουλα είναι μία (StockDashboardLogic).
    public class StockDashboardItemDto
    {
        public string CodePrick { get; set; }
        public string? Description { get; set; }
        public string ProductTypeCode { get; set; }
        public string? ProductDescription { get; set; }
        public int OnHand { get; set; }
        public int Committed { get; set; }
        public int OnOrder { get; set; }
        public int ReorderPoint { get; set; }

        public int Available => StockDashboardLogic.Available(OnHand, Committed);
        public int ToOrder => StockDashboardLogic.ToOrder(ReorderPoint, Available, OnOrder);
        public bool IsFood => StockDashboardLogic.IsFood(CodePrick);
        public string Urgency => StockDashboardLogic.Urgency(Available, Committed, ToOrder);
    }

    public class SetReorderPointRequest
    {
        public string CodePrick { get; set; }
        public string ProductTypeCode { get; set; }
        public int ReorderPoint { get; set; }
    }

    public class SetReorderPointResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    public static class StockDashboardLogic
    {
        public const string UrgencyUrgent = "urgent";   // κόκκινο: δεν φτάνει το διαθέσιμο
        public const string UrgencyLow = "low";         // πορτοκαλί: κάτω από το όριο
        public const string UrgencyOk = "ok";           // ουδέτερο

        public static int Available(int onHand, int committed) => onHand - committed;

        public static int ToOrder(int reorderPoint, int available, int onOrder)
            => Math.Max(0, reorderPoint - (available + onOrder));

        // Ομαδοποίηση dashboard: Τρόφιμα = κωδικός που ξεκινά από F (case-insensitive).
        public static bool IsFood(string? codePrick)
            => !string.IsNullOrWhiteSpace(codePrick)
               && codePrick.TrimStart().StartsWith("F", StringComparison.OrdinalIgnoreCase);

        // Επείγον όταν το διαθέσιμο έχει εξαντληθεί ΚΑΙ υπάρχει λόγος (ζήτηση ή
        // έλλειμμα ορίου) -- αλλιώς ένα ανενεργό είδος με παντού μηδενικά θα
        // κοκκίνιζε. Χαμηλό όταν απλώς έπεσε κάτω από το όριο αναπαραγγελίας.
        public static string Urgency(int available, int committed, int toOrder)
        {
            if (available <= 0 && (committed > 0 || toOrder > 0)) return UrgencyUrgent;
            if (toOrder > 0) return UrgencyLow;
            return UrgencyOk;
        }
    }
}
