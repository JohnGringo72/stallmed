namespace StallmedManager.Shared.Models
{
    public class ChatBotRequest
    {
        public string Message { get; set; }
        // Προαιρετικό: "SM"/"BM" (ή "1"/"2" κατά WebOrders) -- αν λείπει, ο bot
        // ανιχνεύει εταιρεία μέσα στο μήνυμα ή δείχνει και τις δύο.
        public string CompanyId { get; set; }
    }
}
