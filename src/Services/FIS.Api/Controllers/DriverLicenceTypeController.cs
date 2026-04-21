using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
public class DriverLicenceTypeController : BaseApiController
{
    private readonly FisDbContext _context;
    private readonly ILogger<DriverLicenceTypeController> _logger;

    public DriverLicenceTypeController(FisDbContext context, ILogger<DriverLicenceTypeController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all driver licence types from driver_licence_types table
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> GetAll()
    {
        try
        {
            var types = await _context.DriverLicenceTypes
                .Where(t => !t.is_deleted)
                .OrderBy(t => t.driver_licence_type_description)
                .Select(t => new
                {
                    driver_licence_type_id = t.driver_licence_type_id,
                    driver_licence_type_code = t.driver_licence_type_code,
                    driver_licence_type_description = t.driver_licence_type_description
                })
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} driver licence types", types.Count);
            return Ok(types);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving driver licence types");
            return StatusCode(500, new { error = "Failed to retrieve driver licence types", message = ex.Message });
        }
    }
}
