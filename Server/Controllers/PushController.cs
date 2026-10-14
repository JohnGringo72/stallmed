using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StallmedManager.Server.Models;
using StallmedManager.Server.Services;
using StallmedManager.Shared.Models;

namespace StallmedManager.Server.Controllers
{
    // ---- Ειδοποιήσεις Push: εγγραφή/διαγραφή συσκευών ----
    // Ταυτότητα χρήστη: ο client στέλνει UserID (ίδιο pattern με τα υπόλοιπα modules).
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PushController : ControllerBase
    {
        private readonly StallmedContext _context;
        private readonly PushNotificationService _push;
        private readonly ILogger<PushController> _logger;

        public PushController(StallmedContext context, PushNotificationService push, ILogger<PushController> logger)
        {
            _context = context;
            _push = push;
            _logger = logger;
        }

        // Το δημόσιο κλειδί VAPID το χρειάζεται ο browser για να φτιάξει τη συνδρομή.
        [HttpGet("config")]
        public ActionResult<PushConfigDto> GetConfig()
        {
            _push.RememberSiteOrigin($"{Request.Scheme}://{Request.Host}");
            return Ok(new PushConfigDto { Enabled = _push.IsConfigured, PublicKey = _push.PublicKey });
        }

        [HttpPost("subscribe")]
        public async Task<ActionResult<PushResult>> Subscribe([FromBody] PushSubscribeRequest req)
        {
            if (req.UserID <= 0 || string.IsNullOrWhiteSpace(req.Endpoint)
                || string.IsNullOrWhiteSpace(req.P256dh) || string.IsNullOrWhiteSpace(req.Auth))
                return Ok(new PushResult { Success = false, Message = "Ελλιπή στοιχεία συνδρομής." });

            try
            {
                // Ίδια συσκευή = ίδιο endpoint: ενημερώνεται (π.χ. άλλαξε ο χρήστης που συνδέθηκε).
                var sub = await _context.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == req.Endpoint);
                if (sub == null)
                {
                    sub = new UserPushSubscription { Endpoint = req.Endpoint, CreatedAt = DateTime.Now };
                    _context.PushSubscriptions.Add(sub);
                }
                sub.UserID = req.UserID;
                sub.P256dh = req.P256dh;
                sub.Auth = req.Auth;
                sub.UserAgent = req.UserAgent?.Length > 400 ? req.UserAgent.Substring(0, 400) : req.UserAgent;
                sub.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                return Ok(new PushResult { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Αποτυχία αποθήκευσης συνδρομής push -- τρέξε το sql/push_subscriptions.sql");
                return Ok(new PushResult { Success = false, Message = "Δεν αποθηκεύτηκε η συσκευή στον server." });
            }
        }

        // Στο logout: η συσκευή σταματά να παίρνει ειδοποιήσεις του χρήστη που έφυγε.
        [HttpPost("unsubscribe")]
        public async Task<ActionResult<PushResult>> Unsubscribe([FromBody] PushUnsubscribeRequest req)
        {
            try
            {
                var sub = await _context.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == req.Endpoint);
                if (sub != null)
                {
                    _context.PushSubscriptions.Remove(sub);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Αποτυχία διαγραφής συνδρομής push");
            }
            return Ok(new PushResult { Success = true });
        }

        [HttpPost("test")]
        public async Task<ActionResult<PushResult>> Test([FromBody] PushTestRequest req)
        {
            if (!_push.IsConfigured)
                return Ok(new PushResult { Success = false, Message = "Δεν έχουν ρυθμιστεί τα κλειδιά Push στον server." });

            var sent = await _push.SendNowAsync(req.UserID, "Δοκιμή ειδοποίησης",
                "Οι ειδοποιήσεις δουλεύουν σε αυτή τη συσκευή ✔", "/mytasks");
            return Ok(sent > 0
                ? new PushResult { Success = true, Message = $"Στάλθηκε σε {sent} συσκευή(ές)." }
                : new PushResult { Success = false, Message = "Δεν βρέθηκε ενεργή συσκευή για τον χρήστη." });
        }
    }
}
