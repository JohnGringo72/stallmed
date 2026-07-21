namespace StallmedManager.Shared.Models
{
    public class ChatBotResponse
    {
        public string Reply { get; set; }
        public string Type { get; set; } // "stock", "orders", "doctor", "alerts", "shipments", "help", "default"
    }
}
