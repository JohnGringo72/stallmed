namespace StallmedManager.Client
{
    // Κοινή λογική εμφάνισης τιμολόγησης (χρησιμοποιείται από DoctorOrders.razor και Shipments.razor)
    public static class InvoiceDisplay
    {
        public static string Emoji(string? invoiceType) => invoiceType switch
        {
            "Δωρεάν" => "🔴",
            "Έκπτωση" => "🟡",
            _ => "🟢"
        };

        public static string Label(string? invoiceType, string? invoiceNote)
        {
            var type = string.IsNullOrEmpty(invoiceType) ? "Κανονικό" : invoiceType;
            return type switch
            {
                "Έκπτωση" => string.IsNullOrWhiteSpace(invoiceNote) ? "Έκπτωση" : $"Έκπτωση {invoiceNote}",
                "Δωρεάν" => string.IsNullOrWhiteSpace(invoiceNote) ? "Δωρεάν" : $"Δωρεάν ({invoiceNote})",
                _ => "Κανονικό"
            };
        }
    }
}
