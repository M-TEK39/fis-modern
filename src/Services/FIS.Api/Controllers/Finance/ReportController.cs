using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Json;
using FIS.Api.Services;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

/// <summary>
/// Reporting API endpoints
/// Provides vehicle, financial, maintenance, and trip reports with export capabilities
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
public class ReportController : BaseApiController
{
    private const long VehicleManagementPermission = 1;
    // The legacy all-access administrator level. This is deliberately distinct
    // from the Financial bit: that single bit represents both own- and
    // all-departments financial roles.
    private const long FullLegacyAdministratorAccessLevel = 32767;

    private readonly IReportingService _reportingService;
    private readonly ILegacyReportResultService _legacyReportResultService;
    private readonly IFineRepository _fineRepository;
    private readonly IVehicleSourceRepository _vehicleSourceRepository;
    private readonly IVehicleStatusReportRepository _vehicleStatusReportRepository;
    private readonly IFmlReportRepository _fmlReportRepository;
    private readonly IJobCardRepository _jobCardRepository;
    private readonly ILogbookRepository _logbookRepository;
    private readonly FisDbContext _context;
    private readonly ILogger<ReportController> _logger;

    public ReportController(
        IReportingService reportingService,
        ILegacyReportResultService legacyReportResultService,
        IFineRepository fineRepository,
        IVehicleSourceRepository vehicleSourceRepository,
        IVehicleStatusReportRepository vehicleStatusReportRepository,
        IFmlReportRepository fmlReportRepository,
        IJobCardRepository jobCardRepository,
        ILogbookRepository logbookRepository,
        FisDbContext context,
        ILogger<ReportController> logger
    )
    {
        _reportingService =
            reportingService ?? throw new ArgumentNullException(nameof(reportingService));
        _legacyReportResultService =
            legacyReportResultService
            ?? throw new ArgumentNullException(nameof(legacyReportResultService));
        _fineRepository = fineRepository ?? throw new ArgumentNullException(nameof(fineRepository));
        _vehicleSourceRepository =
            vehicleSourceRepository
            ?? throw new ArgumentNullException(nameof(vehicleSourceRepository));
        _vehicleStatusReportRepository =
            vehicleStatusReportRepository
            ?? throw new ArgumentNullException(nameof(vehicleStatusReportRepository));
        _fmlReportRepository =
            fmlReportRepository ?? throw new ArgumentNullException(nameof(fmlReportRepository));
        _jobCardRepository =
            jobCardRepository ?? throw new ArgumentNullException(nameof(jobCardRepository));
        _logbookRepository =
            logbookRepository ?? throw new ArgumentNullException(nameof(logbookRepository));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Vehicle Reports


    /// <summary>
    /// Generate a legacy-style dynamic report grid using the requested report key and query-string filters.
    /// </summary>
    [HttpGet("dynamic/{reportKey}")]
    [ProducesResponseType(typeof(LegacyReportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LegacyReportResultDto>> GetDynamicLegacyReport(
        string reportKey,
        CancellationToken cancellationToken
    )
    {
        // Keep the existing permission boundary for dynamic reports and add
        // the legacy Reports check for the Asset List route. Other dynamic
        // report callers retain their pre-existing authorization behavior.
        if ((IsFineReportKey(reportKey) || IsAssetListReportKey(reportKey)) && !HasReportsRole())
        {
            return Forbid();
        }

        try
        {
            var filters = Request.Query.ToDictionary(
                pair => pair.Key,
                pair => (string?)pair.Value.ToString(),
                StringComparer.OrdinalIgnoreCase
            );

            if (filters.ContainsKey("view"))
            {
                filters.Remove("view");
            }

            ExpandLegacyParameterPairs(filters);
            NormalizeLegacyAliases(filters);

            if (IsAssetListReportKey(reportKey))
            {
                var assetListScope = await ApplyAssetListProfileScopeAsync(
                    reportKey,
                    filters,
                    cancellationToken
                );
                if (!assetListScope.Allowed)
                {
                    return Forbid();
                }
            }

            var report = await _legacyReportResultService.GetReportAsync(
                reportKey,
                filters,
                cancellationToken
            );
            return Ok(report);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Unknown legacy report key requested: {ReportKey}", reportKey);
            return NotFound(new { error = "Unknown legacy report key", reportKey });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating dynamic legacy report {ReportKey}", reportKey);
            return StatusCode(
                500,
                new { error = "Failed to generate legacy report", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Provides the request-fresh selector scope used by the legacy Asset List
    /// Department and Site routes. The frontend uses this only to render the
    /// matching selector; GetDynamicLegacyReport remains the resource boundary.
    /// </summary>
    [HttpGet("asset-list-scope")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> GetAssetListScope(CancellationToken cancellationToken)
    {
        if (!HasReportsRole())
        {
            return Forbid();
        }

        var scope = await ResolveAssetListProfileScopeAsync(cancellationToken);
        if (scope is null)
        {
            return Forbid();
        }

        return Ok(
            new
            {
                allDepartments = scope.AllDepartments,
                departmentCode = scope.DepartmentCode,
                siteCode = scope.SiteCode,
            }
        );
    }

    private static void ExpandLegacyParameterPairs(IDictionary<string, string?> filters)
    {
        var hasNumberedParameters = filters.Keys.Any(key =>
            key.StartsWith("ParamName", StringComparison.OrdinalIgnoreCase)
        );
        if (!hasNumberedParameters)
        {
            return;
        }

        for (var index = 1; index <= 20; index++)
        {
            var nameKey = $"ParamName{index}";
            var valueKey = $"ParamValue{index}";

            if (
                !filters.TryGetValue(nameKey, out var parameterName)
                || string.IsNullOrWhiteSpace(parameterName)
            )
            {
                continue;
            }

            filters.TryGetValue(valueKey, out var parameterValue);
            if (!filters.ContainsKey(parameterName))
            {
                filters[parameterName] = parameterValue;
            }
        }
    }

    private static void NormalizeLegacyAliases(IDictionary<string, string?> filters)
    {
        CopyAliasIfMissing(filters, "from", "StartDate");
        CopyAliasIfMissing(filters, "from", "FromDate");
        CopyAliasIfMissing(filters, "to", "EndDate");
        CopyAliasIfMissing(filters, "to", "ToDate");
        CopyAliasIfMissing(filters, "dept", "DepartmentID");
        CopyAliasIfMissing(filters, "site", "SiteID");
        CopyAliasIfMissing(filters, "province", "ProvinceID");
        CopyAliasIfMissing(filters, "province", "lstProvinceID");
        CopyAliasIfMissing(filters, "department", "lstDepartmentID");
        CopyAliasIfMissing(filters, "site", "lstSites");
        CopyAliasIfMissing(filters, "SearchType", "searchType");
        CopyAliasIfMissing(filters, "search", "txtNum");
        CopyAliasIfMissing(filters, "vmf", "v_code");
    }

    private static void CopyAliasIfMissing(
        IDictionary<string, string?> filters,
        string canonicalKey,
        string aliasKey
    )
    {
        if (filters.ContainsKey(canonicalKey))
        {
            return;
        }

        if (filters.TryGetValue(aliasKey, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            filters[canonicalKey] = value;
        }
    }

    /// <summary>
    /// The legacy Asset List selector permits all departments only to members
    /// of Financial Data (All Departments). Other Reports users are bound to
    /// the department recorded in their ASP.NET profile, while retaining the
    /// legacy province selector and their department's site selector.
    /// </summary>
    private async Task<AssetListScopeResult> ApplyAssetListProfileScopeAsync(
        string reportKey,
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        if (IsAssetListAllReportKey(reportKey) || IsAssetListProvinceReportKey(reportKey))
        {
            return AssetListScopeResult.Allow;
        }

        var profileScope = await ResolveAssetListProfileScopeAsync(cancellationToken);
        if (profileScope is null || profileScope.AllDepartments)
        {
            return profileScope is { AllDepartments: true }
                ? AssetListScopeResult.Allow
                : AssetListScopeResult.Deny;
        }

        if (!profileScope.DepartmentCode.HasValue)
        {
            return AssetListScopeResult.Deny;
        }

        var profileDepartmentCode = profileScope.DepartmentCode.Value;

        if (IsAssetListDepartmentReportKey(reportKey))
        {
            SetAssetListFilterId(filters, profileDepartmentCode);
            return AssetListScopeResult.Allow;
        }

        if (!IsAssetListSiteReportKey(reportKey))
        {
            return AssetListScopeResult.Allow;
        }

        var requestedSiteCode = GetAssetListSiteFilterId(filters);
        if (!requestedSiteCode.HasValue)
        {
            // GetVehicleInserviceReports.aspx defaults the site selector to
            // the profile site before it constructs the report URL.
            if (!profileScope.SiteCode.HasValue)
            {
                return AssetListScopeResult.Deny;
            }

            SetAssetListFilterId(filters, profileScope.SiteCode.Value);
            return AssetListScopeResult.Allow;
        }

        if (requestedSiteCode.Value is <= 0 or > short.MaxValue)
        {
            return AssetListScopeResult.Deny;
        }

        var requestedSiteDepartmentCode = await _context
            .Sites.AsNoTracking()
            .Where(site => site.Site_code == requestedSiteCode.Value)
            .Select(site => site.Depatrment_code)
            .SingleOrDefaultAsync(cancellationToken);
        if (requestedSiteDepartmentCode != profileDepartmentCode)
        {
            return AssetListScopeResult.Deny;
        }

        SetAssetListFilterId(filters, requestedSiteCode.Value);
        return AssetListScopeResult.Allow;
    }

    private async Task<LegacyAssetListProfileScope?> ResolveAssetListProfileScopeAsync(
        CancellationToken cancellationToken
    )
    {
        // A system administrator must retain the legacy all-departments
        // selector. This covers both explicit modern/legacy administrator
        // roles and the legacy all-access level. Do not infer this from the
        // Financial bit alone: it represents both financial scope variants.
        if (HasAssetListAdministratorAccess())
        {
            return LegacyAssetListProfileScope.All;
        }

        var currentUserId = GetCurrentUserId();
        if (currentUserId is <= 0 or > short.MaxValue)
        {
            return null;
        }

        var profile = await _context
            .UserAccessOlds.AsNoTracking()
            .Where(user => user.user_access_code == currentUserId)
            .Select(user => new { user.name, user.Site_code })
            .SingleOrDefaultAsync(cancellationToken);
        if (profile is null)
        {
            return null;
        }

        // The compatibility access-level mapping intentionally produces a
        // broad Financial claim, so it cannot distinguish the legacy own- and
        // all-departments roles. Retain the original membership-role source
        // where it exists.
        if (
            !string.IsNullOrWhiteSpace(profile.name)
            && await HasLegacyNamedRoleAsync(
                profile.name,
                "Financial Data (All Departments)",
                cancellationToken
            )
        )
        {
            return LegacyAssetListProfileScope.All;
        }

        if (!string.IsNullOrWhiteSpace(profile.name))
        {
            var legacyScope = await TryGetLegacyAssetListProfileScopeAsync(
                profile.name,
                cancellationToken
            );
            if (legacyScope is not null)
            {
                return legacyScope;
            }
        }

        // Some supported modern-only databases deliberately omit the ASP.NET
        // membership/profile tables. user_access_old1.Site_code is the
        // established compatibility source in that shape; derive its matching
        // department at runtime rather than denying the Department/Site route.
        if (!profile.Site_code.HasValue)
        {
            return null;
        }

        var departmentCode = await _context
            .Sites.AsNoTracking()
            .Where(site => site.Site_code == profile.Site_code.Value)
            .Select(site => (int?)site.Depatrment_code)
            .SingleOrDefaultAsync(cancellationToken);

        return departmentCode.HasValue
            ? new LegacyAssetListProfileScope(departmentCode.Value, profile.Site_code)
            : null;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review if the query string passed to 'string DbCommand.CommandText' accepts any user input",
        Justification = "The interpolated membership identifiers come only from a fixed allow-list of inspected legacy schema column names; all request-derived values are parameters."
    )]
    private async Task<bool> HasLegacyNamedRoleAsync(
        string username,
        string expectedRole,
        CancellationToken cancellationToken
    )
    {
        var columns = await TryGetLegacyAspNetColumnsAsync(cancellationToken);
        var membershipUserKey = GetSharedLegacyUserKey(
            columns,
            "aspnet_Users",
            "aspnet_UsersInRoles"
        );
        if (membershipUserKey is null)
        {
            return false;
        }

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            var applicationScope =
                columns.TryGetValue("aspnet_Users", out var userColumns)
                && columns.TryGetValue("aspnet_Roles", out var roleColumns)
                && userColumns.Contains("ApplicationId")
                && roleColumns.Contains("ApplicationId")
                    ? " AND r.[ApplicationId] = u.[ApplicationId]"
                    : string.Empty;
            command.CommandText = $"""
                SELECT TOP (1) 1
                FROM [dbo].[aspnet_Users] AS u
                INNER JOIN [dbo].[aspnet_UsersInRoles] AS ur
                    ON ur.[{membershipUserKey}] = u.[{membershipUserKey}]
                INNER JOIN [dbo].[aspnet_Roles] AS r ON r.[RoleId] = ur.[RoleId]
                {applicationScope}
                WHERE LOWER(u.[UserName]) = @username
                  AND LOWER(r.[RoleName]) = @roleName
                """;
            AddStringParameter(command, "@username", username.Trim().ToLowerInvariant());
            AddStringParameter(command, "@roleName", expectedRole.ToLowerInvariant());

            return await command.ExecuteScalarAsync(cancellationToken) is not null;
        }
        catch (DbException ex)
        {
            _logger.LogWarning(
                ex,
                "Could not read legacy named report role for {Username}; enforcing profile scope",
                username
            );
            return false;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review if the query string passed to 'string DbCommand.CommandText' accepts any user input",
        Justification = "The interpolated profile identifiers come only from a fixed allow-list of inspected legacy schema column names; the username remains parameterized."
    )]
    private async Task<LegacyAssetListProfileScope?> TryGetLegacyAssetListProfileScopeAsync(
        string username,
        CancellationToken cancellationToken
    )
    {
        var columns = await TryGetLegacyAspNetColumnsAsync(cancellationToken);
        var profileUserKey = GetSharedLegacyUserKey(
            columns,
            "aspnet_Profile",
            "aspnet_Users"
        );
        if (profileUserKey is null)
        {
            return null;
        }

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT TOP (1)
                    CONVERT(nvarchar(max), p.[PropertyNames]),
                    CONVERT(nvarchar(max), p.[PropertyValuesString])
                FROM [dbo].[aspnet_Profile] AS p
                INNER JOIN [dbo].[aspnet_Users] AS u
                    ON p.[{profileUserKey}] = u.[{profileUserKey}]
                WHERE LOWER(u.[UserName]) = @username
                """;
            AddStringParameter(command, "@username", username.Trim().ToLowerInvariant());

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            var propertyNames = reader.IsDBNull(0) ? null : Convert.ToString(reader.GetValue(0));
            var propertyValues = reader.IsDBNull(1) ? null : Convert.ToString(reader.GetValue(1));
            var department = ParseLegacyProfileInteger(propertyNames, propertyValues, "DepartmentCode");
            var site = ParseLegacyProfileInteger(propertyNames, propertyValues, "SiteCode");
            return department.HasValue
                ? new LegacyAssetListProfileScope(department.Value, site)
                : null;
        }
        catch (DbException ex)
        {
            _logger.LogWarning(
                ex,
                "Could not read legacy report profile scope for {Username}",
                username
            );
            return null;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<IReadOnlyDictionary<string, HashSet<string>>> TryGetLegacyAspNetColumnsAsync(
        CancellationToken cancellationToken
    )
    {
        var columns = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["aspnet_Profile"] = new(StringComparer.OrdinalIgnoreCase),
            ["aspnet_Users"] = new(StringComparer.OrdinalIgnoreCase),
            ["aspnet_UsersInRoles"] = new(StringComparer.OrdinalIgnoreCase),
            ["aspnet_Roles"] = new(StringComparer.OrdinalIgnoreCase),
        };

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT [TABLE_NAME], [COLUMN_NAME]
                FROM [INFORMATION_SCHEMA].[COLUMNS]
                WHERE [TABLE_SCHEMA] = N'dbo'
                  AND [TABLE_NAME] IN (
                    N'aspnet_Profile',
                    N'aspnet_Users',
                    N'aspnet_UsersInRoles',
                    N'aspnet_Roles'
                  )
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var table = reader.IsDBNull(0) ? null : reader.GetString(0);
                var column = reader.IsDBNull(1) ? null : reader.GetString(1);
                if (
                    table is not null
                    && column is not null
                    && columns.TryGetValue(table, out var tableColumns)
                )
                {
                    tableColumns.Add(column);
                }
            }
        }
        catch (DbException ex)
        {
            _logger.LogWarning(ex, "Could not inspect legacy ASP.NET report-profile structures");
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        return columns;
    }

    private static string? GetSharedLegacyUserKey(
        IReadOnlyDictionary<string, HashSet<string>> columns,
        string firstTableName,
        string secondTableName
    )
    {
        if (
            !columns.TryGetValue(firstTableName, out var firstTableColumns)
            || !columns.TryGetValue(secondTableName, out var secondTableColumns)
        )
        {
            return null;
        }

        return firstTableColumns.Contains("UserId") && secondTableColumns.Contains("UserId")
            ? "UserId"
            : firstTableColumns.Contains("user_access_code")
                && secondTableColumns.Contains("user_access_code")
                ? "user_access_code"
                : null;
    }

    private static void AddStringParameter(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = DbType.String;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static int? ParseLegacyProfileInteger(
        string? propertyNames,
        string? propertyValues,
        string propertyName
    )
    {
        if (string.IsNullOrWhiteSpace(propertyNames) || propertyValues is null)
        {
            return null;
        }

        var tokens = propertyNames.Split(':');
        for (var index = 0; index + 3 < tokens.Length; index++)
        {
            if (
                !string.Equals(tokens[index], propertyName, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(tokens[index + 1], "S", StringComparison.OrdinalIgnoreCase)
                || !int.TryParse(
                    tokens[index + 2],
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var offset
                )
                || !int.TryParse(
                    tokens[index + 3],
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var length
                )
                || offset < 0
                || length <= 0
                || offset > propertyValues.Length
                || length > propertyValues.Length - offset
            )
            {
                continue;
            }

            var value = propertyValues.Substring(offset, length).Trim();
            if (
                int.TryParse(
                    value,
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var parsed
                )
                && parsed > 0
            )
            {
                return parsed;
            }
        }

        return null;
    }

    private static int? GetAssetListSiteFilterId(IDictionary<string, string?> filters)
    {
        foreach (var key in new[] { "id", "site", "site_code", "SiteID", "lstSites" })
        {
            if (
                filters.TryGetValue(key, out var rawValue)
                && int.TryParse(rawValue, out var value)
            )
            {
                return value;
            }
        }

        return null;
    }

    private static void SetAssetListFilterId(IDictionary<string, string?> filters, int value)
    {
        var text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        // The legacy filtered procedure gives `id` precedence.
        filters["id"] = text;
    }

    /// <summary>
    /// Generate detailed vehicle report
    /// </summary>
    [HttpGet("vehicle/{vmfCode}")]
    [ProducesResponseType(typeof(VehicleReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetVehicleReport(int vmfCode)
    {
        try
        {
            var report = await _reportingService.GenerateVehicleReportAsync(vmfCode);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating vehicle report for VMF {VmfCode}", vmfCode);
            return StatusCode(
                500,
                new { error = "Failed to generate vehicle report", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Generate vehicle report as PDF
    /// </summary>
    [HttpGet("vehicle/{vmfCode}/pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetVehicleReportPdf(int vmfCode)
    {
        try
        {
            var pdfBytes = await _reportingService.GenerateVehicleReportPdfAsync(vmfCode);
            return File(pdfBytes, "application/pdf", $"VehicleReport_{vmfCode}.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating vehicle PDF for VMF {VmfCode}", vmfCode);
            return StatusCode(500, new { error = "Failed to generate PDF", message = ex.Message });
        }
    }

    /// <summary>
    /// Generate master file report for all vehicles
    /// </summary>
    [HttpGet("masterfile")]
    [ProducesResponseType(typeof(MasterFileReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetMasterFileReport([FromQuery] int? vmfCode = null)
    {
        try
        {
            var report = await _reportingService.GenerateMasterFileReportAsync(vmfCode);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating master file report");
            return StatusCode(
                500,
                new { error = "Failed to generate master file report", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// New and In-Service vehicles report.
    /// Returns a page of vehicles with status In-Service (1) or Out-of-Service (2),
    /// optionally filtered by vehicle source (vs_code), hire type (type_code), or location.
    /// Includes ALL sources — including TSS (vs_code = 6) — which were previously
    /// missing from the legacy report due to a gap in Contract_Type_Group_Mapping.
    /// ⚠️ ASSUMPTION: status_code 1 = In Service, 2 = Out of Service / New-awaiting-assignment.
    ///    Confirm with business unit — see QUESTIONS.md R-3.
    /// </summary>
    [HttpGet("new-in-service")]
    public async Task<ActionResult> GetNewAndInServiceReport(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        [FromQuery] byte? vs_code = null,
        [FromQuery] short? type_code = null,
        [FromQuery] short? location_code = null,
        [FromQuery] short? make_code = null,
        [FromQuery] short? model_code = null,
        [FromQuery] short? vehicle_status_code = null,
        [FromQuery] string? search = null
    )
    {
        if (!HasReportsRole() || !HasVehicleManagementPermission())
        {
            return Forbid();
        }

        try
        {
            var reportPage = await _vehicleStatusReportRepository.GetPageAsync(
                new VehicleStatusReportQuery(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, 100),
                    vs_code,
                    type_code,
                    location_code,
                    make_code,
                    model_code,
                    vehicle_status_code,
                    search
                )
            );

            // Use the compatibility repository for vehicle sources. The client
            // database may not have the expanded audit/contact columns mapped
            // by EF, while the report only needs the source code and name.
            var sourcePage = await _vehicleSourceRepository.GetPageAsync();
            var allSources = sourcePage
                .Items.Where(source => !string.IsNullOrWhiteSpace(source.Name))
                .ToDictionary(source => source.SourceCode, source => source.Name!);

            var allSites = reportPage.Sites.ToDictionary(
                site => site.Code,
                site => site.Description
            );
            var allStatuses = reportPage.Statuses.ToDictionary(
                status => status.Code,
                status => status.Description
            );
            var allTypes = reportPage.Types.ToDictionary(
                type => type.Code,
                type => type.Description
            );
            var allModels = reportPage.Models.ToDictionary(model => model.Code);
            var allMakes = reportPage.Makes.ToDictionary(
                make => make.Code,
                make => make.Description
            );

            var result = reportPage
                .Vehicles.Select(v =>
                {
                    string? hiredFrom =
                        v.VehicleSourceCode.HasValue
                        && allSources.TryGetValue(v.VehicleSourceCode.Value, out var srcName)
                            ? srcName
                            : null;
                    allSites.TryGetValue(v.LocationCode ?? 0, out var siteName);
                    allStatuses.TryGetValue(v.VehicleStatusCode ?? 0, out var statusText);
                    allTypes.TryGetValue(v.TypeCode ?? 0, out var typeDesc);
                    allModels.TryGetValue(v.ModelCode ?? 0, out var modelInfo);
                    var makeDescription =
                        modelInfo?.MakeCode is int modelMakeCode
                        && allMakes.TryGetValue(modelMakeCode, out var makeName)
                            ? makeName
                            : string.Empty;

                    return new
                    {
                        vmf_code = v.VmfCode,
                        fleet_number = v.FleetNumber,
                        registration_number = v.RegistrationNumber,
                        vehicle_status_code = v.VehicleStatusCode,
                        status_text = statusText
                            ?? (v.VehicleStatusCode == 1 ? "In Service" : "Out of Service"),
                        type_code = v.TypeCode,
                        type_description = typeDesc ?? string.Empty,
                        vs_code = v.VehicleSourceCode,
                        hired_from = hiredFrom,
                        model_code = v.ModelCode,
                        make_description = makeDescription,
                        model_description = modelInfo?.Description ?? string.Empty,
                        location_code = v.LocationCode,
                        site_name = siteName ?? string.Empty,
                        chassis_number = v.ChassisNumber,
                        engine_number_1 = v.EngineNumber,
                        year_manufactured = v.YearManufactured,
                        take_on_date = v.TakeOnDate,
                        invoice_number = v.InvoiceNumber,
                        date_created = v.DateCreated,
                        current_odo = v.CurrentOdometer,
                        // Active remark (null if none)
                        active_remark = v.ActiveRemark == null
                            ? null
                            : (object)
                                new
                                {
                                    remark_id = v.ActiveRemark.RemarkId,
                                    remark_category = v.ActiveRemark.Category,
                                    remark_text = v.ActiveRemark.Text,
                                    date_created = v.ActiveRemark.DateCreated,
                                },
                    };
                })
                .ToList();

            _logger.LogInformation(
                "New/In-Service report: {Count} vehicles (search={Search}, vs_code={VsCode}, type={TypeCode}, location={Loc}, make={Make}, model={Model}, status={Status})",
                result.Count,
                search,
                vs_code,
                type_code,
                location_code,
                make_code,
                model_code,
                vehicle_status_code
            );

            return Ok(
                new
                {
                    page = reportPage.Page,
                    page_size = reportPage.PageSize,
                    total_count = reportPage.TotalCount,
                    total_pages = reportPage.TotalPages,
                    remarks_available = reportPage.RemarksAvailable,
                    filters_applied = new
                    {
                        search,
                        vs_code,
                        type_code,
                        location_code,
                        make_code,
                        model_code,
                        vehicle_status_code,
                    },
                    available_filters = new
                    {
                        sites = reportPage.Sites,
                        types = reportPage.Types,
                        makes = reportPage.Makes,
                        statuses = reportPage.Statuses,
                    },
                    assumption_note = "Status 1=InService, 2=OutOfService treated as New/Available. Confirm with business unit.",
                    vehicles = result,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating New/In-Service report");
            return StatusCode(
                500,
                new { error = "Failed to generate New/In-Service report", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Generate universal vehicle report with custom filters
    /// </summary>
    [HttpPost("universal")]
    [ProducesResponseType(typeof(UniversalReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetUniversalReport([FromBody] UniversalReportRequest request)
    {
        try
        {
            var report = await _reportingService.GenerateUniversalReportAsync(request);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating universal report");
            return StatusCode(
                500,
                new { error = "Failed to generate universal report", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Generate profitability report for VIP and pool vehicles.
    /// Dedicated endpoint replacing generic universal mode posting from UI.
    /// </summary>
    [HttpPost("finance/profitability")]
    [ProducesResponseType(typeof(UniversalReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetFinanceProfitabilityReport(
        [FromBody] FinanceProfitabilityRequest request
    )
    {
        try
        {
            var universalRequest = new UniversalReportRequest
            {
                ReportType = "financial",
                Parameters = new Dictionary<string, object>
                {
                    ["mode"] = "profitability",
                    ["financialYear"] = request.FinancialYear ?? string.Empty,
                },
            };

            var report = await _reportingService.GenerateUniversalReportAsync(universalRequest);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating finance profitability report");
            return StatusCode(
                500,
                new { error = "Failed to generate profitability report", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Generate outstanding amounts financial report.
    /// Dedicated endpoint replacing generic universal mode posting from UI.
    /// </summary>
    [HttpPost("finance/outstanding")]
    [ProducesResponseType(typeof(UniversalReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetFinanceOutstandingReport(
        [FromBody] FinanceOutstandingRequest request
    )
    {
        try
        {
            var parameters = new Dictionary<string, object>
            {
                ["mode"] = request.Mode ?? string.Empty,
                ["departmentCode"] = request.DepartmentCode ?? string.Empty,
                ["siteCode"] = request.SiteCode ?? string.Empty,
                ["financialYear"] = request.FinancialYear ?? string.Empty,
            };

            var universalRequest = new UniversalReportRequest
            {
                ReportType = "financial",
                Parameters = parameters,
                VmfCode = request.VmfCode,
            };

            var report = await _reportingService.GenerateUniversalReportAsync(universalRequest);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating finance outstanding report");
            return StatusCode(
                500,
                new { error = "Failed to generate outstanding report", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Generate Wesbank expenses financial report.
    /// Dedicated endpoint replacing generic universal mode posting from UI.
    /// </summary>
    [HttpPost("finance/wesbank")]
    [ProducesResponseType(typeof(UniversalReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetFinanceWesbankReport(
        [FromBody] FinanceWesbankRequest request
    )
    {
        try
        {
            var parameters = new Dictionary<string, object>
            {
                ["mode"] = request.Mode ?? string.Empty,
                ["provinceCode"] = request.ProvinceCode ?? string.Empty,
            };

            var universalRequest = new UniversalReportRequest
            {
                ReportType = "financial",
                Parameters = parameters,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
            };

            var report = await _reportingService.GenerateUniversalReportAsync(universalRequest);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating finance wesbank report");
            return StatusCode(
                500,
                new { error = "Failed to generate wesbank report", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Generate finance audit trail report.
    /// Dedicated endpoint replacing generic universal mode posting from UI.
    /// </summary>
    [HttpPost("finance/audit-trail")]
    [ProducesResponseType(typeof(UniversalReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetFinanceAuditTrailReport(
        [FromBody] FinanceAuditTrailRequest request
    )
    {
        try
        {
            var parameters = new Dictionary<string, object>
            {
                ["mode"] = request.Mode ?? string.Empty,
                ["auditType"] = request.AuditType ?? string.Empty,
                ["outputFormat"] = request.OutputFormat ?? string.Empty,
                ["departmentCode"] = request.DepartmentCode ?? string.Empty,
                ["siteCode"] = request.SiteCode ?? string.Empty,
            };

            var universalRequest = new UniversalReportRequest
            {
                ReportType = "financial",
                Parameters = parameters,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                VmfCode = request.VmfCode,
            };

            var report = await _reportingService.GenerateUniversalReportAsync(universalRequest);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating finance audit trail report");
            return StatusCode(
                500,
                new
                {
                    error = "Failed to generate finance audit trail report",
                    message = ex.Message,
                }
            );
        }
    }

    /// <summary>
    /// Generate finance regional reports (asset and summary variants).
    /// Dedicated endpoint replacing generic universal mode posting from UI.
    /// </summary>
    [HttpPost("finance/regional")]
    [ProducesResponseType(typeof(UniversalReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetFinanceRegionalReport(
        [FromBody] FinanceRegionalRequest request
    )
    {
        try
        {
            var parameters = new Dictionary<string, object>
            {
                ["mode"] = request.Mode ?? string.Empty,
                ["departmentCode"] = request.DepartmentCode ?? string.Empty,
                ["siteCode"] = request.SiteCode ?? string.Empty,
                ["provinceCode"] = request.ProvinceCode ?? string.Empty,
            };

            if (!string.IsNullOrWhiteSpace(request.SummaryType))
            {
                parameters["summaryType"] = request.SummaryType;
            }

            var universalRequest = new UniversalReportRequest
            {
                ReportType = string.IsNullOrWhiteSpace(request.ReportType)
                    ? "financial"
                    : request.ReportType!,
                Parameters = parameters,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
            };

            var report = await _reportingService.GenerateUniversalReportAsync(universalRequest);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating finance regional report");
            return StatusCode(
                500,
                new { error = "Failed to generate finance regional report", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Generate missing kilometres financial report variants.
    /// Dedicated endpoint replacing generic universal mode posting from UI.
    /// </summary>
    [HttpPost("finance/missing-kilometres")]
    [ProducesResponseType(typeof(UniversalReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetFinanceMissingKilometresReport(
        [FromBody] FinanceMissingKilometresRequest request
    )
    {
        try
        {
            var parameters = new Dictionary<string, object>
            {
                ["mode"] = request.Mode ?? string.Empty,
                ["departmentCode"] = request.DepartmentCode ?? string.Empty,
                ["provinceCode"] = request.ProvinceCode ?? string.Empty,
                ["financialYear"] = request.FinancialYear ?? string.Empty,
                ["excludeUnposted"] = request.ExcludeUnposted,
            };

            var universalRequest = new UniversalReportRequest
            {
                ReportType = "financial",
                Parameters = parameters,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
            };

            var report = await _reportingService.GenerateUniversalReportAsync(universalRequest);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating missing kilometres report");
            return StatusCode(
                500,
                new { error = "Failed to generate missing kilometres report", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Generate finance reports page report variants.
    /// Dedicated endpoint replacing generic universal mode posting from UI.
    /// </summary>
    [HttpPost("finance/reports")]
    [ProducesResponseType(typeof(UniversalReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetFinanceReportsPageReport(
        [FromBody] FinanceReportsPageRequest request
    )
    {
        try
        {
            var parameters = new Dictionary<string, object>
            {
                ["mode"] = request.Mode ?? string.Empty,
                ["action"] = request.Action ?? string.Empty,
                ["departmentCode"] = request.DepartmentCode ?? string.Empty,
                ["siteCode"] = request.SiteCode ?? string.Empty,
                ["province"] = request.Province ?? string.Empty,
                ["financialYear"] = request.FinancialYear ?? string.Empty,
                ["batchDate"] = request.BatchDate ?? string.Empty,
            };

            var universalRequest = new UniversalReportRequest
            {
                ReportType = request.ReportType ?? "financial",
                Parameters = parameters,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                VmfCode = request.VmfCode,
            };

            var report = await _reportingService.GenerateUniversalReportAsync(universalRequest);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating finance reports page report");
            return StatusCode(
                500,
                new
                {
                    error = "Failed to generate finance reports page report",
                    message = ex.Message,
                }
            );
        }
    }

    #endregion

    #region Maintenance Reports

    /// <summary>
    /// Generate service history report for a vehicle
    /// </summary>
    [HttpGet("maintenance/history/{vmfCode}")]
    [ProducesResponseType(typeof(ServiceHistoryReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetServiceHistory(int vmfCode)
    {
        try
        {
            var report = await _reportingService.GenerateServiceHistoryReportAsync(vmfCode);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating service history for VMF {VmfCode}", vmfCode);
            return StatusCode(
                500,
                new { error = "Failed to generate service history", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Generate maintenance cost report
    /// </summary>
    [HttpGet("maintenance/cost")]
    [ProducesResponseType(typeof(MaintenanceCostReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetMaintenanceCostReport(
        [FromQuery] int? vmfCode,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate
    )
    {
        try
        {
            var report = await _reportingService.GenerateMaintenanceCostReportAsync(
                vmfCode,
                startDate,
                endDate
            );
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating maintenance cost report");
            return StatusCode(
                500,
                new { error = "Failed to generate maintenance cost report", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Generate maintenance schedule report
    /// </summary>
    [HttpGet("maintenance/schedule")]
    [ProducesResponseType(typeof(MaintenanceScheduleReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetMaintenanceScheduleReport(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate
    )
    {
        try
        {
            var report = await _reportingService.GenerateMaintenanceScheduleReportAsync(
                startDate,
                endDate
            );
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating maintenance schedule report");
            return StatusCode(
                500,
                new
                {
                    error = "Failed to generate maintenance schedule report",
                    message = ex.Message,
                }
            );
        }
    }

    #endregion

    #region Financial Reports

    /// <summary>
    /// Generate vehicle billing history
    /// </summary>
    [HttpGet("billing/history/{vmfCode}")]
    [ProducesResponseType(typeof(VehicleBillingHistoryReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetBillingHistory(int vmfCode, [FromQuery] int financialYear)
    {
        try
        {
            var report = await _reportingService.GenerateVehicleBillingHistoryAsync(
                vmfCode,
                financialYear
            );
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating billing history for VMF {VmfCode}", vmfCode);
            return StatusCode(
                500,
                new { error = "Failed to generate billing history", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Generate contract billing report
    /// </summary>
    [HttpGet("contract/billing/{contractId}")]
    [ProducesResponseType(typeof(ContractBillingReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetContractBilling(
        int contractId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate
    )
    {
        try
        {
            var report = await _reportingService.GenerateContractBillingReportAsync(
                contractId,
                startDate,
                endDate
            );
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error generating contract billing for contract {ContractId}",
                contractId
            );
            return StatusCode(
                500,
                new { error = "Failed to generate contract billing", message = ex.Message }
            );
        }
    }

    #endregion

    #region Trip Reports

    /// <summary>
    /// Generate trip summary report
    /// </summary>
    [HttpGet("trip/summary")]
    [ProducesResponseType(typeof(TripSummaryReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetTripSummary(
        [FromQuery] int? vmfCode,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate
    )
    {
        try
        {
            var report = await _reportingService.GenerateTripSummaryReportAsync(
                vmfCode,
                startDate,
                endDate
            );
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating trip summary");
            return StatusCode(
                500,
                new { error = "Failed to generate trip summary", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Generate a filtered page of trip summary lines without changing the
    /// legacy collection-shaped trip summary endpoint.
    /// </summary>
    [HttpGet("trip/summary/page")]
    [ProducesResponseType(typeof(TripSummaryPage), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetTripSummaryPage(
        [FromQuery] int? vmfCode,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string? search = null,
        [FromQuery] string? filter = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24
    )
    {
        try
        {
            var result = await _reportingService.GenerateTripSummaryPageAsync(
                new TripSummaryPageQuery(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, 100),
                    vmfCode,
                    startDate,
                    endDate,
                    search,
                    filter
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
            _logger.LogError(ex, "Error generating paged trip summary");
            return StatusCode(
                500,
                new { error = "Failed to generate paged trip summary", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Generate trip detail report
    /// </summary>
    [HttpGet("trip/detail/{tripId}")]
    [ProducesResponseType(typeof(TripDetailReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetTripDetail(int tripId)
    {
        try
        {
            var report = await _reportingService.GenerateTripDetailReportAsync(tripId);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating trip detail for trip {TripId}", tripId);
            return StatusCode(
                500,
                new { error = "Failed to generate trip detail", message = ex.Message }
            );
        }
    }

    #endregion

    #region Contract Reports

    /// <summary>
    /// Generate contract summary report
    /// </summary>
    [HttpGet("contract/summary")]
    [ProducesResponseType(typeof(ContractSummaryReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetContractSummary([FromQuery] int? contractId = null)
    {
        try
        {
            var report = await _reportingService.GenerateContractSummaryReportAsync(contractId);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating contract summary");
            return StatusCode(
                500,
                new { error = "Failed to generate contract summary", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Generate authority report for contract
    /// </summary>
    [HttpGet("contract/authority/{contractId}")]
    [ProducesResponseType(typeof(AuthorityReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetAuthorityReport(int contractId)
    {
        try
        {
            var report = await _reportingService.GenerateAuthorityReportAsync(contractId);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error generating authority report for contract {ContractId}",
                contractId
            );
            return StatusCode(
                500,
                new { error = "Failed to generate authority report", message = ex.Message }
            );
        }
    }

    #endregion

    #region Full Maintenance Lease Reports

    [HttpGet("fml/maintenance-history")]
    public async Task<ActionResult> GetFmlMaintenanceHistory(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? finYear = null,
        [FromQuery] string? ggNum = null,
        [FromQuery] string? mode = null,
        [FromQuery] string? search = null
    )
    {
        var parsedFinancialYear = int.TryParse(finYear, out var financialYear)
            ? financialYear
            : (int?)null;
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? ggNum?.Trim() : search.Trim();
        var report = await _fmlReportRepository.GetMaintenanceHistoryAsync(
            startDate,
            endDate,
            parsedFinancialYear,
            normalizedSearch
        );
        return Ok(report);
    }

    [HttpGet("fml/contracts-expiring")]
    public async Task<ActionResult> GetFmlContractsExpiring()
    {
        var report = await _fmlReportRepository.GetContractsExpiringAsync();
        return Ok(report);
    }

    [HttpGet("fml/expired-open")]
    public async Task<ActionResult> GetFmlExpiredOpen()
    {
        var report = await _fmlReportRepository.GetExpiredOpenContractsAsync();
        return Ok(report);
    }

    [HttpGet("fml/vehicles-no-contracts")]
    public async Task<ActionResult> GetFmlVehiclesNoContracts()
    {
        var report = await _fmlReportRepository.GetVehiclesNoContractsAsync();
        return Ok(report);
    }

    [HttpGet("fml/over-utilized")]
    public async Task<ActionResult> GetFmlOverUtilized(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null
    )
    {
        var report = await _fmlReportRepository.GetOverUtilizedAsync(startDate, endDate);
        return Ok(report);
    }

    #endregion

    #region Legacy Losses and Licences Report Endpoints

    [HttpGet("losses")]
    public ActionResult GetLossesRoot()
    {
        return Ok(
            new
            {
                module = "Losses Reports",
                modes = new[] { "vehicle", "all", "no-report", "with-report", "dept-period" },
            }
        );
    }

    [HttpGet("licences")]
    public ActionResult GetLicencesRoot()
    {
        return Ok(
            new
            {
                module = "Licences Reports",
                modes = new[]
                {
                    "gg-number",
                    "gp-number",
                    "register-number",
                    "engine-number",
                    "chassis-number",
                    "site",
                    "all",
                    "dept-period",
                    "expire-date",
                    "month-fees",
                    "old-expire",
                    "sap",
                    "cof",
                    "model-fees",
                    "gg-model-fees",
                    "workgroup",
                    "workgroup-latest",
                    "ggmt-received",
                },
            }
        );
    }

    [HttpPost("losses/vehicle")]
    public async Task<ActionResult<List<Dictionary<string, object?>>>> GetLossesVehicleReport(
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken
    ) => Ok(await ExecuteLegacyGridAsync("losses", payload, "one-vehicle", cancellationToken));

    [HttpPost("losses/all")]
    public async Task<ActionResult<List<Dictionary<string, object?>>>> GetLossesAllReport(
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken
    ) => Ok(await ExecuteLegacyGridAsync("losses", payload, "all", cancellationToken));

    [HttpPost("losses/no-report")]
    public async Task<ActionResult<List<Dictionary<string, object?>>>> GetLossesNoReport(
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken
    ) => Ok(await ExecuteLegacyGridAsync("losses", payload, "no-report", cancellationToken));

    [HttpPost("losses/with-report")]
    public async Task<ActionResult<List<Dictionary<string, object?>>>> GetLossesWithReport(
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken
    ) => Ok(await ExecuteLegacyGridAsync("losses", payload, "with-report", cancellationToken));

    [HttpPost("losses/dept-period")]
    public async Task<ActionResult<List<Dictionary<string, object?>>>> GetLossesDeptPeriod(
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken
    ) => Ok(await ExecuteLegacyGridAsync("losses", payload, "dept-period", cancellationToken));

    [HttpPost("licences/{mode}")]
    public async Task<ActionResult<List<Dictionary<string, object?>>>> GetLicencesByMode(
        string mode,
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken
    )
    {
        var key = mode.ToLowerInvariant() switch
        {
            "gg-number" => "licences-gg-number",
            "gp-number" => "licences-prov-reg-number",
            "register-number" => "licences-register-number",
            "engine-number" => "licences-engine-number",
            "chassis-number" => "licences-chassis-number",
            "site" => "licences-site",
            "all" => "licences-all-with-model-tare-fee",
            "dept-period" => "licences-dept-sites-period",
            "expire-date" => "licences-expire-date",
            "month-fees" => "licences-month-fees",
            "old-expire" => "licences-old-expire-dates",
            "sap" => "licences-sap-info",
            "cof" => "licences-cof-info",
            "model-fees" => "licences-make-model-fee",
            "gg-model-fees" => "licences-all-with-model-tare-fee",
            "workgroup" => "licences-data-workgroup",
            "workgroup-latest" => "licences-data-workgroup-latest",
            "ggmt-received" => "licences-received-by-ggmt",
            _ => throw new KeyNotFoundException($"Unsupported licence report mode '{mode}'"),
        };

        return Ok(await ExecuteLegacyGridAsync(key, payload, null, cancellationToken));
    }

    private async Task<List<Dictionary<string, object?>>> ExecuteLegacyGridAsync(
        string reportKey,
        JsonElement payload,
        string? reportMode,
        CancellationToken cancellationToken
    )
    {
        var filters = JsonPayloadToFilters(payload);
        if (!string.IsNullOrWhiteSpace(reportMode))
        {
            filters["mode"] = reportMode;
        }
        NormalizeLegacyAliases(filters);
        var report = await _legacyReportResultService.GetReportAsync(
            reportKey,
            filters,
            cancellationToken
        );

        var rows = new List<Dictionary<string, object?>>(report.Rows.Count);
        foreach (var row in report.Rows)
        {
            var mapped = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in report.Columns)
            {
                row.TryGetValue(column.Key, out var value);
                mapped[column.Header] = value;
            }

            rows.Add(mapped);
        }

        return rows;
    }

    private static Dictionary<string, string?> JsonPayloadToFilters(JsonElement payload)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var property in payload.EnumerateObject())
        {
            var value = property.Value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                _ => property.Value.ToString(),
            };

            if (!string.IsNullOrWhiteSpace(value))
            {
                result[property.Name] = value;
            }
        }

        return result;
    }

    #endregion

    #region Export Functions

    /// <summary>
    /// Export data to CSV
    /// </summary>
    [HttpPost("export/csv")]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ExportToCsv([FromBody] ExportRequest request)
    {
        try
        {
            // Generic data export pipeline shared across legacy-style report screens.
            _logger.LogInformation("CSV export requested for {Filename}", request.Filename);

            var csvBytes = await _reportingService.ExportToCsvAsync(request.Data, request.Filename);
            return File(csvBytes, "text/csv", request.Filename);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting to CSV");
            return StatusCode(500, new { error = "Failed to export to CSV", message = ex.Message });
        }
    }

    /// <summary>
    /// Export data to Excel
    /// </summary>
    [HttpPost("export/excel")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ExportToExcel([FromBody] ExportRequest request)
    {
        try
        {
            _logger.LogInformation("Excel export requested for {Filename}", request.Filename);

            var excelBytes = await _reportingService.ExportToExcelAsync(
                request.Data,
                request.Filename
            );
            return File(
                excelBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                request.Filename
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting to Excel");
            return StatusCode(
                500,
                new { error = "Failed to export to Excel", message = ex.Message }
            );
        }
    }

    #endregion

    #region Report Metadata

    /// <summary>
    /// Get list of available report definitions
    /// </summary>
    [HttpGet("available")]
    [ProducesResponseType(typeof(List<ReportDefinition>), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetAvailableReports()
    {
        try
        {
            var reports = await _reportingService.GetAvailableReportsAsync();
            return Ok(reports);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available reports");
            return StatusCode(
                500,
                new { error = "Failed to retrieve available reports", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Get report help information
    /// </summary>
    [HttpGet("help")]
    [ProducesResponseType(typeof(ReportHelpDto), StatusCodes.Status200OK)]
    public ActionResult GetHelp()
    {
        var help = new ReportHelpDto
        {
            Title = "Reporting System Help",
            Description =
                "Generate comprehensive reports for vehicles, contracts, maintenance, and financial data",
            Sections = new List<HelpSectionDto>
            {
                new HelpSectionDto
                {
                    Title = "Vehicle Reports",
                    Content =
                        "Access detailed vehicle information including history, maintenance, and utilization",
                },
                new HelpSectionDto
                {
                    Title = "Financial Reports",
                    Content = "Review billing, contract costs, and financial summaries",
                },
                new HelpSectionDto
                {
                    Title = "Export Options",
                    Content = "Export report data to CSV or Excel formats",
                },
            },
        };
        return Ok(help);
    }

    /// <summary>
    /// Request additional report data or custom report generation
    /// </summary>
    [HttpPost("request-additional")]
    [ProducesResponseType(typeof(ReportRequestResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult> RequestAdditional([FromBody] AdditionalReportRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.ReportType))
        {
            return BadRequest(
                new ReportRequestResultDto { Success = false, Message = "Report type is required." }
            );
        }

        try
        {
            var now = DateTime.UtcNow;
            var currentUserId = GetCurrentUserId();

            var nextCodeInt =
                (
                    (int?)
                        await _context
                            .RequestChanges.Where(x => !x.is_deleted)
                            .MaxAsync(x => (short?)x.request_code) ?? 0
                ) + 1;

            if (nextCodeInt > short.MaxValue)
            {
                return StatusCode(
                    500,
                    new ReportRequestResultDto
                    {
                        Success = false,
                        Message = "Unable to allocate request code for report request.",
                    }
                );
            }

            var category = GetParameterString(request.Parameters, "category");
            var priority = GetParameterString(request.Parameters, "priority");
            var email = GetParameterString(request.Parameters, "email");
            var subject = GetParameterString(request.Parameters, "subject");
            var module = GetParameterString(request.Parameters, "module");
            var details = GetParameterString(request.Parameters, "details");

            var entity = new Core.Domain.Entities.System.RequestChange
            {
                request_code = (short)nextCodeInt,
                request_date = now,
                request_name = string.IsNullOrWhiteSpace(subject)
                    ? request.ReportType.Trim()
                    : subject!.Trim(),
                captured_by_userid = currentUserId > 0 ? currentUserId : null,
                change_description = string.IsNullOrWhiteSpace(details)
                    ? request.ReportType.Trim()
                    : details!.Trim(),
                sub_system_affected = string.IsNullOrWhiteSpace(module)
                    ? "Reports"
                    : module!.Trim(),
                request_comment = BuildRequestComment(
                    request.RequestedBy,
                    email,
                    category,
                    priority
                ),
                tech_description = SerializeParameters(request.Parameters),
                approve_or_not = "Pending",
                date_created = now,
                created_by_user_code = currentUserId > 0 ? currentUserId : null,
                is_deleted = false,
            };

            await _context.RequestChanges.AddAsync(entity);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Additional report request captured with request_code {RequestCode} for type {ReportType}",
                entity.request_code,
                request.ReportType
            );

            return Ok(
                new ReportRequestResultDto
                {
                    Success = true,
                    RequestId = entity.request_code.ToString(),
                    Message = "Report request submitted successfully",
                    EstimatedCompletionTime = now.AddMinutes(5),
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error capturing additional report request for report type {ReportType}",
                request.ReportType
            );
            return StatusCode(
                500,
                new ReportRequestResultDto
                {
                    Success = false,
                    Message = "Failed to submit report request.",
                }
            );
        }
    }

    /// <summary>
    /// Get audit trail sourced from contract audit log entries.
    /// Filters: startDate, endDate (inclusive), userId (performed_by_user_code).
    /// </summary>
    [HttpGet("audit-trail")]
    [ProducesResponseType(typeof(ReportAuditTrailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetAuditTrail(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? userId
    )
    {
        try
        {
            var query = _context.ContractAuditLogs.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(a => a.performed_at >= startDate.Value.Date);

            if (endDate.HasValue)
                query = query.Where(a =>
                    a.performed_at <= endDate.Value.Date.AddDays(1).AddSeconds(-1)
                );

            if (!string.IsNullOrWhiteSpace(userId) && int.TryParse(userId, out var userCode))
                query = query.Where(a => a.performed_by_user_code == userCode);

            var entries = await query
                .OrderByDescending(a => a.performed_at)
                .Take(500)
                .Select(a => new AuditEntryDto
                {
                    AuditId = a.id,
                    ReportType = "Contract",
                    UserId = a.performed_by_user_code.ToString(),
                    AccessedDate = a.performed_at,
                    Action = a.action,
                })
                .ToListAsync();

            _logger.LogInformation("Audit trail requested: {Count} entries", entries.Count);
            return Ok(new ReportAuditTrailDto { Entries = entries, TotalCount = entries.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating audit trail report");
            return StatusCode(
                500,
                new { error = "Failed to generate audit trail report", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Registration certificates report — legacy scan_docs-backed certificate listing.
    /// Filters: vmfCode (optional), search/mode for GG, GP, engine, VIN/chassis, or invoice lookup, departmentCode reserved.
    /// </summary>
    [HttpGet("registration-certificates")]
    [ProducesResponseType(typeof(RegistrationCertificatesReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetRegistrationCertificates(
        [FromQuery] int? vmfCode,
        [FromQuery] int? departmentCode,
        [FromQuery] string? mode,
        [FromQuery] string? search
    )
    {
        try
        {
            var normalizedMode = mode?.Trim().ToUpperInvariant() switch
            {
                "GP" => "GP",
                "ENGINE" => "ENGINE",
                "CHASSIS" => "VIN",
                "VIN" => "VIN",
                "INVOICE" => "INVOICE",
                _ => "GG",
            };
            var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

            var query =
                from scanDoc in _context.ScanDocs.AsNoTracking()
                join vehicle in _context.Vehicles.AsNoTracking()
                    on scanDoc.vmf_code equals vehicle.vmf_code
                where !scanDoc.is_deleted && !vehicle.is_deleted
                select new
                {
                    vehicle.vmf_code,
                    vehicle.fleet_number,
                    vehicle.registration_number,
                    vehicle.chassis_number,
                    vehicle.engine_number_1,
                    vehicle.invoice_number,
                    scanDoc.period_begin,
                    scanDoc.period_end,
                    scanDoc.image,
                    DateUploaded = scanDoc.date_updated ?? scanDoc.date_created,
                };

            if (vmfCode.HasValue)
            {
                query = query.Where(row => row.vmf_code == vmfCode.Value);
            }
            else if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                query = normalizedMode switch
                {
                    "GP" => query.Where(row =>
                        row.registration_number != null
                        && row.registration_number.Contains(normalizedSearch)
                    ),
                    "ENGINE" => query.Where(row =>
                        row.engine_number_1 != null
                        && row.engine_number_1.Contains(normalizedSearch)
                    ),
                    "VIN" => query.Where(row =>
                        row.chassis_number != null && row.chassis_number.Contains(normalizedSearch)
                    ),
                    "INVOICE" => query.Where(row =>
                        row.invoice_number != null && row.invoice_number.Contains(normalizedSearch)
                    ),
                    _ => query.Where(row =>
                        row.fleet_number != null && row.fleet_number.Contains(normalizedSearch)
                    ),
                };
            }

            var certificates = await query
                .OrderBy(row => row.fleet_number)
                .ThenBy(row => row.period_begin)
                .ThenBy(row => row.registration_number)
                .Select(row => new CertificateDto
                {
                    VmfCode = row.vmf_code,
                    FleetNumber = row.fleet_number ?? string.Empty,
                    RegistrationNumber = row.registration_number ?? string.Empty,
                    PeriodFrom = row.period_begin,
                    PeriodTo = row.period_end,
                    DateUploaded = row.DateUploaded,
                    RegistrationCertificate = row.image ?? string.Empty,
                })
                .ToListAsync();

            _logger.LogInformation(
                "Registration certificates report: {Count} records",
                certificates.Count
            );
            return Ok(
                new RegistrationCertificatesReportDto
                {
                    Certificates = certificates,
                    TotalCount = certificates.Count,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating registration certificates report");
            return StatusCode(
                500,
                new
                {
                    error = "Failed to generate registration certificates report",
                    message = ex.Message,
                }
            );
        }
    }

    /// <summary>
    /// Capture activity report — shows all records captured within a date range,
    /// broken down per module with summary counts and individual record details.
    /// Filters: date_from (required), date_to (default today), module, site_code, vmf_code, captured_by.
    /// Modules: All | Vehicles | Contracts | Accidents | Fines | JobCards | Logbooks | Documents | Remarks
    /// </summary>
    [HttpGet("capture-activity")]
    public async Task<ActionResult> GetCaptureActivityReport(
        [FromQuery] DateTime date_from,
        [FromQuery] DateTime? date_to = null,
        [FromQuery] string? module = null,
        [FromQuery] short? site_code = null,
        [FromQuery] int? vmf_code = null,
        [FromQuery] int? captured_by = null
    )
    {
        try
        {
            var toDate = (date_to ?? DateTime.Today).Date.AddDays(1).AddSeconds(-1); // end of day
            var fromDate = date_from.Date;
            var moduleFilter = string.IsNullOrWhiteSpace(module) ? "All" : module.Trim();

            var filtersApplied = new Dictionary<string, object?>();
            filtersApplied["date_from"] = fromDate.ToString("yyyy-MM-dd");
            filtersApplied["date_to"] = toDate.Date.ToString("yyyy-MM-dd");
            if (moduleFilter != "All")
                filtersApplied["module"] = moduleFilter;
            if (site_code.HasValue)
                filtersApplied["site_code"] = site_code;
            if (vmf_code.HasValue)
                filtersApplied["vmf_code"] = vmf_code;
            if (captured_by.HasValue)
                filtersApplied["captured_by"] = captured_by;

            // Build a lookup of vmf_code → (fleet_number, registration_number, veh_site_code) to enrich results
            // Only load vehicles that match site/vmf filters to keep query light
            var vehicleBase = _context.Vehicles.Where(v => !v.is_deleted);
            if (vmf_code.HasValue)
                vehicleBase = vehicleBase.Where(v => v.vmf_code == vmf_code.Value);
            if (site_code.HasValue)
                vehicleBase = vehicleBase.Where(v => v.veh_site_code == site_code.Value);
            var vehicleMap = await vehicleBase
                .Select(v => new
                {
                    v.vmf_code,
                    v.fleet_number,
                    v.registration_number,
                    v.veh_site_code,
                })
                .ToDictionaryAsync(v => v.vmf_code, v => v);

            // Helper: resolve vehicle info
            string? FleetNum(int? code) =>
                (code.HasValue && vehicleMap.TryGetValue(code.Value, out var fv))
                    ? fv.fleet_number
                    : null;
            string? RegNum(int? code) =>
                (code.HasValue && vehicleMap.TryGetValue(code.Value, out var rv))
                    ? rv.registration_number
                    : null;

            // Helper: check if a vmf_code passes site/vmf filter
            bool VehicleInScope(int? code)
            {
                if (!vmf_code.HasValue && !site_code.HasValue)
                    return true;
                if (code == null)
                    return false;
                return vehicleMap.ContainsKey(code.Value);
            }

            var summary = new Dictionary<string, int>();
            var details = new Dictionary<string, List<object>>();

            // ── Vehicles ─────────────────────────────────────────────────────────
            if (moduleFilter == "All" || moduleFilter == "Vehicles")
            {
                var q = _context.Vehicles.Where(v =>
                    !v.is_deleted && v.date_created >= fromDate && v.date_created <= toDate
                );
                if (captured_by.HasValue)
                    q = q.Where(v => v.created_by_user_code == captured_by.Value);
                if (vmf_code.HasValue)
                    q = q.Where(v => v.vmf_code == vmf_code.Value);
                if (site_code.HasValue)
                    q = q.Where(v => v.veh_site_code == site_code.Value);
                var rows = await q.OrderByDescending(v => v.date_created)
                    .Select(v => new CaptureActivityEntry
                    {
                        record_id = v.vmf_code,
                        vmf_code = v.vmf_code,
                        fleet_number = v.fleet_number,
                        registration_number = v.registration_number,
                        description =
                            $"Vehicle {v.fleet_number ?? v.registration_number ?? v.vmf_code.ToString()} added",
                        date_captured = v.date_created,
                        captured_by_user_code = v.created_by_user_code,
                        module = "Vehicles",
                    })
                    .ToListAsync();
                summary["Vehicles"] = rows.Count;
                details["Vehicles"] = rows.Cast<object>().ToList();
            }

            // ── Contracts ────────────────────────────────────────────────────────
            if (moduleFilter == "All" || moduleFilter == "Contracts")
            {
                var q = _context.Contracts.Where(c =>
                    !c.is_deleted && c.date_created >= fromDate && c.date_created <= toDate
                );
                if (captured_by.HasValue)
                    q = q.Where(c => c.created_by_user_code == captured_by.Value);
                if (vmf_code.HasValue)
                    q = q.Where(c => c.vmf_code == vmf_code.Value);
                if (site_code.HasValue)
                    q = q.Where(c => c.site_code == site_code.Value);
                var rows = await q.OrderByDescending(c => c.date_created)
                    .Select(c => new
                    {
                        c.contract_code,
                        c.vmf_code,
                        c.site_code,
                        c.date_created,
                        c.created_by_user_code,
                        c.still_current,
                    })
                    .ToListAsync();
                var mapped = rows.Where(c => VehicleInScope(c.vmf_code) || site_code == null)
                    .Select(c =>
                        (object)
                            new CaptureActivityEntry
                            {
                                record_id = c.contract_code,
                                vmf_code = c.vmf_code,
                                fleet_number = FleetNum(c.vmf_code),
                                registration_number = RegNum(c.vmf_code),
                                description =
                                    $"Contract captured (status: {(c.still_current == "Y" ? "Active" : "Inactive")})",
                                date_captured = c.date_created,
                                captured_by_user_code = c.created_by_user_code,
                                module = "Contracts",
                            }
                    )
                    .ToList();
                summary["Contracts"] = mapped.Count;
                details["Contracts"] = mapped;
            }

            // ── Accidents ────────────────────────────────────────────────────────
            if (moduleFilter == "All" || moduleFilter == "Accidents")
            {
                var q = _context.Accidents.Where(a =>
                    !a.is_deleted && a.date_created >= fromDate && a.date_created <= toDate
                );
                if (captured_by.HasValue)
                    q = q.Where(a => a.created_by_user_code == captured_by.Value);
                if (vmf_code.HasValue)
                    q = q.Where(a => a.vmf_code == vmf_code.Value);
                var rows = await q.OrderByDescending(a => a.date_created)
                    .Select(a => new
                    {
                        a.accident_code,
                        a.vmf_code,
                        a.description,
                        a.date_created,
                        a.created_by_user_code,
                    })
                    .ToListAsync();
                var mapped = rows.Where(a => VehicleInScope(a.vmf_code))
                    .Select(a =>
                        (object)
                            new CaptureActivityEntry
                            {
                                record_id = a.accident_code,
                                vmf_code = a.vmf_code,
                                fleet_number = FleetNum(a.vmf_code),
                                registration_number = RegNum(a.vmf_code),
                                description = a.description ?? "Accident recorded",
                                date_captured = a.date_created,
                                captured_by_user_code = a.created_by_user_code,
                                module = "Accidents",
                            }
                    )
                    .ToList();
                summary["Accidents"] = mapped.Count;
                details["Accidents"] = mapped;
            }

            // ── Fines ────────────────────────────────────────────────────────────
            if (moduleFilter == "All" || moduleFilter == "Fines")
            {
                var q = (await _fineRepository.GetAllAsync()).Where(f =>
                    !f.is_deleted && f.date_created >= fromDate && f.date_created <= toDate
                );
                if (captured_by.HasValue)
                    q = q.Where(f => f.created_by_user_code == captured_by.Value);
                if (vmf_code.HasValue)
                    q = q.Where(f => f.vmf_code == vmf_code.Value);
                var rows = q.OrderByDescending(f => f.date_created)
                    .Select(f => new
                    {
                        f.Fine_code,
                        f.vmf_code,
                        f.Offence_reference,
                        f.date_created,
                        f.created_by_user_code,
                    })
                    .ToList();
                var mapped = rows.Where(f => VehicleInScope(f.vmf_code))
                    .Select(f =>
                        (object)
                            new CaptureActivityEntry
                            {
                                record_id = f.Fine_code,
                                vmf_code = f.vmf_code,
                                fleet_number = FleetNum(f.vmf_code),
                                registration_number = RegNum(f.vmf_code),
                                description =
                                    $"Fine {f.Offence_reference ?? f.Fine_code.ToString()} captured",
                                date_captured = f.date_created,
                                captured_by_user_code = f.created_by_user_code,
                                module = "Fines",
                            }
                    )
                    .ToList();
                summary["Fines"] = mapped.Count;
                details["Fines"] = mapped;
            }

            // ── Job Cards ────────────────────────────────────────────────────────
            if (moduleFilter == "All" || moduleFilter == "JobCards")
            {
                // Use the compatibility repository here instead of querying
                // the expanded job_cards table directly. The client database
                // uses dbo.Jobcards and must remain reportable before the
                // expanded schema is installed.
                var rows = (await _jobCardRepository.GetAllAsync())
                    .Where(j =>
                        !j.is_deleted && j.date_created >= fromDate && j.date_created <= toDate
                    )
                    .Where(j =>
                        !captured_by.HasValue || j.created_by_user_code == captured_by.Value
                    )
                    .Where(j => !vmf_code.HasValue || j.vmf_code == vmf_code.Value)
                    .OrderByDescending(j => j.date_created)
                    .Select(j => new
                    {
                        j.job_card_id,
                        j.vmf_code,
                        j.jcs_comment,
                        j.status_code,
                        j.date_created,
                        j.created_by_user_code,
                    })
                    .ToList();
                var mapped = rows.Where(j => VehicleInScope(j.vmf_code))
                    .Select(j =>
                        (object)
                            new CaptureActivityEntry
                            {
                                record_id = j.job_card_id,
                                vmf_code = j.vmf_code,
                                fleet_number = FleetNum(j.vmf_code),
                                registration_number = RegNum(j.vmf_code),
                                description =
                                    j.jcs_comment
                                    ?? $"Job card #{j.job_card_id} (status {j.status_code})",
                                date_captured = j.date_created,
                                captured_by_user_code = j.created_by_user_code,
                                module = "JobCards",
                            }
                    )
                    .ToList();
                summary["JobCards"] = mapped.Count;
                details["JobCards"] = mapped;
            }

            // ── Logbooks ─────────────────────────────────────────────────────────
            if (moduleFilter == "All" || moduleFilter == "Logbooks")
            {
                var rows = (await _logbookRepository.GetAllAsync())
                    .Where(l =>
                        !l.is_deleted && l.date_created >= fromDate && l.date_created <= toDate
                    )
                    .Where(l =>
                        !captured_by.HasValue || l.created_by_user_code == captured_by.Value
                    )
                    .Where(l => !vmf_code.HasValue || l.vmf_code == vmf_code.Value)
                    .OrderByDescending(l => l.date_created)
                    .Select(l => new
                    {
                        l.logbookcode,
                        l.vmf_code,
                        l.date_created,
                        l.created_by_user_code,
                    })
                    .ToList();
                var mapped = rows.Where(l => VehicleInScope(l.vmf_code))
                    .Select(l =>
                        (object)
                            new CaptureActivityEntry
                            {
                                record_id = l.logbookcode,
                                vmf_code = l.vmf_code,
                                fleet_number = FleetNum(l.vmf_code),
                                registration_number = RegNum(l.vmf_code),
                                description = $"Logbook entry #{l.logbookcode}",
                                date_captured = l.date_created,
                                captured_by_user_code = l.created_by_user_code,
                                module = "Logbooks",
                            }
                    )
                    .ToList();
                summary["Logbooks"] = mapped.Count;
                details["Logbooks"] = mapped;
            }

            // ── Documents ────────────────────────────────────────────────────────
            if (moduleFilter == "All" || moduleFilter == "Documents")
            {
                var q = _context.VehicleDocuments.Where(d =>
                    !d.is_deleted && d.date_created >= fromDate && d.date_created <= toDate
                );
                if (captured_by.HasValue)
                    q = q.Where(d => d.created_by_user_code == captured_by.Value);
                if (vmf_code.HasValue)
                    q = q.Where(d => d.vmf_code == vmf_code.Value);
                var rows = await q.OrderByDescending(d => d.date_created)
                    .Select(d => new
                    {
                        d.document_id,
                        d.vmf_code,
                        d.document_category,
                        d.original_file_name,
                        d.date_created,
                        d.created_by_user_code,
                    })
                    .ToListAsync();
                var mapped = rows.Where(d => VehicleInScope(d.vmf_code))
                    .Select(d =>
                        (object)
                            new CaptureActivityEntry
                            {
                                record_id = d.document_id,
                                vmf_code = d.vmf_code,
                                fleet_number = FleetNum(d.vmf_code),
                                registration_number = RegNum(d.vmf_code),
                                description =
                                    $"{d.document_category} document: {d.original_file_name}",
                                date_captured = d.date_created,
                                captured_by_user_code = d.created_by_user_code,
                                module = "Documents",
                            }
                    )
                    .ToList();
                summary["Documents"] = mapped.Count;
                details["Documents"] = mapped;
            }

            // ── Remarks ──────────────────────────────────────────────────────────
            if (moduleFilter == "All" || moduleFilter == "Remarks")
            {
                var q = _context.VehicleRemarks.Where(r =>
                    !r.is_deleted && r.date_created >= fromDate && r.date_created <= toDate
                );
                if (captured_by.HasValue)
                    q = q.Where(r => r.created_by_user_code == captured_by.Value);
                if (vmf_code.HasValue)
                    q = q.Where(r => r.vmf_code == vmf_code.Value);
                var rows = await q.OrderByDescending(r => r.date_created)
                    .Select(r => new
                    {
                        r.remark_id,
                        r.vmf_code,
                        r.remark_text,
                        r.date_created,
                        r.created_by_user_code,
                    })
                    .ToListAsync();
                var mapped = rows.Where(r => VehicleInScope(r.vmf_code))
                    .Select(r =>
                        (object)
                            new CaptureActivityEntry
                            {
                                record_id = r.remark_id,
                                vmf_code = r.vmf_code,
                                fleet_number = FleetNum(r.vmf_code),
                                registration_number = RegNum(r.vmf_code),
                                description = r.remark_text ?? $"Remark #{r.remark_id}",
                                date_captured = r.date_created,
                                captured_by_user_code = r.created_by_user_code,
                                module = "Remarks",
                            }
                    )
                    .ToList();
                summary["Remarks"] = mapped.Count;
                details["Remarks"] = mapped;
            }

            var totalCount = summary.Values.Sum();
            _logger.LogInformation(
                "Capture activity report: {From} – {To}, module={Module}, total={Total}",
                fromDate,
                toDate.Date,
                moduleFilter,
                totalCount
            );

            return Ok(
                new
                {
                    filters_applied = filtersApplied,
                    total_count = totalCount,
                    summary,
                    details,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating capture activity report");
            return StatusCode(500, new { error = "Failed to generate capture activity report" });
        }
    }

    #endregion

    private static bool IsFineReportKey(string reportKey) =>
        reportKey.Equals("fines", StringComparison.OrdinalIgnoreCase)
        || reportKey.StartsWith("fines-", StringComparison.OrdinalIgnoreCase)
        || reportKey.Equals("appear-date", StringComparison.OrdinalIgnoreCase)
        || reportKey.Equals("reissue-submission", StringComparison.OrdinalIgnoreCase)
        || reportKey.Equals("traffic-dept-detail", StringComparison.OrdinalIgnoreCase)
        || reportKey.Equals("dept-site-period", StringComparison.OrdinalIgnoreCase);

    private static bool IsAssetListReportKey(string reportKey) =>
        reportKey.Equals("asset-list", StringComparison.OrdinalIgnoreCase)
        || reportKey.Equals("all-departments", StringComparison.OrdinalIgnoreCase)
        || reportKey.Equals("asset-list-by-province", StringComparison.OrdinalIgnoreCase)
        || reportKey.Equals("asset-list-by-department", StringComparison.OrdinalIgnoreCase)
        || reportKey.Equals("asset-list-by-site", StringComparison.OrdinalIgnoreCase)
        || reportKey.Equals("by-province", StringComparison.OrdinalIgnoreCase)
        || reportKey.Equals("by-department", StringComparison.OrdinalIgnoreCase)
        || reportKey.Equals("by-site", StringComparison.OrdinalIgnoreCase);

    private static bool IsAssetListAllReportKey(string reportKey) =>
        reportKey.Equals("asset-list", StringComparison.OrdinalIgnoreCase)
        || reportKey.Equals("all-departments", StringComparison.OrdinalIgnoreCase);

    private static bool IsAssetListProvinceReportKey(string reportKey) =>
        reportKey.Equals("asset-list-by-province", StringComparison.OrdinalIgnoreCase)
        || reportKey.Equals("by-province", StringComparison.OrdinalIgnoreCase);

    private static bool IsAssetListDepartmentReportKey(string reportKey) =>
        reportKey.Equals("asset-list-by-department", StringComparison.OrdinalIgnoreCase)
        || reportKey.Equals("by-department", StringComparison.OrdinalIgnoreCase);

    private static bool IsAssetListSiteReportKey(string reportKey) =>
        reportKey.Equals("asset-list-by-site", StringComparison.OrdinalIgnoreCase)
        || reportKey.Equals("by-site", StringComparison.OrdinalIgnoreCase);

    private sealed record AssetListScopeResult(bool Allowed)
    {
        public static readonly AssetListScopeResult Allow = new(true);
        public static readonly AssetListScopeResult Deny = new(false);
    }

    private sealed record LegacyAssetListProfileScope(
        int? DepartmentCode,
        int? SiteCode,
        bool AllDepartments = false
    )
    {
        public static readonly LegacyAssetListProfileScope All = new(null, null, true);
    }

    private bool HasReportsRole() => HasAnyRole("Reports");

    private bool HasAssetListAdministratorAccess()
    {
        if (
            HasAnyRole(
                "admin",
                "administrator",
                "system administrator",
                "systemadministrator"
            )
        )
        {
            return true;
        }

        var accessLevelClaim = User.FindFirst("access_level")?.Value;
        return long.TryParse(accessLevelClaim, out var accessLevel)
            && accessLevel == FullLegacyAdministratorAccessLevel;
    }

    private bool HasVehicleManagementPermission()
    {
        var accessLevelClaim = User.FindFirst("access_level")?.Value;
        return long.TryParse(accessLevelClaim, out var accessLevel)
            && (accessLevel & VehicleManagementPermission) == VehicleManagementPermission;
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

    private static string? GetParameterString(Dictionary<string, object> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Null => null,
                _ => element.GetRawText(),
            };
        }

        return value.ToString();
    }

    private static string SerializeParameters(Dictionary<string, object> parameters)
    {
        return JsonSerializer.Serialize(parameters);
    }

    private static string BuildRequestComment(
        string requestedBy,
        string? email,
        string? category,
        string? priority
    )
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(requestedBy))
        {
            parts.Add($"RequestedBy={requestedBy.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            parts.Add($"Email={email.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            parts.Add($"Category={category.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            parts.Add($"Priority={priority.Trim()}");
        }

        return string.Join("; ", parts);
    }
}

#region Report DTOs

/// <summary>
/// Export request model
/// </summary>
public class ExportRequest
{
    public List<object> Data { get; set; } = new();
    public string Filename { get; set; } = "export.csv";
}

public class ReportHelpDto
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public List<HelpSectionDto> Sections { get; set; } = new();
}

public class AdditionalReportRequestDto
{
    public string ReportType { get; set; } = "";
    public Dictionary<string, object> Parameters { get; set; } = new();
    public string RequestedBy { get; set; } = "";
}

public class ReportRequestResultDto
{
    public bool Success { get; set; }
    public string RequestId { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime? EstimatedCompletionTime { get; set; }
}

public class ReportAuditTrailDto
{
    public List<AuditEntryDto> Entries { get; set; } = new();
    public int TotalCount { get; set; }
}

public class AuditEntryDto
{
    public int AuditId { get; set; }
    public string ReportType { get; set; } = "";
    public string UserId { get; set; } = "";
    public DateTime AccessedDate { get; set; }
    public string Action { get; set; } = "";
}

public class RegistrationCertificatesReportDto
{
    public List<CertificateDto> Certificates { get; set; } = new();
    public int TotalCount { get; set; }
}

public class CertificateDto
{
    public int VmfCode { get; set; }
    public string FleetNumber { get; set; } = "";
    public string RegistrationNumber { get; set; } = "";
    public DateTime? PeriodFrom { get; set; }
    public DateTime? PeriodTo { get; set; }
    public DateTime? DateUploaded { get; set; }
    public string RegistrationCertificate { get; set; } = "";
    public DateTime? IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string Status { get; set; } = "";
}

#endregion

public class CaptureActivityEntry
{
    public int record_id { get; set; }
    public int? vmf_code { get; set; }
    public string? fleet_number { get; set; }
    public string? registration_number { get; set; }
    public string? description { get; set; }
    public DateTime date_captured { get; set; }
    public int? captured_by_user_code { get; set; }
    public string module { get; set; } = "";
}

// Note: Report model types referenced above should be defined in IReportingService interface
// VehicleReport, MasterFileReport, UniversalReportRequest, ServiceHistoryReport, etc.

public class FinanceProfitabilityRequest
{
    public string? FinancialYear { get; set; }
}

public class FinanceOutstandingRequest
{
    public string? Mode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SiteCode { get; set; }
    public string? FinancialYear { get; set; }
    public int? VmfCode { get; set; }
}

public class FinanceWesbankRequest
{
    public string? Mode { get; set; }
    public string? ProvinceCode { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class FinanceAuditTrailRequest
{
    public string? Mode { get; set; }
    public string? AuditType { get; set; }
    public string? OutputFormat { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SiteCode { get; set; }
    public int? VmfCode { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class FinanceRegionalRequest
{
    public string? ReportType { get; set; }
    public string? Mode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SiteCode { get; set; }
    public string? ProvinceCode { get; set; }
    public string? SummaryType { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class FinanceMissingKilometresRequest
{
    public string? Mode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? ProvinceCode { get; set; }
    public string? FinancialYear { get; set; }
    public bool ExcludeUnposted { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class FinanceReportsPageRequest
{
    public string? ReportType { get; set; }
    public string? Mode { get; set; }
    public string? Action { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SiteCode { get; set; }
    public string? Province { get; set; }
    public string? FinancialYear { get; set; }
    public string? BatchDate { get; set; }
    public int? VmfCode { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
