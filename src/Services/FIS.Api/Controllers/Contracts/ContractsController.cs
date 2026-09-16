using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Claims;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Contracts;
using FIS.Core.Domain.Entities.Financial;
using FIS.Core.Domain.Entities.ReferenceData;
using FIS.Core.Infrastructure.Repositories;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

/// <summary>
/// Applies the legacy Contracts module entitlement to every Contracts API action.
/// Fine-grained ownership and workflow checks remain on their respective actions.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class ContractAccessAttribute : Attribute, IAsyncActionFilter
{

    private static readonly string[] ContractRoles =
    [
        "contracts",
        "contract",
        "contract (load and manage)",
        "contracts (load and manage)",
        "contract_load_and_manage",
        "contracts_load_and_manage",
        "contract capturer",
        "contracts capturer",
        "contract (cancel and close)",
        "contracts (cancel and close)",
        "contract_cancel_and_close",
        "contracts_cancel_and_close",
        "contract (approver)",
        "contracts approver",
        "contract approver",
        "contracts_approver",
        "contract_approver",
        "back dating contract (approver)",
        "backdating contract (approver)",
        "contract (back dating approver)",
        "contract history back dating",
        "contract_history_backdating",
        "admin",
        "administrator",
        "system administrator",
        "systemadministrator",
    ];

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        var user = context.HttpContext.User;
        var hasNamedRole =
            ContractRoles.Any(user.IsInRole)
            || user.Claims.Any(claim =>
                (
                    claim.Type == ClaimTypes.Role
                    || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
                    || claim.Type.Equals("roles", StringComparison.OrdinalIgnoreCase)
                )
                && claim
                    .Value.Split(
                        ',',
                        StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                    )
                    .Any(role =>
                        ContractRoles.Any(expected =>
                            string.Equals(role, expected, StringComparison.OrdinalIgnoreCase)
                        )
                    )
            );
        if (!hasNamedRole)
        {
            context.Result = new ObjectResult(
                new { error = "You do not have permission to access vehicle contracts." }
            )
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
            return;
        }

        await next();
    }
}

/// <summary>
/// Contract/Hire management API endpoints
/// Provides comprehensive contract operations with legacy business logic
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
[ContractAccess]
public class ContractsController : BaseApiController
{
    private const short LegacyGfleetDepartmentCode = 147;
    private static readonly short[] LegacyGfleetSiteCodes = [1619, 1620, 1621, 1622];

    private readonly IContractRepository _contractRepository;
    private readonly IContractService _contractService;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IModelRepository _modelRepository;
    private readonly ITariffRepository _tariffRepository;
    private readonly ISiteRepository _siteRepository;
    private readonly IContractAuditLogRepository _auditLog;
    private readonly IEmailNotificationService _emailNotification;
    private readonly FisDbContext _context;
    private readonly ILogger<ContractsController> _logger;

    public ContractsController(
        IContractRepository contractRepository,
        IContractService contractService,
        IVehicleRepository vehicleRepository,
        IModelRepository modelRepository,
        ITariffRepository tariffRepository,
        ISiteRepository siteRepository,
        IContractAuditLogRepository auditLog,
        IEmailNotificationService emailNotification,
        FisDbContext context,
        ILogger<ContractsController> logger
    )
    {
        _contractRepository = contractRepository;
        _contractService = contractService;
        _vehicleRepository = vehicleRepository;
        _modelRepository = modelRepository;
        _tariffRepository = tariffRepository;
        _siteRepository = siteRepository;
        _auditLog = auditLog;
        _emailNotification = emailNotification;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Validates that the current user is not the contract owner (prevents self-approval)
    /// </summary>
    /// <returns>Null if validation passes, or ForbidResult with error message if validation fails</returns>
    private ActionResult? ValidateSelfApprovalPrevention(Contract contract, int currentUserId)
    {
        if (IsContractOwner(contract, currentUserId))
        {
            _logger.LogWarning(
                "Self-approval blocked: User {UserId} attempted to approve their own contract {ContractId}",
                currentUserId,
                contract.contract_code
            );

            return StatusCode(
                403,
                new
                {
                    error = "You cannot review or approve your own contract.",
                    contractId = contract.contract_code,
                    userId = currentUserId,
                }
            );
        }

        return null; // Validation passed
    }

    private bool HasAnyRole(params string[] expectedRoles)
    {
        if (expectedRoles.Any(User.IsInRole))
        {
            return true;
        }

        var roleClaims = User
            .Claims.Where(claim =>
                claim.Type == ClaimTypes.Role
                || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
                || claim.Type.Equals("roles", StringComparison.OrdinalIgnoreCase)
            )
            .SelectMany(claim =>
                claim.Value.Split(
                    ',',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                )
            );

        return roleClaims.Any(role =>
            expectedRoles.Any(expected =>
                string.Equals(role, expected, StringComparison.OrdinalIgnoreCase)
            )
        );
    }

    private bool HasContractAdminRole() =>
        HasAnyRole("admin", "administrator", "system administrator", "systemadministrator");

    private bool HasContractLoadAndManageRole() =>
        HasAnyRole(
            "contract (load and manage)",
            "contracts (load and manage)",
            "contract_load_and_manage",
            "contracts_load_and_manage",
            "contract capturer",
            "contracts capturer"
        );

    private bool HasContractCancelAndCloseRole() =>
        HasAnyRole(
            "contract (cancel and close)",
            "contracts (cancel and close)",
            "contract_cancel_and_close",
            "contracts_cancel_and_close"
        );

    private bool HasContractApproverRole() =>
        HasAnyRole(
            "contract (approver)",
            "contracts approver",
            "contract approver",
            "contracts_approver",
            "contract_approver",
            "admin",
            "administrator",
            "system administrator",
            "systemadministrator"
        );

    private bool HasContractHistoryBackdatingRole() =>
        HasAnyRole(
            "contract history back dating",
            "contract_history_backdating",
            "admin",
            "administrator",
            "system administrator",
            "systemadministrator"
        );

    private bool HasContractAccess()
    {
        if (
            HasAnyRole("contracts", "contract")
            || HasContractAdminRole()
            || HasContractLoadAndManageRole()
            || HasContractCancelAndCloseRole()
            || HasContractApproverRole()
            || HasContractHistoryBackdatingRole()
        )
        {
            return true;
        }

        return HasAnyRole("Contracts");
    }

    private bool HasGlobalContractVisibility() =>
        HasContractAdminRole()
        || HasContractApproverRole()
        || HasContractCancelAndCloseRole();

    private bool CanSeeContract(Contract contract, int currentUserId) =>
        HasGlobalContractVisibility() || IsContractOwner(contract, currentUserId);

    private ActionResult? RequireContractRecordVisibility(Contract contract)
    {
        if (CanSeeContract(contract, GetCurrentUserId()))
            return null;
        return StatusCode(
            StatusCodes.Status403Forbidden,
            new { error = "You may only view contracts you own or contracts covered by your approval scope." }
        );
    }

    private bool CanCaptureContract() => HasContractAdminRole() || HasContractLoadAndManageRole();

    private bool CanManageActiveContract() => HasContractAdminRole() || HasContractApproverRole();

    private bool CanCloseActiveContract() =>
        HasContractAdminRole() || HasContractCancelAndCloseRole() || HasContractApproverRole();

    private bool HasProvinceWideVehicleListRole() =>
        HasAnyRole("vehicle list for all departments in province");

    private bool HasAllSitesInDepartmentRole() =>
        HasAnyRole("vehicle list for all sites in department");

    private async Task<ContractLocationScope?> ResolveContractLocationScopeAsync()
    {
        var userAccessCode = GetCurrentUserId();
        if (userAccessCode is <= 0 or > short.MaxValue)
            return null;

        var profileSiteCode = await _context
            .UserAccessOlds.AsNoTracking()
            .Where(user => user.user_access_code == userAccessCode)
            .Select(user => user.Site_code)
            .SingleOrDefaultAsync(HttpContext.RequestAborted);
        if (profileSiteCode is not > 0)
            return null;

        var profileSite = await _siteRepository.GetByIdAsync(profileSiteCode.Value);
        if (profileSite?.Depatrment_code is not > 0)
            return null;

        return new ContractLocationScope(
            profileSite.Site_code,
            profileSite.Depatrment_code.Value,
            profileSite.province_code,
            profileSite.Depatrment_code.Value == LegacyGfleetDepartmentCode,
            HasProvinceWideVehicleListRole(),
            HasAllSitesInDepartmentRole()
        );
    }

    private async Task<IReadOnlyCollection<short>?> ResolveAllowedContractSiteCodesAsync()
    {
        var scope = await ResolveContractLocationScopeAsync();
        if (scope is null)
            return [];
        if (scope.HasUnrestrictedLegacyScope)
            return null;

        var sites = await _siteRepository.GetActiveSitesAsync();
        if (scope.HasProvinceWideDepartmentScope)
        {
            if (!scope.ProvinceCode.HasValue)
                return [];
            sites = sites.Where(site => site.province_code == scope.ProvinceCode.Value);
        }
        else if (scope.HasAllSitesInDepartmentScope)
        {
            sites = sites.Where(site => site.Depatrment_code == scope.DepartmentCode);
        }
        else
        {
            sites = sites.Where(site => site.Site_code == scope.SiteCode);
        }

        return sites.Select(site => site.Site_code).ToArray();
    }

    private async Task<ActionResult?> RequireContractSiteScopeAsync(short siteCode)
    {
        var allowedSiteCodes = await ResolveAllowedContractSiteCodesAsync();
        if (allowedSiteCodes is not null && !allowedSiteCodes.Contains(siteCode))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { error = "You do not have permission to access contracts for this site." }
            );
        }

        return null;
    }

    private Task<ActionResult?> RequireContractScopeAsync(Contract contract) =>
        RequireContractSiteScopeAsync(contract.site_code);

    private sealed record ContractLocationScope(
        short SiteCode,
        short DepartmentCode,
        byte? ProvinceCode,
        bool HasUnrestrictedLegacyScope,
        bool HasProvinceWideDepartmentScope,
        bool HasAllSitesInDepartmentScope
    );

    private ActionResult? RequireContractAccess()
    {
        return HasContractAccess()
            ? null
            : StatusCode(
                403,
                new { error = "You do not have permission to access vehicle contracts." }
            );
    }

    private ActionResult? RequireContractAction(bool allowed, string error)
    {
        if (RequireContractAccess() is { } accessFailure)
        {
            return accessFailure;
        }

        return allowed ? null : StatusCode(403, new { error });
    }

    private static bool IsContractOwner(Contract contract, int currentUserId)
    {
        if (contract.created_by_user_code.HasValue)
        {
            return contract.created_by_user_code.Value == currentUserId;
        }

        return contract.user_code.HasValue && contract.user_code.Value == currentUserId;
    }

    private ActionResult? RequireActiveContract(Contract contract)
    {
        return
            contract.still_current == "Y"
            && (!contract.contract_status_code.HasValue || contract.contract_status_code == 3)
            ? null
            : BadRequest(
                new { error = "Only an active contract can be changed by this operation." }
            );
    }

    private async Task<string?> GetMissingTariffMessageAsync(
        int vmfCode,
        DateTime? effectiveDate = null
    )
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode);

        if (vehicle == null)
        {
            return "Vehicle not found.";
        }

        var model = await _modelRepository.GetByIdAsync(vehicle.model_code);

        if (model == null)
        {
            return "This vehicle cannot be contracted because its model configuration is missing.";
        }

        var checkDate = (effectiveDate ?? DateTime.Today).Date;
        var usesModernVehicleTariff =
            vehicle.year_manufactured >= 2008
            && checkDate >= new DateTime(2009, 4, 1)
            || vehicle.type_code == 5;

        if (usesModernVehicleTariff)
        {
            var connection = _context.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
                await connection.OpenAsync(HttpContext.RequestAborted);

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    IF OBJECT_ID(N'fin.vehicle_tariff', N'U') IS NULL
                        SELECT CAST(0 AS bit);
                    ELSE
                        SELECT CAST(CASE WHEN EXISTS
                        (
                            SELECT 1
                            FROM [fin].[vehicle_tariff] AS [vt]
                            WHERE [vt].[vmf_code] = @vmfCode
                              AND [vt].[start_date] <= @checkDate
                              AND ([vt].[end_date] IS NULL OR [vt].[end_date] >= @checkDate)
                              AND [vt].[start_date] >= DATEADD(year, -1, @checkDate)
                              AND [vt].[vehicle_fixed_tariff] IS NOT NULL
                              AND [vt].[vehicle_kilometer_tariff] IS NOT NULL
                        ) THEN 1 ELSE 0 END AS bit);
                    """;
                var vmfParameter = command.CreateParameter();
                vmfParameter.ParameterName = "@vmfCode";
                vmfParameter.DbType = DbType.Int32;
                vmfParameter.Value = vmfCode;
                command.Parameters.Add(vmfParameter);
                var dateParameter = command.CreateParameter();
                dateParameter.ParameterName = "@checkDate";
                dateParameter.DbType = DbType.DateTime;
                dateParameter.Value = checkDate;
                command.Parameters.Add(dateParameter);

                var valid = await command.ExecuteScalarAsync(HttpContext.RequestAborted);
                if (valid is bool hasTariff && hasTariff)
                    return null;
            }
            finally
            {
                if (shouldClose)
                    await connection.CloseAsync();
            }

            return $"No current calculated vehicle tariff is captured for VMF {vmfCode}. Capture/release the tariff for the current year before opening or submitting this contract.";
        }

        var hasApprovedTariff =
            await _tariffRepository.GetApprovedTariffForClassAsync(model.class_code, checkDate)
            is not null;

        return hasApprovedTariff
            ? null
            : $"No approved tariff is captured for vehicle class {model.class_code}. Capture the tariff before opening or submitting this contract.";
    }

    private async Task<short?> ResolveGfleetDepartmentCodeAsync()
    {
        var byLegacyCode = await _context
            .Set<Department>()
            .AsNoTracking()
            .FirstOrDefaultAsync(d =>
                d.department_code == LegacyGfleetDepartmentCode && !d.is_deleted
            );

        if (byLegacyCode != null)
        {
            return byLegacyCode.department_code;
        }

        var byName = await _context
            .Set<Department>()
            .AsNoTracking()
            .Where(d => !d.is_deleted && d.description != null)
            .FirstOrDefaultAsync(d =>
                EF.Functions.Like(d.description!, "%GFLEET%")
                || EF.Functions.Like(d.description!, "%G-FLEET%")
                || EF.Functions.Like(d.description!, "%GGMT%")
            );

        return byName?.department_code;
    }

    private async Task<bool> IsGfleetInternalSiteAsync(short siteCode, short? departmentCode = null)
    {
        if (LegacyGfleetSiteCodes.Contains(siteCode))
        {
            return true;
        }

        var gfleetDepartmentCode = await ResolveGfleetDepartmentCodeAsync();
        if (gfleetDepartmentCode is null)
        {
            return false;
        }

        if (departmentCode.HasValue)
        {
            return departmentCode.Value == gfleetDepartmentCode.Value;
        }

        var site = await _siteRepository.GetByIdAsync(siteCode);

        return site?.Depatrment_code == gfleetDepartmentCode.Value;
    }

    private async Task<(Site? Site, string? Error)> ResolveValidatedSiteAsync(
        short? siteCode,
        short? departmentCode = null,
        bool restrictToGfleet = false
    )
    {
        if (siteCode is not > 0)
        {
            return (null, "Select a valid site.");
        }

        var site = await _siteRepository.GetByIdAsync(siteCode.Value);

        if (site == null)
        {
            return (null, "Selected site was not found.");
        }

        if (departmentCode.HasValue && site.Depatrment_code != departmentCode.Value)
        {
            return (null, "Selected site does not belong to the chosen department.");
        }

        if (
            restrictToGfleet
            && !await IsGfleetInternalSiteAsync(site.Site_code, site.Depatrment_code)
        )
        {
            return (null, "Select a GFleet home site before closing the contract.");
        }

        return (site, null);
    }

    private async Task<string?> ValidateDriverSiteAlignmentAsync(
        short targetSiteCode,
        int? siteDriverCode
    )
    {
        if (siteDriverCode is not > 0)
        {
            return null;
        }

        var driver = await ResolveSiteDriverAsync(siteDriverCode);
        if (driver == null)
        {
            return "Selected custodian driver was not found.";
        }

        if (driver.site_code == targetSiteCode)
        {
            return null;
        }

        var targetSite = await _siteRepository.GetByIdAsync(targetSiteCode);
        var driverSite = await _siteRepository.GetByIdAsync(driver.site_code);

        var targetProvince = await BuildProvinceSuffixAsync(targetSite?.province_code);
        var driverProvince = await BuildProvinceSuffixAsync(driverSite?.province_code);
        var driverName = ResolveDriverName(driver) ?? $"Driver {driver.site_driver_code}";
        var driverSiteLabel = driverSite?.description ?? $"site {driver.site_code}";
        var targetSiteLabel = targetSite?.description ?? $"site {targetSiteCode}";

        return $"{driverName} belongs to site {driver.site_code} ({driverSiteLabel}){driverProvince} and cannot be assigned to site {targetSiteCode} ({targetSiteLabel}){targetProvince}. Update the vehicle site first if the vehicle has already moved.";
    }

    private async Task<string> BuildProvinceSuffixAsync(byte? provinceCode)
    {
        if (provinceCode is not > 0)
        {
            return string.Empty;
        }

        var province = await _context
            .Set<Province>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.province_code == provinceCode.Value && !p.is_deleted);

        var provinceLabel = string.IsNullOrWhiteSpace(province?.province_name)
            ? $"Province {provinceCode.Value}"
            : province.province_name;

        return $" ({provinceLabel})";
    }

    private async Task UpdateVehicleSiteAsync(Vehicle vehicle, short siteCode, int currentUserId)
    {
        vehicle.location_code = siteCode;
        vehicle.veh_site_code = siteCode;
        vehicle.date_updated = DateTime.UtcNow;
        vehicle.modified_by_user_code = currentUserId;
        await _vehicleRepository.UpdateAsync(vehicle, currentUserId);
    }

    private async Task<Contract> CreateHomeCustodyContractAsync(
        Vehicle vehicle,
        short siteCode,
        int siteDriverCode,
        DateTime startDate,
        int startOdometer,
        int currentUserId,
        string? notes,
        int sourceContractCode
    )
    {
        var selectedDriver =
            await ResolveSiteDriverAsync(siteDriverCode)
            ?? throw new InvalidOperationException("Selected custodian driver was not found.");

        var contract = new Contract
        {
            vmf_code = vehicle.vmf_code,
            site_code = siteCode,
            start_date = startDate.Date,
            start_time = DateTime.UtcNow,
            start_odometer = startOdometer,
            end_odometer = 0,
            still_current = "Y",
            user_code = currentUserId is > 0 and <= short.MaxValue
                ? (short)currentUserId
                : null,
            contract_type = null,
            Driver_id = ResolveDriverIdentity(selectedDriver, null),
            Driver_name = ResolveDriverName(selectedDriver),
            site_driver_code = selectedDriver.site_driver_code,
            Notes = BuildHomeCustodyNotes(notes, sourceContractCode),
            locked_for_transfer = false,
            contract_status_code = 3,
            contract_status_date = DateTime.UtcNow,
            created_by_user_code = currentUserId,
            modified_by_user_code = currentUserId,
            date_created = DateTime.UtcNow,
            date_updated = DateTime.UtcNow,
        };

        return await _contractRepository.CreateAsync(contract, currentUserId);
    }

    private static string BuildHomeCustodyNotes(string? notes, int sourceContractCode)
    {
        var baseNote =
            sourceContractCode > 0
                ? $"Auto-opened GFleet custody contract after closing contract {sourceContractCode}."
                : "Auto-opened GFleet custody contract after manual vehicle site update.";
        return string.IsNullOrWhiteSpace(notes) ? baseNote : $"{baseNote} {notes.Trim()}";
    }

    /// <summary>
    /// Hire a vehicle (create new contract)
    /// Uses ContractService with full validation and journal integration
    /// </summary>
    [HttpPost("hire")]
    [ProducesResponseType(typeof(Contract), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Hire([FromBody] HireContractDto request)
    {
        if (
            RequireContractAction(
                CanCaptureContract(),
                "You do not have permission to capture vehicle contracts."
            ) is
            { } authorization
        )
            return authorization;

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            if (await RequireContractSiteScopeAsync(request.SiteCode) is { } scopeFailure)
                return scopeFailure;

            int currentUserId = GetCurrentUserId();
            var driverSiteValidation = await ValidateDriverSiteAlignmentAsync(
                request.SiteCode,
                request.SiteDriverCode
            );
            if (driverSiteValidation != null)
            {
                return BadRequest(
                    new
                    {
                        error = driverSiteValidation,
                        siteCode = request.SiteCode,
                        siteDriverCode = request.SiteDriverCode,
                    }
                );
            }

            var missingTariffMessage = await GetMissingTariffMessageAsync(request.VmfCode);
            if (missingTariffMessage != null)
            {
                return BadRequest(new { error = missingTariffMessage, vmfCode = request.VmfCode });
            }

            var selectedDriver = await ResolveSiteDriverAsync(request.SiteDriverCode);

            var hireRequest = new HireContractRequest
            {
                VmfCode = request.VmfCode,
                SiteCode = request.SiteCode,
                StartOdometer = request.StartOdometer,
                DriverId = ResolveDriverIdentity(selectedDriver, request.DriverId),
                DriverName = ResolveDriverName(selectedDriver),
                SiteDriverCode = selectedDriver?.site_driver_code,
                // The authenticated capturer owns the new contract. Ignore a
                // caller-supplied user code; ownership can only change via the
                // explicit manager/admin reassignment workflow.
                UserCode = currentUserId is > 0 and <= short.MaxValue
                    ? (short)currentUserId
                    : null,
                Authorisation = NormalizeOptionalText(request.Authorisation),
                Notes = request.Notes,
                TargetReturnDate = request.TargetReturnDate,
                CreatedByUserId = currentUserId,
            };

            var contract = await _contractService.HireVehicleAsync(hireRequest);
            if (contract == null)
                return BadRequest(new { error = "Failed to create contract" });

            return CreatedAtAction(nameof(GetById), new { id = contract.contract_code }, contract);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating contract for vehicle {VmfCode}", request.VmfCode);
            if (
                ex is NotSupportedException
                || (
                    ex is InvalidOperationException
                    && ex.Message.Contains(
                        "legacy contract approval procedure",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            )
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error = "The legacy contract approval procedure is unavailable or has an incompatible parameter contract. No direct-DML fallback was run.",
                    }
                );
            }
            return StatusCode(
                500,
                new { error = "Failed to create contract", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Return a vehicle (end active contract by VMF code)
    /// Uses ContractService for proper business logic
    /// </summary>
    [HttpPost("{vmfCode}/return")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult ReturnVehicle(int vmfCode, [FromBody] ReturnContractDto request)
    {
        if (
            RequireContractAction(
                CanCloseActiveContract(),
                "You do not have permission to close vehicle contracts."
            ) is
            { } authorization
        )
            return authorization;

        // This legacy-compatible endpoint does not carry the home-site,
        // inspection, or custody inputs required by the real close workflow.
        // Refuse the partial route instead of allowing it to bypass the
        // trigger-owned billing and return steps used by CloseContract.
        return Conflict(
            new
            {
                error = "Vehicle return requires the complete contract close workflow, including home site and custody details.",
                use = $"POST /api/contracts/{{contractCode}}/close",
            }
        );
    }

    /// <summary>
    /// Get a paginated, filterable list of contracts.
    /// Used by the frontend contracts table.
    /// Supports filtering by status, site, date range, and still_current flag.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        [FromQuery] short? status = null,
        [FromQuery] short? siteCode = null,
        [FromQuery] string? stillCurrent = null,
        [FromQuery] DateTime? startDateFrom = null,
        [FromQuery] DateTime? startDateTo = null,
        [FromQuery] int? vmfCode = null
    )
    {
        try
        {
            var allowedSiteCodes = await ResolveAllowedContractSiteCodesAsync();
            if (allowedSiteCodes is not null)
            {
                if (siteCode.HasValue && !allowedSiteCodes.Contains(siteCode.Value))
                    return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have permission to access contracts for this site." });
                siteCode = null;
            }

            var result = await _contractRepository.GetPageAsync(
                new ContractPageQuery(
                    page,
                    pageSize,
                    status,
                    siteCode,
                    stillCurrent,
                    startDateFrom,
                    startDateTo,
                    vmfCode,
                    allowedSiteCodes,
                    HasGlobalContractVisibility() ? null : GetCurrentUserId()
                )
            );

            return Ok(
                new
                {
                    page = result.Page,
                    page_size = result.PageSize,
                    total_records = result.TotalRecords,
                    total_pages = result.TotalPages,
                    data = result.Items.Select(MapToDto),
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving contracts list");
            return StatusCode(
                500,
                new { error = "Failed to retrieve contracts", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Looks up vehicles for the contract workflow without relying on EF's
    /// expanded vehicle projection. This keeps vehicle selection usable against
    /// the client-era vehicle_master table as well as expanded databases.
    /// </summary>
    [HttpGet("vehicle-search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ContractVehicleLookup>>> SearchVehiclesForContracts(
        [FromQuery] string query = ""
    )
    {
        try
        {
            var allowedSiteCodes = await ResolveAllowedContractSiteCodesAsync();
            var vehicles = await _contractRepository.SearchVehiclesForContractsAsync(
                query,
                allowedSiteCodes
            );
            return Ok(vehicles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching vehicles for contract workflow");
            return StatusCode(
                500,
                new { error = "Failed to search vehicles for contracts", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Get all active contracts
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IEnumerable<ContractResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ContractResponseDto>>> GetActive()
    {
        var list = await _contractService.GetActiveContractsAsync();
        var allowedSiteCodes = await ResolveAllowedContractSiteCodesAsync();
        if (allowedSiteCodes is not null)
            list = list.Where(contract => allowedSiteCodes.Contains(contract.site_code));
        if (!HasGlobalContractVisibility())
        {
            var ownerCode = GetCurrentUserId();
            list = list.Where(contract => IsContractOwner(contract, ownerCode));
        }
        var dtos = list.Select(MapToDto).ToList();
        return Ok(dtos);
    }

    /// <summary>
    /// Get contract by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ContractResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContractResponseDto?>> GetById(int id)
    {
        var contract = await _contractRepository.GetByIdAsync(id);
        if (contract == null)
            return NotFound();
        if (await RequireContractScopeAsync(contract) is { } scopeFailure)
            return scopeFailure;
        if (RequireContractRecordVisibility(contract) is { } visibilityFailure)
            return visibilityFailure;
        return Ok(MapToDto(contract));
    }

    /// <summary>
    /// Returns a rich, print-ready JSON payload for a contract.
    /// Frontend renders this as a printable contract document using window.print().
    /// Includes vehicle, site, driver, tariff reference, audit trail, and all contract terms.
    /// </summary>
    [HttpGet("{id}/printout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetPrintout(int id)
    {
        try
        {
            var contract = await _contractRepository.GetByIdAsync(id);

            if (contract == null)
                return NotFound(new { error = "Contract not found" });
            if (await RequireContractScopeAsync(contract) is { } scopeFailure)
                return scopeFailure;
            if (RequireContractRecordVisibility(contract) is { } visibilityFailure)
                return visibilityFailure;

            // Capturer and approver user info (names for print document)
            var capturer = contract.created_by_user_code.HasValue
                ? await _context.Users.FindAsync(contract.created_by_user_code.Value)
                : null;
            var approver = contract.approver_code.HasValue
                ? await _context.Users.FindAsync(contract.approver_code.Value)
                : null;

            // Audit trail
            IEnumerable<ContractAuditLog> auditEntries = [];
            try
            {
                auditEntries = await _auditLog.GetByContractAsync(id);
            }
            catch (Exception auditEx)
            {
                _logger.LogWarning(
                    auditEx,
                    "Contract audit trail is unavailable for printout {ContractId}; continuing with legacy contract data",
                    id
                );
            }

            return Ok(
                new
                {
                    printed_at = DateTime.Now,
                    document_title = $"Contract #{contract.contract_code} — Fleet Management",

                    contract = new
                    {
                        contract_code = contract.contract_code,
                        status_code = contract.contract_status_code,
                        status_text = contract.contract_status_code.HasValue
                            ? GetStatusText(contract.contract_status_code.Value)
                            : "Unknown",
                        still_current = contract.still_current,
                        start_date = contract.start_date.ToString("yyyy-MM-dd"),
                        start_time = contract.start_time.ToString("HH:mm"),
                        end_date = contract.end_date?.ToString("yyyy-MM-dd"),
                        target_return_date = contract.target_return_date?.ToString("yyyy-MM-dd"),
                        start_odometer = contract.start_odometer,
                        end_odometer = contract.end_odometer,
                        contract_type = contract.contract_type,
                        driver_id = contract.Driver_id,
                        notes = contract.Notes,
                        authorisation = contract.Authorisation,
                    },

                    vehicle = contract.Vehicle == null
                        ? null
                        : new
                        {
                            vmf_code = contract.Vehicle.vmf_code,
                            fleet_number = contract.Vehicle.fleet_number,
                            registration_number = contract.Vehicle.registration_number,
                            year_manufactured = contract.Vehicle.year_manufactured,
                            model_code = contract.Vehicle.model_code,
                            current_odo = contract.Vehicle.current_odo,
                        },

                    site = contract.Site == null
                        ? null
                        : new
                        {
                            site_code = contract.Site.Site_code,
                            description = contract.Site.description,
                            res_person = contract.Site.res_person,
                            net_address = contract.Site.net_address,
                            telephone = contract.Site.telephone,
                        },

                    parties = new
                    {
                        capturer = capturer == null
                            ? null
                            : new { user_code = capturer.user_access_code, email = capturer.email },
                        approver = approver == null
                            ? null
                            : new { user_code = approver.user_access_code, email = approver.email },
                    },

                    audit_trail = auditEntries.Select(a => new
                    {
                        a.action,
                        a.performed_by_user_code,
                        performed_at = a.performed_at.ToString("yyyy-MM-dd HH:mm"),
                        old_status = a.old_status_code.HasValue
                            ? GetStatusText(a.old_status_code.Value)
                            : null,
                        new_status = a.new_status_code.HasValue
                            ? GetStatusText(a.new_status_code.Value)
                            : null,
                        a.notes,
                    }),
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating printout for contract {Id}", id);
            return StatusCode(
                500,
                new { error = "Failed to generate printout", message = ex.Message }
            );
        }
    }

    #region Mapping

    private static ContractResponseDto MapToDto(Contract contract)
    {
        return new ContractResponseDto
        {
            ContractCode = contract.contract_code,
            VmfCode = contract.vmf_code,
            SiteCode = contract.site_code,
            ContractTypeCode = contract.contract_type,
            ContractStatusCode = contract.contract_status_code,
            ContractStatusDate = contract.contract_status_date,
            StillCurrent = contract.still_current,
            StartDate = contract.start_date,
            StartTime = contract.start_time,
            StartOdometer = contract.start_odometer,
            EndDate = contract.end_date,
            EndTime = contract.end_time,
            EndOdometer = contract.end_odometer,
            MonthlyKm = contract.monthly_km,
            HoursUsed = contract.hours_used,
            TargetReturnDate = contract.target_return_date,
            DriverId = contract.Driver_id,
            DriverName = contract.Driver_name,
            SiteDriverCode = contract.site_driver_code,
            ApproverCode = contract.approver_code,
            ParentContractCode = contract.parent_contract_code,
            ReliefForContract = contract.relief_for_contract,
            VehicleAssessmentCode = contract.vehicle_assessment_code,
            JournalDetailCode = contract.journal_detail_code,
            LockedForTransfer = contract.locked_for_transfer,
            Notes = contract.Notes,
            Authorisation = contract.Authorisation,
            ChargedUntil = contract.Charged_Until,
            CollectorFirstname = contract.collector_firstname,
            CollectorSurname = contract.collector_surname,
            CollectorSaId = contract.collector_sa_id,
            CollectorPassportNumber = contract.collector_passportnumber,
            CollectorOfficeNumber = contract.collector_office_number,
            CollectorCellphoneNumber = contract.collector_cellphone_number,
            CollectorOffice = contract.collector_office,
            CollectorDesignation = contract.collector_designation,
            ReliefVehicleOption = contract.relief_vehicle_option,
            LeaseContractPeriod = contract.lease_contract_period,
            ContractEstimatedOverallKm = contract.contract_estimated_overall_km,
            IntendedStartDate = contract.intended_start_date,
            IntendedStartTime = contract.intended_start_time,
            CaptureDate = contract.capture_date,
            ModifiedDate = contract.modified_date,
            ReassignedFromContractCode = contract.reassigned_from_contract_code,
            UserCode = contract.user_code,
            ContractGroupCode = contract.contract_group_code,
            BasFundCode = contract.bas_fund_code,
            BasObjectiveCode = contract.bas_objective_code,
            BasProjectNumber = contract.bas_project_number,
            BasResponsibilityCode = contract.bas_responsibility_code,
            DateCreated = contract.date_created,
            DateUpdated = contract.date_updated,
            CreatedByUserCode = contract.created_by_user_code,
            ModifiedByUserCode = contract.modified_by_user_code,
            IsDeleted = contract.is_deleted,

            // Include nested data without circular references
            VehicleFleetNumber = contract.Vehicle?.fleet_number,
            VehicleRegistrationNumber = contract.Vehicle?.registration_number,
            SiteDescription = contract.Site?.description,
        };
    }

    #endregion

    #region Legacy Contract Operations

    /// <summary>
    /// Close/end a contract by contract code
    /// Legacy: CloseContract operation with journal detail reversal
    /// </summary>
    [HttpPost("{contractCode}/close")]
    [ProducesResponseType(typeof(Contract), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CloseContract(
        int contractCode,
        [FromBody] CloseContractRequest request
    )
    {
        if (
            RequireContractAction(
                CanCloseActiveContract(),
                "You do not have permission to close vehicle contracts."
            ) is
            { } authorization
        )
            return authorization;

        try
        {
            int currentUserId = GetCurrentUserId();
            var contract = await _contractRepository.GetByIdAsync(contractCode);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });
            if (await RequireContractScopeAsync(contract) is { } scopeFailure)
                return scopeFailure;
            if (RequireActiveContract(contract) is { } activeContractFailure)
                return activeContractFailure;

            if (request.EndDate.Date < contract.start_date.Date)
            {
                return BadRequest(new { error = "Close date cannot be before the contract start date." });
            }

            if (request.EndOdometer < contract.start_odometer)
            {
                return BadRequest(new { error = "Close odometer cannot be less than the contract start odometer." });
            }

            var homeSiteResolution = await ResolveValidatedSiteAsync(
                request.HomeSiteCode,
                request.HomeDepartmentCode,
                restrictToGfleet: true
            );

            if (homeSiteResolution.Error != null)
            {
                return BadRequest(new { error = homeSiteResolution.Error });
            }

            var homeSite = homeSiteResolution.Site!;
            var driverSiteValidation = await ValidateDriverSiteAlignmentAsync(
                homeSite.Site_code,
                request.HomeSiteDriverCode
            );
            if (driverSiteValidation != null)
            {
                return BadRequest(
                    new
                    {
                        error = driverSiteValidation,
                        siteCode = homeSite.Site_code,
                        siteDriverCode = request.HomeSiteDriverCode,
                    }
                );
            }

            if (request.CreateHomeCustodyContract && request.HomeSiteDriverCode is not > 0)
            {
                return BadRequest(
                    new { error = "Select a GFleet custodian driver before closing the contract." }
                );
            }

            var prevStatus = contract.contract_status_code;
            await using var closeTransaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                HttpContext.RequestAborted
            );

            await _contractRepository.EndContractAsync(
                contractCode,
                request.EndDate,
                currentUserId,
                request.EndOdometer,
                request.Notes
            );

            var vehicle = await _vehicleRepository.GetByIdAsync(contract.vmf_code);

            if (vehicle == null)
            {
                return NotFound(new { error = "Vehicle linked to this contract was not found." });
            }

            await UpdateVehicleSiteAsync(vehicle, homeSite.Site_code, currentUserId);

            Contract? homeCustodyContract = null;
            if (request.CreateHomeCustodyContract)
            {
                var homeDriverCode = request.HomeSiteDriverCode.GetValueOrDefault();
                var activeContract = await _contractRepository.GetActiveContractByVehicleAsync(
                    contract.vmf_code
                );
                if (activeContract != null)
                {
                    return Conflict(
                        new
                        {
                            error = "Vehicle still has an active contract after closure. Home custody contract was not created.",
                            contractCode,
                            activeContractCode = activeContract.contract_code,
                        }
                    );
                }

                homeCustodyContract = await CreateHomeCustodyContractAsync(
                    vehicle,
                    homeSite.Site_code,
                    homeDriverCode,
                    request.EndDate,
                    request.EndOdometer,
                    currentUserId,
                    request.Notes,
                    contractCode
                );
            }

            await _auditLog.LogAsync(
                contractCode,
                "Closed",
                currentUserId,
                oldStatus: prevStatus,
                newStatus: 7,
                notes: request.Notes
            );

            await closeTransaction.CommitAsync(HttpContext.RequestAborted);

            _ = _emailNotification.SendContractClosedNotificationAsync(
                contractCode,
                currentUserId,
                "Closed"
            );

            return Ok(
                new
                {
                    message = request.CreateHomeCustodyContract
                        ? "Contract closed successfully and vehicle returned to GFleet custody."
                        : "Contract closed successfully.",
                    contractCode,
                    vehicleSiteCode = homeSite.Site_code,
                    homeCustodyContractCode = homeCustodyContract?.contract_code,
                }
            );
        }
        catch (LegacyContractClosureBillingInvariantException ex)
        {
            if (_context.Database.CurrentTransaction is not null)
                await _context.Database.CurrentTransaction.RollbackAsync(HttpContext.RequestAborted);
            _logger.LogError(
                ex,
                "Legacy contract close changed billing state unexpectedly for contract {ContractCode}",
                contractCode
            );
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    error = "The legacy contract-close workflow did not produce a consistent billing boundary. The close was not reported as successful.",
                    source = "legacy-procedure-required",
                }
            );
        }
        catch (NotSupportedException ex)
            when (ex.Message.Contains("contract-closure", StringComparison.OrdinalIgnoreCase))
        {
            if (_context.Database.CurrentTransaction is not null)
                await _context.Database.CurrentTransaction.RollbackAsync(HttpContext.RequestAborted);
            _logger.LogError(ex, "Legacy contract closure trigger is unavailable for {ContractCode}", contractCode);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    error = "The legacy contract-closure trigger is unavailable. No direct-DML close fallback was run.",
                    source = "legacy-procedure-required",
                }
            );
        }
        catch (Exception ex)
        {
            if (_context.Database.CurrentTransaction is not null)
                await _context.Database.CurrentTransaction.RollbackAsync(HttpContext.RequestAborted);
            _logger.LogError(ex, "Error closing contract {ContractCode}", contractCode);
            return StatusCode(
                500,
                new { error = "Failed to close contract", message = ex.Message }
            );
        }
    }

    [HttpPost("vehicle/{vmfCode}/site-assignment")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> UpdateVehicleSiteAssignment(
        int vmfCode,
        [FromBody] VehicleSiteAssignmentRequest request
    )
    {
        if (
            RequireContractAction(
                CanManageActiveContract(),
                "You do not have permission to update vehicle site assignments."
            ) is
            { } authorization
        )
            return authorization;

        try
        {
            var currentUserId = GetCurrentUserId();
            var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode);

            if (vehicle == null)
            {
                return NotFound(new { error = "Vehicle not found.", vmfCode });
            }

            var activeContract = await _contractRepository.GetActiveContractByVehicleAsync(vmfCode);
            if (activeContract != null)
            {
                return Conflict(
                    new
                    {
                        error = "Vehicle site can only be updated here when there is no active contract. Use contract reassignment while a contract is active.",
                        vmfCode,
                        activeContractCode = activeContract.contract_code,
                    }
                );
            }

            var siteResolution = await ResolveValidatedSiteAsync(
                request.SiteCode,
                request.DepartmentCode
            );
            if (siteResolution.Error != null)
            {
                return BadRequest(new { error = siteResolution.Error });
            }

            var targetSite = siteResolution.Site!;
            var driverSiteValidation = await ValidateDriverSiteAlignmentAsync(
                targetSite.Site_code,
                request.SiteDriverCode
            );
            if (driverSiteValidation != null)
            {
                return BadRequest(
                    new
                    {
                        error = driverSiteValidation,
                        siteCode = targetSite.Site_code,
                        siteDriverCode = request.SiteDriverCode,
                    }
                );
            }

            if (request.CreateHomeCustodyContract)
            {
                if (
                    !await IsGfleetInternalSiteAsync(
                        targetSite.Site_code,
                        targetSite.Depatrment_code
                    )
                )
                {
                    return BadRequest(
                        new
                        {
                            error = "Home custody contracts can only be opened against GFleet internal sites.",
                        }
                    );
                }

                if (request.SiteDriverCode is not > 0)
                {
                    return BadRequest(
                        new
                        {
                            error = "Select a GFleet custodian driver before opening a home custody contract.",
                        }
                    );
                }
            }

            await UpdateVehicleSiteAsync(vehicle, targetSite.Site_code, currentUserId);

            Contract? homeCustodyContract = null;
            if (request.CreateHomeCustodyContract)
            {
                var siteDriverCode = request.SiteDriverCode.GetValueOrDefault();

                homeCustodyContract = await CreateHomeCustodyContractAsync(
                    vehicle,
                    targetSite.Site_code,
                    siteDriverCode,
                    DateTime.UtcNow.Date,
                    Math.Max(vehicle.current_odo, 0),
                    currentUserId,
                    request.Notes,
                    sourceContractCode: 0
                );
            }

            return Ok(
                new
                {
                    message = request.CreateHomeCustodyContract
                        ? "Vehicle site updated and GFleet custody contract opened."
                        : "Vehicle site updated successfully.",
                    vmfCode,
                    siteCode = targetSite.Site_code,
                    homeCustodyContractCode = homeCustodyContract?.contract_code,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating vehicle site assignment for {VmfCode}", vmfCode);
            return StatusCode(
                500,
                new { error = "Failed to update vehicle site assignment", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Extend contract target return date
    /// Legacy: ExtendContractTargetReturnDate operation
    /// </summary>
    [HttpPut("{contractCode}/extend")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ExtendContract(
        int contractCode,
        [FromBody] ExtendContractRequest request
    )
    {
        if (
            RequireContractAction(
                CanManageActiveContract(),
                "You do not have permission to extend vehicle contracts."
            ) is
            { } authorization
        )
            return authorization;

        try
        {
            int currentUserId = GetCurrentUserId();

            var contract = await _contractRepository.GetByIdAsync(contractCode);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });
            if (await RequireContractScopeAsync(contract) is { } scopeFailure)
                return scopeFailure;
            if (RequireActiveContract(contract) is { } activeContractFailure)
                return activeContractFailure;

            contract.target_return_date = request.NewTargetReturnDate;
            contract.Notes = string.IsNullOrWhiteSpace(request.Notes) ? contract.Notes : request.Notes;
            contract.contract_estimated_overall_km = request.EstimatedOverallKilometres
                ?? contract.contract_estimated_overall_km;
            await _contractRepository.ExtendExistingAsync(contract, currentUserId);

            return Ok(
                new
                {
                    message = "Contract extended successfully",
                    newTargetReturnDate = request.NewTargetReturnDate,
                }
            );
        }
        catch (LegacyContractExtensionBillingInvariantException ex)
        {
            _logger.LogError(
                ex,
                "Legacy contract extension changed billing state unexpectedly for contract {ContractCode}",
                contractCode
            );
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    error = "The legacy contract-extension workflow changed billing state unexpectedly. The extension was not reported as successful.",
                    source = "legacy-procedure-required",
                }
            );
        }
        catch (NotSupportedException ex)
            when (ex.Message.Contains("contract-extension", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(ex, "Legacy contract extension procedure is unavailable or incompatible for {ContractCode}", contractCode);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    error = "The legacy contract-extension procedure is unavailable or incompatible. No direct-DML fallback was run.",
                    source = "legacy-procedure-required",
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extending contract {ContractCode}", contractCode);
            return StatusCode(
                500,
                new { error = "Failed to extend contract", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Cancel a contract
    /// Legacy: CancelContract operation
    /// </summary>
    [HttpPost("{contractCode}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult CancelContract(
        int contractCode,
        [FromBody] CancelContractRequest? request = null
    )
    {
        // MNT_Vehicle_Contract_DetailManagement only cancels a pending
        // contract through DEV_UPD_Contract_New_ApproveDeclineOrCancel. It
        // does not expose a direct cancellation of an active contract. Do not
        // substitute modern direct DML for the distinct legacy close flow.
        return Conflict(
            new
            {
                error = "Active-contract cancellation is not a legacy workflow. Use the legacy close process.",
                contractCode,
            }
        );
    }

    /// <summary>
    /// Reassign contract to a different vehicle or department
    /// </summary>
    [HttpPost("{contractId}/reassign")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ReassignContract(
        int contractId,
        [FromBody] ContractReassignDto request
    )
    {
        if (
            RequireContractAction(
                CanManageActiveContract(),
                "You do not have permission to reassign vehicle contracts."
            ) is
            { } authorization
        )
            return authorization;

        try
        {
            int currentUserId = GetCurrentUserId();

            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });
            if (await RequireContractScopeAsync(contract) is { } scopeFailure)
                return scopeFailure;

            if (
                contract.still_current != "Y"
                || (contract.contract_status_code.HasValue && contract.contract_status_code != 3)
            )
                return BadRequest(new { error = "Only an active contract can be reassigned." });

            var destinationSiteCode = request.NewSiteCode ?? contract.site_code;
            if (destinationSiteCode is not > 0)
                return BadRequest(new { error = "Select a destination site." });

            if (await RequireContractSiteScopeAsync(destinationSiteCode) is { } destinationScopeFailure)
                return destinationScopeFailure;

            if (string.IsNullOrWhiteSpace(request.Reason))
                return BadRequest(
                    new { error = "A reason is required when reassigning a contract." }
                );

            var effectiveDate = request.StartDate?.Date ?? DateTime.Now.Date;
            if (effectiveDate < contract.start_date.Date)
                return BadRequest(
                    new
                    {
                        error = "The effective reassignment date cannot precede the current contract start date.",
                    }
                );

            var startOdometer = request.StartOdometer ?? contract.start_odometer;
            if (startOdometer < 0)
                return BadRequest(new { error = "Start odometer cannot be negative." });

            var newCapturerCode = request.NewCapturerUserCode ?? contract.user_code;
            if (newCapturerCode is not > 0)
                return BadRequest(new { error = "Select the user who will own the reassigned contract." });
            if (newCapturerCode.Value > short.MaxValue)
                return BadRequest(new { error = "The selected contract capturer code is outside the legacy range." });
            var newCapturer = await _context.UserAccessOlds.AsNoTracking().SingleOrDefaultAsync(
                user => user.user_access_code == (short)newCapturerCode.Value && user.user_active,
                HttpContext.RequestAborted
            );
            if (newCapturer is null)
                return BadRequest(new { error = "The selected contract capturer is not an active FIS user." });

            Driver? destinationDriver = null;
            if (request.NewSiteDriverCode.HasValue)
            {
                if (request.NewSiteDriverCode is not > 0)
                    return BadRequest(new { error = "Select a valid destination custodian driver." });

                destinationDriver = await ResolveSiteDriverAsync(request.NewSiteDriverCode);
                if (destinationDriver is null)
                    return BadRequest(new { error = "The selected destination custodian driver was not found." });
            }

            var destinationDriverCode = request.NewSiteDriverCode ?? contract.site_driver_code;
            var driverSiteValidation = await ValidateDriverSiteAlignmentAsync(
                destinationSiteCode,
                destinationDriverCode
            );
            if (driverSiteValidation != null)
            {
                return BadRequest(
                    new
                    {
                        error = driverSiteValidation,
                        siteCode = destinationSiteCode,
                        siteDriverCode = destinationDriverCode,
                    }
                );
            }

            var capturerChanged = newCapturerCode != contract.user_code;
            var custodianChanged = destinationDriverCode != contract.site_driver_code;
            var siteChanged = destinationSiteCode != contract.site_code;
            if (!capturerChanged && !custodianChanged && !siteChanged)
            {
                return BadRequest(
                    new
                    {
                        error = "Select a different destination site, custodian driver, or contract capturer.",
                    }
                );
            }

            // DEV_UPD_Contract_ReassignExisting owns the legacy close, insert,
            // grouping, status-history, and transaction behavior.
            var destinationDriverId = destinationDriver is null
                ? contract.Driver_id
                : ResolveDriverIdentity(destinationDriver, null);
            var destinationDriverName = destinationDriver is null
                ? contract.Driver_name
                : ResolveDriverName(destinationDriver);
            var reassignedContract = new Contract
            {
                vmf_code = contract.vmf_code,
                site_code = destinationSiteCode,
                start_date = effectiveDate,
                start_time = effectiveDate,
                end_date = null,
                end_time = null,
                start_odometer = startOdometer,
                end_odometer = 0,
                still_current = "Y",
                contract_type = contract.contract_type,
                Driver_id = destinationDriverId,
                Authorisation = contract.Authorisation,
                Driver_name = destinationDriverName,
                Notes = $"{contract.Notes}\nReassigned: {request.Reason.Trim()}",
                target_return_date = contract.target_return_date,
                user_code = newCapturerCode.Value <= short.MaxValue
                    ? (short)newCapturerCode.Value
                    : contract.user_code,
                Charged_Until = contract.Charged_Until,
                bas_objective_code = contract.bas_objective_code,
                bas_responsibility_code = contract.bas_responsibility_code,
                relief_for_contract = contract.relief_for_contract,
                locked_for_transfer = contract.locked_for_transfer,
                hours_used = contract.hours_used,
                bas_project_number = contract.bas_project_number,
                journal_detail_code = contract.journal_detail_code,
                parent_contract_code = contract.parent_contract_code,
                contract_group_code = contract.contract_group_code,
                bas_fund_code = contract.bas_fund_code,
                monthly_km = contract.monthly_km,
                contract_status_code = 3,
                contract_status_date = effectiveDate,
                vehicle_assessment_code = contract.vehicle_assessment_code,
                approver_code = contract.approver_code,
                site_driver_code = destinationDriverCode,
                collector_firstname = contract.collector_firstname,
                collector_surname = contract.collector_surname,
                collector_sa_id = contract.collector_sa_id,
                collector_passportnumber = contract.collector_passportnumber,
                collector_office_number = contract.collector_office_number,
                collector_cellphone_number = contract.collector_cellphone_number,
                collector_office = contract.collector_office,
                collector_designation = contract.collector_designation,
                relief_vehicle_option = contract.relief_vehicle_option,
                lease_contract_period = contract.lease_contract_period,
                contract_estimated_overall_km = contract.contract_estimated_overall_km,
                intended_start_date = contract.intended_start_date,
                intended_start_time = contract.intended_start_time,
                reassigned_from_contract_code = contractId,
                date_created = DateTime.UtcNow,
                date_updated = DateTime.UtcNow,
                created_by_user_code = newCapturerCode.Value,
                modified_by_user_code = currentUserId,
            };

            var created = await _contractRepository.ReassignExistingAsync(
                contract,
                reassignedContract,
                currentUserId
            );

            await _auditLog.LogAsync(
                contractId,
                "Reassigned",
                currentUserId,
                oldStatus: contract.contract_status_code,
                newStatus: 7,
                notes: request.Reason
            );
            await _auditLog.LogAsync(
                created.contract_code,
                "Reassigned",
                currentUserId,
                oldStatus: null,
                newStatus: 3,
                notes: request.Reason
            );

            return Ok(
                new
                {
                    message = "Contract reassigned successfully",
                    contractId,
                    newContractCode = created.contract_code,
                }
            );
        }
        catch (Exception ex)
            when (ex is NotSupportedException || ex is InvalidOperationException && ex.Message.Contains("legacy contract-reassignment", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(ex, "Legacy contract reassignment workflow unavailable for {ContractId}", contractId);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    error = "The legacy contract-reassignment procedure is unavailable or incompatible. No direct-DML fallback was run.",
                    source = "legacy-procedure-required",
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reassigning contract {ContractId}", contractId);
            return StatusCode(
                500,
                new { error = "Failed to reassign contract", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Create a relief vehicle assignment for a contract
    /// </summary>
    [HttpPost("{contractId}/relief")]
    [ProducesResponseType(typeof(Contract), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CreateReliefContract(
        int contractId,
        [FromBody] ReliefVehicleDto request
    )
    {
        if (
            RequireContractAction(
                CanManageActiveContract(),
                "You do not have permission to create relief contracts."
            ) is
            { } authorization
        )
            return authorization;

        try
        {
            int currentUserId = GetCurrentUserId();

            var parentContract = await _contractRepository.GetByIdAsync(contractId);
            if (parentContract == null)
                return NotFound(new { error = "Parent contract not found" });
            if (await RequireContractScopeAsync(parentContract) is { } scopeFailure)
                return scopeFailure;

            if (
                parentContract.still_current != "Y"
                || (
                    parentContract.contract_status_code.HasValue
                    && parentContract.contract_status_code != 3
                )
            )
                return BadRequest(
                    new { error = "Relief can only be created for an active contract." }
                );

            if (parentContract.relief_vehicle_option != true)
                return BadRequest(
                    new { error = "This contract is not opted in for a relief vehicle." }
                );

            if (request.ReliefVmfCode <= 0)
                return BadRequest(new { error = "Select a valid relief vehicle." });

            if (string.IsNullOrWhiteSpace(request.Reason))
                return BadRequest(
                    new { error = "A reason is required when creating a relief contract." }
                );

            var allowedSiteCodes = await ResolveAllowedContractSiteCodesAsync();
            var reliefVehicle = await _contractRepository.GetVehicleForContractAsync(
                request.ReliefVmfCode,
                allowedSiteCodes
            );
            if (reliefVehicle == null)
                return NotFound(new { error = "Relief vehicle not found" });

            if (reliefVehicle.VehicleStatusCode.HasValue && reliefVehicle.VehicleStatusCode != 1)
                return BadRequest(new { error = "The selected relief vehicle is not in service." });

            if (
                await _contractRepository.GetActiveContractByVehicleAsync(request.ReliefVmfCode)
                != null
            )
                return Conflict(
                    new { error = "The selected vehicle already has an active contract." }
                );

            // Create relief contract linked to parent
            var reliefContract = new Contract
            {
                vmf_code = request.ReliefVmfCode,
                site_code = parentContract.site_code,
                start_date = DateTime.Now,
                start_time = DateTime.Now,
                start_odometer = request.StartOdometer ?? 0,
                end_odometer = 0,
                // The legacy relief flow creates status 9 through
                // DEV_INS_Contract_New_ForApproval, then activates it through
                // DEV_UPD_Contract_NewActivate. Do not direct-insert an
                // already-active replacement row.
                still_current = "N",
                contract_type = "F", // Legacy relief contract type
                relief_for_contract = contractId,
                parent_contract_code = contractId,
                relief_vehicle_option = false,
                Notes = $"Relief for contract {contractId}: {request.Reason}",
                target_return_date = request.TargetReturnDate,
                contract_status_code = 9,
                contract_status_date = DateTime.Now,
                date_created = DateTime.Now,
                created_by_user_code = currentUserId,
                modified_by_user_code = currentUserId,
                date_updated = DateTime.Now,
            };

            var pending = await _contractRepository.CreateForApprovalAsync(
                reliefContract,
                currentUserId
            );
            var created = await _contractRepository.ActivatePendingAsync(
                pending,
                parentContract.contract_code,
                currentUserId
            );

            return CreatedAtAction(nameof(GetById), new { id = created.contract_code }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating relief contract for {ContractId}", contractId);
            if (
                ex is NotSupportedException
                || (
                    ex is InvalidOperationException
                    && ex.Message.Contains(
                        "legacy",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            )
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error = "The legacy relief-contract procedures are unavailable or incompatible. No direct-DML fallback was run.",
                    }
                );
            }
            return StatusCode(
                500,
                new { error = "Failed to create relief contract", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Validate contract before submission
    /// </summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(ContractValidationResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContractValidationResultDto>> ValidateContract(
        [FromBody] ContractValidationRequestDto request
    )
    {
        try
        {
            if (await RequireContractSiteScopeAsync(request.SiteCode) is { } scopeFailure)
                return scopeFailure;

            var result = new ContractValidationResultDto
            {
                IsValid = true,
                Errors = new List<string>(),
                Warnings = new List<string>(),
            };

            // Check if vehicle exists
            var vehicle = await _vehicleRepository.GetByIdAsync(request.VmfCode);
            if (vehicle == null)
            {
                result.IsValid = false;
                result.Errors.Add("Vehicle not found");
            }

            // Check for active contracts
            if (await _contractRepository.HasActiveContractAsync(request.VmfCode))
            {
                result.IsValid = false;
                result.Errors.Add("Vehicle already has an active contract");
            }

            // Add warnings if needed
            if (
                request.StartOdometer.HasValue
                && vehicle != null
                && request.StartOdometer < vehicle.current_odo
            )
            {
                result.Warnings.Add("Start odometer is less than current vehicle odometer");
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating contract");
            return StatusCode(
                500,
                new { error = "Failed to validate contract", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Submit contract for approval (workflow)
    /// </summary>
    [HttpPost("{contractId}/submit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SubmitContractForApproval(int contractId)
    {
        if (
            RequireContractAction(
                CanCaptureContract(),
                "You do not have permission to submit vehicle contracts."
            ) is
            { } authorization
        )
            return authorization;

        try
        {
            int currentUserId = GetCurrentUserId();

            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });
            if (await RequireContractScopeAsync(contract) is { } scopeFailure)
                return scopeFailure;

            // Only allow submit/resubmit from Draft (0/null) or Declined for Correction (4)
            var status = contract.contract_status_code;
            if (status != null && status != 0 && status != 4)
                return BadRequest(
                    new
                    {
                        error = "Contract cannot be submitted in its current state.",
                        current_status = status,
                        hint = "Only Draft (0) or Declined-for-Correction (4) contracts can be submitted.",
                    }
                );

            // Verify the submitter is the original capturer
            if (!HasContractAdminRole() && !IsContractOwner(contract, currentUserId))
                return StatusCode(
                    403,
                    new
                    {
                        error = "Only the original capturer can submit this contract.",
                        contractId,
                    }
                );

            var missingTariffMessage = await GetMissingTariffMessageAsync(contract.vmf_code);
            if (missingTariffMessage != null)
            {
                return BadRequest(
                    new
                    {
                        error = missingTariffMessage,
                        contractId,
                        vmfCode = contract.vmf_code,
                    }
                );
            }

            var prevStatus = contract.contract_status_code;
            contract.contract_status_code = 1; // Pending Review
            contract.contract_status_date = DateTime.Now;

            // Submission is the legacy approval-phase mutation. The archived
            // procedure writes status history and preserves the pending-row
            // ownership/trigger transaction; a generic UPDATE would bypass
            // that workflow.
            await _contractRepository.UpdatePendingForApprovalAsync(contract, currentUserId);
            await _auditLog.LogAsync(
                contractId,
                "Submitted",
                currentUserId,
                oldStatus: prevStatus,
                newStatus: 1
            );

            return Ok(new { message = "Contract submitted for approval", contractId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting contract {ContractId} for approval", contractId);
            if (
                ex is NotSupportedException
                || (
                    ex is InvalidOperationException
                    && ex.Message.Contains(
                        "legacy procedure",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            )
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error = "The legacy pending-contract procedure is unavailable or incompatible. No direct-DML fallback was run.",
                    }
                );
            }
            return StatusCode(
                500,
                new { error = "Failed to submit contract", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Approve contract (without activation)
    /// </summary>
    [HttpPost("{contractId}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> ApproveContract(
        int contractId,
        [FromBody] ContractApprovalDto? request = null
    )
    {
        try
        {
            if (
                RequireContractAction(
                    HasContractApproverRole(),
                    "You do not have permission to review vehicle contracts."
                ) is
                { } authorization
            )
                return authorization;

            int currentUserId = GetCurrentUserId();

            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });
            if (await RequireContractScopeAsync(contract) is { } scopeFailure)
                return scopeFailure;
            if (contract.contract_status_code != 1)
                return BadRequest(new { error = "Only pending-review contracts can be approved." });

            // Prevent self-approval
            var selfApprovalCheck = ValidateSelfApprovalPrevention(contract, currentUserId);
            if (selfApprovalCheck != null)
                return selfApprovalCheck;

            // Update contract status to approved (status code 2 = Approved)
            contract.contract_status_code = 2; // Approved
            contract.contract_status_date = DateTime.Now;

            if (!string.IsNullOrEmpty(request?.ApprovalNotes))
                contract.Notes = $"{contract.Notes}\nApproval: {request.ApprovalNotes}";

            await _contractRepository.UpdatePendingDecisionAsync(contract, currentUserId);
            await _auditLog.LogAsync(
                contractId,
                "Approved",
                currentUserId,
                oldStatus: 1,
                newStatus: 2,
                notes: request?.ApprovalNotes
            );

            return Ok(new { message = "Contract approved", contractId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving contract {ContractId}", contractId);
            return StatusCode(
                500,
                new { error = "Failed to approve contract", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Approve and activate contract in one step
    /// </summary>
    [HttpPost("{contractId}/approve-activate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> ApproveAndActivateContract(
        int contractId,
        [FromBody] ContractApprovalDto? request = null
    )
    {
        try
        {
            if (
                RequireContractAction(
                    HasContractApproverRole(),
                    "You do not have permission to review vehicle contracts."
                ) is
                { } authorization
            )
                return authorization;

            int currentUserId = GetCurrentUserId();

            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });
            if (await RequireContractScopeAsync(contract) is { } scopeFailure)
                return scopeFailure;
            if (contract.contract_status_code is not (1 or 2))
                return BadRequest(
                    new { error = "Only pending-review or approved contracts can be activated." }
                );

            // Prevent self-approval
            var selfApprovalCheck = ValidateSelfApprovalPrevention(contract, currentUserId);
            if (selfApprovalCheck != null)
                return selfApprovalCheck;

            // The legacy approval procedure first records status 2, then its
            // activation procedure closes/replaces the prior contract and
            // makes the pending record active in one database transaction.
            var existingContractCode =
                (await _contractRepository.GetActiveContractByVehicleAsync(contract.vmf_code))
                    ?.contract_code ?? 0;
            contract.contract_status_code = 2; // Approved
            contract.contract_status_date = DateTime.Now;

            if (!string.IsNullOrEmpty(request?.ApprovalNotes))
                contract.Notes = $"{contract.Notes}\nApproved & Activated: {request.ApprovalNotes}";

            var approvedContract = await _contractRepository.UpdatePendingDecisionAsync(
                contract,
                currentUserId
            );
            await _contractRepository.ActivatePendingAsync(
                approvedContract,
                existingContractCode,
                currentUserId
            );
            await _auditLog.LogAsync(
                contractId,
                "ApprovedAndActivated",
                currentUserId,
                oldStatus: 1,
                newStatus: 3,
                notes: request?.ApprovalNotes
            );

            _ = _emailNotification.SendContractOpenedNotificationAsync(
                contractId,
                contract.created_by_user_code ?? currentUserId,
                currentUserId
            );

            return Ok(new { message = "Contract approved and activated", contractId });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error approving and activating contract {ContractId}",
                contractId
            );
            return StatusCode(
                500,
                new { error = "Failed to approve and activate contract", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Decline contract with request for correction
    /// </summary>
    [HttpPost("{contractId}/decline-correction")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> DeclineContractWithCorrection(
        int contractId,
        [FromBody] ContractDeclineDto request
    )
    {
        try
        {
            if (
                RequireContractAction(
                    HasContractApproverRole(),
                    "You do not have permission to review vehicle contracts."
                ) is
                { } authorization
            )
                return authorization;

            int currentUserId = GetCurrentUserId();

            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });
            if (await RequireContractScopeAsync(contract) is { } scopeFailure)
                return scopeFailure;
            if (contract.contract_status_code != 1)
                return BadRequest(
                    new { error = "Only pending-review contracts can be returned for correction." }
                );

            // Prevent self-review/decline
            var selfApprovalCheck = ValidateSelfApprovalPrevention(contract, currentUserId);
            if (selfApprovalCheck != null)
                return selfApprovalCheck;

            // Update contract status to correction required (status code 4 = Needs Correction)
            contract.contract_status_code = 4; // Needs correction
            contract.contract_status_date = DateTime.Now;
            contract.Notes = $"{contract.Notes}\nCorrection Required: {request.DeclineReason}";

            await _contractRepository.UpdatePendingDecisionAsync(contract, currentUserId);
            await _auditLog.LogAsync(
                contractId,
                "DeclinedForCorrection",
                currentUserId,
                oldStatus: 1,
                newStatus: 4,
                notes: request.DeclineReason
            );

            return Ok(new { message = "Contract declined with correction request", contractId });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error declining contract {ContractId} for correction",
                contractId
            );
            return StatusCode(
                500,
                new { error = "Failed to decline contract", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Decline contract (reject)
    /// </summary>
    [HttpPost("{contractId}/decline")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> DeclineContract(
        int contractId,
        [FromBody] ContractDeclineDto request
    )
    {
        try
        {
            if (
                RequireContractAction(
                    HasContractApproverRole(),
                    "You do not have permission to review vehicle contracts."
                ) is
                { } authorization
            )
                return authorization;

            int currentUserId = GetCurrentUserId();

            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });
            if (await RequireContractScopeAsync(contract) is { } scopeFailure)
                return scopeFailure;
            if (contract.contract_status_code != 1)
                return BadRequest(new { error = "Only pending-review contracts can be declined." });

            // Prevent self-review/decline
            var selfApprovalCheck = ValidateSelfApprovalPrevention(contract, currentUserId);
            if (selfApprovalCheck != null)
                return selfApprovalCheck;

            // Update contract status to declined (status code 5 = Declined)
            contract.contract_status_code = 5; // Declined
            contract.contract_status_date = DateTime.Now;
            contract.still_current = "N";
            contract.Notes = $"{contract.Notes}\nDeclined: {request.DeclineReason}";

            await _contractRepository.UpdatePendingDecisionAsync(contract, currentUserId);
            await _auditLog.LogAsync(
                contractId,
                "Declined",
                currentUserId,
                oldStatus: 1,
                newStatus: 5,
                notes: request.DeclineReason
            );

            return Ok(new { message = "Contract declined", contractId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error declining contract {ContractId}", contractId);
            return StatusCode(
                500,
                new { error = "Failed to decline contract", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Recall a contract from Pending Review back to Draft.
    /// Only the original capturer can recall, and only when status is Pending Review (1).
    /// Use this to fix mistakes before the approver sees the contract.
    /// </summary>
    [HttpPost("{contractId}/recall")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> RecallContract(int contractId)
    {
        if (
            RequireContractAction(
                CanCaptureContract(),
                "You do not have permission to recall vehicle contracts."
            ) is
            { } authorization
        )
            return authorization;

        try
        {
            int currentUserId = GetCurrentUserId();

            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });
            if (await RequireContractScopeAsync(contract) is { } scopeFailure)
                return scopeFailure;

            // Only the original capturer can recall
            if (!HasContractAdminRole() && !IsContractOwner(contract, currentUserId))
                return StatusCode(
                    403,
                    new
                    {
                        error = "Only the original capturer can recall this contract.",
                        contractId,
                    }
                );

            // Can only recall from Pending Review (1)
            if (contract.contract_status_code != 1)
                return BadRequest(
                    new
                    {
                        error = "Contract can only be recalled when it is in Pending Review status.",
                        current_status = contract.contract_status_code,
                    }
                );

            contract.contract_status_code = 0; // Back to Draft
            contract.contract_status_date = DateTime.Now;

            // Recall is still a pending-contract edit. Keep it on the legacy
            // approval-phase procedure so status/history and capturer
            // ownership are not changed by generic direct DML.
            await _contractRepository.UpdatePendingForApprovalAsync(contract, currentUserId);
            await _auditLog.LogAsync(
                contractId,
                "Recalled",
                currentUserId,
                oldStatus: 1,
                newStatus: 0
            );

            _logger.LogInformation(
                "Contract {ContractId} recalled to Draft by user {UserId}",
                contractId,
                currentUserId
            );

            return Ok(
                new
                {
                    message = "Contract recalled to draft. You may now edit and resubmit.",
                    contractId,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recalling contract {ContractId}", contractId);
            return StatusCode(
                500,
                new { error = "Failed to recall contract", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Edit a contract's details.
    /// Only allowed when the contract is in Draft (0) or Declined for Correction (4) status.
    /// After editing a Declined-for-Correction contract, call /submit to resubmit for approval.
    /// </summary>
    [HttpPut("{contractId}/edit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> EditContract(int contractId, [FromBody] EditContractDto request)
    {
        if (
            RequireContractAction(
                CanCaptureContract(),
                "You do not have permission to edit vehicle contracts."
            ) is
            { } authorization
        )
            return authorization;

        try
        {
            int currentUserId = GetCurrentUserId();
            var selectedDriver = await ResolveSiteDriverAsync(request.SiteDriverCode);

            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });
            if (await RequireContractScopeAsync(contract) is { } scopeFailure)
                return scopeFailure;

            // Capturers can only edit their own records; administrators may correct a record
            // without impersonating its original owner.
            if (
                !HasContractAdminRole()
                && (!HasContractLoadAndManageRole() || !IsContractOwner(contract, currentUserId))
            )
                return StatusCode(
                    403,
                    new
                    {
                        error = "Only the original capturer or an administrator can edit this contract.",
                        contractId,
                    }
                );

            // The legacy detail page lets the original capturer edit a
            // pending-review row (status 1) as well as a draft (0) or a
            // declined-for-correction row (4). Keep the owner check above so
            // approvers cannot modify the pending record.
            var status = contract.contract_status_code;
            if (status != null && status is not (0 or 1 or 4))
                return BadRequest(
                    new
                    {
                        error = "Contract cannot be edited in its current state.",
                        current_status = status,
                        hint = "Only Draft, Pending Review, and Declined-for-Correction contracts can be edited.",
                    }
                );

            var targetSiteCode = request.SiteCode ?? contract.site_code;
            if (await RequireContractSiteScopeAsync(targetSiteCode) is { } targetScopeFailure)
                return targetScopeFailure;
            var driverSiteValidation = await ValidateDriverSiteAlignmentAsync(
                targetSiteCode,
                request.SiteDriverCode
            );
            if (driverSiteValidation != null)
            {
                return BadRequest(
                    new
                    {
                        error = driverSiteValidation,
                        siteCode = targetSiteCode,
                        siteDriverCode = request.SiteDriverCode,
                    }
                );
            }

            // Apply updates — only overwrite fields that were provided
            if (request.SiteCode.HasValue)
                contract.site_code = request.SiteCode.Value;
            if (request.SiteDriverCode.HasValue)
            {
                if (selectedDriver != null)
                {
                    contract.site_driver_code = selectedDriver.site_driver_code;
                    contract.Driver_id = ResolveDriverIdentity(selectedDriver, request.DriverId);
                    contract.Driver_name = ResolveDriverName(selectedDriver);
                }
                else if (request.SiteDriverCode.Value <= 0)
                {
                    contract.site_driver_code = null;
                    contract.Driver_id = null;
                    contract.Driver_name = null;
                }
            }
            else if (request.DriverId != null)
            {
                contract.Driver_id = NormalizeOptionalText(request.DriverId);
            }

            // Contract ownership is immutable during ordinary edits. The
            // original capturer remains user_code; ownership transfer is only
            // available through the explicit manager/admin reassignment flow.

            if (request.Authorisation != null)
            {
                contract.Authorisation = NormalizeOptionalText(request.Authorisation);
            }

            if (request.Notes != null)
                contract.Notes = request.Notes;
            if (request.TargetReturnDate.HasValue)
                contract.target_return_date = request.TargetReturnDate;
            if (request.StartOdometer.HasValue)
                contract.start_odometer = request.StartOdometer.Value;

            contract.date_updated = DateTime.Now;
            contract.modified_by_user_code = currentUserId;

            await _contractRepository.UpdatePendingForApprovalAsync(contract, currentUserId);
            await _auditLog.LogAsync(
                contractId,
                "Edited",
                currentUserId,
                oldStatus: status,
                newStatus: status,
                notes: "Contract fields updated"
            );

            _logger.LogInformation(
                "Contract {ContractId} edited by user {UserId} (status={Status})",
                contractId,
                currentUserId,
                status
            );

            return Ok(
                new
                {
                    message = status == 4
                        ? "Contract updated. Call /submit to resubmit for approval."
                        : "Contract updated.",
                    contractId,
                    contract_status_code = contract.contract_status_code,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing contract {ContractId}", contractId);
            if (
                ex is NotSupportedException
                || (
                    ex is InvalidOperationException
                    && ex.Message.Contains(
                        "legacy procedure",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            )
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error = "The legacy pending-contract procedure is unavailable or incompatible. No direct-DML fallback was run.",
                    }
                );
            }
            return StatusCode(500, new { error = "Failed to edit contract", message = ex.Message });
        }
    }

    /// <summary>
    /// Get pending approval details for a contract
    /// </summary>
    [HttpGet("{contractId}/pending")]
    [ProducesResponseType(typeof(ContractPendingDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContractPendingDetailsDto>> GetPendingApprovalDetails(
        int contractId
    )
    {
        try
        {
            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });
            if (await RequireContractScopeAsync(contract) is { } scopeFailure)
                return scopeFailure;

            var result = new ContractPendingDetailsDto
            {
                ContractCode = contract.contract_code,
                VmfCode = contract.vmf_code,
                SiteCode = contract.site_code,
                StatusCode = contract.contract_status_code,
                StatusDate = contract.contract_status_date,
                IsPending = contract.contract_status_code == 1,
                SubmittedDate = contract.date_created,
                SubmittedByUserCode = contract.created_by_user_code,
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error getting pending details for contract {ContractId}",
                contractId
            );
            return StatusCode(
                500,
                new { error = "Failed to get pending details", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Updates the legacy contract history fields used by the history backdating workflow.
    /// No new columns are introduced; the existing contract dates and odometers are updated
    /// through the compatibility repository so older client databases remain supported.
    /// </summary>
    [HttpPut("{contractId}/history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateContractHistory(
        int contractId,
        [FromBody] ContractHistoryBackdatingRequest request
    )
    {
        try
        {
            if (
                RequireContractAction(
                    HasContractHistoryBackdatingRole(),
                    "You do not have permission to backdate contract history."
                ) is
                { } authorization
            )
                return authorization;

            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });
            if (await RequireContractScopeAsync(contract) is { } scopeFailure)
                return scopeFailure;

            if (request.StartDate == default)
                return BadRequest(new { error = "Start date is required." });

            if (request.EndDate.HasValue && request.EndDate.Value.Date < request.StartDate.Date)
                return BadRequest(new { error = "End date cannot be before the start date." });

            if (request.StartOdometer.HasValue && request.StartOdometer.Value < 0)
                return BadRequest(new { error = "Start odometer cannot be negative." });

            if (request.EndOdometer.HasValue && request.EndOdometer.Value < 0)
                return BadRequest(new { error = "End odometer cannot be negative." });

            if (
                request.StartOdometer.HasValue
                && request.EndOdometer.HasValue
                && request.EndOdometer.Value < request.StartOdometer.Value
            )
                return BadRequest(
                    new { error = "End odometer cannot be less than the start odometer." }
                );

            var currentUserId = GetCurrentUserId();
            var previousStartDate = contract.start_date;
            var previousEndDate = contract.end_date;
            var previousStartOdometer = contract.start_odometer;
            var previousEndOdometer = contract.end_odometer;

            contract.start_date = request.StartDate.Date;
            if (request.EndDate.HasValue)
            {
                contract.end_date = request.EndDate.Value.Date;
            }
            if (request.StartOdometer.HasValue)
            {
                contract.start_odometer = request.StartOdometer.Value;
            }
            if (request.EndOdometer.HasValue)
            {
                contract.end_odometer = request.EndOdometer.Value;
            }

            await _contractRepository.UpdateAsync(contract, currentUserId);
            await _auditLog.LogAsync(
                contractId,
                "HistoryBackdated",
                currentUserId,
                notes: $"Start date {previousStartDate:yyyy-MM-dd} -> {contract.start_date:yyyy-MM-dd}; "
                    + $"end date {previousEndDate:yyyy-MM-dd} -> {contract.end_date:yyyy-MM-dd}; "
                    + $"start odometer {previousStartOdometer} -> {contract.start_odometer}; "
                    + $"end odometer {previousEndOdometer?.ToString() ?? "-"} -> {contract.end_odometer?.ToString() ?? "-"}"
            );

            return Ok(
                new
                {
                    message = "Contract history updated successfully.",
                    contractCode = contract.contract_code,
                    startDate = contract.start_date,
                    endDate = contract.end_date,
                    startOdometer = contract.start_odometer,
                    endOdometer = contract.end_odometer,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating contract history {ContractId}", contractId);
            return StatusCode(
                500,
                new { error = "Failed to update contract history", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Get the full audit trail for a contract — every state change and edit, in chronological order.
    /// </summary>
    [HttpGet("{contractId}/audit-log")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetAuditLog(int contractId)
    {
        try
        {
            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });
            if (await RequireContractScopeAsync(contract) is { } scopeFailure)
                return scopeFailure;

            var entries = await _auditLog.GetByContractAsync(contractId);
            return Ok(
                new
                {
                    contract_code = contractId,
                    total_entries = entries.Count(),
                    audit_trail = entries.Select(e => new
                    {
                        e.id,
                        e.action,
                        e.performed_by_user_code,
                        e.performed_at,
                        e.old_status_code,
                        old_status_text = e.old_status_code.HasValue
                            ? GetStatusText(e.old_status_code.Value)
                            : null,
                        e.new_status_code,
                        new_status_text = e.new_status_code.HasValue
                            ? GetStatusText(e.new_status_code.Value)
                            : null,
                        e.field_changed,
                        e.old_value,
                        e.new_value,
                        e.notes,
                    }),
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving audit log for contract {ContractId}",
                contractId
            );
            return StatusCode(
                500,
                new { error = "Failed to retrieve audit log", message = ex.Message }
            );
        }
    }

    private static string GetStatusText(short status) =>
        status switch
        {
            0 => "Draft",
            1 => "Pending Review",
            2 => "Approved",
            3 => "Active",
            4 => "Declined for Correction",
            5 => "Declined",
            6 => "Cancelled",
            7 => "Closed",
            _ => $"Unknown ({status})",
        };

    private async Task<Driver?> ResolveSiteDriverAsync(int? siteDriverCode)
    {
        if (siteDriverCode is not > 0)
        {
            return null;
        }

        return await _context
            .Drivers.AsNoTracking()
            .FirstOrDefaultAsync(driver =>
                driver.site_driver_code == siteDriverCode.Value
                && driver.driver_active
                && !driver.is_deleted
            );
    }

    private static string? ResolveDriverIdentity(Driver? driver, string? fallbackDriverId)
    {
        var resolved =
            driver?.driver_SA_id
            ?? driver?.driver_passportnumber
            ?? driver?.driver_persalnumber
            ?? driver?.driver_contractnumber
            ?? fallbackDriverId;

        return NormalizeOptionalText(resolved);
    }

    private static string? ResolveDriverName(Driver? driver)
    {
        if (driver == null)
        {
            return null;
        }

        return NormalizeOptionalText($"{driver.driver_firstname} {driver.driver_surname}");
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    /// <summary>
    /// Search for available relief vehicles
    /// </summary>
    [HttpGet("relief/search")]
    [ProducesResponseType(
        typeof(IEnumerable<ReliefVehicleSearchResultDto>),
        StatusCodes.Status200OK
    )]
    public async Task<ActionResult<IEnumerable<ReliefVehicleSearchResultDto>>> SearchReliefVehicles(
        [FromQuery] string? query = null
    )
    {
        try
        {
            var allowedSiteCodes = await ResolveAllowedContractSiteCodesAsync();
            var candidates = await _contractRepository.SearchVehiclesForContractsAsync(
                query ?? string.Empty,
                allowedSiteCodes
            );
            var results = new List<ReliefVehicleSearchResultDto>();
            foreach (var vehicle in candidates)
            {
                if (vehicle.VehicleStatusCode.HasValue && vehicle.VehicleStatusCode != 1)
                    continue;

                if (
                    await _contractRepository.GetActiveContractByVehicleAsync(vehicle.VmfCode)
                    != null
                )
                    continue;

                results.Add(
                    new ReliefVehicleSearchResultDto
                    {
                        VmfCode = vehicle.VmfCode,
                        FleetNumber = vehicle.FleetNumber ?? string.Empty,
                        RegistrationNumber = vehicle.RegistrationNumber,
                        IsAvailable = true,
                    }
                );
            }

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching relief vehicles");
            return StatusCode(
                500,
                new { error = "Failed to search relief vehicles", message = ex.Message }
            );
        }
    }

    #endregion

    #region Data Repair Endpoints

    /// <summary>
    /// Dry-run preview of the contract status data repair.
    /// Shows exactly how many contracts would be updated, broken down by category.
    /// No data is changed — safe to call multiple times.
    ///
    /// Repair logic (mirrors legacy migration script 12):
    ///   still_current='Y' + status NULL  → set status 3 (Active)
    ///   still_current='N' + status NULL  → set status 7 (Closed)
    ///   still_current='N' + status NULL + end_date NULL → flagged as data quality issue
    /// </summary>
    [HttpGet("data-repair/preview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> DataRepairPreview()
    {
        if (RequireContractAction(HasContractAdminRole(), "You do not have permission to preview contract data repairs.") is { } authorization)
            return authorization;

        try
        {
            var affected = await _context
                .Contracts.Where(c => !c.is_deleted && c.contract_status_code == null)
                .Select(c => new
                {
                    c.contract_code,
                    c.still_current,
                    c.end_date,
                    c.vmf_code,
                })
                .ToListAsync();

            var activeRepairs = affected.Where(c => c.still_current == "Y").ToList();

            var closedWithEndDate = affected
                .Where(c => c.still_current == "N" && c.end_date.HasValue)
                .ToList();

            var closedMissingEndDate = affected
                .Where(c => c.still_current == "N" && !c.end_date.HasValue)
                .ToList();

            return Ok(
                new
                {
                    summary = new
                    {
                        total_affected = affected.Count,
                        will_set_active = activeRepairs.Count,
                        will_set_closed_normal = closedWithEndDate.Count,
                        will_set_closed_data_quality_flag = closedMissingEndDate.Count,
                    },
                    details = new
                    {
                        active_contracts = activeRepairs.Select(c => new
                        {
                            c.contract_code,
                            c.vmf_code,
                            proposed_status = 3,
                            proposed_status_text = "Active",
                        }),
                        closed_contracts = closedWithEndDate.Select(c => new
                        {
                            c.contract_code,
                            c.vmf_code,
                            c.end_date,
                            proposed_status = 7,
                            proposed_status_text = "Closed",
                        }),
                        data_quality_issues = closedMissingEndDate.Select(c => new
                        {
                            c.contract_code,
                            c.vmf_code,
                            proposed_status = 7,
                            proposed_status_text = "Closed",
                            warning = "end_date is NULL — contract closed without a recorded end date",
                        }),
                    },
                    note = "Call POST /data-repair/run to apply these changes.",
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating data repair preview");
            return StatusCode(
                500,
                new { error = "Failed to generate preview", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Execute the contract status data repair.
    /// Sets contract_status_code on all contracts where it is currently NULL,
    /// using still_current as the authoritative source of truth.
    ///
    /// All changes are individually audit-logged under action "DataRepair".
    /// This endpoint is idempotent — running it twice is safe (NULL check prevents re-processing).
    /// </summary>
    [HttpPost("data-repair/run")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> DataRepairRun()
    {
        if (
            RequireContractAction(
                HasContractAdminRole(),
                "You do not have permission to repair contract data."
            ) is
            { } authorization
        )
            return authorization;

        try
        {
            int currentUserId = GetCurrentUserId();

            var nullStatusContracts = await _context
                .Contracts.Where(c => !c.is_deleted && c.contract_status_code == null)
                .ToListAsync();

            if (nullStatusContracts.Count == 0)
                return Ok(
                    new
                    {
                        message = "No contracts require repair. All contracts already have a status code.",
                        repaired = 0,
                    }
                );

            int repairedActive = 0;
            int repairedClosed = 0;
            int dataQualityFlags = 0;

            var auditTasks = new List<Task>();

            foreach (var contract in nullStatusContracts)
            {
                short newStatus;
                string? note = null;

                if (contract.still_current == "Y")
                {
                    newStatus = 3; // Active
                    repairedActive++;
                }
                else
                {
                    newStatus = 7; // Closed
                    repairedClosed++;

                    if (!contract.end_date.HasValue)
                    {
                        note = "Data quality: end_date was NULL at time of repair";
                        dataQualityFlags++;
                    }
                }

                contract.contract_status_code = newStatus;
                contract.contract_status_date = DateTime.Now;
                contract.modified_by_user_code = currentUserId;
                contract.date_updated = DateTime.Now;

                auditTasks.Add(
                    _auditLog.LogAsync(
                        contract.contract_code,
                        "DataRepair",
                        currentUserId,
                        oldStatus: null,
                        newStatus: newStatus,
                        notes: note
                            ?? $"Status backfilled from still_current='{contract.still_current}' (legacy v2.1.05 migration)"
                    )
                );
            }

            await _context.SaveChangesAsync();
            await Task.WhenAll(auditTasks);

            _logger.LogInformation(
                "Data repair completed by user {UserId}: {Active} active, {Closed} closed ({DQ} data quality flags)",
                currentUserId,
                repairedActive,
                repairedClosed,
                dataQualityFlags
            );

            return Ok(
                new
                {
                    message = "Data repair completed successfully.",
                    repaired = nullStatusContracts.Count,
                    breakdown = new
                    {
                        set_to_active = repairedActive,
                        set_to_closed = repairedClosed,
                        data_quality_flags = dataQualityFlags,
                        data_quality_note = dataQualityFlags > 0
                            ? $"{dataQualityFlags} contracts were closed (still_current='N') but had no end_date recorded. Status set to Closed; audit log notes the discrepancy."
                            : null,
                    },
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing data repair");
            return StatusCode(
                500,
                new { error = "Failed to execute data repair", message = ex.Message }
            );
        }
    }

    #endregion
}

#region DTOs

public class HireContractDto
{
    [Required]
    public int VmfCode { get; set; }

    [Required]
    public short SiteCode { get; set; }
    public int? StartOdometer { get; set; }
    public string? DriverId { get; set; }
    public int? SiteDriverCode { get; set; }
    public short? UserCode { get; set; }
    public string? Authorisation { get; set; }
    public string? Notes { get; set; }
    public DateTime? TargetReturnDate { get; set; }
}

public class ReturnContractDto
{
    public int? EndOdometer { get; set; }
    public string? Notes { get; set; }
}

public class CloseContractRequest
{
    [Required]
    public DateTime EndDate { get; set; }

    [Required]
    public int EndOdometer { get; set; }
    public string? Notes { get; set; }

    [Required]
    public short? HomeDepartmentCode { get; set; }

    [Required]
    public short? HomeSiteCode { get; set; }
    public int? HomeSiteDriverCode { get; set; }
    public bool CreateHomeCustodyContract { get; set; } = true;
}

public class ExtendContractRequest
{
    [Required]
    public DateTime NewTargetReturnDate { get; set; }
    public string? Notes { get; set; }
    public int? EstimatedOverallKilometres { get; set; }
}

public class CancelContractRequest
{
    public string? CancellationReason { get; set; }
}

/// <summary>
/// DTO for editing a Draft or Declined-for-Correction contract.
/// All fields are optional — only provided fields are updated.
/// </summary>
public class EditContractDto
{
    public short? SiteCode { get; set; }
    public string? DriverId { get; set; }
    public int? SiteDriverCode { get; set; }
    public short? UserCode { get; set; }
    public string? Authorisation { get; set; }
    public string? Notes { get; set; }
    public DateTime? TargetReturnDate { get; set; }
    public int? StartOdometer { get; set; }
}

// New DTOs for Phase 1 endpoints

public class ContractReassignDto
{
    public int? NewVmfCode { get; set; }
    public short? NewSiteCode { get; set; }
    /// <summary>
    /// Optional destination custodian. When supplied, the selected driver must
    /// belong to the destination site; managers/admins can use this to replace
    /// a departed employee or client custodian without changing the vehicle's
    /// billing/contract history.
    /// </summary>
    public int? NewSiteDriverCode { get; set; }
    /// <summary>
    /// Optional target capturer. Managers/admins can explicitly transfer
    /// ownership; when omitted the original capturer remains owner.
    /// </summary>
    public int? NewCapturerUserCode { get; set; }
    public DateTime? StartDate { get; set; }
    public int? StartOdometer { get; set; }

    [Required]
    public string Reason { get; set; } = string.Empty;
}

public class VehicleSiteAssignmentRequest
{
    [Required]
    public short? DepartmentCode { get; set; }

    [Required]
    public short? SiteCode { get; set; }
    public int? SiteDriverCode { get; set; }
    public bool CreateHomeCustodyContract { get; set; }
    public string? Notes { get; set; }
}

public class ReliefVehicleDto
{
    [Required]
    public int ReliefVmfCode { get; set; }
    public int? StartOdometer { get; set; }
    public DateTime? TargetReturnDate { get; set; }

    [Required]
    public string Reason { get; set; } = string.Empty;
}

public class ContractValidationRequestDto
{
    [Required]
    public int VmfCode { get; set; }
    public short SiteCode { get; set; }
    public int? StartOdometer { get; set; }
}

public class ContractValidationResultDto
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class ContractApprovalDto
{
    public string? ApprovalNotes { get; set; }
}

public class ContractDeclineDto
{
    [Required]
    public string DeclineReason { get; set; } = string.Empty;
}

public class ContractPendingDetailsDto
{
    public int ContractCode { get; set; }
    public int VmfCode { get; set; }
    public short SiteCode { get; set; }
    public short? StatusCode { get; set; }
    public DateTime? StatusDate { get; set; }
    public bool IsPending { get; set; }
    public DateTime SubmittedDate { get; set; }
    public int? SubmittedByUserCode { get; set; }
}

public class ContractHistoryBackdatingRequest
{
    [Required]
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? StartOdometer { get; set; }
    public int? EndOdometer { get; set; }
}

public class ReliefVehicleSearchResultDto
{
    public int VmfCode { get; set; }
    public string FleetNumber { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public short? MakeCode { get; set; }
    public short? ModelCode { get; set; }
    public bool IsAvailable { get; set; }
}

public class ContractResponseDto
{
    public int ContractCode { get; set; }
    public int VmfCode { get; set; }
    public short SiteCode { get; set; }
    public string? ContractTypeCode { get; set; }
    public short? ContractStatusCode { get; set; }
    public DateTime? ContractStatusDate { get; set; }
    public string? StillCurrent { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime StartTime { get; set; }
    public int StartOdometer { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? EndTime { get; set; }
    public int? EndOdometer { get; set; }
    public int? MonthlyKm { get; set; }
    public short? HoursUsed { get; set; }
    public DateTime? TargetReturnDate { get; set; }
    public string? DriverId { get; set; }
    public string? DriverName { get; set; }
    public int? SiteDriverCode { get; set; }
    public int? ApproverCode { get; set; }
    public int? ParentContractCode { get; set; }
    public int? ReliefForContract { get; set; }
    public int? VehicleAssessmentCode { get; set; }
    public Guid? JournalDetailCode { get; set; }
    public bool LockedForTransfer { get; set; }
    public string? Notes { get; set; }
    public string? Authorisation { get; set; }
    public DateTime? ChargedUntil { get; set; }
    public string? CollectorFirstname { get; set; }
    public string? CollectorSurname { get; set; }
    public string? CollectorSaId { get; set; }
    public string? CollectorPassportNumber { get; set; }
    public string? CollectorOfficeNumber { get; set; }
    public string? CollectorCellphoneNumber { get; set; }
    public string? CollectorOffice { get; set; }
    public string? CollectorDesignation { get; set; }
    public bool? ReliefVehicleOption { get; set; }
    public byte? LeaseContractPeriod { get; set; }
    public int? ContractEstimatedOverallKm { get; set; }
    public DateTime? IntendedStartDate { get; set; }
    public TimeSpan? IntendedStartTime { get; set; }
    public DateTime? CaptureDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public int? ReassignedFromContractCode { get; set; }
    public short? UserCode { get; set; }
    public int? ContractGroupCode { get; set; }
    public string? BasFundCode { get; set; }
    public string? BasObjectiveCode { get; set; }
    public string? BasProjectNumber { get; set; }
    public string? BasResponsibilityCode { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateUpdated { get; set; }
    public int? CreatedByUserCode { get; set; }
    public int? ModifiedByUserCode { get; set; }
    public bool IsDeleted { get; set; }

    // Navigation properties - flattened to prevent circular references
    public string? VehicleFleetNumber { get; set; }
    public string? VehicleRegistrationNumber { get; set; }
    public string? SiteDescription { get; set; }
}

#endregion
