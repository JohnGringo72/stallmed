// Service worker της SBT Suite: ΜΟΝΟ για ειδοποιήσεις push και εγκατάσταση στο κινητό.
// Σκόπιμα ΔΕΝ κάνει cache αρχείων (δεν έχει fetch handler): μετά από ανέβασμα νέας
// έκδοσης ο χρήστης πρέπει να βλέπει αμέσως τη νέα, όχι μια παλιά αποθηκευμένη.

self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', (event) => event.waitUntil(self.clients.claim()));

// Ο server στέλνει JSON: { title, body, url, tag }
self.addEventListener('push', (event) => {
    let data = {};
    try { data = event.data ? event.data.json() : {}; }
    catch (e) { data = { body: event.data ? event.data.text() : '' }; }

    const title = data.title || 'SBT Suite';
    const options = {
        body: data.body || '',
        icon: '/images/app/icon-192.png',
        badge: '/images/app/badge-96.png',   // μικρό μονόχρωμο εικονίδιο στη γραμμή του Android
        tag: data.tag || undefined,
        renotify: !!data.tag,                 // ίδιο θέμα: αντικαθιστά την παλιά, αλλά ξαναχτυπάει
        data: { url: data.url || '/' }
    };

    event.waitUntil(Promise.all([
        self.registration.showNotification(title, options),
        // Ανοιχτά παράθυρα της εφαρμογής: ανανεώνουν αμέσως λίστα/συνομιλία, χωρίς refresh.
        self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((all) => {
            all.forEach((client) => client.postMessage({ type: 'sbt-refresh', tag: data.tag || null }));
        })
    ]));
});

// Πάτημα στην ειδοποίηση: αν η εφαρμογή είναι ήδη ανοιχτή πάει εκεί, αλλιώς την ανοίγει.
self.addEventListener('notificationclick', (event) => {
    event.notification.close();
    const url = new URL((event.notification.data && event.notification.data.url) || '/', self.location.origin).href;

    event.waitUntil((async () => {
        const all = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
        for (const client of all) {
            if (new URL(client.url).origin === self.location.origin) {
                await client.focus();
                // Η εφαρμογή ακούει αυτό το μήνυμα και πλοηγείται χωρίς πλήρη επαναφόρτωση.
                client.postMessage({ type: 'sbt-navigate', url: url });
                return;
            }
        }
        await self.clients.openWindow(url);
    })());
});
