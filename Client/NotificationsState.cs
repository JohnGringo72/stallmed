namespace StallmedManager.Client
{
    // Κοινή κατάσταση ειδοποιήσεων ανάμεσα στη σελίδα Tasks και στο μενού.
    // Όταν ο χρήστης ανοίξει ένα θέμα, το καμπανάκι πρέπει να ενημερωθεί ΑΜΕΣΩΣ
    // και όχι στον επόμενο έλεγχο του λεπτού.
    public class NotificationsState
    {
        public event Func<Task>? RefreshRequested;

        public async Task RequestRefresh()
        {
            var handler = RefreshRequested;
            if (handler != null) await handler.Invoke();
        }
    }
}
