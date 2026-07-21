using System.Collections.Generic;

namespace StallmedManager.Shared.Models
{
    public class ChatRequest
    {
        public string Message { get; set; }
        public List<ChatMessage> History { get; set; } = new();
    }
}
