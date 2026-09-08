using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Api.Services;

/// <summary>
/// Writes the legacy Call Centre loss fields when the client Losses table is
/// present, while remaining usable against the expanded table that contains
/// only the modern loss columns. No schema is assumed or changed at runtime.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers are fixed allowlisted columns and all values are parameterized."
)]
public sealed class LossCompatibilityService
{
    private const string TableName = "losses";

    private static readonly string[] RequiredColumns = ["loss_code", "vmf_code", "loss_date"];

    private readonly FisDbContext _context;

    public LossCompatibilityService(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<short> CreateAsync(
        LossCaptureValues values,
        int currentUserId,
        CancellationToken cancellationToken = default
    )
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
            now
        );
        AddOptionalValue(
            writeValues,
            availableColumns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );
        AddOptionalValue(
            writeValues,
            availableColumns,
            "is_deleted",
            "@isDeleted",
            DbType.Boolean,
            false
        );

        return await ExecuteInsertAsync(writeValues, cancellationToken);
    }

    private async Task<short> ExecuteInsertAsync(
        IReadOnlyList<WriteValue> values,
        CancellationToken cancellationToken
    )
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
                INSERT INTO [dbo].[{TableName}] ({string.Join(
                    ", ",
                    values.Select(value => $"[{value.Column}]")
                )})
                OUTPUT INSERTED.[loss_code]
                VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
                """;
            AddParameters(command, values);

            return Convert.ToInt16(await command.ExecuteScalarAsync(cancellationToken));
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<HashSet<string>> GetAvailableColumnsAsync(
        CancellationToken cancellationToken
    )
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

            var missingColumns = RequiredColumns
                .Where(column => !columns.Contains(column))
                .ToArray();
            if (missingColumns.Length > 0)
            {
                throw new InvalidOperationException(
                    $"The required loss compatibility columns are not available: {string.Join(", ", missingColumns)}"
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

    private static List<WriteValue> BuildWriteValues(LossCaptureValues values) =>
        [
            new("vmf_code", "@vmfCode", DbType.Int32, values.VmfCode),
            new("loss_date", "@lossDate", DbType.DateTime2, values.LossDate),
            new("loss_type_code", "@lossTypeCode", DbType.Int16, values.LossTypeCode),
            new("site_code", "@siteCode", DbType.Int16, values.SiteCode),
            new("dept_contact", "@departmentContact", DbType.String, values.DepartmentContact),
            new("place_of_loss", "@placeOfLoss", DbType.String, values.PlaceOfLoss),
            new("driver_name", "@driverName", DbType.String, values.DriverName),
            new("Remarks", "@remarks", DbType.String, values.Remarks),
            new("cover_forfeit", "@coverForfeit", DbType.Decimal, values.CoverForfeit),
            new("cancelled", "@cancelled", DbType.Decimal, values.Cancelled),
            new(
                "report_from_dept",
                "@reportFromDepartment",
                DbType.Decimal,
                values.ReportFromDepartment
            ),
            new(
                "garaging_authority",
                "@garagingAuthority",
                DbType.Decimal,
                values.GaragingAuthority
            ),
            new(
                "compensation_order",
                "@compensationOrder",
                DbType.Decimal,
                values.CompensationOrder
            ),
            new("prosecute", "@prosecute", DbType.Decimal, values.Prosecute),
            new("Call_Refer", "@callRefer", DbType.Decimal, values.CallRefer),
            new("Tow_need", "@towNeed", DbType.String, values.TowNeed),
        ];

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

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}

public sealed record LossCaptureValues(
    int VmfCode,
    DateTime LossDate,
    short? LossTypeCode,
    short? SiteCode,
    string? DepartmentContact,
    string? PlaceOfLoss,
    string? DriverName,
    string? Remarks,
    decimal CoverForfeit,
    decimal Cancelled,
    decimal ReportFromDepartment,
    decimal GaragingAuthority,
    decimal CompensationOrder,
    decimal Prosecute,
    decimal? CallRefer,
    string? TowNeed
);
