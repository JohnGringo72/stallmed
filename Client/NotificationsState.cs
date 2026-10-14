namespace StallmedManager.Client
{
    // Κοινή κατάσταση ειδοποιήσεων ανάμεσα στη σελίδα Tasks και στο μενού.
    // Όταν ο χρήστης ανοίξει ένα θέμα, το καμπανάκι πρέπει να ενημερωθεί ΑΜΕΣΩΣ
    // και όχι στον επόμενο έλεγχο του λεπτού.
    public class NotificationsState
    {
        public event Func<Task>? RefreshRequested;

        // Ήρθε νέα κίνηση σε θέματα (push ή έλεγχος του καμπανακιού): η σελίδα Tasks
        // ανανεώνει λίστα και ανοιχτή συνομιλία χωρίς refresh από τον χρήστη.
        public event Func<Task>? ActivityChanged;

        public async Task RequestRefresh()
        {
            var handler = RefreshRequested;
            if (handler != null) await handler.Invoke();
        }

        public async Task RaiseActivityChanged()
        {
            var handler = ActivityChanged;
            if (handler != null) await handler.Invoke();
        }
    }
}
