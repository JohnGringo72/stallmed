using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StallmedManager.Shared.Models
{
    // ---- Ειδοποιήσεις Push (κινητό / desktop, σαν το Viber) ----
    // Πίνακας: sql/push_subscriptions.sql (χωρίς EF migrations, όπως όλο το project).
    // Κάθε συσκευή/browser που ενεργοποιεί ειδοποιήσεις είναι μία γραμμή.
    // Το Endpoint είναι μοναδικό: αν συνδεθεί άλλος χρήστης στην ίδια συσκευή,
    // η γραμμή απλώς αλλάζει UserID.
    [Table("PushSubscriptions")]
    public class UserPushSubscription
    {
        [Key]
        public long SubscriptionID { get; set; }
        public int UserID { get; set; }
        public string Endpoint { get; set; } = "";
        public string P256dh { get; set; } = "";
        public string Auth { get; set; } = "";
        public string? UserAgent { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class PushConfigDto
    {
        public bool Enabled { get; set; }
        public string? PublicKey { get; set; }
        public string? Problem { get; set; }   // τι λείπει από τη ρύθμιση του server
    }

    public class PushSubscribeRequest
    {
        public int UserID { get; set; }
        public string Endpoint { get; set; } = "";
        public string P256dh { get; set; } = "";
        public string Auth { get; set; } = "";
        public string? UserAgent { get; set; }
    }

    public class PushUnsubscribeRequest
    {
        public string Endpoint { get; set; } = "";
    }

    public class PushTestRequest
    {
        public int UserID { get; set; }
    }

    public class PushResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
