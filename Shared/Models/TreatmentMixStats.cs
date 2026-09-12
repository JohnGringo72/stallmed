namespace StallmedManager.Shared.Models
{
    // Στατιστικό μειγμάτων (WebOrders.Allergen) ανά είδος (WebOrders.TreatmentDescription)
    public class TreatmentMixGroup
    {
        public string Treatment { get; set; } = "";
        public int TotalQNT { get; set; }
        public List<TreatmentMixRow> Mixes { get; set; } = new();
    }

    public class TreatmentMixRow
    {
        // Ετικέτα για γραμμές με κενό Allergen
        public const string NoMixLabel = "(χωρίς μείγμα)";

        public string Allergen { get; set; } = "";
        public int QNT { get; set; }
    }
}
