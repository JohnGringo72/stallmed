using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StallmedManager.Server.Services;
using StallmedManager.Shared.Models;

namespace StallmedManager.Server.Controllers
{
    [Route("api/chatbot")]
    [ApiController]
    [Authorize]
    public class ChatBotController : ControllerBase
    {
        private readonly ChatBotService _chatBotService;
        private readonly ILogger<ChatBotController> _logger;

        public ChatBotController(ChatBotService chatBotService, ILogger<ChatBotController> logger)
        {
            _chatBotService = chatBotService;
            _logger = logger;
        }

        [HttpPost("query")]
        public async Task<ActionResult<ChatBotResponse>> Query([FromBody] ChatBotRequest request)
        {
            _logger.LogInformation("ChatBot query received: {Message}", request?.Message);

            if (string.IsNullOrWhiteSpace(request?.Message))
                return BadRequest();

            try
            {
                var result = await _chatBotService.ProcessMessage(request.Message, request.CompanyId);
                _logger.LogInformation("ChatBot response type: {Type}", result.Type);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChatBot query FAILED for: {Message}", request.Message);
                return Ok(new ChatBotResponse
                {
                    Reply = $"⚠️ Σφάλμα: {ex.Message}",
                    Type = "error"
                });
            }
        }

        // Γρήγορο τεστ ότι ο controller φορτώνει: /api/chatbot/ping
        // AllowAnonymous ώστε να δουλεύει και απευθείας από τον browser χωρίς
        // token (το class-level [Authorize] θα το μπλόκαρε). Δεν εκθέτει δεδομένα.
        [HttpGet("ping")]
        [AllowAnonymous]
        public ActionResult Ping()
        {
            return Ok(new ChatBotResponse { Reply = "🏓 Pong! Ο ChatBot δουλεύει.", Type = "test" });
        }
    }
}
