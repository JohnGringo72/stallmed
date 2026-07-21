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
            if (string.IsNullOrWhiteSpace(request?.Message))
                return BadRequest();

            try
            {
                return Ok(await _chatBotService.ProcessMessage(request.Message, request.CompanyId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChatBot query failed for message: {Message}", request.Message);
                return Ok(new ChatBotResponse
                {
                    Reply = "⚠️ Κάτι πήγε στραβά κατά την αναζήτηση. Δοκίμασε ξανά.",
                    Type = "error"
                });
            }
        }
    }
}
