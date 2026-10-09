namespace StallmedManager.Shared.Models
{
    // Στατιστικό μειγμάτων (WebOrders.Allergen) ανά είδος (WebOrders.TreatmentDescription)
    public class TreatmentMixGroup
    {
        public string Treatment { get; set; } = "";
        // Κωδικός σκευάσματος (WebOrders.TreatmentID). Αν το ίδιο όνομα έχει
        // περισσότερους από έναν κωδικούς, μπαίνουν όλοι χωρισμένοι με "/".
        public string TreatmentCode { get; set; } = "";
        public int TotalQNT { get; set; }
        // Στοκ αποθήκης (ασθενής "A A"): τι υπάρχει εδώ και τι αναμένεται.
        // Received (3) = εδώ, Manufacturing (2) = αναμένεται.
        public int StockHere { get; set; }
        public int StockExpected { get; set; }
        public int StockQNT => StockHere + StockExpected;
        public List<TreatmentMixRow> Mixes { get; set; } = new();
    }

    public class TreatmentMixRow
    {
        // Ετικέτα για γραμμές με κενό Allergen
        public const string NoMixLabel = "(χωρίς μείγμα)";

        public string Allergen { get; set; } = "";
        // Οι κωδικοί των αλλεργιογόνων του μείγματος, π.χ. "P-012, P-093".
        // Στη Stallmedicals συνήθως ένας, στη Belta πολλοί ανά θεραπεία.
        public string AllergenCodes { get; set; } = "";
        public int QNT { get; set; }
        // Στοκ για το ίδιο μείγμα: εδώ και αναμενόμενα
        public int StockHere { get; set; }
        public int StockExpected { get; set; }
        public int StockQNT => StockHere + StockExpected;
    }
}
