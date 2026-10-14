using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StallmedManager.Server.Models;
using WebPush;

namespace StallmedManager.Server.Services
{
    // Ειδοποιήσεις Push σε κινητά/desktop (Web Push με κλειδιά VAPID).
    // Ρυθμίσεις στο appsettings.json, section "Push" (Subject, PublicKey, PrivateKey).
    //
    // Η αποστολή γίνεται στο παρασκήνιο: ο χρήστης που αποθηκεύει ένα θέμα δεν
    // περιμένει να απαντήσουν οι servers της Google/Apple. Είναι best-effort --
    // αν αποτύχει, το θέμα έχει ήδη αποθηκευτεί και το καμπανάκι το δείχνει κανονικά.
    public class PushNotificationService
    {
        private readonly IConfiguration _config;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PushNotificationService> _logger;
        private readonly WebPushClient _client = new();

        public PushNotificationService(IConfiguration config, IServiceScopeFactory scopeFactory,
            ILogger<PushNotificationService> logger)
        {
            _config = config;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public string? PublicKey => _config["Push:PublicKey"];

        // Η Apple/Google θέλουν "subject" (mailto: ή https://) για επικοινωνία. Αν δεν
        // γραφτεί στο appsettings, χρησιμοποιείται η διεύθυνση του site, όπως τη βλέπει
        // ο server στο πρώτο αίτημα του client (το /api/push/config καλείται σε κάθε άνοιγμα).
        private string? _siteOrigin;
        public void RememberSiteOrigin(string origin)
        {
            if (_siteOrigin == null && origin.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                _siteOrigin = origin;
        }
        private string? Subject =>
            !string.IsNullOrEmpty(_config["Push:Subject"]) ? _config["Push:Subject"] : _siteOrigin;

        public bool IsConfigured =>
            !string.IsNullOrEmpty(Subject) &&
            !string.IsNullOrEmpty(_config["Push:PublicKey"]) &&
            !string.IsNullOrEmpty(_config["Push:PrivateKey"]);

        // url: σελίδα που ανοίγει όταν ο χρήστης πατήσει την ειδοποίηση.
        // tag: ειδοποιήσεις με ίδιο tag αντικαθιστούν η μία την άλλη (π.χ. ένα θέμα = μία ειδοποίηση).
        public void Queue(IEnumerable<int> userIds, string title, string body, string url, string? tag = null)
        {
            var ids = userIds.Distinct().ToList();
            if (ids.Count == 0) return;

            if (!IsConfigured)
            {
                _logger.LogInformation("Παράλειψη push: δεν έχουν ρυθμιστεί κλειδιά στο Push του appsettings.");
                return;
            }

            _ = Task.Run(() => SendAsync(ids, title, body, url, tag));
        }

        // Άμεση αποστολή (για το κουμπί δοκιμής): επιστρέφει πόσες συσκευές την πήραν.
        public async Task<int> SendNowAsync(int userId, string title, string body, string url)
        {
            if (!IsConfigured) return 0;
            return await SendAsync(new List<int> { userId }, title, body, url, "test");
        }

        private async Task<int> SendAsync(List<int> userIds, string title, string body, string url, string? tag)
        {
            var sent = 0;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<StallmedContext>();

                var subs = await context.PushSubscriptions
                    .Where(s => userIds.Contains(s.UserID))
                    .ToListAsync();
                if (subs.Count == 0) return 0;

                var vapid = new VapidDetails(Subject,_config["Push:PublicKey"], _config["Push:PrivateKey"]);
                var payload = JsonSerializer.Serialize(new
                {
                    title,
                    body = Shorten(body, 180),
                    url,
                    tag
                });
                var options = new Dictionary<string, object>
                {
                    ["vapidDetails"] = vapid,
                    ["TTL"] = 60 * 60 * 24,   // αν το κινητό είναι κλειστό, παραδίδεται μέσα σε 1 ημέρα
                    // "high": το Android δεν την καθυστερεί όταν η οθόνη είναι σβηστή
                    ["headers"] = new Dictionary<string, object> { ["Urgency"] = "high" }
                };

                var expired = new List<Shared.Models.UserPushSubscription>();
                foreach (var s in subs)
                {
                    try
                    {
                        await _client.SendNotificationAsync(new PushSubscription(s.Endpoint, s.P256dh, s.Auth), payload, options);
                        sent++;
                    }
                    catch (WebPushException ex) when (ex.StatusCode == HttpStatusCode.Gone || ex.StatusCode == HttpStatusCode.NotFound)
                    {
                        // Ο χρήστης απενεργοποίησε τις ειδοποιήσεις ή απεγκατέστησε την εφαρμογή.
                        expired.Add(s);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Αποτυχία push στη συσκευή {Id} του χρήστη {User}", s.SubscriptionID, s.UserID);
                    }
                }

                if (expired.Count > 0)
                {
                    context.PushSubscriptions.RemoveRange(expired);
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Αποτυχία αποστολής push -- υπάρχει ο πίνακας PushSubscriptions; (sql/push_subscriptions.sql)");
            }
            return sent;
        }

        private static string Shorten(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var t = text.Trim();
            return t.Length <= max ? t : t.Substring(0, max - 1) + "…";
        }
    }
}
