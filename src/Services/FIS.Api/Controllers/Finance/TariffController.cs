using FIS.Core.Application.Services.Billing;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

/// <summary>
/// Tariff calculation API endpoints
/// Provides access to the comprehensive tariff calculation system
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
public class TariffController : BaseApiController
{
    private readonly ITariffCalculationService _tariffCalculationService;
    private readonly FisDbContext _context;
    private readonly ILogger<TariffController> _logger;

    public TariffController(
        ITariffCalculationService tariffCalculationService,
        FisDbContext context,
        ILogger<TariffController> logger
    )
    {
        _tariffCalculationService =
            tariffCalculationService
            ?? throw new ArgumentNullException(nameof(tariffCalculationService));
        _context = context;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Calculate tariff for a contract
    /// </summary>
    /// <param name="contractCode">Contract identifier</param>
    /// <param name="checkDate">Date to check tariff for</param>
    /// <param name="tariffType">Type of tariff (Fixed or Kilos)</param>
    /// <returns>Tariff result with amount and status</returns>
    [HttpGet("contract/{contractCode}")]
    [ProducesResponseType(typeof(TariffResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TariffResult>> GetContractTariff(
        int contractCode,
        [FromQuery] DateTime? checkDate = null,
        [FromQuery] string tariffType = "Fixed"
    )
    {
        try
        {
            var date = checkDate ?? DateTime.Now;
            var type = tariffType.ToLower() == "kilos" ? TariffType.Kilos : TariffType.Fixed;

            _logger.LogInformation(
                "Getting tariff for contract {ContractCode}, date {Date}, type {Type}",
                contractCode,
                date,
                type
            );

            var result = await _tariffCalculationService.GetVehicleTariffAsync(
                contractCode,
                date,
                type
            );

            if (result.Status != TariffStatus.Valid && result.Amount < 0)
            {
                return NotFound(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error calculating tariff for contract {ContractCode}",
                contractCode
            );
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Calculate tariff for a vehicle with full parameters
    /// </summary>
    /// <param name="request">Tariff calculation request</param>
    /// <returns>Tariff result</returns>
    [HttpPost("calculate")]
    [ProducesResponseType(typeof(TariffResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TariffResult>> CalculateTariff(
        [FromBody] TariffCalculationRequest request
    )
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _logger.LogInformation("Calculating tariff for vehicle {VmfCode}", request.VmfCode);

            var result = await _tariffCalculationService.GetVehicleTariffAsync(
                request.StartDate,
                request.EndDate,
                request.StartOdometer,
                request.EndOdometer,
                request.VmfCode,
                request.SiteCode,
                request.DepartmentCode,
                request.ContractType,
                request.CheckDate,
                request.TariffType
            );

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating tariff for vehicle {VmfCode}", request.VmfCode);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Check if site/department is GGMT internal (should not be billed)
    /// </summary>
    [HttpGet("internal-check")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public ActionResult<bool> CheckGGMTInternal(
        [FromQuery] short siteCode,
        [FromQuery] int departmentCode
    )
    {
        var isInternal = _tariffCalculationService.IsGGMTInternal(siteCode, departmentCode);
        return Ok(
            new
            {
                siteCode,
                departmentCode,
                isInternal,
            }
        );
    }

    /// <summary>
    /// Check if vehicle is missing (should return 0.00 tariff)
    /// </summary>
    [HttpGet("missing-check")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public ActionResult<bool> CheckVehicleMissing([FromQuery] int vehicleStatusCode)
    {
        var isMissing = _tariffCalculationService.IsVehicleMissing(vehicleStatusCode);
        return Ok(new { vehicleStatusCode, isMissing });
    }

    /// <summary>
    /// Check if lease contract should be pro-rated to 0.00
    /// </summary>
    [HttpGet("lease-prorate-check")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public ActionResult<bool> CheckLeaseProRated([FromQuery] DateTime contractStartDate)
    {
        var isProRated = _tariffCalculationService.IsLeaseProRated(contractStartDate);
        return Ok(new { contractStartDate, isProRated });
    }

    /// <summary>
    /// Preview the current approved tariff for a vehicle — used to auto-populate tariff
    /// information when a new contract is being opened (before the contract exists).
    ///
    /// Lookup chain: vehicle → model → class_code → current approved tariff
    /// Returns null tariff fields if no approved tariff exists for the vehicle's class.
    /// </summary>
    [HttpGet("preview-for-vehicle/{vmfCode}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> PreviewForVehicle(int vmfCode)
    {
        try
        {
            var vehicle = await _context
                .Vehicles.Include(v => v.Model)
                .FirstOrDefaultAsync(v => v.vmf_code == vmfCode);

            if (vehicle == null)
                return NotFound(new { error = $"Vehicle {vmfCode} not found" });

            if (vehicle.Model == null)
                return Ok(
                    new
                    {
                        vmf_code = vmfCode,
                        fleet_number = vehicle.fleet_number,
                        year_manufactured = vehicle.year_manufactured,
                        class_code = (short?)null,
                        tariff = (object?)null,
                        note = "No model linked to vehicle — cannot resolve tariff class",
                    }
                );

            var classCode = vehicle.Model.class_code;
            var today = DateTime.Today;

            // Find the currently effective, approved tariff for this vehicle class
            var tariff = await _context
                .Tariffs.Where(t =>
                    t.class_code == classCode
                    && t.tariff_approval_status == 2
                    && // Approved
                    !t.is_deleted
                    && t.effective_start_date <= today
                    && (t.effective_end_date == null || t.effective_end_date >= today)
                )
                .OrderByDescending(t => t.effective_start_date)
                .FirstOrDefaultAsync();

            return Ok(
                new
                {
                    vmf_code = vmfCode,
                    fleet_number = vehicle.fleet_number,
                    registration = vehicle.registration_number,
                    year_manufactured = vehicle.year_manufactured,
                    model_code = vehicle.model_code,
                    class_code = classCode,
                    tariff = tariff == null
                        ? null
                        : new
                        {
                            tariff_code = tariff.tariff_code,
                            class_code = tariff.class_code,
                            year_manufactured = tariff.year_manufactured,
                            monthly_fixed_amount = tariff.monthly_fixed_amount,
                            monthly_odo_amount = tariff.monthly_odo_amount,
                            daily_fixed_amount = tariff.daily_fixed_amount,
                            hourly_fixed_amount = tariff.hourly_fixed_amount,
                            fuel_kilo_tariff = tariff.fuel_kilo_tariff,
                            effective_start_date = tariff.effective_start_date.ToString(
                                "yyyy-MM-dd"
                            ),
                            effective_end_date = tariff.effective_end_date?.ToString("yyyy-MM-dd"),
                        },
                    note = tariff == null
                        ? $"No approved tariff found for class {classCode} effective today"
                        : "Current approved tariff returned",
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing tariff for vehicle {VmfCode}", vmfCode);
            return StatusCode(
                500,
                new { error = "Failed to preview tariff", message = ex.Message }
            );
        }
    }
}

/// <summary>
/// Request model for tariff calculation
/// </summary>
public class TariffCalculationRequest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int StartOdometer { get; set; }
    public int EndOdometer { get; set; }
    public int VmfCode { get; set; }
    public short SiteCode { get; set; }
    public int DepartmentCode { get; set; }
    public string ContractType { get; set; } = "A";
    public DateTime CheckDate { get; set; } = DateTime.Now;
    public TariffType TariffType { get; set; } = TariffType.Fixed;
}
