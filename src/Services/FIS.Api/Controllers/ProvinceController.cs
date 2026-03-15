using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
public class ProvinceController : BaseApiController
{
    private readonly FisDbContext _context;
    private readonly ILogger<ProvinceController> _logger;

    public ProvinceController(FisDbContext context, ILogger<ProvinceController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all provinces
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> GetAll()
    {
        try
        {
            var provinces = await _context.Provinces
                .Where(p => !p.is_deleted)
                .OrderBy(p => p.province_name)
                .Select(p => new
                {
                    province_code = p.province_code.ToString(),
                    province_name = p.province_name,
                    province_abbreviation = p.province_abbreviation
                })
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} provinces", provinces.Count);
            return Ok(provinces);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving provinces");
            return StatusCode(500, new { error = "Failed to retrieve provinces", message = ex.Message });
        }
    }
}
