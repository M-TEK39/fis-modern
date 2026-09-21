using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Reflection;

namespace FIS.Api.Services.Finance;

public sealed class LegacyFinanceAuthorizationFilter : IAsyncActionFilter, IOrderedFilter
{
    private readonly LegacyFinanceAccessService _financeAccess;

    public LegacyFinanceAuthorizationFilter(LegacyFinanceAccessService financeAccess)
    {
        _financeAccess = financeAccess;
    }

    public int Order => -1_000;

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        var access = await _financeAccess.ResolveAsync(
            context.HttpContext.User,
            context.HttpContext.RequestAborted
        );
        var isAllowed = access.HasFinanceAccess
            || (HasActionAttribute<LegacyFinanceBatchAccessAttribute>(context) && access.CanUseBatchOperations)
            || (HasActionAttribute<LegacyFinanceReportsAccessAttribute>(context) && access.CanRunGeneralReports)
            || (HasActionAttribute<LegacyFinanceAuditAccessAttribute>(context) && access.CanRunAuditTrailReports)
            || (HasActionAttribute<LegacyFinanceHeadOfficeAccessAttribute>(context) && access.CanUseHeadOfficeFinanceFeatures)
            || (HasActionAttribute<LegacyFinanceTariffAccessAttribute>(context) && access.CanManageTariffParameters)
            || (HasActionAttribute<LegacyFinanceDataAccessAttribute>(context) && access.CanMaintainFinanceData)
            || (HasActionAttribute<LegacyFinanceBasMaintenanceAccessAttribute>(context) && access.CanMaintainBASCorrectionData)
            || HasActionAttribute<LegacyFinanceBatchProgressReadAttribute>(context);
        if (!isAllowed)
        {
            context.Result = new ForbidResult();
            return;
        }

        context.HttpContext.Items[LegacyFinanceAccessService.AccessContextItemKey] = access;
        await next();
    }

    internal static bool HasActionAttribute<TAttribute>(ActionExecutingContext context)
        where TAttribute : Attribute =>
        context.ActionDescriptor is ControllerActionDescriptor descriptor
        && descriptor.MethodInfo.IsDefined(typeof(TAttribute), inherit: true);
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class LegacyFinanceBatchAccessAttribute : Attribute;

/// <summary>
/// BatchInProgress.aspx is reachable by any authenticated user while a batch
/// is running. Reading BatchIsRunning plus ADM_CheckJobStatus / ADM_CheckRecordedLogs
/// is therefore not limited to Advanced Financial Operations - Batch.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LegacyFinanceBatchProgressReadAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method)]
public sealed class LegacyFinanceReportsAccessAttribute : Attribute;

/// <summary>
/// Marks Finance data-maintenance and BAS selection actions. These actions
/// are available to Financial Data roles even when the employee does not hold
/// the separate Financial Reports role.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LegacyFinanceDataAccessAttribute : Attribute;

/// <summary>
/// The FISReports audit-trail menu is available to the general legacy
/// <c>Reports</c> role. Its department selector has different scope rules to
/// Wesbank and Regional reports, so it must not reuse their unrestricted
/// request-scope marker.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LegacyFinanceAuditAccessAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method)]
public sealed class LegacyFinanceHeadOfficeAccessAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method)]
public sealed class LegacyFinanceTariffAccessAttribute : Attribute;

/// <summary>
/// Finance/GetExpenditureReport.aspx exposed its all-departments option only
/// when the employee held both legacy all-department data permissions. That
/// option posts the intentional legacy value <c>0</c>, rather than an omitted
/// department value.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LegacyFinanceOutstandingAllDepartmentsAccessAttribute : Attribute;

/// <summary>
/// Marks the two FinanceMain.aspx BAS correction screens whose department
/// selector was controlled by the legacy <c>dlist</c> flag. They remain
/// finance-data operations, but Head Office/COIS users with Financial Data
/// (All Departments) may select another department on these screens.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LegacyFinanceBasMaintenanceAccessAttribute : Attribute;

public sealed class LegacyFinanceReportScopeFilter : IAsyncActionFilter, IOrderedFilter
{
    private readonly LegacyFinanceAccessService _financeAccess;

    public LegacyFinanceReportScopeFilter(LegacyFinanceAccessService financeAccess)
    {
        _financeAccess = financeAccess;
    }

    public int Order => -900;

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        var access = await ResolveAccessAsync(context);
        if (!access.HasFinanceAccess && !LegacyFinanceAuthorizationFilter.HasActionAttribute<LegacyFinanceReportsAccessAttribute>(context))
        {
            context.Result = new ForbidResult();
            return;
        }

        var filterBy = context.ActionArguments.TryGetValue("filterBy", out var rawFilter)
            ? rawFilter as string
            : null;
        var isSiteReport = string.Equals(filterBy, "Site", StringComparison.OrdinalIgnoreCase);
        var isProvinceReport = string.Equals(filterBy, "Province", StringComparison.OrdinalIgnoreCase);

        // Finance/GetFinancialReports.aspx offers Province only for the
        // DetailedInvoicedReport route. The other invoice procedures accept
        // Department or Site only; allowing Province through them would make
        // the procedure treat a province code as a department id.
        if (
            isProvinceReport
            && (
                context.ActionDescriptor is not ControllerActionDescriptor descriptor
                || descriptor.MethodInfo.Name != "GetInvoiceDetailed"
            )
        )
        {
            context.Result = new BadRequestObjectResult(
                new { error = "Province is available only for the detailed invoice report." }
            );
            return;
        }

        if (!context.ActionArguments.TryGetValue("id", out var rawId) || rawId is not short id)
        {
            context.Result = new BadRequestObjectResult(new { error = "A valid report location is required." });
            return;
        }

        // Dedicated report actions bind a missing `short id` as zero. Legacy
        // pages populated that value from the user's profile, so retain that
        // default for a restricted user instead of querying without a scope.
        if (id <= 0 && !access.CanSelectAllDepartments && access.Profile is not null)
        {
            id = isSiteReport
                ? access.Profile.SiteCode
                : isProvinceReport
                    ? access.Profile.ProvinceCode ?? 0
                    : access.Profile.DepartmentCode;
            context.ActionArguments["id"] = id;
        }

        if (id <= 0)
        {
            context.Result = new BadRequestObjectResult(new { error = "A valid report location is required." });
            return;
        }

        if (isProvinceReport)
        {
            var departmentCode = context.ActionArguments.TryGetValue(
                "departmentCode",
                out var rawDepartmentCode
            )
                ? rawDepartmentCode as short?
                : null;
            if (!departmentCode.HasValue && access.Profile is not null)
            {
                departmentCode = access.Profile.DepartmentCode;
                context.ActionArguments["departmentCode"] = departmentCode;
            }

            if (!departmentCode.HasValue || departmentCode.Value <= 0)
            {
                context.Result = new BadRequestObjectResult(
                    new { error = "A department is required for a province invoice report." }
                );
                return;
            }

            var provinceAllowed = await _financeAccess.CanAccessDepartmentAsync(
                access,
                departmentCode.Value,
                context.HttpContext.RequestAborted
            ) && await _financeAccess.CanAccessProvinceAsync(
                access,
                id,
                context.HttpContext.RequestAborted
            );
            if (!provinceAllowed)
            {
                context.Result = new ForbidResult();
                return;
            }

            // The legacy selector is DEV_SEL_Provinces_PerDepartment. Check
            // this relationship server-side as well so an elevated user cannot
            // combine unrelated department and province values by hand.
            var provinceBelongsToDepartment = await _financeAccess.DepartmentHasProvinceAsync(
                departmentCode.Value,
                id,
                context.HttpContext.RequestAborted
            );
            if (!provinceBelongsToDepartment)
            {
                context.Result = new BadRequestObjectResult(
                    new { error = "The selected province is not available for the selected department." }
                );
                return;
            }

            await next();
            return;
        }

        var allowed = isSiteReport
            ? await _financeAccess.CanAccessSiteAsync(access, id, context.HttpContext.RequestAborted)
            : await _financeAccess.CanAccessDepartmentAsync(access, id, context.HttpContext.RequestAborted);
        if (!allowed)
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }

    private async Task<LegacyFinanceAccessService.LegacyFinanceAccessContext> ResolveAccessAsync(
        ActionExecutingContext context
    )
    {
        if (
            context.HttpContext.Items.TryGetValue(LegacyFinanceAccessService.AccessContextItemKey, out var value)
            && value is LegacyFinanceAccessService.LegacyFinanceAccessContext access
        )
        {
            return access;
        }

        return await _financeAccess.ResolveAsync(
            context.HttpContext.User,
            context.HttpContext.RequestAborted
        );
    }
}

public sealed class LegacyFinanceDataAuthorizationFilter : IAsyncActionFilter, IOrderedFilter
{
    private readonly LegacyFinanceAccessService _financeAccess;

    public LegacyFinanceDataAuthorizationFilter(LegacyFinanceAccessService financeAccess)
    {
        _financeAccess = financeAccess;
    }

    public int Order => -900;

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        var access = await ResolveAccessAsync(context);
        var isBasMaintenanceAction = LegacyFinanceAuthorizationFilter.HasActionAttribute<
            LegacyFinanceBasMaintenanceAccessAttribute
        >(context);
        var canMaintainAction = isBasMaintenanceAction
            ? access.CanMaintainBASCorrectionData
            : access.CanMaintainFinanceData;
        if (!canMaintainAction)
        {
            context.Result = new ForbidResult();
            return;
        }

        // A number of long-lived Finance endpoints have a bare departmentCode
        // argument rather than a shared request DTO. Default it to the legacy
        // profile department and reject a supplied department outside that scope.
        if (context.ActionArguments.TryGetValue("departmentCode", out var rawDepartmentCode))
        {
            var canSelectRequestedDepartment = isBasMaintenanceAction
                ? access.CanSelectAllBASCorrectionDepartments
                : access.CanMaintainAllFinanceData;
            if (!canSelectRequestedDepartment && access.Profile is null)
            {
                context.Result = new ForbidResult();
                return;
            }

            if (rawDepartmentCode is null)
            {
                if (access.Profile is not null)
                {
                    // Every legacy Finance screen starts on the user's profile
                    // department. Elevated users may choose another department
                    // afterwards; an omitted query value was never a request
                    // for an unscoped, all-department result.
                    context.ActionArguments["departmentCode"] = (int)access.Profile.DepartmentCode;
                }
                else if (!canSelectRequestedDepartment)
                {
                    context.Result = new ForbidResult();
                    return;
                }
            }
            else if (!TryReadPositiveInt(rawDepartmentCode, out var departmentCode))
            {
                context.Result = new BadRequestObjectResult(
                    new { error = "Department code must be a valid positive value." }
                );
                return;
            }
            else if (
                !canSelectRequestedDepartment
                && (
                    isBasMaintenanceAction
                        ? access.Profile?.DepartmentCode != departmentCode
                        : !await _financeAccess.CanAccessDepartmentAsync(
                            access,
                            departmentCode,
                            context.HttpContext.RequestAborted
                        )
                )
            )
            {
                context.Result = new ForbidResult();
                return;
            }
        }

        context.HttpContext.Items[LegacyFinanceAccessService.AccessContextItemKey] = access;
        await next();
    }

    private async Task<LegacyFinanceAccessService.LegacyFinanceAccessContext> ResolveAccessAsync(
        ActionExecutingContext context
    )
    {
        if (
            context.HttpContext.Items.TryGetValue(LegacyFinanceAccessService.AccessContextItemKey, out var value)
            && value is LegacyFinanceAccessService.LegacyFinanceAccessContext access
        )
        {
            return access;
        }

        return await _financeAccess.ResolveAsync(
            context.HttpContext.User,
            context.HttpContext.RequestAborted
        );
    }

    private static bool TryReadPositiveInt(object value, out int parsed)
    {
        switch (value)
        {
            case int number:
                parsed = number;
                return parsed > 0;
            case short number:
                parsed = number;
                return parsed > 0;
            case string text:
                return int.TryParse(text, out parsed) && parsed > 0;
            default:
                parsed = 0;
                return false;
        }
    }
}

/// <summary>
/// Applies the profile default and validates the optional location fields used
/// by the Finance report request DTOs. The report controller receives several
/// historical request shapes, so this deliberately operates on the shared
/// property names instead of introducing a new transport contract.
/// </summary>
public sealed class LegacyFinanceRequestScopeFilter : IAsyncActionFilter, IOrderedFilter
{
    private readonly LegacyFinanceAccessService _financeAccess;

    public LegacyFinanceRequestScopeFilter(LegacyFinanceAccessService financeAccess)
    {
        _financeAccess = financeAccess;
    }

    public int Order => -900;

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        var access = await _financeAccess.ResolveAsync(
            context.HttpContext.User,
            context.HttpContext.RequestAborted
        );
        if (
            !access.HasFinanceAccess
            && !LegacyFinanceAuthorizationFilter.HasActionAttribute<LegacyFinanceReportsAccessAttribute>(context)
            && !LegacyFinanceAuthorizationFilter.HasActionAttribute<LegacyFinanceAuditAccessAttribute>(context)
        )
        {
            context.Result = new ForbidResult();
            return;
        }

        // ShowWesbankReport.aspx and ShowTotalCostReport.aspx authorize the
        // general Reports role directly and do not derive a department/site
        // restriction from the user's Finance profile. Preserve that separate
        // legacy reporting path rather than applying the Finance form scope.
        if (LegacyFinanceAuthorizationFilter.HasActionAttribute<LegacyFinanceReportsAccessAttribute>(context))
        {
            await next();
            return;
        }

        var canSelectAllDepartments = LegacyFinanceAuthorizationFilter.HasActionAttribute<
            LegacyFinanceOutstandingAllDepartmentsAccessAttribute
        >(context)
            ? access.CanMaintainAllFinanceData
            : LegacyFinanceAuthorizationFilter.HasActionAttribute<LegacyFinanceAuditAccessAttribute>(context)
                ? access.CanSelectAllAuditDepartments
                : access.CanSelectAllDepartments;
        var request = context.ActionArguments.Values.FirstOrDefault(value => value is not null);
        if (request is null || canSelectAllDepartments)
        {
            await next();
            return;
        }

        if (access.Profile is null)
        {
            context.Result = new ForbidResult();
            return;
        }

        var mode = ReadString(request, "Mode");
        var departmentCode = ReadOrDefault(request, "DepartmentCode", access.Profile.DepartmentCode);
        var siteCode = ReadOrDefault(
            request,
            "SiteCode",
            string.Equals(mode, "site", StringComparison.OrdinalIgnoreCase)
                ? access.Profile.SiteCode
                : null
        );
        var provinceCode = ReadOrDefault(request, "ProvinceCode", access.Profile.ProvinceCode)
            ?? ReadOrDefault(request, "Province", access.Profile.ProvinceCode);

        if (
            HasInvalidLocationValue(request, "DepartmentCode")
            || HasInvalidLocationValue(request, "SiteCode")
            || HasInvalidLocationValue(request, "ProvinceCode")
            || HasInvalidLocationValue(request, "Province")
        )
        {
            context.Result = new BadRequestObjectResult(new { error = "Report location values must be valid positive codes." });
            return;
        }

        if (
            !await IsAllowedDepartmentAsync(access, departmentCode, context.HttpContext.RequestAborted)
            || !await IsAllowedSiteAsync(access, siteCode, context.HttpContext.RequestAborted)
            || !await IsAllowedProvinceAsync(access, provinceCode, context.HttpContext.RequestAborted)
        )
        {
            context.Result = new ForbidResult();
            return;
        }

        if (RequiresHeadOfficeAccess(request) && !access.CanUseHeadOfficeFinanceFeatures)
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }

    private static string? ReadString(object request, string propertyName) =>
        request.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)?.GetValue(request) as string;

    private static int? ReadOrDefault(object request, string propertyName, object? defaultValue)
    {
        var property = request.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property is null || property.PropertyType != typeof(string))
        {
            return null;
        }

        var supplied = property.GetValue(request) as string;
        if (string.IsNullOrWhiteSpace(supplied) && defaultValue is not null)
        {
            supplied = Convert.ToString(defaultValue, System.Globalization.CultureInfo.InvariantCulture);
            property.SetValue(request, supplied);
        }

        return int.TryParse(supplied, out var result) && result > 0 ? result : null;
    }

    private static bool HasInvalidLocationValue(object request, string propertyName)
    {
        var value = ReadString(request, propertyName);
        return !string.IsNullOrWhiteSpace(value)
            && (!int.TryParse(value, out var parsed) || parsed <= 0);
    }

    private async Task<bool> IsAllowedDepartmentAsync(
        LegacyFinanceAccessService.LegacyFinanceAccessContext access,
        int? departmentCode,
        CancellationToken cancellationToken
    ) => departmentCode is null || await _financeAccess.CanAccessDepartmentAsync(access, departmentCode, cancellationToken);

    private async Task<bool> IsAllowedSiteAsync(
        LegacyFinanceAccessService.LegacyFinanceAccessContext access,
        int? siteCode,
        CancellationToken cancellationToken
    ) => siteCode is null || await _financeAccess.CanAccessSiteAsync(access, siteCode, cancellationToken);

    private async Task<bool> IsAllowedProvinceAsync(
        LegacyFinanceAccessService.LegacyFinanceAccessContext access,
        int? provinceCode,
        CancellationToken cancellationToken
    ) => provinceCode is null || await _financeAccess.CanAccessProvinceAsync(access, provinceCode, cancellationToken);

    private static bool RequiresHeadOfficeAccess(object request)
    {
        var action = ReadString(request, "Action");
        var mode = ReadString(request, "Mode");
        return action?.StartsWith("income", StringComparison.OrdinalIgnoreCase) == true
            || action?.StartsWith("download-income", StringComparison.OrdinalIgnoreCase) == true
            || mode?.StartsWith("income", StringComparison.OrdinalIgnoreCase) == true
            || mode?.StartsWith("download-income", StringComparison.OrdinalIgnoreCase) == true;
    }
}

public sealed class LegacyFinanceHeadOfficeAuthorizationFilter : LegacyFinanceCapabilityFilter
{
    public LegacyFinanceHeadOfficeAuthorizationFilter(LegacyFinanceAccessService financeAccess)
        : base(financeAccess) { }

    protected override bool IsAllowed(LegacyFinanceAccessService.LegacyFinanceAccessContext access) =>
        access.CanUseHeadOfficeFinanceFeatures;

    protected override bool RequiresFinanceModuleAccess => false;
}

public sealed class LegacyFinanceDepartment147AuthorizationFilter : LegacyFinanceCapabilityFilter
{
    public LegacyFinanceDepartment147AuthorizationFilter(LegacyFinanceAccessService financeAccess)
        : base(financeAccess) { }

    protected override bool IsAllowed(LegacyFinanceAccessService.LegacyFinanceAccessContext access) =>
        access.CanUseDepartment147Features;
}

/// <summary>
/// FinanceMain.aspx exposed the outstanding and missing-kilometres groups only
/// to the named Financial Reports role. Financial-data maintenance alone does
/// not unlock those reporting groups.
/// </summary>
public sealed class LegacyFinanceFinancialReportsAuthorizationFilter : LegacyFinanceCapabilityFilter
{
    public LegacyFinanceFinancialReportsAuthorizationFilter(LegacyFinanceAccessService financeAccess)
        : base(financeAccess) { }

    protected override bool IsAllowed(LegacyFinanceAccessService.LegacyFinanceAccessContext access) =>
        access.CanRunFinancialReports;
}

public sealed class LegacyFinanceCoisAuthorizationFilter : LegacyFinanceCapabilityFilter
{
    public LegacyFinanceCoisAuthorizationFilter(LegacyFinanceAccessService financeAccess)
        : base(financeAccess) { }

    protected override bool IsAllowed(LegacyFinanceAccessService.LegacyFinanceAccessContext access) =>
        access.IsCois;
}

public sealed class LegacyFinanceBatchAuthorizationFilter : LegacyFinanceCapabilityFilter
{
    public LegacyFinanceBatchAuthorizationFilter(LegacyFinanceAccessService financeAccess)
        : base(financeAccess) { }

    protected override bool RequiresFinanceModuleAccess => false;

    protected override bool IsAllowed(LegacyFinanceAccessService.LegacyFinanceAccessContext access) =>
        access.CanUseBatchOperations;
}

public sealed class LegacyFinanceTariffParametersAuthorizationFilter : LegacyFinanceCapabilityFilter
{
    public LegacyFinanceTariffParametersAuthorizationFilter(LegacyFinanceAccessService financeAccess)
        : base(financeAccess) { }

    protected override bool RequiresFinanceModuleAccess => false;

    protected override bool IsAllowed(LegacyFinanceAccessService.LegacyFinanceAccessContext access) =>
        access.CanManageTariffParameters;
}

public sealed class LegacyFinanceTariffApproverAuthorizationFilter : LegacyFinanceCapabilityFilter
{
    public LegacyFinanceTariffApproverAuthorizationFilter(LegacyFinanceAccessService financeAccess)
        : base(financeAccess) { }

    protected override bool RequiresFinanceModuleAccess => false;

    protected override bool IsAllowed(LegacyFinanceAccessService.LegacyFinanceAccessContext access) =>
        access.CanApproveTariffParameters;
}

public sealed class LegacyFinanceOwnDepartmentDataAuthorizationFilter : LegacyFinanceCapabilityFilter
{
    public LegacyFinanceOwnDepartmentDataAuthorizationFilter(LegacyFinanceAccessService financeAccess)
        : base(financeAccess) { }

    protected override bool IsAllowed(LegacyFinanceAccessService.LegacyFinanceAccessContext access) =>
        access.CanMaintainOwnDepartmentFinanceData;
}

public sealed class LegacyFinanceBillingHistoryAuthorizationFilter : IAsyncActionFilter, IOrderedFilter
{
    private readonly LegacyFinanceAccessService _financeAccess;

    public LegacyFinanceBillingHistoryAuthorizationFilter(LegacyFinanceAccessService financeAccess)
    {
        _financeAccess = financeAccess;
    }

    public int Order => -900;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var access = await _financeAccess.ResolveAsync(
            context.HttpContext.User,
            context.HttpContext.RequestAborted
        );
        var scope = access.GetBillingHistoryScope();
        if (scope is null)
        {
            context.Result = new ForbidResult();
            return;
        }

        context.HttpContext.Items[LegacyFinanceAccessService.AccessContextItemKey] = access;
        context.HttpContext.Items[BillingHistoryScopeItemKey] = scope;
        await next();
    }

    public const string BillingHistoryScopeItemKey = "LegacyFinanceBillingHistoryScope";
}

public abstract class LegacyFinanceCapabilityFilter : IAsyncActionFilter, IOrderedFilter
{
    private readonly LegacyFinanceAccessService _financeAccess;

    protected LegacyFinanceCapabilityFilter(LegacyFinanceAccessService financeAccess)
    {
        _financeAccess = financeAccess;
    }

    public int Order => -900;

    protected virtual bool RequiresFinanceModuleAccess => true;

    protected abstract bool IsAllowed(LegacyFinanceAccessService.LegacyFinanceAccessContext access);

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        var access = await _financeAccess.ResolveAsync(
            context.HttpContext.User,
            context.HttpContext.RequestAborted
        );
        if ((RequiresFinanceModuleAccess && !access.HasFinanceAccess) || !IsAllowed(access))
        {
            context.Result = new ForbidResult();
            return;
        }

        context.HttpContext.Items[LegacyFinanceAccessService.AccessContextItemKey] = access;
        await next();
    }
}
