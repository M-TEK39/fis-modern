using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Api.Services;

/// <summary>
/// Writes the Call Centre capture fields that belong to the legacy accident
/// table without requiring the expanded table columns to exist.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers are fixed allowlisted columns and all values are parameterized.")]
public sealed class AccidentCompatibilityService
{
    private const string TableName = "accident";

    private static readonly string[] RequiredColumns =
    [
        "accident_code",
        "vmf_code"
    ];

    private readonly FisDbContext _context;

    public AccidentCompatibilityService(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<int> CreateAsync(
        AccidentCaptureValues values,
        int currentUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        var availableColumns = await GetAvailableColumnsAsync(cancellationToken);
        var writeValues = BuildWriteValues(values)
            .Where(value => availableColumns.Contains(value.Column))
            .ToList();
        var now = DateTime.UtcNow;

        AddOptionalValue(
            writeValues,
            availableColumns,
            "date_created",
            "@dateCreated",
            DbType.DateTime2,
            now);
        AddOptionalValue(
            writeValues,
            availableColumns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null);
        AddOptionalValue(
            writeValues,
            availableColumns,
            "is_deleted",
            "@isDeleted",
            DbType.Boolean,
            false);

        var accidentCode = await ExecuteInsertAsync(writeValues, cancellationToken);
        return accidentCode;
    }

    private async Task<int> ExecuteInsertAsync(
        IReadOnlyList<WriteValue> values,
        CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"""
                INSERT INTO [dbo].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))})
                OUTPUT INSERTED.[accident_code]
                VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
                """;
            AddParameters(command, values);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<HashSet<string>> GetAvailableColumnsAsync(CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
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
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(0));
            }

            var missingColumns = RequiredColumns.Where(column => !columns.Contains(column)).ToArray();
            if (missingColumns.Length > 0)
            {
                throw new InvalidOperationException(
                    $"The required accident compatibility columns are not available: {string.Join(", ", missingColumns)}");
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

    private static List<WriteValue> BuildWriteValues(AccidentCaptureValues values)
        =>
        [
            new("vmf_code", "@vmfCode", DbType.Int32, values.VmfCode),
            new("Call_Refer", "@callRefer", DbType.Decimal, values.CallRefer),
            new("captured_person", "@capturedPerson", DbType.String, values.CapturedPerson),
            new("occurence_date", "@occurenceDate", DbType.Date, values.OccurenceDate?.Date),
            // DbType.DateTime2 converts safely to SQL Server time(3) on the
            // legacy table and remains valid for the modern datetime2 column.
            new("occurence_time", "@occurenceTime", DbType.DateTime2, values.OccurenceTime),
            new("description", "@description", DbType.String, values.Description),
            new("driver_name", "@driverName", DbType.String, values.DriverName),
            new("driver_employ_number", "@driverEmployNumber", DbType.String, values.DriverEmployNumber),
            new("driver_telno", "@driverTelno", DbType.String, values.DriverTelno),
            new("driver_site_code", "@driverSiteCode", DbType.Int16, values.DriverSiteCode),
            new("transoffic_name", "@transofficName", DbType.String, values.TransportOfficerName),
            new("transoffic_tel", "@transofficTel", DbType.String, values.TransportOfficerTel),
            new("death", "@death", DbType.String, values.Death),
            new("injured", "@injured", DbType.String, values.Injured),
            new("third_party_regno", "@thirdPartyRegno", DbType.String, values.ThirdPartyRegistration),
            new("third_party_owner", "@thirdPartyOwner", DbType.String, values.ThirdPartyOwner),
            new("third_party_tel", "@thirdPartyTel", DbType.String, values.ThirdPartyTelephone),
            new("damage_description", "@damageDescription", DbType.String, values.DamageDescription),
            new("date_updated", "@dateUpdated", DbType.DateTime2, values.DateUpdated),
            new("notes", "@notes", DbType.String, values.Notes),
            new("acc_type_code", "@accTypeCode", DbType.Int16, values.AccidentTypeCode),
            new("flag_gg_hq", "@flagGgHq", DbType.String, values.FlagGgHq),
            new("flag_gg_hq_date", "@flagGgHqDate", DbType.DateTime2, values.FlagGgHqDate),
            new("occurence_place", "@occurencePlace", DbType.String, values.OccurencePlace),
            new("Tow_need", "@towNeed", DbType.String, values.TowNeed)
        ];

    private static void AddOptionalValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> availableColumns,
        string column,
        string parameter,
        DbType type,
        object? value)
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

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}

public sealed record AccidentCaptureValues(
    int VmfCode,
    decimal? CallRefer,
    string? CapturedPerson,
    DateTime? OccurenceDate,
    DateTime? OccurenceTime,
    string? Description,
    string? DriverName,
    string? DriverEmployNumber,
    string? DriverTelno,
    short? DriverSiteCode,
    string? TransportOfficerName,
    string? TransportOfficerTel,
    string? Death,
    string? Injured,
    string? ThirdPartyRegistration,
    string? ThirdPartyOwner,
    string? ThirdPartyTelephone,
    string? DamageDescription,
    DateTime DateUpdated,
    string? Notes,
    short AccidentTypeCode,
    string? FlagGgHq,
    DateTime FlagGgHqDate,
    string? OccurencePlace,
    string? TowNeed);
