using Microsoft.JSInterop;
using StallmedManager.Shared.Models;

namespace StallmedManager.Client
{
    // Ειδοποιήσεις Push της συσκευής (κινητό/desktop): εγγραφή στον server,
    // συγχρονισμός σε κάθε άνοιγμα και διαγραφή στο logout.
    // Η JS πλευρά είναι το window.sbtPush στο index.html.
    public class PushClientService
    {
        private readonly DataService dataService;
        private readonly UserManager userManager;
        private readonly IJSRuntime js;
        private PushConfigDto? config;

        public PushClientService(DataService dataService, UserManager userManager, IJSRuntime js)
        {
            this.dataService = dataService;
            this.userManager = userManager;
            this.js = js;
        }

        // "unsupported" | "ios-needs-install" | "default" | "granted" | "denied" | "server-off"
        public string State { get; private set; } = "unsupported";

        // Η συσκευή είναι γραμμένη στον server και θα παίρνει push.
        public bool Active { get; private set; }

        public event Action? Changed;

        // Καλείται στην εκκίνηση: αν η άδεια έχει ήδη δοθεί, ξαναγράφει τη συσκευή στον
        // server (π.χ. συνδέθηκε άλλος χρήστης στο ίδιο κινητό ή άλλαξε η συνδρομή).
        public async Task InitializeAsync()
        {
            try
            {
                config = await dataService.Get<PushConfigDto>("api/push/config");
                State = await js.InvokeAsync<string>("sbtPush.state");
                if (config == null || !config.Enabled || string.IsNullOrEmpty(config.PublicKey))
                {
                    if (State != "ios-needs-install" && State != "unsupported") State = "server-off";
                    return;
                }

                if (State == "granted")
                {
                    var sub = await js.InvokeAsync<SubscriptionPayload?>("sbtPush.current");
                    // Άδεια υπάρχει αλλά όχι συνδρομή (π.χ. παλιό σύστημα ειδοποιήσεων): τη φτιάχνουμε σιωπηλά.
                    sub ??= await js.InvokeAsync<SubscriptionPayload?>("sbtPush.subscribe", config.PublicKey);
                    Active = sub?.Endpoint != null && await SaveAsync(sub);
                }
            }
            catch (Exception)
            {
                // Παλιό index.html από cache ή browser χωρίς υποστήριξη: απλώς δεν ενεργοποιείται.
                State = "unsupported";
            }
            finally
            {
                Changed?.Invoke();
            }
        }

        // Από κλικ του χρήστη -- οι browsers δίνουν άδεια μόνο έτσι.
        public async Task<string?> EnableAsync()
        {
            if (config == null || !config.Enabled || string.IsNullOrEmpty(config.PublicKey))
                return "Οι ειδοποιήσεις δεν έχουν ρυθμιστεί στον server.";
            try
            {
                var sub = await js.InvokeAsync<SubscriptionPayload?>("sbtPush.subscribe", config.PublicKey);
                State = await js.InvokeAsync<string>("sbtPush.state");
                if (sub?.Endpoint == null)
                {
                    return State == "denied"
                        ? "Οι ειδοποιήσεις είναι μπλοκαρισμένες. Επίτρεψέ τες από τις ρυθμίσεις του browser/κινητού."
                        : "Δεν ενεργοποιήθηκαν οι ειδοποιήσεις.";
                }
                Active = await SaveAsync(sub);
                return Active ? null : "Δεν αποθηκεύτηκε η συσκευή στον server.";
            }
            catch (Exception)
            {
                return "Δεν ενεργοποιήθηκαν οι ειδοποιήσεις σε αυτόν τον browser.";
            }
            finally
            {
                Changed?.Invoke();
            }
        }

        public async Task<PushResult?> SendTestAsync()
        {
            if (userManager.User == null) return null;
            return await dataService.Post<PushTestRequest, PushResult>("api/push/test",
                new PushTestRequest { UserID = userManager.User.IdUser });
        }

        // Πριν το logout (όσο υπάρχει ακόμα token): η συσκευή σταματά να παίρνει
        // ειδοποιήσεις του χρήστη που αποσυνδέεται.
        public async Task UnregisterAsync()
        {
            try
            {
                var sub = await js.InvokeAsync<SubscriptionPayload?>("sbtPush.current");
                if (sub?.Endpoint != null)
                    await dataService.Post<PushUnsubscribeRequest, PushResult>("api/push/unsubscribe",
                        new PushUnsubscribeRequest { Endpoint = sub.Endpoint });
            }
            catch (Exception) { }
            Active = false;
        }

        public async Task SetBadgeAsync(int count)
        {
            try { await js.InvokeVoidAsync("sbtPush.setBadge", count); }
            catch (Exception) { }
        }

        private async Task<bool> SaveAsync(SubscriptionPayload sub)
        {
            if (userManager.User == null) return false;
            var result = await dataService.Post<PushSubscribeRequest, PushResult>("api/push/subscribe", new PushSubscribeRequest
            {
                UserID = userManager.User.IdUser,
                Endpoint = sub.Endpoint!,
                P256dh = sub.P256dh ?? "",
                Auth = sub.Auth ?? "",
                UserAgent = sub.UserAgent
            });
            return result?.Success == true;
        }

        private class SubscriptionPayload
        {
            public string? Endpoint { get; set; }
            public string? P256dh { get; set; }
            public string? Auth { get; set; }
            public string? UserAgent { get; set; }
        }
    }
}
