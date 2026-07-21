namespace StallmedManager.Shared.Models
{
    public class ChatMessage
    {
        public string Role { get; set; }    // "user" ή "assistant"
        public string Content { get; set; }
    }
}
