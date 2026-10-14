namespace StallmedManager.Shared.Models
{
    // Σύνοψη ημέρας: ποιος έκανε τι. Μαζεύει τις ενέργειες από όλα τα μέρη
    // της εφαρμογής που καταγράφουν χρήστη και ώρα.
    public class DailySummaryDto
    {
        public DateTime Date { get; set; }
        // Μετρήσεις ολόκληρης της ημέρας, ανεξάρτητα από χρήστη
        public List<DailyTotalDto> Totals { get; set; } = new();
        // Ταξινομημένοι από τον πιο δραστήριο προς τον λιγότερο
        public List<DailyUserSummaryDto> Users { get; set; } = new();
        // Ενέργειες που δεν καταγράφουν χρήστη -- π.χ. οι αποστολές κρατούν
        // μόνο ShippedAt. Φαίνονται ξεχωριστά για να μη λείπουν από την εικόνα.
        public List<DailyActionGroupDto> Unattributed { get; set; } = new();

        public int TotalActions => Users.Sum(u => u.TotalActions);
    }

    // Ένα νούμερο της ημέρας, π.χ. «Νέοι ασθενείς: 4»
    public class DailyTotalDto
    {
        public string Kind { get; set; } = "";
        public string Icon { get; set; } = "";
        public string Label { get; set; } = "";
        public int Value { get; set; }
        // Δεύτερη γραμμή κάτω από το νούμερο, π.χ. «312 τεμάχια»
        public string? Detail { get; set; }
    }

    public class DailyUserSummaryDto
    {
        public int? UserId { get; set; }
        public string UserName { get; set; } = "";
        public int TotalActions { get; set; }
        public List<DailyActionGroupDto> Groups { get; set; } = new();
    }

    public class DailyActionGroupDto
    {
        // Σταθερό κλειδί του είδους ενέργειας, για χρώματα/εικονίδια στην οθόνη
        public string Kind { get; set; } = "";
        public string Label { get; set; } = "";
        public string Icon { get; set; } = "";
        public int Count { get; set; }
        public List<DailyActionItemDto> Items { get; set; } = new();
    }

    public class DailyActionItemDto
    {
        public DateTime At { get; set; }
        public string Title { get; set; } = "";
        public string? Detail { get; set; }
    }
}
