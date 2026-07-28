// ============================================================
// ΑΝΕΝΕΡΓΟΣ ΚΩΔΙΚΑΣ (23/07/2026): δεν αναφέρεται πουθενά στο project.
// Σχολιάστηκε αντί να διαγραφεί -- αφαίρεσε τα // αν ξαναχρειαστεί.
// ============================================================
// using Microsoft.AspNetCore.Mvc;
// using StallmedManager.Server.Models;
// using StallmedManager.Shared.Models;
// 
// namespace StallmedManager.Server.Controllers
// {
//     [ApiController]
//     [Route("[controller]")]
//     public class WeatherForecastController : ControllerBase
//     {
//         private StallmedContext context;
//         private static readonly string[] Summaries = new[]
//         {
//         "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
//     };
// 
//         private readonly ILogger<WeatherForecastController> _logger;
// 
//         public WeatherForecastController(ILogger<WeatherForecastController> logger, StallmedContext context)
//         {
//             _logger = logger;
//             this.context = context;
//         }
// 
//         [HttpGet]
//         public IEnumerable<WeatherForecast> Get()
//         {
//             var j = context.OnlineData.ToList();
//             return Enumerable.Range(1, 5).Select(index => new WeatherForecast
//             {
//                 Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
//                 TemperatureC = Random.Shared.Next(-20, 55),
//                 Summary = Summaries[Random.Shared.Next(Summaries.Length)]
//             })
//             .ToArray();
//         }
//     }
// }