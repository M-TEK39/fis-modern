using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Operations;
using FIS.Core.Infrastructure.Repositories;
using FIS.Data.SqlServer;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize(Roles = "Private Hire Vehicles,Taxi information maintenance,SystemAdministrator,System Administrator")]
[Route("api/[controller]")]
public class TaxiController : BaseApiController
{
    private readonly ITaxiRepository _repository;
    private readonly ITaxiWhiteLogRepository _whiteLogRepository;
    private readonly ISiteRepository _siteRepository;
    private readonly IContractRepository _contractRepository;
    private readonly FisDbContext _context;
    private readonly ILogger<TaxiController> _logger;

    public TaxiController(
        ITaxiRepository repository,
        ITaxiWhiteLogRepository whiteLogRepository,
        ISiteRepository siteRepository,
        IContractRepository contractRepository,
        FisDbContext context,
        ILogger<TaxiController> logger
    )
    {
        _repository = repository;
        _whiteLogRepository = whiteLogRepository;
        _siteRepository = siteRepository;
        _contractRepository = contractRepository;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Taxi>>> GetAll()
    {
        try
        {
            return Ok(await _repository.GetAllAsync(await ResolveAllowedSiteCodesAsync()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpGet("page")]
    public async Task<ActionResult> GetPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        [FromQuery] bool pendingOnly = false,
        [FromQuery] bool jiaPickupOnly = false,
        [FromQuery] string? search = null
    )
    {
        try
        {
            var result = await _repository.GetPageAsync(
                new TaxiPageQuery(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, 100),
                    pendingOnly,
                    jiaPickupOnly,
                    string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                    await ResolveAllowedSiteCodesAsync()
                )
            );
            return Ok(
                new
                {
                    items = result.Items,
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.Total,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged taxi requests");
            return StatusCode(500);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Taxi>> GetById(int id)
    {
        try
        {
            var item = await _repository.GetByIdAsync(id, await ResolveAllowedSiteCodesAsync());
            return item == null ? NotFound() : Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpGet("lookup/{rekNum}")]
    public async Task<ActionResult<Taxi>> GetByRequisition(string rekNum)
    {
        try
        {
            var item = await _repository.GetLatestByRequisitionAsync(
                rekNum,
                await ResolveAllowedSiteCodesAsync()
            );
            return item == null ? NotFound() : Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up taxi requisition {RekNum}", rekNum);
            return StatusCode(500);
        }
    }

    [HttpPost]
    public async Task<ActionResult<Taxi>> Create([FromBody] Taxi item)
    {
        try
        {
            if (item is null)
                return BadRequest(new { error = "Taxi request data is required." });
            if (!await IsSiteAllowedAsync(item.site_code))
                return Forbid();
            var created = await _repository.CreateAsync(item, GetCurrentUserId());
            return CreatedAtAction(nameof(GetById), new { id = created.request_id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (LegacyTaxiWorkflowUnavailableException ex)
        {
            _logger.LogWarning(ex, "Taxi request create is unavailable without the legacy trigger workflow");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "The legacy taxi-request accounting workflow is unavailable.", source = "legacy-trigger-required" }
            );
        }
        catch (LegacyTaxiProcedureContractException ex)
        {
            _logger.LogError(ex, "Taxi request create cannot use the deployed legacy requisition procedure");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "The deployed legacy taxi-requisition procedure is incompatible.", source = "legacy-procedure-contract" }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpPost("recurring")]
    [Authorize(Roles = "Book Recurring Taxi,Book Recuring Taxi,SystemAdministrator,System Administrator")]
    public async Task<ActionResult<IEnumerable<Taxi>>> CreateRecurring(
        [FromBody] CreateRecurringTaxiRequest request
    )
    {
        if (request is null || request.Taxi is null)
            return BadRequest(new { error = "Recurring taxi request data is required." });
        if (request.StartDate.Date > request.EndDate.Date)
            return BadRequest(new { error = "The recurring booking end date must be on or after its start date." });

        try
        {
            if (!await IsSiteAllowedAsync(request.Taxi.site_code))
                return Forbid();
            var created = await _repository.CreateRecurringAsync(
                request.Taxi,
                request.StartDate,
                request.EndDate,
                GetCurrentUserId()
            );
            return Ok(created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (LegacyTaxiRecurringWorkflowUnavailableException ex)
        {
            _logger.LogError(ex, "Recurring taxi request sequence is unavailable");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "The legacy recurring taxi sequence is unavailable. No recurring requests were written.", source = "legacy-sequence-required" }
            );
        }
        catch (LegacyTaxiWorkflowUnavailableException ex)
        {
            _logger.LogWarning(ex, "Recurring taxi request trigger workflow is unavailable");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "The legacy taxi-request accounting workflow is unavailable. No recurring requests were written.", source = "legacy-trigger-required" }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating recurring taxi requests");
            return StatusCode(500);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Taxi>> Update(int id, [FromBody] Taxi item)
    {
        try
        {
            if (item is null)
                return BadRequest(new { error = "Taxi request data is required." });
            if (id != item.request_id)
                return BadRequest();
            var allowedSites = await ResolveAllowedSiteCodesAsync();
            var existing = await _repository.GetByIdAsync(id, allowedSites);
            if (existing is null)
                return NotFound();
            if (!await IsSiteAllowedAsync(item.site_code))
                return Forbid();
            return Ok(await _repository.UpdateAsync(item, GetCurrentUserId()));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (LegacyTaxiWorkflowUnavailableException ex)
        {
            _logger.LogWarning(ex, "Taxi request update is unavailable without the legacy trigger workflow");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "The legacy taxi-request accounting workflow is unavailable.", source = "legacy-trigger-required" }
            );
        }
        catch (LegacyTaxiProcedureContractException ex)
        {
            _logger.LogError(ex, "Taxi request update cannot use the deployed legacy requisition contract");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "The deployed legacy taxi-requisition procedure is incompatible.", source = "legacy-procedure-contract" }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            if (await _repository.GetByIdAsync(id, await ResolveAllowedSiteCodesAsync()) is null)
                return NotFound();
            await _repository.DeleteAsync(id, GetCurrentUserId());
            return NoContent();
        }
        catch (LegacyTaxiWorkflowUnavailableException ex)
        {
            _logger.LogWarning(ex, "Taxi request delete is unavailable without the legacy trigger workflow");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "The legacy taxi-request delete workflow is unavailable.", source = "legacy-trigger-required" }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    // --- White Log endpoints ---

    [HttpPost("white-log")]
    public async Task<ActionResult> CreateWhiteLog([FromBody] CreateWhiteLogRequest request)
    {
        try
        {
            if (!await IsVehicleAllowedAsync(request.vmf_code))
                return Forbid();
            if (request.end_odo <= request.start_odo)
                return BadRequest("End odometer must be greater than start odometer.");

            if (request.end_date < request.start_date)
                return BadRequest("End date must be on or after start date.");

            var log = new TaxiWhiteLog
            {
                vmf_code = request.vmf_code,
                start_odo = request.start_odo,
                end_odo = request.end_odo,
                start_date = request.start_date,
                end_date = request.end_date,
                driver = request.driver,
                user_access_code = (short)GetCurrentUserId(),
            };

            var created = await _whiteLogRepository.CreateAsync(log, GetCurrentUserId());
            return Ok(created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating white log");
            return StatusCode(500);
        }
    }

    [HttpGet("white-log")]
    public async Task<ActionResult<IEnumerable<TaxiWhiteLog>>> GetAllWhiteLogs()
    {
        try
        {
            var logs = await _whiteLogRepository.GetAllAsync();
            var allowed = await ResolveAllowedSiteCodesAsync();
            if (allowed is null)
                return Ok(logs);

            var allowedVehicleCodes = (await _contractRepository.GetActiveContractsAsync())
                .Where(contract => allowed.Contains(contract.site_code))
                .Select(contract => contract.vmf_code)
                .ToHashSet();
            return Ok(logs.Where(log => allowedVehicleCodes.Contains(log.vmf_code)).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpGet("white-log/vehicle/{vmfCode}")]
    public async Task<ActionResult<IEnumerable<TaxiWhiteLog>>> GetWhiteLogsByVehicle(int vmfCode)
    {
        try
        {
            if (!await IsVehicleAllowedAsync(vmfCode))
                return Forbid();
            return Ok(await _whiteLogRepository.GetByVehicleAsync(vmfCode));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    private async Task<IReadOnlySet<short>?> ResolveAllowedSiteCodesAsync()
    {
        if (HasGlobalTaxiScope())
            return null;

        var userId = GetCurrentUserId();
        var profileSiteCode = await _context.UserAccessOlds.AsNoTracking()
            .Where(user => user.user_access_code == userId)
            .Select(user => user.Site_code)
            .SingleOrDefaultAsync(HttpContext.RequestAborted);
        if (profileSiteCode is not > 0)
            return new HashSet<short>();

        var profileSite = await _siteRepository.GetByIdAsync(profileSiteCode.Value);
        if (profileSite is null)
            return new HashSet<short>();

        var sites = await _siteRepository.GetActiveSitesAsync();
        if (
            HasRole("Vehicle List for All Departments in Province")
            && profileSite.province_code.HasValue
        )
        {
            sites = sites.Where(site => site.province_code == profileSite.province_code.Value);
        }
        else if (
            HasRole("Vehicle List for All Sites in Department")
            && profileSite.Depatrment_code.HasValue
        )
        {
            sites = sites.Where(site => site.Depatrment_code == profileSite.Depatrment_code.Value);
        }
        else
        {
            sites = sites.Where(site => site.Site_code == profileSite.Site_code);
        }

        return sites.Select(site => site.Site_code).ToHashSet();
    }

    private async Task<bool> IsSiteAllowedAsync(short siteCode)
    {
        var allowed = await ResolveAllowedSiteCodesAsync();
        return allowed is null || allowed.Contains(siteCode);
    }

    private async Task<bool> IsVehicleAllowedAsync(int vmfCode)
    {
        if (vmfCode <= 0)
            return false;
        var allowed = await ResolveAllowedSiteCodesAsync();
        if (allowed is null)
            return true;
        var contract = await _contractRepository.GetActiveContractByVehicleAsync(vmfCode);
        return contract is not null && allowed.Contains(contract.site_code);
    }

    private bool HasGlobalTaxiScope() =>
        HasRole("SystemAdministrator") || HasRole("System Administrator");

    private bool HasRole(string expectedRole) =>
        User.Claims
            .Where(claim =>
                claim.Type == ClaimTypes.Role
                || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
                || claim.Type.Equals("roles", StringComparison.OrdinalIgnoreCase)
            )
            .SelectMany(claim =>
                claim.Value.Split(
                    ',',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                )
            )
            .Any(role => string.Equals(role, expectedRole, StringComparison.OrdinalIgnoreCase));
}

public class CreateWhiteLogRequest
{
    public int vmf_code { get; set; }
    public long start_odo { get; set; }
    public long end_odo { get; set; }
    public DateTime start_date { get; set; }
    public DateTime end_date { get; set; }
    public string? driver { get; set; }
}

public sealed class CreateRecurringTaxiRequest
{
    public Taxi Taxi { get; set; } = new();
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
