using FIS.Core.Application.Services.Billing;
using FIS.Api.Services;
using FIS.Core.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// Tariff calculation API endpoints
/// Provides access to the comprehensive tariff calculation system
/// </summary>
[ApiController]
[Authorize(
    Roles =
        "Contracts,Contract (Load and Manage),Contract (Approver),Financial Reports,Financial Data (Own Department),Financial Data (All Departments),Financial Tariff Parameters,Financial Tariff Parameters (Approver),Vehicle Master"
)]
[Route("api/[controller]")]
[Produces("application/json")]
public class TariffController : BaseApiController
{
    private readonly ITariffCalculationService _tariffCalculationService;
    private readonly IContractRepository _contractRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ITariffRepository _tariffRepository;
    private readonly LegacyVehicleScopeService _vehicleScope;
    private readonly ILogger<TariffController> _logger;

    public TariffController(
        ITariffCalculationService tariffCalculationService,
        IContractRepository contractRepository,
        IVehicleRepository vehicleRepository,
        ITariffRepository tariffRepository,
        LegacyVehicleScopeService vehicleScope,
        ILogger<TariffController> logger
    )
    {
        _tariffCalculationService =
            tariffCalculationService
            ?? throw new ArgumentNullException(nameof(tariffCalculationService));
        _contractRepository = contractRepository;
        _vehicleRepository = vehicleRepository;
        _tariffRepository = tariffRepository;
        _vehicleScope = vehicleScope;
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
            var contract = await _contractRepository.GetByIdAsync(contractCode);
            if (contract is null)
                return NotFound(new { error = $"Contract {contractCode} not found." });
            if (!await IsSiteAllowedAsync(contract.site_code))
                return NotFound(new { error = $"Contract {contractCode} not found." });

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
            if (request is null || !ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!await IsVehicleAllowedAsync(request.VmfCode))
                return NotFound(new { error = $"Vehicle {request.VmfCode} not found." });
            if (!await IsSiteAllowedAsync(request.SiteCode))
                return Forbid();

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
            var vehicle = await _vehicleRepository.GetByIdAsync(
                vmfCode,
                await ResolveAllowedVehicleSiteCodesAsync(),
                GetCurrentUserId()
            );

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

            // Resolve through the guarded compatibility repository. The
            // original tariff table predates the expanded audit columns that
            // EF's static model expects, so a direct DbSet query can fail on
            // the restored client schema even though the legacy tariff row is
            // present.
            var tariff = await _tariffRepository.GetApprovedTariffForClassAsync(classCode, today);

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

    private Task<IReadOnlySet<short>?> ResolveAllowedVehicleSiteCodesAsync() =>
        _vehicleScope.ResolveAllowedSiteCodesAsync(User, HttpContext.RequestAborted);

    private async Task<bool> IsVehicleAllowedAsync(int vmfCode) =>
        await _vehicleRepository.GetByIdAsync(
            vmfCode,
            await ResolveAllowedVehicleSiteCodesAsync(),
            GetCurrentUserId()
        ) is not null;

    private async Task<bool> IsSiteAllowedAsync(short siteCode)
    {
        var allowed = await ResolveAllowedVehicleSiteCodesAsync();
        return allowed is null || allowed.Contains(siteCode);
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
