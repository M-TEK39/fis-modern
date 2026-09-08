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
/// Persists Call_centre business data against both the original client schema
/// and the expanded schema. The original table has no shared audit columns,
/// so EF cannot safely materialize or write it when those properties are part
/// of the model. Runtime column inspection keeps those fields optional without
/// changing any database object.
/// </summary>
public class CallCentreRepository : ICallCentreRepository
{
    private const string TableName = "Call_centre";

    private static readonly string[] LegacyColumns =
    [
        "Call_centre_code",
        "vmf_code",
        "Call_time",
        "Call_date",
        "Capture_name",
        "User_access_code",
        "Caller_name",
        "Driver_name",
        "Driver_persalno",
        "Driver_Licno",
        "GG_number",
        "Driver_base_station",
        "Driver_Site",
        "Driver_tel",
        "Driver_cell",
        "Driver_fax",
        "Driver_email",
        "Incident_type",
        "Incident_Desc",
        "Incident_date",
        "Incident_time",
        "Caller_tel",
        "TrOfficer_name",
        "TrOfficer_tel",
        "TrOfficer_Site",
        "Incident_town",
        "Incident_street",
        "Counter",
        "Caller_fax",
        "TrOfficer_fax",
        "Caller_email",
        "TrOfficer_email",
        "Inform_CRO",
        "CRO_Remarks",
        "Incident_Remarks",
        "Notify_list_code",
        "call_closed",
    ];

    private static readonly string[] OptionalColumns =
    [
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private static readonly string[] RequiredColumns = ["Call_centre_code", "vmf_code"];

    private readonly FisDbContext _context;

    public CallCentreRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<CallCentre?> GetByIdAsync(short callCentreCode)
    {
        return (
            await QueryAsync(
                "WHERE [Call_centre_code] = @callCentreCode",
                command => AddParameter(command, "@callCentreCode", DbType.Int16, callCentreCode)
            )
        ).SingleOrDefault();
    }

    public async Task<IEnumerable<CallCentre>> GetAllAsync()
    {
        return await QueryAsync();
    }

    public async Task<IEnumerable<CallCentre>> GetByVehicleAsync(int vmfCode)
    {
        return await QueryAsync(
            "WHERE [vmf_code] = @vmfCode",
            command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode)
        );
    }

    public async Task<IEnumerable<CallCentre>> GetByDateRangeAsync(
        DateTime startDate,
        DateTime endDate
    )
    {
        return await QueryAsync(
            "WHERE [Call_date] >= @startDate AND [Call_date] <= @endDate",
            command =>
            {
                AddParameter(command, "@startDate", DbType.Date, startDate.Date);
                AddParameter(command, "@endDate", DbType.Date, endDate.Date);
            }
        );
    }

    public async Task<CallCentre> CreateAsync(CallCentre callCentre, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(callCentre);

        var availableColumns = await GetAvailableColumnsAsync();
        var values = BuildLegacyWriteValues(callCentre)
            .Where(value => availableColumns.Contains(value.Column))
            .ToList();
        var now = DateTime.UtcNow;

        AddOptionalValue(
            values,
            availableColumns,
            "date_created",
            "@dateCreated",
            DbType.DateTime2,
            now
        );
        AddOptionalValue(
            values,
            availableColumns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );
        AddOptionalValue(
            values,
            availableColumns,
            "is_deleted",
            "@isDeleted",
            DbType.Boolean,
            false
        );

        var callCentreCode = await ExecuteInsertAsync(values);
        callCentre.Call_centre_code = callCentreCode;
        callCentre.date_created = now;
        callCentre.created_by_user_code = currentUserId > 0 ? currentUserId : null;
        callCentre.is_deleted = false;
        return callCentre;
    }

    public async Task<CallCentre> UpdateAsync(CallCentre callCentre, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(callCentre);

        var existing = await GetByIdAsync(callCentre.Call_centre_code);
        if (existing == null)
        {
            throw new InvalidOperationException(
                $"CallCentre with Call_centre_code {callCentre.Call_centre_code} not found"
            );
        }

        var availableColumns = await GetAvailableColumnsAsync();
        var values = BuildLegacyWriteValues(callCentre)
            .Where(value => availableColumns.Contains(value.Column))
            .ToList();
        var now = DateTime.UtcNow;

        AddOptionalValue(
            values,
            availableColumns,
            "date_updated",
            "@dateUpdated",
            DbType.DateTime2,
            now
        );
        AddOptionalValue(
            values,
            availableColumns,
            "modified_by_user_code",
            "@modifiedByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );
        AddOptionalValue(
            values,
            availableColumns,
            "is_deleted",
            "@isDeleted",
            DbType.Boolean,
            callCentre.is_deleted
        );

        await ExecuteUpdateAsync(callCentre.Call_centre_code, values, availableColumns);
        callCentre.date_created = existing.date_created;
        callCentre.created_by_user_code = existing.created_by_user_code;
        callCentre.date_updated = now;
        callCentre.modified_by_user_code = currentUserId > 0 ? currentUserId : null;
        return callCentre;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "DeleteAsync selects between fixed legacy SQL statements and uses a parameter for the record identifier."
    )]
    public async Task DeleteAsync(short callCentreCode, int currentUserId)
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
                var assignments = new List<string> { "[is_deleted] = 1" };
                if (availableColumns.Contains("date_updated"))
                {
                    assignments.Add("[date_updated] = @dateUpdated");
                    AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
                }

                if (availableColumns.Contains("modified_by_user_code"))
                {
                    assignments.Add("[modified_by_user_code] = @modifiedByUserCode");
                    AddParameter(
                        command,
                        "@modifiedByUserCode",
                        DbType.Int32,
                        currentUserId > 0 ? currentUserId : null
                    );
                }

                command.CommandText = $"""
                    UPDATE [dbo].[{TableName}]
                    SET {string.Join(", ", assignments)}
                    WHERE [Call_centre_code] = @callCentreCode
                    AND {GetActiveFilter(availableColumns)}
                    """;
            }
            else
            {
                command.CommandText = $"""
                    DELETE FROM [dbo].[{TableName}]
                    WHERE [Call_centre_code] = @callCentreCode
                    """;
            }

            AddParameter(command, "@callCentreCode", DbType.Int16, callCentreCode);
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

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SELECT list and filters are composed only from fixed legacy columns and allowlisted optional columns; values are parameters."
    )]
    private async Task<List<CallCentre>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null
    )
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
            var projection = LegacyColumns
                .Select(column => GetColumnProjection(availableColumns, column))
                .Concat(
                    OptionalColumns.Select(column =>
                        GetOptionalProjection(availableColumns, column)
                    )
                )
                .ToArray();
            var whereClause = string.IsNullOrWhiteSpace(predicate)
                ? $"WHERE {GetActiveFilter(availableColumns)}"
                : $"{predicate} AND {GetActiveFilter(availableColumns)}";

            command.CommandText = $"""
                SELECT {string.Join(", ", projection)}
                FROM [dbo].[{TableName}]
                {whereClause}
                ORDER BY [Call_date] DESC, [Call_centre_code] DESC
                """;
            configure?.Invoke(command);

            var results = new List<CallCentre>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapCallCentre(reader, availableColumns));
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

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The INSERT statement is composed only from fixed legacy columns and allowlisted optional values; every value is parameterized."
    )]
    private async Task<short> ExecuteInsertAsync(IReadOnlyList<WriteValue> values)
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
            command.CommandText = $"""
                INSERT INTO [dbo].[{TableName}] ({string.Join(
                    ", ",
                    values.Select(value => $"[{value.Column}]")
                )})
                OUTPUT INSERTED.[Call_centre_code]
                VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
                """;
            AddParameters(command, values);
            return Convert.ToInt16(await command.ExecuteScalarAsync());
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
        Justification = "The UPDATE statement is composed only from fixed legacy columns and allowlisted optional values; every value is parameterized."
    )]
    private async Task ExecuteUpdateAsync(
        short callCentreCode,
        IReadOnlyList<WriteValue> values,
        IReadOnlySet<string> availableColumns
    )
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
            command.CommandText = $"""
                UPDATE [dbo].[{TableName}]
                SET {string.Join(
                    ", ",
                    values.Select(value => $"[{value.Column}] = {value.Parameter}")
                )}
                WHERE [Call_centre_code] = @callCentreCode
                AND {GetActiveFilter(availableColumns)}
                """;
            AddParameters(command, values);
            AddParameter(command, "@callCentreCode", DbType.Int16, callCentreCode);
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

    private async Task<HashSet<string>> GetAvailableColumnsAsync()
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
            AddParameter(command, "@table", DbType.String, TableName);

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(0));
            }

            var missingColumns = RequiredColumns
                .Where(column => !columns.Contains(column))
                .ToArray();
            if (missingColumns.Length > 0)
            {
                throw new InvalidOperationException(
                    $"The required Call_centre compatibility columns are not available: {string.Join(", ", missingColumns)}"
                );
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

    private static CallCentre MapCallCentre(
        DbDataReader reader,
        IReadOnlySet<string> availableColumns
    )
    {
        var callDate = ReadDateTime(reader, "Call_date");
        var incidentDate = ReadDateTime(reader, "Incident_date");

        return new CallCentre
        {
            Call_centre_code = ReadInt16(reader, "Call_centre_code") ?? 0,
            vmf_code = ReadInt32(reader, "vmf_code"),
            Call_time = ReadTime(reader, "Call_time"),
            Call_date = callDate,
            Capture_name = ReadString(reader, "Capture_name"),
            User_access_code = ReadInt16(reader, "User_access_code"),
            Caller_name = ReadString(reader, "Caller_name"),
            Driver_name = ReadString(reader, "Driver_name"),
            Driver_persalno = ReadString(reader, "Driver_persalno"),
            Driver_Licno = ReadString(reader, "Driver_Licno"),
            GG_number = ReadString(reader, "GG_number"),
            Driver_base_station = ReadString(reader, "Driver_base_station"),
            Driver_Site = ReadInt16(reader, "Driver_Site"),
            Driver_tel = ReadString(reader, "Driver_tel"),
            Driver_cell = ReadString(reader, "Driver_cell"),
            Driver_fax = ReadString(reader, "Driver_fax"),
            Driver_email = ReadString(reader, "Driver_email"),
            Incident_type = ReadString(reader, "Incident_type"),
            Incident_Desc = ReadString(reader, "Incident_Desc"),
            Incident_date = incidentDate,
            Incident_time = ReadTime(reader, "Incident_time"),
            Caller_tel = ReadString(reader, "Caller_tel"),
            TrOfficer_name = ReadString(reader, "TrOfficer_name"),
            TrOfficer_tel = ReadString(reader, "TrOfficer_tel"),
            TrOfficer_Site = ReadInt16(reader, "TrOfficer_Site"),
            Incident_town = ReadString(reader, "Incident_town"),
            Incident_street = ReadString(reader, "Incident_street"),
            Counter = ReadInt16(reader, "Counter"),
            Caller_fax = ReadString(reader, "Caller_fax"),
            TrOfficer_fax = ReadString(reader, "TrOfficer_fax"),
            Caller_email = ReadString(reader, "Caller_email"),
            TrOfficer_email = ReadString(reader, "TrOfficer_email"),
            Inform_CRO = ReadString(reader, "Inform_CRO"),
            CRO_Remarks = ReadString(reader, "CRO_Remarks"),
            Incident_Remarks = ReadString(reader, "Incident_Remarks"),
            Notify_list_code = ReadInt32(reader, "Notify_list_code"),
            call_closed = ReadString(reader, "call_closed"),
            date_created =
                ReadDateTimeIfAvailable(reader, availableColumns, "date_created")
                ?? callDate
                ?? incidentDate
                ?? DateTime.MinValue,
            date_updated = ReadDateTimeIfAvailable(reader, availableColumns, "date_updated"),
            created_by_user_code = ReadInt32IfAvailable(
                reader,
                availableColumns,
                "created_by_user_code"
            ),
            modified_by_user_code = ReadInt32IfAvailable(
                reader,
                availableColumns,
                "modified_by_user_code"
            ),
            is_deleted = ReadBooleanIfAvailable(reader, availableColumns, "is_deleted") ?? false,
        };
    }

    private static List<WriteValue> BuildLegacyWriteValues(CallCentre callCentre)
    {
        return
        [
            new("vmf_code", "@vmfCode", DbType.Int32, callCentre.vmf_code),
            // SQL Server accepts datetime2 parameters for both the legacy
            // time(3) column and the expanded datetime2 column. Keeping the
            // complete value preserves the date on the expanded schema while
            // the legacy schema stores only the time portion.
            new("Call_time", "@callTime", DbType.DateTime2, callCentre.Call_time),
            new("Call_date", "@callDate", DbType.Date, callCentre.Call_date?.Date),
            new("Capture_name", "@captureName", DbType.String, callCentre.Capture_name),
            new("User_access_code", "@userAccessCode", DbType.Int16, callCentre.User_access_code),
            new("Caller_name", "@callerName", DbType.String, callCentre.Caller_name),
            new("Driver_name", "@driverName", DbType.String, callCentre.Driver_name),
            new("Driver_persalno", "@driverPersalno", DbType.String, callCentre.Driver_persalno),
            new("Driver_Licno", "@driverLicno", DbType.String, callCentre.Driver_Licno),
            new("GG_number", "@ggNumber", DbType.String, callCentre.GG_number),
            new(
                "Driver_base_station",
                "@driverBaseStation",
                DbType.String,
                callCentre.Driver_base_station
            ),
            new("Driver_Site", "@driverSite", DbType.Int16, callCentre.Driver_Site),
            new("Driver_tel", "@driverTel", DbType.String, callCentre.Driver_tel),
            new("Driver_cell", "@driverCell", DbType.String, callCentre.Driver_cell),
            new("Driver_fax", "@driverFax", DbType.String, callCentre.Driver_fax),
            new("Driver_email", "@driverEmail", DbType.String, callCentre.Driver_email),
            new("Incident_type", "@incidentType", DbType.String, callCentre.Incident_type),
            new("Incident_Desc", "@incidentDesc", DbType.String, callCentre.Incident_Desc),
            new("Incident_date", "@incidentDate", DbType.Date, callCentre.Incident_date?.Date),
            new("Incident_time", "@incidentTime", DbType.DateTime2, callCentre.Incident_time),
            new("Caller_tel", "@callerTel", DbType.String, callCentre.Caller_tel),
            new(
                "TrOfficer_name",
                "@transportOfficerName",
                DbType.String,
                callCentre.TrOfficer_name
            ),
            new("TrOfficer_tel", "@transportOfficerTel", DbType.String, callCentre.TrOfficer_tel),
            new("TrOfficer_Site", "@transportOfficerSite", DbType.Int16, callCentre.TrOfficer_Site),
            new("Incident_town", "@incidentTown", DbType.String, callCentre.Incident_town),
            new("Incident_street", "@incidentStreet", DbType.String, callCentre.Incident_street),
            new("Counter", "@counter", DbType.Int16, callCentre.Counter),
            new("Caller_fax", "@callerFax", DbType.String, callCentre.Caller_fax),
            new("TrOfficer_fax", "@transportOfficerFax", DbType.String, callCentre.TrOfficer_fax),
            new("Caller_email", "@callerEmail", DbType.String, callCentre.Caller_email),
            new("Inform_CRO", "@informCro", DbType.String, callCentre.Inform_CRO),
            new("CRO_Remarks", "@croRemarks", DbType.String, callCentre.CRO_Remarks),
            new("Incident_Remarks", "@incidentRemarks", DbType.String, callCentre.Incident_Remarks),
            new("Notify_list_code", "@notifyListCode", DbType.Int32, callCentre.Notify_list_code),
            new("call_closed", "@callClosed", DbType.String, callCentre.call_closed),
        ];
    }

    private static void AddOptionalValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> availableColumns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
    {
        if (availableColumns.Contains(column))
        {
            values.Add(new WriteValue(column, parameter, type, value));
        }
    }

    private static void AddParameters(DbCommand command, IEnumerable<WriteValue> values)
    {
        foreach (var value in values)
        {
            AddParameter(command, value.Parameter, value.Type, value.Value);
        }
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string GetActiveFilter(IReadOnlySet<string> availableColumns) =>
        availableColumns.Contains("is_deleted") ? "ISNULL([is_deleted], 0) = 0" : "1 = 1";

    private static string GetOptionalProjection(IReadOnlySet<string> columns, string column)
    {
        if (columns.Contains(column))
        {
            return $"[{column}] AS [{column}]";
        }

        var sqlType = column switch
        {
            "date_created" or "date_updated" => "datetime2",
            "created_by_user_code" or "modified_by_user_code" => "int",
            "is_deleted" => "bit",
            _ => "sql_variant",
        };
        return $"CAST(NULL AS {sqlType}) AS [{column}]";
    }

    private static string GetColumnProjection(IReadOnlySet<string> columns, string column)
    {
        if (columns.Contains(column))
        {
            return $"[{column}] AS [{column}]";
        }

        return $"CAST(NULL AS {GetLegacySqlType(column)}) AS [{column}]";
    }

    private static string GetLegacySqlType(string column) =>
        column switch
        {
            "Call_centre_code"
            or "User_access_code"
            or "Driver_Site"
            or "TrOfficer_Site"
            or "Counter" => "smallint",
            "vmf_code" or "Notify_list_code" => "int",
            "Call_time" or "Incident_time" => "time",
            "Call_date" or "Incident_date" => "date",
            _ => "varchar(1)",
        };

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal).TrimEnd();
    }

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }

    private static DateTime? ReadDateTimeIfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column
    ) => columns.Contains(column) ? ReadDateTime(reader, column) : null;

    private static DateTime? ReadTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetValue(ordinal);
        return value switch
        {
            TimeSpan time => DateTime.MinValue.Add(time),
            DateTime dateTime => DateTime.MinValue.Add(dateTime.TimeOfDay),
            _ => DateTime.MinValue.Add(TimeSpan.Parse(Convert.ToString(value)!)),
        };
    }

    private static int? ReadInt32(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static int? ReadInt32IfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column
    ) => columns.Contains(column) ? ReadInt32(reader, column) : null;

    private static short? ReadInt16(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt16(reader.GetValue(ordinal));
    }

    private static short? ReadInt16IfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column
    ) => columns.Contains(column) ? ReadInt16(reader, column) : null;

    private static bool? ReadBooleanIfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column
    )
    {
        if (!columns.Contains(column))
        {
            return null;
        }

        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToBoolean(reader.GetValue(ordinal));
    }

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}
