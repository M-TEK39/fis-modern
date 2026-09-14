using System.Data;
using System.Globalization;
using System.Security.Claims;
using FIS.Data.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Services.Finance;

/// <summary>
/// Resolves the Finance permissions and location scope used by the legacy
/// Finance pages. The location is deliberately loaded from the user's legacy
/// profile for each request; department, site, and province values supplied by
/// a browser are never a source of authority.
/// </summary>
public sealed class LegacyFinanceAccessService
{
    public const string AccessContextItemKey = "LegacyFinanceAccessContext";

    private const long FullLegacyAdministratorAccessLevel = 32767;
    private const string LegacyUsernameClaimType = "legacy_username";

    private readonly FisDbContext _context;
    private readonly ILogger<LegacyFinanceAccessService> _logger;

    public LegacyFinanceAccessService(
        FisDbContext context,
        ILogger<LegacyFinanceAccessService> logger
    )
    {
        _context = context;
        _logger = logger;
    }

    public async Task<LegacyFinanceAccessContext> ResolveAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default
    )
    {
        var roles = principal
            .Claims.Where(claim =>
                claim.Type == ClaimTypes.Role || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
            )
            .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(role => role.Trim())
            .Where(role => role.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var accessLevel = ReadLongClaim(principal, "access_level");
        var userAccessCode = ReadIntClaim(principal, "user_access_code");
        var profile = await ResolveProfileScopeAsync(userAccessCode, cancellationToken);
        // Legacy pages compare User.Identity.Name to the literal `cois`; an
        // email local part and a similarly named role are not equivalent.
        var legacyUsername = principal.FindFirst(LegacyUsernameClaimType)?.Value?.Trim();
        var isCois = string.Equals(legacyUsername, "cois", StringComparison.OrdinalIgnoreCase);
        var isFullAdministrator = accessLevel == FullLegacyAdministratorAccessLevel
            || roles.Contains("administrator")
            || roles.Contains("admin");

        var hasAllDepartmentDataRole = string.Equals(
            principal.FindFirst("finance_all_departments")?.Value,
            "true",
            StringComparison.OrdinalIgnoreCase
        );
        var hasProvinceWideVehicleListRole = string.Equals(
            principal.FindFirst("finance_all_department_vehicle_list")?.Value,
            "true",
            StringComparison.OrdinalIgnoreCase
        );

        return new LegacyFinanceAccessContext(
            profile,
            roles,
            isFullAdministrator,
            isCois,
            hasAllDepartmentDataRole,
            hasProvinceWideVehicleListRole
        );
    }

    /// <summary>
    /// The fine-grained legacy role tables are optional in expanded databases.
    /// When they exist, they remain the source of truth for the permission that
    /// spans all departments; the access-level bit is deliberately not enough.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetLegacyNamedRolesAsync(
        string? username,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return [];
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
            command.CommandText = """
                SELECT r.[RoleName]
                FROM [dbo].[aspnet_Users] AS u
                INNER JOIN [dbo].[aspnet_UsersInRoles] AS ur ON ur.[UserId] = u.[UserId]
                INNER JOIN [dbo].[aspnet_Roles] AS r
                    ON r.[RoleId] = ur.[RoleId]
                   AND r.[ApplicationId] = u.[ApplicationId]
                WHERE LOWER(u.[UserName]) = @username
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@username";
            parameter.DbType = DbType.String;
            parameter.Value = username.Trim().ToLowerInvariant();
            command.Parameters.Add(parameter);

            var roles = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var role = reader.IsDBNull(0) ? null : reader.GetString(0).Trim();
                if (!string.IsNullOrWhiteSpace(role))
                {
                    roles.Add(role);
                }
            }

            return roles;
        }
        catch (SqlException ex) when (ex.Number is 207 or 208)
        {
            _logger.LogInformation(
                "Legacy ASP.NET role tables are unavailable; continuing without named role hydration for {Username}",
                username
            );
            return [];
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<LegacyFinanceProfileScope?> ResolveProfileScopeAsync(
        int? userAccessCode,
        CancellationToken cancellationToken = default
    )
    {
        if (userAccessCode is null or <= 0 or > short.MaxValue)
        {
            return null;
        }

        var siteCode = await _context
            .UserAccessOlds.AsNoTracking()
            .Where(user => user.user_access_code == userAccessCode.Value)
            .Select(user => user.Site_code)
            .SingleOrDefaultAsync(cancellationToken);
        if (!siteCode.HasValue)
        {
            return null;
        }

        var site = await _context
            .Sites.AsNoTracking()
            .Where(item => item.Site_code == siteCode.Value)
            .Select(item => new { item.Depatrment_code, item.province_code })
            .SingleOrDefaultAsync(cancellationToken);
        if (site is null || !site.Depatrment_code.HasValue)
        {
            return null;
        }

        return new LegacyFinanceProfileScope(
            site.Depatrment_code.Value,
            siteCode.Value,
            site.province_code
        );
    }

    public Task<bool> CanAccessDepartmentAsync(
        LegacyFinanceAccessContext access,
        int? departmentCode,
        CancellationToken cancellationToken = default
    )
    {
        if (!departmentCode.HasValue || departmentCode.Value is <= 0 or > short.MaxValue)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(
            access.CanSelectAllDepartments || access.Profile?.DepartmentCode == departmentCode.Value
        );
    }

    public async Task<bool> CanAccessSiteAsync(
        LegacyFinanceAccessContext access,
        int? siteCode,
        CancellationToken cancellationToken = default
    )
    {
        if (!siteCode.HasValue || siteCode.Value is <= 0 or > short.MaxValue)
        {
            return false;
        }

        if (access.CanSelectAllDepartments)
        {
            return true;
        }

        if (access.Profile is null)
        {
            return false;
        }

        return await _context.Sites.AsNoTracking().AnyAsync(
            site =>
                site.Site_code == siteCode.Value
                && site.Depatrment_code == access.Profile.DepartmentCode,
            cancellationToken
        );
    }

    public async Task<bool> CanAccessProvinceAsync(
        LegacyFinanceAccessContext access,
        int? provinceCode,
        CancellationToken cancellationToken = default
    )
    {
        if (!provinceCode.HasValue || provinceCode.Value is <= 0 or > byte.MaxValue)
        {
            return false;
        }

        if (access.CanSelectAllDepartments)
        {
            return true;
        }

        if (access.Profile is null)
        {
            return false;
        }

        return await _context.Sites.AsNoTracking().AnyAsync(
            site =>
                site.Depatrment_code == access.Profile.DepartmentCode
                && site.province_code == provinceCode.Value,
            cancellationToken
        );
    }

    public Task<bool> DepartmentHasProvinceAsync(
        int departmentCode,
        int provinceCode,
        CancellationToken cancellationToken = default
    ) => _context.Sites.AsNoTracking().AnyAsync(
        site =>
            site.Depatrment_code == departmentCode
            && site.province_code == provinceCode,
        cancellationToken
    );

    private static int? ReadIntClaim(ClaimsPrincipal principal, string type)
    {
        return int.TryParse(principal.FindFirst(type)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static long ReadLongClaim(ClaimsPrincipal principal, string type)
    {
        return long.TryParse(principal.FindFirst(type)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    public sealed record LegacyFinanceProfileScope(
        short DepartmentCode,
        short SiteCode,
        byte? ProvinceCode
    );

    public sealed record LegacyFinanceAccessContext(
        LegacyFinanceProfileScope? Profile,
        IReadOnlySet<string> Roles,
        bool IsFullAdministrator,
        bool IsCois,
        bool HasAllDepartmentDataRole,
        bool HasProvinceWideVehicleListRole
    )
    {
        // FinanceMain.aspx itself is visible only to Financial Reports. The
        // data roles unlock sections within that module; they do not grant a
        // separate Finance entry point.
        public bool HasFinanceAccess => IsFullAdministrator || Roles.Contains("Financial Reports");

        // GetFinancialReports.aspx only offered cross-department selection when
        // both of these named roles were present. A generic financial bit must
        // not be upgraded into all-department authority.
        public bool CanSelectAllDepartments => IsFullAdministrator
            || (Roles.Contains("Financial Reports") && HasAllDepartmentDataRole);

        public bool CanMaintainFinanceData => IsFullAdministrator
            || Roles.Contains("Financial Data (Own Department)")
            || Roles.Contains("Financial Data (All Departments)");

        public bool CanRunFinancialReports => IsFullAdministrator
            || Roles.Contains("Financial Reports");

        // The all-departments data role may maintain BAS data across departments
        // without also being entitled to select every department in the reporting
        // screens. The legacy report menu required the additional Financial Reports
        // role; allocation maintenance did not.
        public bool CanMaintainAllFinanceData => IsFullAdministrator
            || (Roles.Contains("Financial Data (All Departments)") && HasProvinceWideVehicleListRole);

        // FinanceMain.aspx exposed both correction links to Financial Data
        // (Own Department) users. EditJournalBASCodes.aspx additionally
        // recognizes Financial Data (All Departments) and Vehicle List for
        // All Sites in Department. All of those paths are limited to the
        // profile department unless FinanceMain.aspx supplied dlist=1 for the
        // all-departments role at Head Office (or COIS).
        public bool CanMaintainBASCorrectionData => IsFullAdministrator
            || Roles.Contains("Financial Data (Own Department)")
            || Roles.Contains("Financial Data (All Departments)")
            || Roles.Contains("Vehicle List for All Sites in Department");

        public bool CanSelectAllBASCorrectionDepartments => IsFullAdministrator
            || (
                Roles.Contains("Financial Data (All Departments)")
                && CanUseHeadOfficeFinanceFeatures
            );

        public bool CanUseHeadOfficeFinanceFeatures => IsCois || Profile?.SiteCode == 1598;

        public bool CanUseDepartment147Features => IsCois || Profile?.DepartmentCode == 147;

        public bool CanUseBatchOperations => Roles.Contains("Advanced Financial Operations - Batch");

        public bool CanRunGeneralReports => IsFullAdministrator
            || Roles.Contains("Reports")
            || Roles.Contains("Financial Reports");

        public bool CanRunAuditTrailReports => IsFullAdministrator || Roles.Contains("Reports");

        // FISReports/AuditTrail.aspx is itself gated by Reports, while its
        // department selector separately opens to Financial Data (All
        // Departments). Financial Reports is not a prerequisite on this
        // legacy path.
        public bool CanSelectAllAuditDepartments => IsFullAdministrator
            || Roles.Contains("Financial Data (All Departments)");

        public bool CanManageTariffParameters => IsFullAdministrator
            || Roles.Contains("Financial Tariff Parameters")
            || Roles.Contains("Financial Tariff Parameters (Approver)");

        public bool CanApproveTariffParameters => IsFullAdministrator
            || Roles.Contains("Financial Tariff Parameters (Approver)");

        public bool CanMaintainOwnDepartmentFinanceData => IsFullAdministrator
            || Roles.Contains("Financial Data (Own Department)");

        public LegacyFinanceBillingHistoryScope? GetBillingHistoryScope()
        {
            if (!CanRunFinancialReports)
            {
                return null;
            }

            if (IsFullAdministrator || Roles.Contains("Financial Data (All Departments)"))
            {
                return new LegacyFinanceBillingHistoryScope(0, 0);
            }

            if (Profile is null)
            {
                return null;
            }

            return Roles.Contains("Financial Data (Own Department)")
                ? new LegacyFinanceBillingHistoryScope(Profile.DepartmentCode, 0)
                : new LegacyFinanceBillingHistoryScope(Profile.DepartmentCode, Profile.SiteCode);
        }
    }

    public sealed record LegacyFinanceBillingHistoryScope(short DepartmentCode, short SiteCode);
}
