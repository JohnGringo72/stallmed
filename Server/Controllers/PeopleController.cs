using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StallmedManager.Server.Models;
using StallmedManager.Shared;
using StallmedManager.Shared.Models;

namespace StallmedManager.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PeopleController : ControllerBase
    {
        private StallmedContext context;
        private readonly ILogger<PeopleController> _logger;

        public PeopleController(ILogger<PeopleController> logger, StallmedContext context)
        {
            _logger = logger;
            this.context = context;
        }

        [HttpGet]
        [Authorize]
        public IEnumerable<Person> Get([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            return context.OnlineData.Where(c => c.OrderDate >= fromDate && c.OrderDate <= toDate);
        }
    }
}