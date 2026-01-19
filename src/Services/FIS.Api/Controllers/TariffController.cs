using FIS.Core.Application.Services.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// Tariff calculation API endpoints
/// Provides access to the comprehensive tariff calculation system
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TariffController : ControllerBase
{
    private readonly ITariffCalculationService _tariffCalculationService;
    private readonly ILogger<TariffController> _logger;

    public TariffController(
        ITariffCalculationService tariffCalculationService,
        ILogger<TariffController> logger)
    {
        _tariffCalculationService = tariffCalculationService ?? throw new ArgumentNullException(nameof(tariffCalculationService));
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
        [FromQuery] string tariffType = "Fixed")
    {
        try
        {
            var date = checkDate ?? DateTime.Now;
            var type = tariffType.ToLower() == "kilos" ? TariffType.Kilos : TariffType.Fixed;

            _logger.LogInformation("Getting tariff for contract {ContractCode}, date {Date}, type {Type}",
                contractCode, date, type);

            var result = await _tariffCalculationService.GetVehicleTariffAsync(
                contractCode, date, type);

            if (result.Status != TariffStatus.Valid && result.Amount < 0)
            {
                return NotFound(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating tariff for contract {ContractCode}", contractCode);
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
        [FromBody] TariffCalculationRequest request)
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
                request.TariffType);

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
        [FromQuery] int departmentCode)
    {
        var isInternal = _tariffCalculationService.IsGGMTInternal(siteCode, departmentCode);
        return Ok(new { siteCode, departmentCode, isInternal });
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
