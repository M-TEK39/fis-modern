using System.Data;
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

    public DriverLicenceTypeController(
        FisDbContext context,
        ILogger<DriverLicenceTypeController> logger
    )
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Lookup for the expanded dbo.driver_licence_types table. Archive
    /// validation maintains dbo.driver_licence instead; missing tables return
    /// an empty list rather than projecting is_deleted.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> GetAll()
    {
        try
        {
            if (!await TableExistsAsync())
            {
                return Ok(Array.Empty<object>());
            }

            var types = await _context
                .DriverLicenceTypes.AsNoTracking()
                .OrderBy(t => t.driver_licence_type_description)
                .Select(t => new
                {
                    driver_licence_type_id = t.driver_licence_type_id,
                    driver_licence_type_code = t.driver_licence_type_code,
                    driver_licence_type_description = t.driver_licence_type_description,
                })
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} driver licence types", types.Count);
            return Ok(types);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving driver licence types");
            return StatusCode(500, new { error = "Failed to retrieve driver licence types" });
        }
    }

    private async Task<bool> TableExistsAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT CASE WHEN OBJECT_ID(N'dbo.driver_licence_types', N'U') IS NULL THEN 0 ELSE 1 END";
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) == 1;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
