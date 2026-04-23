using Microsoft.AspNetCore.Mvc;
using StallmedManager.Services;
using StallmedManager.Models;

namespace StallmedManager.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class StockSearchController : ControllerBase
{
    private readonly StockSearchService _service;

    public StockSearchController(StockSearchService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<StockSearchResult>>> Get(
        [FromQuery] string? searchText,
        [FromQuery] string? companyID,
        [FromQuery] string? treatment,
        [FromQuery] string? allergen)
    {
        var results = await _service.SearchAsync(
            searchText ?? "",
            companyID ?? "",
            treatment ?? "",
            allergen ?? "");
        return Ok(results);
    }

    [HttpGet("options")]
    public async Task<ActionResult<StockFilterOptions>> GetOptions(
        [FromQuery] string? companyID,
        [FromQuery] string? treatment)
    {
        var options = await _service.GetFilterOptionsAsync(
            companyID ?? "",
            treatment ?? "");
        return Ok(options);
    }
}