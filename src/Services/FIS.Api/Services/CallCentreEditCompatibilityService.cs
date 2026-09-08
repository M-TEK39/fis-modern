using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Api.Services;

/// <summary>
/// Reads and updates the child rows used by the legacy Call Centre edit
/// workflow. The client database predates the audit columns in the expanded
/// schema, so this service inspects the live table shape and never asks EF to
/// materialize a child table that may not have those columns.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Table and column identifiers come only from fixed definitions; all values are parameterized."
)]
public sealed class CallCentreEditCompatibilityService
{
    private static readonly ChildTableDefinition AccidentTable = new(
        "accident",
        "accident_code",
        "Call_Refer"
    );

    private static readonly ChildTableDefinition LossTable = new(
        "losses",
        "loss_code",
        "Call_Refer"
    );

    private static readonly ChildTableDefinition TowingTable = new(
        "Towing",
        "Towing_code",
        "Call_refer"
    );

    private readonly FisDbContext _context;

    public CallCentreEditCompatibilityService(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<CallCentreEditDetails> GetAsync(
        short callCentreCode,
        CancellationToken cancellationToken = default
    )
    {
        var accident = await ReadRowAsync(AccidentTable, callCentreCode, cancellationToken);
        var loss = await ReadRowAsync(LossTable, callCentreCode, cancellationToken);
        var towing = await ReadRowAsync(TowingTable, callCentreCode, cancellationToken);

        return new CallCentreEditDetails
        {
            AccidentTableAvailable = accident.TableAvailable,
            Accident = accident.Values,
            LossTableAvailable = loss.TableAvailable,
            Loss = loss.Values,
            TowingTableAvailable = towing.TableAvailable,
            Towing = towing.Values,
        };
    }

    public async Task UpdateAsync(
        short callCentreCode,
        CallCentreEditUpdate update,
        int currentUserId,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(update);

        if (update.Accident is not null)
        {
            await UpdateAccidentAsync(
                callCentreCode,
                update.Accident,
                currentUserId,
                cancellationToken
            );
        }

        if (update.Loss is not null)
        {
            await UpdateLossAsync(callCentreCode, update.Loss, currentUserId, cancellationToken);
        }

        if (update.Towing is not null)
        {
            await UpdateTowingAsync(
                callCentreCode,
                update.Towing,
                currentUserId,
                cancellationToken
            );
        }
    }

    private async Task UpdateAccidentAsync(
        short callCentreCode,
        AccidentEditUpdate update,
        int currentUserId,
        CancellationToken cancellationToken
    )
    {
        var row = await ReadRowAsync(AccidentTable, callCentreCode, cancellationToken);
        if (!row.TableAvailable || row.Values is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var values = new List<WriteValue>();
        AddValue(
            values,
            row.Columns,
            "occurence_date",
            "@occurenceDate",
            DbType.Date,
            update.OccurenceDate?.Date
        );
        AddValue(
            values,
            row.Columns,
            "occurence_time",
            "@occurenceTime",
            DbType.DateTime2,
            update.OccurenceTime
        );
        AddValue(
            values,
            row.Columns,
            "description",
            "@description",
            DbType.String,
            update.Description
        );
        AddValue(
            values,
            row.Columns,
            "driver_name",
            "@driverName",
            DbType.String,
            update.DriverName
        );
        AddValue(
            values,
            row.Columns,
            "driver_employ_number",
            "@driverEmployNumber",
            DbType.String,
            update.DriverEmployNumber
        );
        AddValue(
            values,
            row.Columns,
            "driver_telno",
            "@driverTelno",
            DbType.String,
            update.DriverTelno
        );
        AddValue(
            values,
            row.Columns,
            "driver_site_code",
            "@driverSiteCode",
            DbType.Int16,
            update.DriverSiteCode
        );
        AddValue(
            values,
            row.Columns,
            "transoffic_name",
            "@transportOfficerName",
            DbType.String,
            update.TransportOfficerName
        );
        AddValue(
            values,
            row.Columns,
            "transoffic_tel",
            "@transportOfficerTel",
            DbType.String,
            update.TransportOfficerTel
        );
        AddValue(values, row.Columns, "death", "@death", DbType.String, update.Death);
        AddValue(values, row.Columns, "injured", "@injured", DbType.String, update.Injured);
        AddValue(
            values,
            row.Columns,
            "third_party_regno",
            "@thirdPartyRegistration",
            DbType.String,
            update.ThirdPartyRegistration
        );
        AddValue(
            values,
            row.Columns,
            "third_party_owner",
            "@thirdPartyOwner",
            DbType.String,
            update.ThirdPartyOwner
        );
        AddValue(
            values,
            row.Columns,
            "third_party_tel",
            "@thirdPartyTelephone",
            DbType.String,
            update.ThirdPartyTelephone
        );
        AddValue(
            values,
            row.Columns,
            "damage_description",
            "@damageDescription",
            DbType.String,
            update.DamageDescription
        );
        AddValue(values, row.Columns, "notes", "@notes", DbType.String, update.Notes);
        AddValue(
            values,
            row.Columns,
            "occurence_place",
            "@occurencePlace",
            DbType.String,
            update.OccurencePlace
        );
        AddValue(values, row.Columns, "Tow_need", "@towNeed", DbType.String, update.TowNeed);

        // The legacy update path marks an edited accident as captured by the
        // call centre and refreshes the legacy modification date.
        AddValue(values, row.Columns, "date_updated", "@dateUpdated", DbType.DateTime2, now);
        AddValue(values, row.Columns, "acc_type_code", "@accidentTypeCode", DbType.Int16, 1);
        AddValue(values, row.Columns, "Flag_gg_hq", "@flagGgHq", DbType.String, "C");
        AddValue(values, row.Columns, "Flag_gg_hq_date", "@flagGgHqDate", DbType.DateTime2, now);
        AddValue(
            values,
            row.Columns,
            "modified_by_user_code",
            "@modifiedByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );

        await ExecuteUpdateAsync(AccidentTable, row.Columns, row.Values, values, cancellationToken);
    }

    private async Task UpdateLossAsync(
        short callCentreCode,
        LossEditUpdate update,
        int currentUserId,
        CancellationToken cancellationToken
    )
    {
        var row = await ReadRowAsync(LossTable, callCentreCode, cancellationToken);
        if (!row.TableAvailable || row.Values is null)
        {
            return;
        }

        var values = new List<WriteValue>();
        AddValue(values, row.Columns, "loss_date", "@lossDate", DbType.Date, update.LossDate?.Date);
        AddValue(
            values,
            row.Columns,
            "loss_type_code",
            "@lossTypeCode",
            DbType.Int16,
            update.LossTypeCode
        );
        AddValue(values, row.Columns, "Site_code", "@siteCode", DbType.Int16, update.SiteCode);
        AddValue(
            values,
            row.Columns,
            "dept_contact",
            "@departmentContact",
            DbType.String,
            update.DepartmentContact
        );
        AddValue(
            values,
            row.Columns,
            "place_of_loss",
            "@placeOfLoss",
            DbType.String,
            update.PlaceOfLoss
        );
        AddValue(
            values,
            row.Columns,
            "driver_name",
            "@driverName",
            DbType.String,
            update.DriverName
        );
        AddValue(values, row.Columns, "Remarks", "@remarks", DbType.String, update.Remarks);
        AddValue(values, row.Columns, "Tow_need", "@towNeed", DbType.String, update.TowNeed);
        AddValue(
            values,
            row.Columns,
            "date_updated",
            "@dateUpdated",
            DbType.DateTime2,
            DateTime.UtcNow
        );
        AddValue(
            values,
            row.Columns,
            "modified_by_user_code",
            "@modifiedByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );

        await ExecuteUpdateAsync(LossTable, row.Columns, row.Values, values, cancellationToken);
    }

    private async Task UpdateTowingAsync(
        short callCentreCode,
        TowingEditUpdate update,
        int currentUserId,
        CancellationToken cancellationToken
    )
    {
        var row = await ReadRowAsync(TowingTable, callCentreCode, cancellationToken);
        if (!row.TableAvailable || row.Values is null)
        {
            return;
        }

        var values = new List<WriteValue>();
        AddValue(
            values,
            row.Columns,
            "Tow_location_start",
            "@location",
            DbType.String,
            update.Location
        );
        AddValue(
            values,
            row.Columns,
            "Vehicle_problem",
            "@vehicleProblem",
            DbType.String,
            update.VehicleProblem
        );
        AddValue(values, row.Columns, "Site_code", "@siteCode", DbType.Int16, update.SiteCode);
        AddValue(
            values,
            row.Columns,
            "Tow_Truck_code",
            "@towTruckCode",
            DbType.Int16,
            update.TowTruckCode
        );
        AddValue(
            values,
            row.Columns,
            "Contact_person_name",
            "@contactPersonName",
            DbType.String,
            update.ContactPersonName
        );
        AddValue(
            values,
            row.Columns,
            "Contact_person_tel",
            "@contactPersonTel",
            DbType.String,
            update.ContactPersonTel
        );
        AddValue(values, row.Columns, "Remaks", "@remarks", DbType.String, update.Remarks);
        AddValue(
            values,
            row.Columns,
            "date_updated",
            "@dateUpdated",
            DbType.DateTime2,
            DateTime.UtcNow
        );
        AddValue(
            values,
            row.Columns,
            "modified_by_user_code",
            "@modifiedByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );

        await ExecuteUpdateAsync(TowingTable, row.Columns, row.Values, values, cancellationToken);
    }

    private async Task<ChildRow> ReadRowAsync(
        ChildTableDefinition table,
        short callCentreCode,
        CancellationToken cancellationToken
    )
    {
        var columns = await GetAvailableColumnsAsync(table.Name, cancellationToken);
        var referenceColumn = ResolveColumn(columns, table.ReferenceColumn);
        var keyColumn = ResolveColumn(columns, table.KeyColumn);
        if (referenceColumn is null || keyColumn is null)
        {
            return new ChildRow(false, columns, null);
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
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"""
                SELECT TOP (1) *
                FROM [dbo].[{table.Name}]
                WHERE [{referenceColumn}] = @callCentreCode
                  AND {GetActiveFilter(columns)}
                ORDER BY [{keyColumn}] DESC
                """;
            AddParameter(command, "@callCentreCode", DbType.Int16, callCentreCode);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return new ChildRow(true, columns, null);
            }

            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                var value = reader.GetValue(index);
                values[reader.GetName(index)] = value is DBNull ? null : value;
            }

            return new ChildRow(true, columns, values);
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
        string tableName,
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
            AddParameter(command, "@table", DbType.String, tableName);

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
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

    private async Task ExecuteUpdateAsync(
        ChildTableDefinition table,
        IReadOnlySet<string> columns,
        IReadOnlyDictionary<string, object?> row,
        IReadOnlyList<WriteValue> values,
        CancellationToken cancellationToken
    )
    {
        if (values.Count == 0)
        {
            return;
        }

        var keyColumn =
            ResolveColumn(columns, table.KeyColumn)
            ?? throw new InvalidOperationException(
                $"The {table.Name} compatibility key is not available."
            );
        if (!row.TryGetValue(keyColumn, out var keyValue) || keyValue is null || keyValue is DBNull)
        {
            return;
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
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"""
                UPDATE [dbo].[{table.Name}]
                SET {string.Join(
                    ", ",
                    values.Select(value => $"[{value.Column}] = {value.Parameter}")
                )}
                WHERE [{keyColumn}] = @recordCode
                  AND {GetActiveFilter(columns)}
                """;
            AddParameters(command, values);
            AddParameter(
                command,
                "@recordCode",
                table == AccidentTable ? DbType.Int32 : DbType.Int16,
                keyValue
            );
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> columns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
    {
        var availableColumn = ResolveColumn(columns, column);
        if (availableColumn is not null)
        {
            values.Add(new WriteValue(availableColumn, parameter, type, value));
        }
    }

    private static string? ResolveColumn(IReadOnlySet<string> columns, string requested) =>
        columns.FirstOrDefault(column =>
            string.Equals(column, requested, StringComparison.OrdinalIgnoreCase)
        );

    private static string GetActiveFilter(IReadOnlySet<string> columns) =>
        columns.Contains("is_deleted") ? "ISNULL([is_deleted], 0) = 0" : "1 = 1";

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

    private sealed record ChildTableDefinition(
        string Name,
        string KeyColumn,
        string ReferenceColumn
    );

    private sealed record ChildRow(
        bool TableAvailable,
        IReadOnlySet<string> Columns,
        IReadOnlyDictionary<string, object?>? Values
    );

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}

public sealed class CallCentreEditDetails
{
    public bool AccidentTableAvailable { get; init; }
    public IReadOnlyDictionary<string, object?>? Accident { get; init; }
    public bool LossTableAvailable { get; init; }
    public IReadOnlyDictionary<string, object?>? Loss { get; init; }
    public bool TowingTableAvailable { get; init; }
    public IReadOnlyDictionary<string, object?>? Towing { get; init; }
}

public sealed class CallCentreEditUpdate
{
    public AccidentEditUpdate? Accident { get; init; }
    public LossEditUpdate? Loss { get; init; }
    public TowingEditUpdate? Towing { get; init; }
}

public sealed class AccidentEditUpdate
{
    public DateTime? OccurenceDate { get; init; }
    public DateTime? OccurenceTime { get; init; }
    public string? Description { get; init; }
    public string? DriverName { get; init; }
    public string? DriverEmployNumber { get; init; }
    public string? DriverTelno { get; init; }
    public short? DriverSiteCode { get; init; }
    public string? TransportOfficerName { get; init; }
    public string? TransportOfficerTel { get; init; }
    public string? Death { get; init; }
    public string? Injured { get; init; }
    public string? ThirdPartyRegistration { get; init; }
    public string? ThirdPartyOwner { get; init; }
    public string? ThirdPartyTelephone { get; init; }
    public string? DamageDescription { get; init; }
    public string? Notes { get; init; }
    public string? OccurencePlace { get; init; }
    public string? TowNeed { get; init; }
}

public sealed class LossEditUpdate
{
    public DateTime? LossDate { get; init; }
    public short? LossTypeCode { get; init; }
    public short? SiteCode { get; init; }
    public string? DepartmentContact { get; init; }
    public string? PlaceOfLoss { get; init; }
    public string? DriverName { get; init; }
    public string? Remarks { get; init; }
    public string? TowNeed { get; init; }
}

public sealed class TowingEditUpdate
{
    public string? Location { get; init; }
    public string? VehicleProblem { get; init; }
    public short? SiteCode { get; init; }
    public short? TowTruckCode { get; init; }
    public string? ContactPersonName { get; init; }
    public string? ContactPersonTel { get; init; }
    public string? Remarks { get; init; }
}
