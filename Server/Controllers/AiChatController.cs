using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StallmedManager.Server.Services;
using StallmedManager.Shared.Models;
using System.Text;
using System.Text.Json;

namespace StallmedManager.Server.Controllers
{
    [Route("api/ai")]
    [ApiController]
    [Authorize]
    public class AiChatController : ControllerBase
    {
        private const string AnthropicUrl = "https://api.anthropic.com/v1/messages";
        private const string Model = "claude-sonnet-4-6";
        private const int MaxTokens = 1024;
        // Πόσα προηγούμενα μηνύματα του chat στέλνονται στο API (έλεγχος κόστους/tokens)
        private const int MaxHistoryMessages = 10;

        private const string SystemPromptTemplate = @"Είσαι ο AI βοηθός του SBT Suite — πλατφόρμα διαχείρισης φαρμακευτικών παραγγελιών
για τις εταιρείες SM (StallMedicals, CompanyID=""1"") και BM (BeltaMed, CompanyID=""2"").

Διαχειρίζεσαι:
- Παραγγελίες εμβολίων αλλεργίας (WebOrders) — γιατροί, ασθενείς, φαρμακεία
- Prick Test παραγγελίες (DoctorOrders) — status: Open, ReadyToShip, Fulfilled, Cancelled
- Αποθέματα (Stock) με FIFO διαχείριση, lots και ημερομηνίες λήξης
- Αλλεργιογόνα (Treatments) με κωδικούς SM/BM
- Αποστολές (Shipments) και παραλαβές (Receiving)

ΤΡΕΧΟΝΤΑ ΔΕΔΟΜΕΝΑ:
{contextData}

Κανόνες:
- Απαντάς ΠΑΝΤΑ στα Ελληνικά
- Σύντομα και πρακτικά
- Αν δεν ξέρεις κάτι ακριβώς, πες το ειλικρινά
- Μπορείς να προτείνεις ενέργειες (π.χ. ""πρέπει να παραγγείλεις grass pollen"")
- Μη δίνεις ψευδή νούμερα — χρησιμοποίησε μόνο τα δεδομένα context";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly AiContextService _contextService;
        private readonly ILogger<AiChatController> _logger;

        public AiChatController(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            AiContextService contextService,
            ILogger<AiChatController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _contextService = contextService;
            _logger = logger;
        }

        [HttpPost("chat")]
        public async Task<ActionResult<ChatResponse>> Chat([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Message))
                return BadRequest();

            var apiKey = _configuration["Anthropic:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                // Το key μπαίνει μόνο στο production appsettings.json — ποτέ στον client
                return Ok(new ChatResponse
                {
                    Reply = "Ο AI βοηθός δεν έχει ρυθμιστεί ακόμα (λείπει το API key). Επικοινώνησε με τον διαχειριστή."
                });
            }

            string contextData;
            try
            {
                contextData = await _contextService.GetContextAsync();
            }
            catch (Exception ex)
            {
                // Αν αποτύχει η βάση, ο βοηθός συνεχίζει χωρίς live δεδομένα
                _logger.LogError(ex, "AiContextService failed to build context");
                contextData = "(Τα τρέχοντα δεδομένα δεν είναι διαθέσιμα αυτή τη στιγμή.)";
            }

            var systemPrompt = SystemPromptTemplate.Replace("{contextData}", contextData);

            // History + νέο μήνυμα, με εναλλαγή ρόλων όπως απαιτεί το API
            var messages = new List<object>();
            foreach (var m in (request.History ?? new List<ChatMessage>())
                     .Where(m => m.Role is "user" or "assistant" && !string.IsNullOrWhiteSpace(m.Content))
                     .TakeLast(MaxHistoryMessages))
            {
                messages.Add(new { role = m.Role, content = m.Content });
            }
            messages.Add(new { role = "user", content = request.Message });

            var body = new
            {
                model = Model,
                max_tokens = MaxTokens,
                system = systemPrompt,
                messages
            };

            try
            {
                var http = _httpClientFactory.CreateClient("Anthropic");
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, AnthropicUrl);
                httpRequest.Headers.Add("x-api-key", apiKey);
                httpRequest.Headers.Add("anthropic-version", "2023-06-01");
                httpRequest.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

                using var response = await http.SendAsync(httpRequest);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Anthropic API error {Status}: {Body}", (int)response.StatusCode, json);
                    return Ok(new ChatResponse { Reply = "⚠️ Ο AI βοηθός δεν μπόρεσε να απαντήσει. Δοκίμασε ξανά σε λίγο." });
                }

                using var doc = JsonDocument.Parse(json);
                var reply = new StringBuilder();
                if (doc.RootElement.TryGetProperty("content", out var content))
                {
                    foreach (var block in content.EnumerateArray())
                    {
                        if (block.TryGetProperty("type", out var type) && type.GetString() == "text"
                            && block.TryGetProperty("text", out var text))
                        {
                            reply.Append(text.GetString());
                        }
                    }
                }

                if (reply.Length == 0)
                    return Ok(new ChatResponse { Reply = "⚠️ Ο AI βοηθός δεν επέστρεψε απάντηση. Δοκίμασε ξανά." });

                return Ok(new ChatResponse { Reply = reply.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to call Anthropic API");
                return Ok(new ChatResponse { Reply = "⚠️ Σφάλμα επικοινωνίας με τον AI βοηθό. Δοκίμασε ξανά." });
            }
        }
    }
}
