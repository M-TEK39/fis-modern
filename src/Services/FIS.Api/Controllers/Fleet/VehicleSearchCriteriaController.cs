using FIS.Core.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
public class VehicleSearchCriteriaController : BaseApiController
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ILogger<VehicleSearchCriteriaController> _logger;

    public VehicleSearchCriteriaController(
        IVehicleRepository vehicleRepository,
        ILogger<VehicleSearchCriteriaController> logger
    )
    {
        _vehicleRepository = vehicleRepository;
        _logger = logger;
    }

    /// <summary>
    /// Returns fleet numbers and registration numbers for datalist autocomplete
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<string>>> Get()
    {
        if (!User.IsInRole("Vehicle Master"))
            return Forbid();

        try
        {
            var vehicles = await _vehicleRepository.GetActiveVehiclesAsync();

            var keywords = vehicles
                .SelectMany(v => new[] { v.fleet_number, v.registration_number })
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(k => k)
                .ToList();

            return Ok(keywords);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vehicle search criteria");
            return StatusCode(
                500,
                new { error = "Failed to fetch search criteria", message = ex.Message }
            );
        }
    }
}
