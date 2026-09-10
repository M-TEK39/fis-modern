using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Contracts;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Persists Private_hire and Contractors against both the original client schema and
/// databases containing the optional modern audit columns. The legacy business columns
/// remain the source of truth; no schema change is required.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers come only from fixed compatibility allowlists; all submitted values are parameters."
)]
public sealed class PrivateHireRepository : IPrivateHireRepository
{
    private const string PrivateHireTableName = "Private_hire";
    private const string ContractorsTableName = "Contractors";

    private static readonly string[] PrivateHireBusinessColumns =
    [
        "PHV_code",
        "registration_number",
        "model_code",
        "site_code",
        "contracted_to",
        "engine_number",
        "chassis_number",
        "year_manufactured",
        "bank_code",
        "colour",
        "tank_capacity",
        "contractor_id",
        "fuel_card",
        "fuel_card_receiver",
        "take_on_date",
        "take_on_odo",
        "return_date",
        "return_odo",
        "km_tariff",
        "daily_tariff",
        "hourly_tariff",
        "model_desc",
    ];

    private static readonly string[] ContractorBusinessColumns =
    [
        "contractor_id",
        "contractor_name",
        "physical_address",
        "postal_address",
        "tel_number",
        "fax_number",
        "email_address",
        "contact_person",
        "active",
        "type",
        "quotations",
        "project_name",
        "project_begdat",
        "project_enddat",
    ];

    private static readonly string[] ContractorRequiredColumns =
    [
        "contractor_id",
        "contractor_name",
    ];

    private static readonly string[] OptionalAuditColumns =
    [
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private static readonly HashSet<string> DateColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "take_on_date",
        "return_date",
        "date_created",
        "date_updated",
        "project_begdat",
        "project_enddat",
    };

    private readonly FisDbContext _context;

    public PrivateHireRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<PrivateHire?> GetByIdAsync(int privateHireCode) =>
        (
            await QueryPrivateHiresAsync(
                "[PHV_code] = @privateHireCode",
                command =>
                    AddParameter(
                        command,
                        "@privateHireCode",
                        DbType.Int16,
                        checked((short)privateHireCode)
                    )
            )
        ).SingleOrDefault();

    public async Task<IEnumerable<PrivateHire>> GetByVehicleAsync(int vmfCode)
    {
        // Private_hire has no VMF foreign key. Preserve the legacy endpoint contract,
        // which returned the active rows with a registration rather than guessing a join.
        _ = vmfCode;
        return await QueryPrivateHiresAsync("[registration_number] IS NOT NULL");
    }

    public async Task<IEnumerable<PrivateHire>> GetByDateRangeAsync(
        DateTime startDate,
        DateTime endDate
    ) =>
        await QueryPrivateHiresAsync(
            "[take_on_date] >= @startDate AND [take_on_date] <= @endDate",
            command =>
            {
                AddParameter(command, "@startDate", DbType.DateTime, startDate);
                AddParameter(command, "@endDate", DbType.DateTime, endDate);
            }
        );

    public async Task<IEnumerable<PrivateHire>> GetActiveHiresAsync() =>
        await QueryPrivateHiresAsync(
            "[return_date] IS NULL OR [return_date] >= @recentReturnDate",
            command =>
                AddParameter(
                    command,
                    "@recentReturnDate",
                    DbType.DateTime,
                    DateTime.Now.AddDays(-30)
                )
        );

    public async Task<PrivateHire> CreateAsync(PrivateHire privateHire, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(privateHire);
        ValidatePrivateHire(privateHire);

        var columns = await GetAvailableColumnsAsync(
            PrivateHireTableName,
            PrivateHireBusinessColumns
        );
        var values = BuildPrivateHireValues(privateHire, columns);
        var now = DateTime.UtcNow;
        AddValue(values, columns, "date_created", "@dateCreated", DbType.DateTime2, now);
        AddValue(
            values,
            columns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            UserIdOrNull(currentUserId)
        );
        AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);

        privateHire.date_created = now;
        privateHire.created_by_user_code = UserIdOrNull(currentUserId);
        privateHire.is_deleted = false;
        privateHire.PHV_code = checked(
            (short)await ExecuteInsertAsync(PrivateHireTableName, "PHV_code", values)
        );
        return await GetByIdAsync(privateHire.PHV_code)
            ?? throw new InvalidOperationException(
                $"Private hire {privateHire.PHV_code} could not be read after creation."
            );
    }

    public async Task UpdateAsync(PrivateHire privateHire, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(privateHire);
        var existing =
            await GetByIdAsync(privateHire.PHV_code)
            ?? throw new InvalidOperationException(
                $"Private hire {privateHire.PHV_code} not found"
            );

        MergePrivateHire(privateHire, existing);
        ValidatePrivateHire(privateHire);
        var columns = await GetAvailableColumnsAsync(
            PrivateHireTableName,
            PrivateHireBusinessColumns
        );
        var values = BuildPrivateHireValues(privateHire, columns);
        var now = DateTime.UtcNow;
        AddValue(values, columns, "date_updated", "@dateUpdated", DbType.DateTime2, now);
        AddValue(
            values,
            columns,
            "modified_by_user_code",
            "@modifiedByUserCode",
            DbType.Int32,
            UserIdOrNull(currentUserId)
        );
        await ExecuteUpdateAsync(
            PrivateHireTableName,
            "PHV_code",
            privateHire.PHV_code,
            values,
            columns
        );

        privateHire.date_updated = now;
        privateHire.modified_by_user_code = UserIdOrNull(currentUserId);
    }

    public async Task DeleteAsync(int privateHireCode, int currentUserId) =>
        await DeleteAsync(PrivateHireTableName, "PHV_code", privateHireCode, currentUserId);

    public async Task<IEnumerable<PrivateHire>> SearchHiresAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await QueryPrivateHiresAsync();
        return await QueryPrivateHiresAsync(
            "[registration_number] LIKE @searchTerm OR [engine_number] LIKE @searchTerm OR [chassis_number] LIKE @searchTerm",
            command => AddParameter(command, "@searchTerm", DbType.String, $"%{searchTerm.Trim()}%")
        );
    }

    public async Task<IEnumerable<PrivateHireContractorRecord>> GetContractorsAsync() =>
        await QueryContractorsAsync();

    public async Task<PrivateHireContractorRecord?> GetContractorByIdAsync(int contractorId) =>
        (
            await QueryContractorsAsync(
                "[contractor_id] = @contractorId",
                command =>
                    AddParameter(
                        command,
                        "@contractorId",
                        DbType.Int16,
                        checked((short)contractorId)
                    )
            )
        ).SingleOrDefault();

    public async Task<PrivateHireContractorRecord> CreateContractorAsync(
        PrivateHireContractorRecord contractor,
        int currentUserId
    )
    {
        ArgumentNullException.ThrowIfNull(contractor);
        if (string.IsNullOrWhiteSpace(contractor.contractor_name))
            throw new ArgumentException("Contractor name is required.", nameof(contractor));

        var columns = await GetAvailableColumnsAsync(
            ContractorsTableName,
            ContractorRequiredColumns
        );
        var values = BuildContractorValues(contractor, columns);
        var now = DateTime.UtcNow;
        AddValue(values, columns, "date_created", "@dateCreated", DbType.DateTime2, now);
        AddValue(
            values,
            columns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            UserIdOrNull(currentUserId)
        );
        AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);

        var id = await ExecuteInsertAsync(ContractorsTableName, "contractor_id", values);
        contractor.contractor_id = checked((short)id);
        contractor.date_created = now;
        contractor.created_by_user_code = UserIdOrNull(currentUserId);
        contractor.is_deleted = false;
        return await GetContractorByIdAsync(contractor.contractor_id)
            ?? throw new InvalidOperationException(
                $"Contractor {contractor.contractor_id} could not be read after creation."
            );
    }

    public async Task UpdateContractorAsync(
        PrivateHireContractorRecord contractor,
        int currentUserId
    )
    {
        ArgumentNullException.ThrowIfNull(contractor);
        var existing =
            await GetContractorByIdAsync(contractor.contractor_id)
            ?? throw new InvalidOperationException(
                $"Contractor {contractor.contractor_id} not found"
            );

        MergeContractor(contractor, existing);
        if (string.IsNullOrWhiteSpace(contractor.contractor_name))
            throw new ArgumentException("Contractor name is required.", nameof(contractor));

        var columns = await GetAvailableColumnsAsync(
            ContractorsTableName,
            ContractorRequiredColumns
        );
        var values = BuildContractorValues(contractor, columns);
        var now = DateTime.UtcNow;
        AddValue(values, columns, "date_updated", "@dateUpdated", DbType.DateTime2, now);
        AddValue(
            values,
            columns,
            "modified_by_user_code",
            "@modifiedByUserCode",
            DbType.Int32,
            UserIdOrNull(currentUserId)
        );
        await ExecuteUpdateAsync(
            ContractorsTableName,
            "contractor_id",
            contractor.contractor_id,
            values,
            columns
        );

        contractor.date_updated = now;
        contractor.modified_by_user_code = UserIdOrNull(currentUserId);
    }

    public async Task DeleteContractorAsync(int contractorId, int currentUserId) =>
        await DeleteAsync(ContractorsTableName, "contractor_id", contractorId, currentUserId);

    private async Task<List<PrivateHire>> QueryPrivateHiresAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null
    )
    {
        var columns = await GetAvailableColumnsAsync(
            PrivateHireTableName,
            PrivateHireBusinessColumns
        );
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        var conditions = new List<string> { GetActiveFilter(columns) };
        if (!string.IsNullOrWhiteSpace(predicate))
            conditions.Add($"({predicate})");
        command.CommandText = $"""
            SELECT {string.Join(
                ", ",
                PrivateHireBusinessColumns.Concat(OptionalAuditColumns).Select(column =>
                    GetProjection(columns, column)
                )
            )}
            FROM [dbo].[{PrivateHireTableName}]
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY [PHV_code] DESC
            """;
        configure?.Invoke(command);

        var results = new List<PrivateHire>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(MapPrivateHire(reader, columns));
        return results;
    }

    private async Task<List<PrivateHireContractorRecord>> QueryContractorsAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null
    )
    {
        var columns = await GetAvailableColumnsAsync(
            ContractorsTableName,
            ContractorRequiredColumns
        );
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        var conditions = new List<string> { GetActiveFilter(columns) };
        if (!string.IsNullOrWhiteSpace(predicate))
            conditions.Add($"({predicate})");
        command.CommandText = $"""
            SELECT {string.Join(
                ", ",
                ContractorBusinessColumns.Concat(OptionalAuditColumns).Select(column =>
                    GetProjection(columns, column)
                )
            )}
            FROM [dbo].[{ContractorsTableName}]
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY [contractor_name], [contractor_id]
            """;
        configure?.Invoke(command);

        var results = new List<PrivateHireContractorRecord>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(MapContractor(reader, columns));
        return results;
    }

    private async Task<Dictionary<string, ColumnInfo>> GetAvailableColumnsAsync(
        string tableName,
        IReadOnlyCollection<string> requiredColumns
    )
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            SELECT [COLUMN_NAME], [DATA_TYPE]
            FROM [INFORMATION_SCHEMA].[COLUMNS]
            WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table
            """;
        AddParameter(command, "@schema", DbType.String, "dbo");
        AddParameter(command, "@table", DbType.String, tableName);

        var columns = new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            columns[name] = new ColumnInfo(name, reader.GetString(1));
        }

        foreach (var required in requiredColumns.Where(column => !columns.ContainsKey(column)))
            throw new InvalidOperationException(
                $"The required {tableName} compatibility column {required} is not available."
            );
        return columns;
    }

    private async Task<int> ExecuteInsertAsync(
        string tableName,
        string keyColumn,
        IReadOnlyList<WriteValue> values
    )
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            INSERT INTO [dbo].[{tableName}] ({string.Join(
                ", ",
                values.Select(value => $"[{value.Column}]")
            )})
            OUTPUT INSERTED.[{keyColumn}]
            VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
            """;
        AddParameters(command, values);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task ExecuteUpdateAsync(
        string tableName,
        string keyColumn,
        int key,
        IReadOnlyList<WriteValue> values,
        IReadOnlyDictionary<string, ColumnInfo> columns
    )
    {
        if (values.Count == 0)
            return;
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            UPDATE [dbo].[{tableName}]
            SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))}
            WHERE [{keyColumn}] = @key
              AND {GetActiveFilter(columns)}
            """;
        AddParameters(command, values);
        AddParameter(command, "@key", DbType.Int16, checked((short)key));
        await command.ExecuteNonQueryAsync();
    }

    private async Task DeleteAsync(string tableName, string keyColumn, int key, int currentUserId)
    {
        var requiredColumns = keyColumn.Equals("PHV_code", StringComparison.OrdinalIgnoreCase)
            ? PrivateHireBusinessColumns
            : ContractorRequiredColumns;
        var columns = await GetAvailableColumnsAsync(tableName, requiredColumns);
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

        if (columns.ContainsKey("is_deleted"))
        {
            var assignments = new List<string> { "[is_deleted] = @isDeleted" };
            AddParameter(command, "@isDeleted", DbType.Boolean, true);
            if (columns.ContainsKey("date_updated"))
            {
                assignments.Add("[date_updated] = @dateUpdated");
                AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
            }
            if (columns.ContainsKey("modified_by_user_code"))
            {
                assignments.Add("[modified_by_user_code] = @modifiedByUserCode");
                AddParameter(
                    command,
                    "@modifiedByUserCode",
                    DbType.Int32,
                    UserIdOrNull(currentUserId)
                );
            }
            command.CommandText =
                $"UPDATE [dbo].[{tableName}] SET {string.Join(", ", assignments)} WHERE [{keyColumn}] = @key AND {GetActiveFilter(columns)}";
        }
        else
        {
            command.CommandText = $"DELETE FROM [dbo].[{tableName}] WHERE [{keyColumn}] = @key";
        }

        AddParameter(command, "@key", DbType.Int16, checked((short)key));
        await command.ExecuteNonQueryAsync();
    }

    private static List<WriteValue> BuildPrivateHireValues(
        PrivateHire privateHire,
        IReadOnlyDictionary<string, ColumnInfo> columns
    )
    {
        var values = new List<WriteValue>();
        AddRequiredValue(
            values,
            columns,
            "registration_number",
            "@registrationNumber",
            DbType.String,
            privateHire.registration_number
        );
        AddRequiredValue(
            values,
            columns,
            "model_code",
            "@modelCode",
            DbType.Int16,
            privateHire.model_code
        );
        AddRequiredValue(
            values,
            columns,
            "site_code",
            "@siteCode",
            DbType.Int32,
            privateHire.site_code
        );
        AddValue(
            values,
            columns,
            "contracted_to",
            "@contractedTo",
            DbType.Int32,
            privateHire.contracted_to
        );
        AddValue(
            values,
            columns,
            "engine_number",
            "@engineNumber",
            DbType.String,
            privateHire.engine_number
        );
        AddValue(
            values,
            columns,
            "chassis_number",
            "@chassisNumber",
            DbType.String,
            privateHire.chassis_number
        );
        AddValue(
            values,
            columns,
            "year_manufactured",
            "@yearManufactured",
            DbType.String,
            privateHire.year_manufactured
        );
        AddValue(values, columns, "bank_code", "@bankCode", DbType.String, privateHire.bank_code);
        AddValue(values, columns, "colour", "@colour", DbType.String, privateHire.colour);
        AddValue(
            values,
            columns,
            "tank_capacity",
            "@tankCapacity",
            DbType.Int32,
            privateHire.tank_capacity
        );
        AddRequiredValue(
            values,
            columns,
            "contractor_id",
            "@contractorId",
            DbType.Int16,
            privateHire.contractor_id
        );
        AddValue(values, columns, "fuel_card", "@fuelCard", DbType.String, privateHire.fuel_card);
        AddValue(
            values,
            columns,
            "fuel_card_receiver",
            "@fuelCardReceiver",
            DbType.String,
            privateHire.fuel_card_receiver
        );
        AddRequiredValue(
            values,
            columns,
            "take_on_date",
            "@takeOnDate",
            DbType.DateTime,
            privateHire.take_on_date
        );
        AddRequiredValue(
            values,
            columns,
            "take_on_odo",
            "@takeOnOdo",
            DbType.Int32,
            privateHire.take_on_odo
        );
        AddValue(
            values,
            columns,
            "return_date",
            "@returnDate",
            DbType.DateTime,
            privateHire.return_date
        );
        AddRequiredValue(
            values,
            columns,
            "return_odo",
            "@returnOdo",
            DbType.Int32,
            privateHire.return_odo
        );
        AddValue(values, columns, "km_tariff", "@kmTariff", DbType.Decimal, privateHire.km_tariff);
        AddValue(
            values,
            columns,
            "daily_tariff",
            "@dailyTariff",
            DbType.Decimal,
            privateHire.daily_tariff
        );
        AddValue(
            values,
            columns,
            "hourly_tariff",
            "@hourlyTariff",
            DbType.Decimal,
            privateHire.hourly_tariff
        );
        AddValue(
            values,
            columns,
            "model_desc",
            "@modelDesc",
            DbType.String,
            privateHire.model_desc
        );
        return values;
    }

    private static List<WriteValue> BuildContractorValues(
        PrivateHireContractorRecord contractor,
        IReadOnlyDictionary<string, ColumnInfo> columns
    )
    {
        var values = new List<WriteValue>();
        AddRequiredValue(
            values,
            columns,
            "contractor_name",
            "@contractorName",
            DbType.String,
            contractor.contractor_name?.Trim()
        );
        AddValue(
            values,
            columns,
            "physical_address",
            "@physicalAddress",
            DbType.String,
            contractor.physical_address?.Trim()
        );
        AddValue(
            values,
            columns,
            "postal_address",
            "@postalAddress",
            DbType.String,
            contractor.postal_address?.Trim()
        );
        AddValue(
            values,
            columns,
            "tel_number",
            "@telNumber",
            DbType.String,
            contractor.tel_number?.Trim()
        );
        AddValue(
            values,
            columns,
            "fax_number",
            "@faxNumber",
            DbType.String,
            contractor.fax_number?.Trim()
        );
        AddValue(
            values,
            columns,
            "email_address",
            "@emailAddress",
            DbType.String,
            contractor.email_address?.Trim()
        );
        AddValue(
            values,
            columns,
            "contact_person",
            "@contactPerson",
            DbType.String,
            contractor.contact_person?.Trim()
        );
        AddValue(values, columns, "active", "@active", DbType.Int16, contractor.active);
        AddValue(values, columns, "type", "@type", DbType.String, contractor.type?.Trim());
        AddValue(
            values,
            columns,
            "quotations",
            "@quotations",
            DbType.Boolean,
            contractor.quotations
        );
        AddValue(
            values,
            columns,
            "project_name",
            "@projectName",
            DbType.String,
            contractor.project_name?.Trim()
        );
        AddValue(
            values,
            columns,
            "project_begdat",
            "@projectBegdat",
            DbType.Date,
            contractor.project_begdat
        );
        AddValue(
            values,
            columns,
            "project_enddat",
            "@projectEnddat",
            DbType.Date,
            contractor.project_enddat
        );
        return values;
    }

    private static void MergePrivateHire(PrivateHire target, PrivateHire source)
    {
        target.registration_number ??= source.registration_number;
        if (target.model_code == 0)
            target.model_code = source.model_code;
        if (target.site_code == 0)
            target.site_code = source.site_code;
        target.contracted_to ??= source.contracted_to;
        target.engine_number ??= source.engine_number;
        target.chassis_number ??= source.chassis_number;
        target.year_manufactured ??= source.year_manufactured;
        target.bank_code ??= source.bank_code;
        target.colour ??= source.colour;
        target.tank_capacity ??= source.tank_capacity;
        if (target.contractor_id == 0)
            target.contractor_id = source.contractor_id;
        target.fuel_card ??= source.fuel_card;
        target.fuel_card_receiver ??= source.fuel_card_receiver;
        if (target.take_on_date == default)
            target.take_on_date = source.take_on_date;
        if (target.take_on_odo == 0)
            target.take_on_odo = source.take_on_odo;
        target.return_date ??= source.return_date;
        if (target.return_odo == 0)
            target.return_odo = source.return_odo;
        target.km_tariff ??= source.km_tariff;
        target.daily_tariff ??= source.daily_tariff;
        target.hourly_tariff ??= source.hourly_tariff;
        target.model_desc ??= source.model_desc;
    }

    private static void MergeContractor(
        PrivateHireContractorRecord target,
        PrivateHireContractorRecord source
    )
    {
        target.contractor_name = string.IsNullOrWhiteSpace(target.contractor_name)
            ? source.contractor_name
            : target.contractor_name;
        target.physical_address ??= source.physical_address;
        target.postal_address ??= source.postal_address;
        target.tel_number ??= source.tel_number;
        target.fax_number ??= source.fax_number;
        target.email_address ??= source.email_address;
        target.contact_person ??= source.contact_person;
        target.active ??= source.active;
        target.type ??= source.type;
        target.quotations ??= source.quotations;
        target.project_name ??= source.project_name;
        target.project_begdat ??= source.project_begdat;
        target.project_enddat ??= source.project_enddat;
    }

    private static void ValidatePrivateHire(PrivateHire privateHire)
    {
        if (string.IsNullOrWhiteSpace(privateHire.registration_number))
            throw new ArgumentException("Registration number is required.", nameof(privateHire));
        if (privateHire.take_on_date == default)
            throw new ArgumentException("Take-on date is required.", nameof(privateHire));
    }

    private static PrivateHire MapPrivateHire(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns
    ) =>
        new()
        {
            PHV_code = ReadInt16(reader, "PHV_code") ?? 0,
            registration_number = ReadString(reader, "registration_number"),
            model_code = ReadInt16(reader, "model_code") ?? 0,
            site_code = ReadInt32(reader, "site_code") ?? 0,
            contracted_to = ReadInt32(reader, "contracted_to"),
            engine_number = ReadString(reader, "engine_number"),
            chassis_number = ReadString(reader, "chassis_number"),
            year_manufactured = ReadString(reader, "year_manufactured"),
            bank_code = ReadString(reader, "bank_code"),
            colour = ReadString(reader, "colour"),
            tank_capacity = ReadInt32(reader, "tank_capacity"),
            contractor_id = ReadInt16(reader, "contractor_id") ?? 0,
            fuel_card = ReadString(reader, "fuel_card"),
            fuel_card_receiver = ReadString(reader, "fuel_card_receiver"),
            take_on_date = ReadDateTime(reader, "take_on_date") ?? default,
            take_on_odo = ReadInt32(reader, "take_on_odo") ?? 0,
            return_date = ReadDateTime(reader, "return_date"),
            return_odo = ReadInt32(reader, "return_odo") ?? 0,
            km_tariff = ReadDecimal(reader, "km_tariff"),
            daily_tariff = ReadDecimal(reader, "daily_tariff"),
            hourly_tariff = ReadDecimal(reader, "hourly_tariff"),
            model_desc = ReadString(reader, "model_desc"),
            date_created = ReadDateTimeIfAvailable(reader, columns, "date_created") ?? default,
            date_updated = ReadDateTimeIfAvailable(reader, columns, "date_updated"),
            created_by_user_code = ReadInt32IfAvailable(reader, columns, "created_by_user_code"),
            modified_by_user_code = ReadInt32IfAvailable(reader, columns, "modified_by_user_code"),
            is_deleted = ReadBooleanIfAvailable(reader, columns, "is_deleted") ?? false,
        };

    private static PrivateHireContractorRecord MapContractor(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns
    ) =>
        new()
        {
            contractor_id = ReadInt16(reader, "contractor_id") ?? 0,
            contractor_name = ReadString(reader, "contractor_name"),
            physical_address = ReadString(reader, "physical_address"),
            postal_address = ReadString(reader, "postal_address"),
            tel_number = ReadString(reader, "tel_number"),
            fax_number = ReadString(reader, "fax_number"),
            email_address = ReadString(reader, "email_address"),
            contact_person = ReadString(reader, "contact_person"),
            active = ReadInt16(reader, "active"),
            type = ReadString(reader, "type"),
            quotations = ReadBoolean(reader, "quotations"),
            project_name = ReadString(reader, "project_name"),
            project_begdat = ReadDateTime(reader, "project_begdat"),
            project_enddat = ReadDateTime(reader, "project_enddat"),
            date_created = ReadDateTimeIfAvailable(reader, columns, "date_created"),
            date_updated = ReadDateTimeIfAvailable(reader, columns, "date_updated"),
            created_by_user_code = ReadInt32IfAvailable(reader, columns, "created_by_user_code"),
            modified_by_user_code = ReadInt32IfAvailable(reader, columns, "modified_by_user_code"),
            is_deleted = ReadBooleanIfAvailable(reader, columns, "is_deleted") ?? false,
        };

    private static void AddRequiredValue(
        ICollection<WriteValue> values,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
    {
        if (!columns.ContainsKey(column))
            throw new InvalidOperationException(
                $"The required compatibility column {column} is not available."
            );
        values.Add(new WriteValue(column, parameter, type, value));
    }

    private static void AddValue(
        ICollection<WriteValue> values,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
    {
        if (columns.ContainsKey(column) && value is not null)
            values.Add(new WriteValue(column, parameter, type, value));
    }

    private static void AddParameters(DbCommand command, IEnumerable<WriteValue> values)
    {
        foreach (var value in values)
            AddParameter(command, value.Parameter, value.Type, value.Value);
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string GetActiveFilter(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        columns.ContainsKey("is_deleted") ? "ISNULL([is_deleted], 0) = 0" : "1 = 1";

    private static string GetProjection(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    ) =>
        columns.ContainsKey(column)
            ? $"[{column}] AS [{column}]"
            : $"CAST(NULL AS {GetSqlType(column)}) AS [{column}]";

    private static string GetSqlType(string column) =>
        DateColumns.Contains(column)
            ? column.EndsWith("dat", StringComparison.OrdinalIgnoreCase)
                ? "date"
                : "datetime2"
            : column switch
            {
                "PHV_code" or "contractor_id" or "model_code" or "active" => "smallint",
                "site_code"
                or "contracted_to"
                or "tank_capacity"
                or "take_on_odo"
                or "return_odo" => "int",
                "km_tariff" or "daily_tariff" or "hourly_tariff" => "money",
                "created_by_user_code" or "modified_by_user_code" => "int",
                "quotations" or "is_deleted" => "bit",
                _ => "varchar(1)",
            };

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToString(reader.GetValue(ordinal))?.TrimEnd();
    }

    private static short? ReadInt16(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt16(reader.GetValue(ordinal));
    }

    private static int? ReadInt32(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static decimal? ReadDecimal(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal));
    }

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
            return null;
        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateTime dateTime => dateTime,
            DateTimeOffset dateTimeOffset => dateTimeOffset.DateTime,
            _ when DateTime.TryParse(Convert.ToString(value), out var parsed) => parsed,
            _ => null,
        };
    }

    private static bool? ReadBoolean(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToBoolean(reader.GetValue(ordinal));
    }

    private static DateTime? ReadDateTimeIfAvailable(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    ) => columns.ContainsKey(column) ? ReadDateTime(reader, column) : null;

    private static int? ReadInt32IfAvailable(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    ) => columns.ContainsKey(column) ? ReadInt32(reader, column) : null;

    private static bool? ReadBooleanIfAvailable(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    ) => columns.ContainsKey(column) ? ReadBoolean(reader, column) : null;

    private async Task<ConnectionScope> OpenConnectionAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();
        return new ConnectionScope(connection, shouldClose);
    }

    private static int? UserIdOrNull(int currentUserId) => currentUserId > 0 ? currentUserId : null;

    private sealed record ColumnInfo(string Name, string DataType);

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);

    private sealed class ConnectionScope : IAsyncDisposable
    {
        public ConnectionScope(DbConnection connection, bool shouldClose)
        {
            Connection = connection;
            _shouldClose = shouldClose;
        }

        public DbConnection Connection { get; }
        private readonly bool _shouldClose;

        public async ValueTask DisposeAsync()
        {
            if (_shouldClose)
                await Connection.CloseAsync();
        }
    }
}
