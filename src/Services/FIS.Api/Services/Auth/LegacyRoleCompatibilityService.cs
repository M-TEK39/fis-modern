using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FIS.Data.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Services;

/// <summary>
/// Resolves the role names used by the legacy FIS authorization model.
///
/// Legacy installations may have ASP.NET membership role rows, the older
/// comma-separated Access_str IDs, or only the AccessLevel bitmask. The
/// membership rows and AccessLevels_2 catalog are preferred; a missing
/// optional catalog is a compatibility state and never grants guessed roles.
/// </summary>
public sealed class LegacyRoleCompatibilityService
{
    // The legacy FIS database models a system administrator as a single
    // SystemAdministrator role while the application protects each module
    // with its own role name.  Preserve that legacy administrator contract by
    // materialising the module roles into the authenticated principal.  This
    // is deliberately limited to the two administrator names used by the
    // restored FIS sources; ordinary users still receive only their assigned
    // membership/access-catalog roles.
    private static readonly string[] SystemAdministratorRoles =
    [
        "SystemAdministrator",
        "System Administrator",
        // Older restored FIS profiles use the shorter administrator names.
        // Other module controllers already treat these as global administrator
        // aliases; materialise the same module scope at login so navigation and
        // server authorization cannot disagree.
        "Administrator",
        "Admin",
    ];

    private static readonly string[] LegacyModuleRoles =
    [
        "Accidents",
        "Acquisition",
        "Asset Verification",
        "Auction",
        "BAS Journal Parameters",
        "Back Dating Contract (Approver)",
        "Call Centre",
        "Clearance",
        "Contracts",
        "Contract (Load and Manage)",
        "Contract (Cancel and Close)",
        "Contract (Approver)",
        "Contract History Back Dating",
        "Demo Vehicles",
        "Departmental Contracts",
        "Financial Data (All Departments)",
        "Financial Data (Own Department)",
        "Financial Reports",
        "Financial Tariff Parameters",
        "Financial Tariff Parameters (Approver)",
        "Advanced Financial Operations - Batch",
        "Fines",
        "Fuelcards",
        "GG Number Maintenance",
        "GGMT BAS Transfer Parameters",
        "Help Desk Functions",
        "JobCard Authorizer",
        "JobCard Capturer",
        "Lease Vehicle Authorizer",
        "Lease Vehicle Capturer",
        "Lease Vehicle Pending",
        "Licence",
        "Logistics",
        "Logbooks",
        "Logsheets",
        "Losses",
        "Losses HQ",
        "Management Reports",
        "Missing Vehicles",
        "Monitor",
        "Private Hire Vehicles",
        "Reports",
        "Taxi information maintenance",
        "Taxi Invoices",
        "Towing",
        "Tracking",
        "Trip Authorities",
        "Trouble Shooting",
        "User Administration",
        "Validation",
        "Vehicle Inception Authorizer",
        "Vehicle Inception Capturer",
        "Vehicle List for All Departments in Province",
        "Vehicle List for All Sites in Department",
        "Vehicle Master",
        "TSS",
        "Workshop",
        "Book Recurring Taxi",
        "Book Recuring Taxi",
        "Driver and Authoriser Management",
    ];

    private static readonly string[] AccessLevelCatalogTables =
    [
        "[dbo].[AccessLevels_2]",
        "[dbo].[AccessLevels]"
    ];

    private readonly FisDbContext _context;
    private readonly ILogger<LegacyRoleCompatibilityService> _logger;

    public LegacyRoleCompatibilityService(
        FisDbContext context,
        ILogger<LegacyRoleCompatibilityService> logger
    )
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> ResolveRolesAsync(
        string? username,
        string? accessString,
        long accessLevel,
        CancellationToken cancellationToken = default
    )
    {
        var membershipRoles = await GetMembershipRolesAsync(username, cancellationToken);
        var catalogRoles = await GetAccessCatalogRolesAsync(
            accessString,
            accessLevel,
            cancellationToken
        );

        var roles = membershipRoles
            .Concat(catalogRoles)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (
            roles.Any(role =>
                SystemAdministratorRoles.Contains(role, StringComparer.OrdinalIgnoreCase)
            )
        )
        {
            roles = roles
                .Concat(LegacyModuleRoles)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return roles;
    }

    public async Task<IReadOnlyList<string>> GetMembershipRolesAsync(
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
            return await ReadMembershipRolesAsync(
                connection,
                username.Trim().ToLowerInvariant(),
                cancellationToken
            );
        }
        catch (SqlException ex) when (IsMissingSchemaObject(ex))
        {
            // FIS shipped with both the standard ASP.NET membership schema
            // (`UserId`) and the earlier compatibility schema where the join
            // key was named `user_access_code`. Keep both layouts read-only so
            // role claims survive a restore from either generation.
            try
            {
                return await ReadLegacyMembershipRolesAsync(
                    connection,
                    username.Trim().ToLowerInvariant(),
                    cancellationToken
                );
            }
            catch (SqlException legacyException) when (IsMissingSchemaObject(legacyException))
            {
                _logger.LogInformation(
                    "Legacy ASP.NET role tables are unavailable; resolving authorization from the legacy access catalog"
                );
                return [];
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<IReadOnlyList<string>> ReadMembershipRolesAsync(
        System.Data.Common.DbConnection connection,
        string normalizedUsername,
        CancellationToken cancellationToken
    )
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
        AddUsernameParameter(command, normalizedUsername);
        return await ReadRoleNamesAsync(command, cancellationToken);
    }

    private static async Task<IReadOnlyList<string>> ReadLegacyMembershipRolesAsync(
        System.Data.Common.DbConnection connection,
        string normalizedUsername,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT r.[RoleName]
            FROM [dbo].[aspnet_Users] AS u
            INNER JOIN [dbo].[aspnet_UsersInRoles] AS ur
                ON ur.[user_access_code] = u.[user_access_code]
            INNER JOIN [dbo].[aspnet_Roles] AS r
                ON r.[RoleId] = ur.[RoleId]
               AND r.[ApplicationId] = u.[ApplicationId]
            WHERE LOWER(u.[UserName]) = @username
            """;
        AddUsernameParameter(command, normalizedUsername);
        return await ReadRoleNamesAsync(command, cancellationToken);
    }

    private static void AddUsernameParameter(
        System.Data.Common.DbCommand command,
        string normalizedUsername
    )
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@username";
        parameter.DbType = DbType.String;
        parameter.Value = normalizedUsername;
        command.Parameters.Add(parameter);
    }

    private static async Task<IReadOnlyList<string>> ReadRoleNamesAsync(
        System.Data.Common.DbCommand command,
        CancellationToken cancellationToken
    )
    {
        var roles = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                var role = reader.GetString(0).Trim();
                if (role.Length > 0)
                {
                    roles.Add(role);
                }
            }
        }

        return roles;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification =
            "The catalog identifier is selected only from the fixed allow-list above; every user value is passed as a DbParameter."
    )]
    private async Task<IReadOnlyList<string>> GetAccessCatalogRolesAsync(
        string? accessString,
        long accessLevel,
        CancellationToken cancellationToken
    )
    {
        var parsedIds = ParseAccessIds(accessString);
        var hasExplicitAccessIds = !string.IsNullOrWhiteSpace(accessString);
        if (hasExplicitAccessIds && parsedIds.Count == 0)
        {
            _logger.LogWarning(
                "Legacy Access_str was present but contained no numeric access-level IDs; resolving any independent AccessLevel catalog entries without trusting the malformed value"
            );
        }

        if (!hasExplicitAccessIds && accessLevel <= 0)
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
            var roles = new List<string>();
            if (parsedIds.Count > 0)
            {
                var procedureRoles = await TryGetProcedureRolesAsync(
                    connection,
                    parsedIds,
                    cancellationToken
                );
                if (procedureRoles is not null)
                {
                    roles.AddRange(procedureRoles);
                }
                else
                {
                    foreach (var table in AccessLevelCatalogTables)
                    {
                        try
                        {
                            roles.AddRange(
                                await ReadAccessCatalogRolesAsync(
                                    connection,
                                    table,
                                    parsedIds,
                                    accessLevel: null,
                                    cancellationToken: cancellationToken
                                )
                            );
                            break;
                        }
                        catch (SqlException ex) when (IsMissingSchemaObject(ex))
                        {
                            _logger.LogInformation(
                                "Legacy access catalog {AccessCatalog} is unavailable; trying the compatibility catalog",
                                table
                            );
                        }
                    }
                }
            }

            // The archived transfer function builds Access_str from the
            // AccessLevel bitmask and then appends any explicit Access_str
            // entries. Resolve both sources: older restores may populate only
            // one of them, and combining them preserves the legacy union
            // without inventing a modern role map.
            if (accessLevel > 0)
            {
                foreach (var table in AccessLevelCatalogTables)
                {
                    try
                    {
                        roles.AddRange(
                            await ReadAccessCatalogRolesAsync(
                                connection,
                                table,
                                accessIds: [],
                                accessLevel: accessLevel,
                                cancellationToken: cancellationToken
                            )
                        );
                        break;
                    }
                    catch (SqlException ex) when (IsMissingSchemaObject(ex))
                    {
                        _logger.LogInformation(
                            "Legacy access catalog {AccessCatalog} is unavailable; trying the compatibility catalog",
                            table
                        );
                    }
                }
            }

            if (roles.Count == 0)
            {
                _logger.LogWarning(
                    "No legacy access catalog was available or matched the user's legacy access values; no catalog-derived module roles were granted"
                );
            }

            return roles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
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
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification =
            "The catalog identifier is selected only from the fixed allow-list above; every user value is passed as a DbParameter."
    )]
    private static async Task<IReadOnlyList<string>> ReadAccessCatalogRolesAsync(
        System.Data.Common.DbConnection connection,
        string table,
        IReadOnlyList<short> accessIds,
        long? accessLevel,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        var parameters = new List<string>();
        for (var index = 0; index < accessIds.Count; index++)
        {
            var name = $"@accessId{index}";
            parameters.Add(name);
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.DbType = DbType.Int16;
            parameter.Value = accessIds[index];
            command.Parameters.Add(parameter);
        }

        string catalogPredicate;
        if (accessIds.Count > 0)
        {
            catalogPredicate = $"[AccessLevelID] IN ({string.Join(", ", parameters)})";
        }
        else
        {
            var accessLevelParameter = command.CreateParameter();
            accessLevelParameter.ParameterName = "@accessLevel";
            accessLevelParameter.DbType = DbType.Int64;
            accessLevelParameter.Value = accessLevel ?? 0L;
            command.Parameters.Add(accessLevelParameter);
            catalogPredicate =
                "@accessLevel <> 0 AND [AccessLevelValue] > 0 AND ([AccessLevelValue] & @accessLevel) = [AccessLevelValue]";
        }

        command.CommandText = $"""
            SELECT [AccessLevelName]
            FROM {table}
            WHERE {catalogPredicate}
            ORDER BY [AccessLevelID]
            """;

        var roles = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                var role = reader.GetString(0).Trim();
                if (role.Length > 0)
                {
                    roles.Add(role);
                }
            }
        }

        return roles;
    }

    private async Task<IReadOnlyList<string>?> TryGetProcedureRolesAsync(
        System.Data.Common.DbConnection connection,
        IReadOnlyList<short> accessIds,
        CancellationToken cancellationToken
    )
    {
        var roles = new List<string>();
        foreach (var accessId in accessIds)
        {
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "[dbo].[DEV_SEL_LookupOldAccessLevelName]";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@AccessID";
                parameter.DbType = DbType.Int16;
                parameter.Value = accessId;
                command.Parameters.Add(parameter);

                var value = await command.ExecuteScalarAsync(cancellationToken);
                if (value is not null and not DBNull)
                {
                    var role = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
                    if (!string.IsNullOrWhiteSpace(role))
                    {
                        roles.Add(role);
                    }
                }
            }
            catch (SqlException ex) when (IsMissingSchemaObject(ex))
            {
                _logger.LogInformation(
                    "Legacy access-level lookup procedure is unavailable; trying the catalog table compatibility path"
                );
                return null;
            }
        }

        return roles;
    }

    private static List<short> ParseAccessIds(string? accessString)
    {
        if (string.IsNullOrWhiteSpace(accessString))
        {
            return [];
        }

        return accessString
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(value =>
                short.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                    ? (short?)id
                    : null
            )
            .Where(id => id is > 0)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
    }

    private static bool IsMissingSchemaObject(SqlException exception) =>
        exception.Number is 207 or 208 or 2812;
}
