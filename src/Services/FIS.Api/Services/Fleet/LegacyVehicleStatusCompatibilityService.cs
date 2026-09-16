using System.Data;
using System.Data.Common;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Services.Fleet;

/// <summary>
/// Executes the legacy vehicle-status mutation. The archived procedure owns
/// vehicle_master, vehicle_history, vehicle_status_history, sold-field, and
/// transaction behavior; this service never substitutes EF writes when a
/// deployed procedure has a different contract.
/// </summary>
public sealed class LegacyVehicleStatusCompatibilityService
{
    private const string ProcedureName = "DEV_UPD_VehicleStatusHistory";
    private const string SuccessorsProcedureName = "DEV_SEL_VehicleStatus_Successors";

    private static readonly string[] SuccessorsParameters = ["@vehicle_status_code"];

    private static readonly string[] RequiredParametersWithoutOdometer =
    [
        "@vmf_code",
        "@vehicle_status_history_code",
        "@vehicle_status_code",
        "@status_start_date",
        "@Comments",
        "@user_access_code",
    ];

    private static readonly string[] SoldParametersWithoutOdometer =
    [
        "@vmf_code",
        "@vehicle_status_history_code",
        "@vehicle_status_code",
        "@status_start_date",
        "@Comments",
        "@user_access_code",
        "@sold_to",
        "@sold_date",
        "@sold_amount",
    ];

    // Some later compatibility databases added an explicit end-odometer
    // parameter. The archived client procedures in v2.1.01/v2.1.08 do not;
    // accept both source-backed generations and only send the extra value when
    // the deployed procedure actually exposes it.
    private static readonly string[] RequiredParametersWithOdometer =
    [
        .. RequiredParametersWithoutOdometer,
        "@end_Odo",
    ];

    private static readonly string[] SoldParametersWithOdometer =
    [
        .. SoldParametersWithoutOdometer,
        "@end_Odo",
    ];

    private readonly FisDbContext _context;

    public LegacyVehicleStatusCompatibilityService(FisDbContext context) => _context = context;

    /// <summary>
    /// Reads the legacy status transition graph. The archived maintenance page
    /// calls DEV_SEL_VehicleStatus_Successors and then enables each returned
    /// row only when the signed-in user has one of that row's required roles.
    /// Keep that source of truth on the server so a caller cannot post an
    /// arbitrary status code around the disabled radio buttons.
    /// </summary>
    public async Task<IReadOnlyList<LegacyVehicleStatusTransition>?> GetSuccessorsAsync(
        short currentStatusCode,
        IReadOnlySet<string> userRoles,
        CancellationToken cancellationToken = default,
        bool allowAllRoles = false
    )
    {
        ArgumentNullException.ThrowIfNull(userRoles);

        await using var scope = await OpenConnectionAsync(cancellationToken);
        var connection = scope.Connection;
        var parameters = await GetNamedProcedureParametersAsync(
            connection,
            SuccessorsProcedureName,
            cancellationToken
        );
        if (parameters is null)
            return null;

        if (!parameters.SequenceEqual(SuccessorsParameters, StringComparer.OrdinalIgnoreCase))
        {
            throw new LegacyVehicleStatusProcedureContractException(
                $"The deployed legacy procedure {SuccessorsProcedureName} does not match the archived parameter contract."
            );
        }

        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = $"[dbo].[{SuccessorsProcedureName}]";
        command.CommandTimeout = 60;
        AddParameter(command, "@vehicle_status_code", DbType.Int16, currentStatusCode);

        var transitions = new List<LegacyVehicleStatusTransition>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var statusCodeOrdinal = FindOrdinal(reader, "vehicle_status_code");
        var descriptionOrdinal = FindOrdinal(reader, "status_description");
        var rolesOrdinal = FindOrdinal(reader, "user_roles");
        if (statusCodeOrdinal is null || descriptionOrdinal is null || rolesOrdinal is null)
        {
            throw new LegacyVehicleStatusProcedureContractException(
                $"The deployed legacy procedure {SuccessorsProcedureName} returned an unexpected result shape."
            );
        }

        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.IsDBNull(statusCodeOrdinal.Value))
                continue;

            var requiredRoles = reader.IsDBNull(rolesOrdinal.Value)
                ? Array.Empty<string>()
                : (Convert.ToString(reader.GetValue(rolesOrdinal.Value)) ?? string.Empty)
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (!allowAllRoles && !requiredRoles.Any(role => userRoles.Contains(role)))
                continue;

            var description = reader.IsDBNull(descriptionOrdinal.Value)
                ? string.Empty
                : Convert.ToString(reader.GetValue(descriptionOrdinal.Value))?.Trim() ?? string.Empty;
            transitions.Add(
                new LegacyVehicleStatusTransition(
                    Convert.ToInt16(reader.GetValue(statusCodeOrdinal.Value)),
                    description,
                    requiredRoles
                )
            );
        }

        return transitions
            .GroupBy(transition => transition.StatusCode)
            .Select(group => group.First())
            .OrderBy(transition => transition.Description, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<bool?> TryUpdateAsync(
        LegacyVehicleStatusRequest request,
        CancellationToken cancellationToken = default
    )
    {
        await using var scope = await OpenConnectionAsync(cancellationToken);
        var connection = scope.Connection;
        var parameters = await GetNamedProcedureParametersAsync(
            connection,
            ProcedureName,
            cancellationToken
        );
        if (parameters is null)
            return null;

        var usesSoldContract = parameters.SequenceEqual(
                SoldParametersWithoutOdometer,
                StringComparer.OrdinalIgnoreCase
            )
            || parameters.SequenceEqual(
                SoldParametersWithOdometer,
                StringComparer.OrdinalIgnoreCase
            );
        var usesOdometerParameter = parameters.SequenceEqual(
                RequiredParametersWithOdometer,
                StringComparer.OrdinalIgnoreCase
            )
            || parameters.SequenceEqual(
                SoldParametersWithOdometer,
                StringComparer.OrdinalIgnoreCase
            );
        var matchesArchivedContract = parameters.SequenceEqual(
                RequiredParametersWithoutOdometer,
                StringComparer.OrdinalIgnoreCase
            )
            || parameters.SequenceEqual(
                RequiredParametersWithOdometer,
                StringComparer.OrdinalIgnoreCase
            )
            || parameters.SequenceEqual(
                SoldParametersWithoutOdometer,
                StringComparer.OrdinalIgnoreCase
            )
            || parameters.SequenceEqual(
                SoldParametersWithOdometer,
                StringComparer.OrdinalIgnoreCase
            );
        if (!matchesArchivedContract)
        {
            throw new LegacyVehicleStatusProcedureContractException(
                $"The deployed legacy procedure {ProcedureName} does not match an archived parameter contract."
            );
        }
        if (request.StatusCode == 5 && !usesSoldContract)
        {
            throw new LegacyVehicleStatusProcedureContractException(
                $"The deployed legacy procedure {ProcedureName} cannot persist the required sold fields."
            );
        }

        // The later archived procedure resolves a null history code by
        // matching the vehicle's current status and status date. Mirror that
        // lookup for callers that do not post the hidden history key, rather
        // than selecting an arbitrary latest history row for the vehicle.
        var historyCode = request.HistoryCode ?? await ReadCurrentHistoryCodeAsync(
            connection,
            request.VmfCode,
            cancellationToken
        );

        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = $"[dbo].[{ProcedureName}]";
        command.CommandTimeout = 180;
        AddParameter(command, "@vmf_code", DbType.Int32, request.VmfCode);
        AddParameter(command, "@vehicle_status_history_code", DbType.Int32, historyCode);
        AddParameter(command, "@vehicle_status_code", DbType.Int16, request.StatusCode);
        AddParameter(command, "@status_start_date", DbType.Date, request.StatusStartDate.Date);
        AddParameter(command, "@Comments", DbType.AnsiString, request.Comments, size: 500);
        AddParameter(command, "@user_access_code", DbType.Int32, request.UserAccessCode);
        if (usesSoldContract)
        {
            AddParameter(command, "@sold_to", DbType.AnsiString, request.SoldTo, size: 60);
            AddParameter(command, "@sold_date", DbType.Date, request.SoldDate?.Date);
            AddParameter(command, "@sold_amount", DbType.Decimal, request.SoldAmount);
        }
        if (usesOdometerParameter)
        {
            AddParameter(command, "@end_Odo", DbType.Int32, request.EndOdometer);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }

    private static async Task<IReadOnlyList<string>?> GetNamedProcedureParametersAsync(
        DbConnection connection,
        string procedureName,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT [p].[name]
            FROM [sys].[procedures] AS [sp]
            INNER JOIN [sys].[schemas] AS [s] ON [s].[schema_id] = [sp].[schema_id]
            INNER JOIN [sys].[parameters] AS [p] ON [p].[object_id] = [sp].[object_id]
            WHERE [s].[name] = N'dbo'
              AND [sp].[name] = @procedureName
              AND [p].[parameter_id] > 0
            ORDER BY [p].[parameter_id]
            """;
        AddParameter(command, "@procedureName", DbType.String, procedureName);

        var names = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                names.Add(reader.GetString(0));
        }

        if (names.Count > 0)
            return names;

        await using var existsCommand = connection.CreateCommand();
        existsCommand.CommandText = "SELECT OBJECT_ID(@procedureName, 'P');";
        AddParameter(existsCommand, "@procedureName", DbType.String, $"dbo.{procedureName}");
        var objectId = await existsCommand.ExecuteScalarAsync(cancellationToken);
        return objectId is null or DBNull ? null : names;
    }

    private static int? FindOrdinal(DbDataReader reader, string columnName)
    {
        for (var index = 0; index < reader.FieldCount; index++)
        {
            if (string.Equals(reader.GetName(index), columnName, StringComparison.OrdinalIgnoreCase))
                return index;
        }

        return null;
    }

    private static async Task<int?> ReadCurrentHistoryCodeAsync(
        DbConnection connection,
        int vmfCode,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (1) [vehicle_status_history_code]
            FROM [dbo].[vehicle_status_history]
            WHERE [vmf_code] = @vmfCode
              AND [vehicle_status_code] = (
                  SELECT [vehicle_status_code]
                  FROM [dbo].[vehicle_master]
                  WHERE [vmf_code] = @vmfCode
              )
              AND CONVERT(date, [status_start_date]) = (
                  SELECT CONVERT(date, [vehicle_status_date])
                  FROM [dbo].[vehicle_master]
                  WHERE [vmf_code] = @vmfCode
              )
            ORDER BY [status_capture_date] DESC, [vehicle_status_history_code] DESC
        """;
        AddParameter(command, "@vmfCode", DbType.Int32, vmfCode);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : Convert.ToInt32(value);
    }

    private async Task<ConnectionScope> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync(cancellationToken);
        return new ConnectionScope(connection, shouldClose);
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        DbType type,
        object? value,
        int? size = null
    )
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        if (size.HasValue)
            parameter.Size = size.Value;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private sealed record ConnectionScope(DbConnection Connection, bool ShouldClose) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            if (ShouldClose)
                await Connection.CloseAsync();
        }
    }
}

public sealed record LegacyVehicleStatusRequest(
    int VmfCode,
    int? HistoryCode,
    short StatusCode,
    DateTime StatusStartDate,
    string? Comments,
    int UserAccessCode,
    int? EndOdometer,
    string? SoldTo,
    DateTime? SoldDate,
    decimal? SoldAmount
);

public sealed record LegacyVehicleStatusTransition(
    short StatusCode,
    string Description,
    IReadOnlyList<string> RequiredRoles
);

public sealed class LegacyVehicleStatusProcedureContractException : InvalidOperationException
{
    public LegacyVehicleStatusProcedureContractException(string message) : base(message) { }
}
