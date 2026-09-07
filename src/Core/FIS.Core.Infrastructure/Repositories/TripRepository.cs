using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Reads and writes trip authorities against both the original client table
/// and databases containing the later audit columns. The legacy trip fields
/// remain the source of truth; optional columns are selected only when the
/// connected database contains them.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers come only from fixed compatibility allowlists; submitted values are parameters.")]
public sealed class TripRepository : ITripRepository
{
    private const string TableName = "trip_authorities";
    private const string ContractTableName = "contract";
    private const string VehicleTableName = "vehicle_master";
    private const string ModelTableName = "model";
    private const string MakeTableName = "make";

    private static readonly string[] RequiredColumns =
    [
        "trip_authority_code", "contract_code", "approver_name", "approver_rank", "approver_tel",
        "end_odo_meter", "expiry_date", "trip_reason", "trip_request_number", "issue_date",
        "trip_type_code", "trip_incident_type_code", "user_access_code", "locked_for_transfer",
        "Trip_Is_Monthly"
    ];

    private static readonly string[] OptionalColumns =
    [
        "date_created", "date_updated", "created_by_user_code", "modified_by_user_code", "is_deleted"
    ];

    private static readonly string[] RequiredContractColumns =
    ["contract_code", "vmf_code", "site_code"];

    private readonly FisDbContext _context;

    public TripRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Trip?> GetByIdAsync(int tripId)
        => (await QueryAsync(
            "[t].[trip_authority_code] = @tripId",
            command => AddParameter(command, "@tripId", DbType.Int32, tripId),
            includeDeleted: false,
            take: 1)).SingleOrDefault();

    public async Task<IEnumerable<Trip>> GetAllAsync()
        => await QueryAsync(orderBy: "[t].[issue_date] DESC, [t].[trip_authority_code] DESC");

    public async Task<IEnumerable<TripAuthorityVehicle>> GetTripAuthorityVehiclesAsync()
    {
        var contractColumns = await GetTableColumnsAsync(ContractTableName);
        var vehicleColumns = await GetTableColumnsAsync(VehicleTableName);
        var requiredContractColumns = new[] { "contract_code", "vmf_code", "site_code", "still_current", "contract_type" };
        var requiredVehicleColumns = new[] { "vmf_code", "vehicle_status_code", "fleet_number", "registration_number", "licence_due_date" };

        var missingColumns = requiredContractColumns
            .Where(column => !contractColumns.Contains(column))
            .Concat(requiredVehicleColumns.Where(column => !vehicleColumns.Contains(column)))
            .ToArray();
        if (missingColumns.Length > 0)
        {
            throw new InvalidOperationException(
                $"The required Trip Authority vehicle compatibility columns are not available: {string.Join(", ", missingColumns)}");
        }

        var modelColumns = await GetTableColumnsAsync(ModelTableName);
        var makeColumns = await GetTableColumnsAsync(MakeTableName);
        var hasModelProjection = new[] { "model_code", "make_code", "model_description" }.All(modelColumns.Contains);
        var hasMakeProjection = new[] { "make_code", "make_description" }.All(makeColumns.Contains);

        var projection = new List<string>
        {
            "[c].[vmf_code] AS [vmf_code]",
            "[c].[contract_code] AS [contract_code]",
            "[c].[site_code] AS [site_code]",
            "[v].[fleet_number] AS [fleet_number]",
            "[v].[registration_number] AS [registration_number]",
            "[v].[licence_due_date] AS [licence_due_date]",
            "[c].[contract_type] AS [contract_type]",
            hasModelProjection
                ? "[m].[model_description] AS [model_description]"
                : "CAST(NULL AS varchar(250)) AS [model_description]",
            hasMakeProjection && hasModelProjection
                ? "[mk].[make_description] AS [make_description]"
                : "CAST(NULL AS varchar(250)) AS [make_description]"
        };

        var joins = new List<string>();
        if (hasModelProjection)
        {
            joins.Add($"LEFT JOIN [dbo].[{ModelTableName}] AS [m] ON [m].[model_code] = [v].[model_code]");
        }

        if (hasMakeProjection && hasModelProjection)
        {
            joins.Add($"LEFT JOIN [dbo].[{MakeTableName}] AS [mk] ON [mk].[make_code] = [m].[make_code]");
        }

        var conditions = new List<string>
        {
            "[c].[still_current] = 'Y'",
            "[v].[vehicle_status_code] > 0"
        };
        if (contractColumns.Contains("is_deleted"))
        {
            conditions.Add("ISNULL([c].[is_deleted], 0) = 0");
        }

        if (vehicleColumns.Contains("is_deleted"))
        {
            conditions.Add("ISNULL([v].[is_deleted], 0) = 0");
        }

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"""
                SELECT {string.Join(", ", projection)}
                FROM [dbo].[{ContractTableName}] AS [c]
                INNER JOIN [dbo].[{VehicleTableName}] AS [v] ON [v].[vmf_code] = [c].[vmf_code]
                {string.Join(Environment.NewLine, joins)}
                WHERE {string.Join(" AND ", conditions)}
                ORDER BY [v].[fleet_number], [c].[contract_code]
                """;

            var results = new List<TripAuthorityVehicle>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new TripAuthorityVehicle(
                    ReadInt32(reader, "vmf_code") ?? 0,
                    ReadInt32(reader, "contract_code") ?? 0,
                    ReadInt16(reader, "site_code") ?? 0,
                    ReadString(reader, "fleet_number"),
                    ReadString(reader, "registration_number"),
                    ReadDateTime(reader, "licence_due_date"),
                    ReadString(reader, "make_description"),
                    ReadString(reader, "model_description"),
                    ReadString(reader, "contract_type")));
            }

            return results;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<IEnumerable<Trip>> GetTripsByContractAsync(int contractCode)
        => await QueryAsync(
            "[t].[contract_code] = @contractCode",
            command => AddParameter(command, "@contractCode", DbType.Int32, contractCode));

    public async Task<IEnumerable<Trip>> GetTripsByVehicleAsync(int vmfCode)
    {
        var contractColumns = await GetTableColumnsAsync(ContractTableName);
        if (!RequiredContractColumns.All(contractColumns.Contains))
        {
            return [];
        }

        return await QueryAsync(
            "[c].[vmf_code] = @vmfCode",
            command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode),
            contractJoin: true);
    }

    public async Task<IEnumerable<Trip>> GetTripsByDriverAsync(string driverId)
    {
        if (string.IsNullOrWhiteSpace(driverId))
        {
            return [];
        }

        var contractColumns = await GetTableColumnsAsync(ContractTableName);
        if (!RequiredContractColumns.All(contractColumns.Contains))
        {
            return [];
        }

        var driverColumn = contractColumns.Contains("site_driver_code")
            ? "[c].[site_driver_code] = @driverId"
            : contractColumns.Contains("Driver_id")
                ? "[c].[Driver_id] = @driverId"
                : null;
        if (driverColumn is null)
        {
            return [];
        }

        return await QueryAsync(
            driverColumn,
            command => AddParameter(command, "@driverId", DbType.String, driverId.Trim()),
            contractJoin: true);
    }

    public async Task<IEnumerable<Trip>> GetTripsByDateRangeAsync(DateTime startDate, DateTime endDate)
        => await QueryAsync(
            "[t].[issue_date] >= @startDate AND [t].[issue_date] <= @endDate",
            command =>
            {
                AddParameter(command, "@startDate", DbType.DateTime, startDate);
                AddParameter(command, "@endDate", DbType.DateTime, endDate);
            });

    public async Task<Trip> CreateAsync(Trip trip, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(trip);

        var availableColumns = await GetAvailableColumnsAsync();
        var now = DateTime.UtcNow;
        trip.date_created = now;
        trip.date_updated = now;
        trip.created_by_user_code = currentUserId > 0 ? currentUserId : null;
        trip.modified_by_user_code = currentUserId > 0 ? currentUserId : null;
        trip.is_deleted = false;

        var values = BuildValues(trip, availableColumns, includeKey: false, includeCreateAudit: true);
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"""
                INSERT INTO [dbo].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))})
                OUTPUT INSERTED.[trip_authority_code]
                VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
                """;
            AddParameters(command, values);
            trip.trip_authority_code = Convert.ToInt32(await command.ExecuteScalarAsync());
            return trip;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task UpdateAsync(Trip trip, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(trip);

        var existing = await GetByIdAsync(trip.trip_authority_code)
            ?? throw new InvalidOperationException($"Trip with trip_authority_code {trip.trip_authority_code} not found");
        var availableColumns = await GetAvailableColumnsAsync();
        var now = DateTime.UtcNow;
        trip.date_created = existing.date_created;
        trip.created_by_user_code = existing.created_by_user_code;
        trip.date_updated = now;
        trip.modified_by_user_code = currentUserId > 0 ? currentUserId : existing.modified_by_user_code;
        trip.is_deleted = existing.is_deleted;

        var values = BuildValues(trip, availableColumns, includeKey: false, includeCreateAudit: false);
        if (values.Count == 0)
        {
            return;
        }

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"""
                UPDATE [dbo].[{TableName}]
                SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))}
                WHERE [trip_authority_code] = @tripId
                """;
            AddParameters(command, values);
            AddParameter(command, "@tripId", DbType.Int32, trip.trip_authority_code);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task DeleteAsync(int tripId, int currentUserId)
    {
        var availableColumns = await GetAvailableColumnsAsync();
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            if (availableColumns.Contains("is_deleted"))
            {
                var updates = new List<string> { "[is_deleted] = @isDeleted" };
                AddParameter(command, "@isDeleted", DbType.Boolean, true);
                if (availableColumns.Contains("date_updated"))
                {
                    updates.Add("[date_updated] = @dateUpdated");
                    AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
                }

                if (availableColumns.Contains("modified_by_user_code"))
                {
                    updates.Add("[modified_by_user_code] = @modifiedByUserCode");
                    AddParameter(command, "@modifiedByUserCode", DbType.Int32, currentUserId > 0 ? currentUserId : null);
                }

                command.CommandText = $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", updates)} WHERE [trip_authority_code] = @tripId";
            }
            else
            {
                command.CommandText = $"DELETE FROM [dbo].[{TableName}] WHERE [trip_authority_code] = @tripId";
            }

            AddParameter(command, "@tripId", DbType.Int32, tripId);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<List<Trip>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null,
        bool includeDeleted = false,
        bool contractJoin = false,
        int? take = null,
        string orderBy = "[t].[issue_date] DESC, [t].[trip_authority_code] DESC")
    {
        var availableColumns = await GetAvailableColumnsAsync();
        var hasContractProjection = contractJoin || await HasContractProjectionAsync();
        var projection = RequiredColumns
            .Concat(OptionalColumns)
            .Select(column => GetColumnProjection("t", column, availableColumns))
            .ToList();

        if (hasContractProjection)
        {
            projection.Add("[c].[vmf_code] AS [contract_vmf_code]");
            projection.Add("[c].[site_code] AS [contract_site_code]");
        }
        else
        {
            projection.Add("CAST(NULL AS int) AS [contract_vmf_code]");
            projection.Add("CAST(NULL AS smallint) AS [contract_site_code]");
        }

        var conditions = new List<string>();
        if (!string.IsNullOrWhiteSpace(predicate))
        {
            conditions.Add(predicate);
        }

        if (!includeDeleted && availableColumns.Contains("is_deleted"))
        {
            conditions.Add("([t].[is_deleted] = 0 OR [t].[is_deleted] IS NULL)");
        }

        var whereClause = conditions.Count == 0 ? string.Empty : $"WHERE {string.Join(" AND ", conditions)}";
        var contractClause = hasContractProjection
            ? $"LEFT JOIN [dbo].[{ContractTableName}] AS [c] ON [c].[contract_code] = [t].[contract_code]"
            : string.Empty;
        var topClause = take.HasValue ? $"TOP ({take.Value}) " : string.Empty;

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"""
                SELECT {topClause}{string.Join(", ", projection)}
                FROM [dbo].[{TableName}] AS [t]
                {contractClause}
                {whereClause}
                ORDER BY {orderBy}
                """;
            configure?.Invoke(command);

            var results = new List<Trip>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapTrip(reader, hasContractProjection));
            }

            return results;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<bool> HasContractProjectionAsync()
    {
        var columns = await GetTableColumnsAsync(ContractTableName);
        return RequiredContractColumns.All(columns.Contains);
    }

    private async Task<HashSet<string>> GetAvailableColumnsAsync()
    {
        var columns = await GetTableColumnsAsync(TableName);
        var missingColumns = RequiredColumns.Where(column => !columns.Contains(column)).ToArray();
        if (missingColumns.Length > 0)
        {
            throw new InvalidOperationException(
                $"The required trip_authorities compatibility columns are not available: {string.Join(", ", missingColumns)}");
        }

        return columns;
    }

    private async Task<HashSet<string>> GetTableColumnsAsync(string tableName)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT [COLUMN_NAME]
                FROM [INFORMATION_SCHEMA].[COLUMNS]
                WHERE [TABLE_SCHEMA] = @schema
                  AND [TABLE_NAME] = @table
                """;
            AddParameter(command, "@schema", DbType.String, "dbo");
            AddParameter(command, "@table", DbType.String, tableName);

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(0));
            }

            return columns;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static Trip MapTrip(DbDataReader reader, bool hasContractProjection)
    {
        var trip = new Trip
        {
            trip_authority_code = ReadInt32(reader, "trip_authority_code") ?? 0,
            contract_code = ReadInt32(reader, "contract_code") ?? 0,
            approver_name = ReadString(reader, "approver_name"),
            approver_rank = ReadString(reader, "approver_rank"),
            approver_tel = ReadString(reader, "approver_tel"),
            end_odo_meter = ReadInt32(reader, "end_odo_meter"),
            expiry_date = ReadDateTime(reader, "expiry_date"),
            trip_reason = ReadString(reader, "trip_reason"),
            trip_request_number = ReadString(reader, "trip_request_number"),
            issue_date = ReadDateTime(reader, "issue_date") ?? DateTime.MinValue,
            trip_type_code = ReadInt16(reader, "trip_type_code") ?? 0,
            trip_incident_type_code = ReadInt16(reader, "trip_incident_type_code") ?? 0,
            user_access_code = ReadInt16(reader, "user_access_code"),
            locked_for_transfer = ReadBoolean(reader, "locked_for_transfer"),
            Trip_Is_Monthly = ReadBoolean(reader, "Trip_Is_Monthly"),
            date_created = ReadDateTime(reader, "date_created") ?? DateTime.MinValue,
            date_updated = ReadDateTime(reader, "date_updated"),
            created_by_user_code = ReadInt32(reader, "created_by_user_code"),
            modified_by_user_code = ReadInt32(reader, "modified_by_user_code"),
            is_deleted = ReadBoolean(reader, "is_deleted")
        };

        if (hasContractProjection)
        {
            var vmfCode = ReadInt32(reader, "contract_vmf_code");
            var siteCode = ReadInt16(reader, "contract_site_code");
            if (vmfCode.HasValue || siteCode.HasValue)
            {
                trip.Contract = new Contract
                {
                    contract_code = trip.contract_code,
                    vmf_code = vmfCode ?? 0,
                    site_code = siteCode ?? 0
                };
            }
        }

        return trip;
    }

    private static List<WriteValue> BuildValues(
        Trip trip,
        IReadOnlySet<string> availableColumns,
        bool includeKey,
        bool includeCreateAudit)
    {
        var values = new List<WriteValue>();
        AddValue(values, availableColumns, "trip_authority_code", "@tripAuthorityCode", DbType.Int32, trip.trip_authority_code, includeKey);
        AddValue(values, availableColumns, "contract_code", "@contractCode", DbType.Int32, trip.contract_code);
        AddValue(values, availableColumns, "approver_name", "@approverName", DbType.String, trip.approver_name);
        AddValue(values, availableColumns, "approver_rank", "@approverRank", DbType.String, trip.approver_rank);
        AddValue(values, availableColumns, "approver_tel", "@approverTel", DbType.String, trip.approver_tel);
        AddValue(values, availableColumns, "end_odo_meter", "@endOdoMeter", DbType.Int32, trip.end_odo_meter);
        AddValue(values, availableColumns, "expiry_date", "@expiryDate", DbType.DateTime, trip.expiry_date);
        AddValue(values, availableColumns, "trip_reason", "@tripReason", DbType.String, trip.trip_reason);
        AddValue(values, availableColumns, "trip_request_number", "@tripRequestNumber", DbType.String, trip.trip_request_number);
        AddValue(values, availableColumns, "issue_date", "@issueDate", DbType.DateTime, trip.issue_date);
        AddValue(values, availableColumns, "trip_type_code", "@tripTypeCode", DbType.Int16, trip.trip_type_code);
        AddValue(values, availableColumns, "trip_incident_type_code", "@tripIncidentTypeCode", DbType.Int16, trip.trip_incident_type_code);
        AddValue(values, availableColumns, "user_access_code", "@userAccessCode", DbType.Int16, trip.user_access_code);
        AddValue(values, availableColumns, "locked_for_transfer", "@lockedForTransfer", DbType.Boolean, trip.locked_for_transfer);
        AddValue(values, availableColumns, "Trip_Is_Monthly", "@tripIsMonthly", DbType.Boolean, trip.Trip_Is_Monthly);

        if (includeCreateAudit)
        {
            AddValue(values, availableColumns, "date_created", "@dateCreated", DbType.DateTime2, trip.date_created);
            AddValue(values, availableColumns, "created_by_user_code", "@createdByUser", DbType.Int32, trip.created_by_user_code);
        }

        AddValue(values, availableColumns, "date_updated", "@dateUpdated", DbType.DateTime2, trip.date_updated);
        AddValue(values, availableColumns, "modified_by_user_code", "@modifiedByUserCode", DbType.Int32, trip.modified_by_user_code);
        AddValue(values, availableColumns, "is_deleted", "@isDeleted", DbType.Boolean, trip.is_deleted);
        return values;
    }

    private static string GetColumnProjection(string alias, string column, IReadOnlySet<string> availableColumns)
        => availableColumns.Contains(column)
            ? $"[{alias}].[{column}] AS [{column}]"
            : $"CAST(NULL AS sql_variant) AS [{column}]";

    private static void AddValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> availableColumns,
        string column,
        string parameter,
        DbType dbType,
        object? value,
        bool include = true)
    {
        if (include && availableColumns.Contains(column))
        {
            values.Add(new WriteValue(column, parameter, dbType, value));
        }
    }

    private static void AddParameters(DbCommand command, IEnumerable<WriteValue> values)
    {
        foreach (var value in values)
        {
            AddParameter(command, value.Parameter, value.DbType, value.Value);
        }
    }

    private static void AddParameter(DbCommand command, string name, DbType dbType, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal));
    }

    private static int? ReadInt32(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static short? ReadInt16(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt16(reader.GetValue(ordinal));
    }

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }

    private static bool ReadBoolean(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return !reader.IsDBNull(ordinal) && Convert.ToBoolean(reader.GetValue(ordinal));
    }

    private readonly record struct WriteValue(string Column, string Parameter, DbType DbType, object? Value);
}
