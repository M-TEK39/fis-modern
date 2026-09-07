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
/// Persists the model table against the original legacy schema and the
/// expanded schema. The legacy model fields are always selected and written;
/// type and audit fields are negotiated at runtime.
/// </summary>
public sealed class ModelRepository : IModelRepository
{
    private const string TableName = "model";

    private static readonly string[] RequiredColumns =
    [
        "model_code",
        "make_code",
        "unit_of_measure_code",
        "fuel_type_code",
        "licence_code",
        "maint_trigger_code",
        "class_code",
        "model_description",
        "engine_type",
        "engine_capacity",
        "rated_power",
        "fuel_tank_capacity",
        "target_consumption",
        "target_tyre_life",
        "service_interval",
        "vemm_code",
        "licence_fee_code",
        "gvm",
        "transmission",
        "wesbank_kilos_per_litre"
    ];

    private static readonly string[] OptionalColumns =
    [
        "type_code",
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted"
    ];

    private readonly FisDbContext _context;

    public ModelRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Model?> GetByIdAsync(short modelCode)
        => (await QueryAsync(
            "[model].[model_code] = @modelCode",
            command => AddParameter(command, "@modelCode", DbType.Int16, modelCode)))
            .SingleOrDefault();

    public async Task<Model?> GetByNameAsync(string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return null;
        }

        return (await QueryAsync(
            "LOWER([model].[model_description]) = @modelDescription",
            command => AddParameter(command, "@modelDescription", DbType.String, modelName.Trim().ToLowerInvariant())))
            .SingleOrDefault();
    }

    public async Task<IEnumerable<Model>> GetAllModelsAsync()
        => await QueryAsync();

    public async Task<IEnumerable<Model>> GetModelsByMakeAsync(short makeCode)
        => await QueryAsync(
            "[model].[make_code] = @makeCode",
            command => AddParameter(command, "@makeCode", DbType.Int16, makeCode));

    public async Task<IEnumerable<Model>> GetModelsByEngineTypeAsync(string engineType)
    {
        if (string.IsNullOrWhiteSpace(engineType))
        {
            return await GetAllModelsAsync();
        }

        return await QueryAsync(
            "LOWER(COALESCE([model].[engine_type], '')) LIKE @engineType",
            command => AddParameter(command, "@engineType", DbType.String, $"%{engineType.Trim().ToLowerInvariant()}%"));
    }

    public async Task<IEnumerable<Model>> SearchModelsAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllModelsAsync();
        }

        return await QueryAsync(
            "(LOWER([model].[model_description]) LIKE @searchTerm OR "
            + "LOWER(COALESCE([model].[engine_type], '')) LIKE @searchTerm OR "
            + "LOWER(COALESCE([make].[make_description], '')) LIKE @searchTerm)",
            command => AddParameter(command, "@searchTerm", DbType.String, $"%{searchTerm.Trim().ToLowerInvariant()}%"));
    }

    public async Task<ModelDeleteCheck> GetDeleteCheckAsync(short modelCode)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            var vehicleCount = await TableExistsAsync(connection, "dbo", "vehicle_master", transaction)
                ? await CountAsync(
                    connection,
                    "SELECT COUNT(1) FROM [dbo].[vehicle_master] WHERE [model_code] = @modelCode",
                    modelCode,
                    transaction)
                : 0;
            return new ModelDeleteCheck(vehicleCount);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<Model> CreateAsync(Model model, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(model);

        var availableColumns = await GetAvailableColumnsAsync();
        var now = DateTime.UtcNow;
        var values = BuildLegacyValues(model);
        AddOptionalValue(values, availableColumns, "type_code", "@typeCode", DbType.Int16, model.type_code);
        AddCreateAuditValues(values, availableColumns, currentUserId, now);

        model.date_created = now;
        model.created_by_user_code = currentUserId > 0 ? currentUserId : null;
        model.is_deleted = false;
        model.model_code = await ExecuteInsertAsync(values);
        return model;
    }

    public async Task<Model> UpdateAsync(Model model, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(model);

        var existing = await GetByIdAsync(model.model_code)
            ?? throw new InvalidOperationException($"Model with model_code {model.model_code} not found");
        var availableColumns = await GetAvailableColumnsAsync();
        var now = DateTime.UtcNow;
        var values = BuildLegacyValues(model);
        AddOptionalValue(values, availableColumns, "type_code", "@typeCode", DbType.Int16, model.type_code);
        AddUpdateAuditValues(values, availableColumns, currentUserId, now);

        await ExecuteUpdateAsync(model.model_code, values);
        model.date_created = existing.date_created;
        model.created_by_user_code = existing.created_by_user_code;
        model.date_updated = now;
        model.modified_by_user_code = currentUserId > 0 ? currentUserId : null;
        model.is_deleted = existing.is_deleted;
        model.Make = existing.Make;
        return model;
    }

    public async Task<Model> UpdateLicenceFeeAsync(short modelCode, short licenceFeeCode, int currentUserId)
    {
        var existing = await GetByIdAsync(modelCode)
            ?? throw new InvalidOperationException($"Model with model_code {modelCode} not found");
        var availableColumns = await GetAvailableColumnsAsync();
        var values = new List<WriteValue>
        {
            new("licence_fee_code", "@licenceFeeCode", DbType.Int16, licenceFeeCode)
        };
        AddUpdateAuditValues(values, availableColumns, currentUserId, DateTime.UtcNow);

        await ExecuteUpdateAsync(modelCode, values);
        return await GetByIdAsync(modelCode) ?? existing;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The DELETE or soft-delete statement is selected from fixed compatibility branches and the model code is parameterized.")]
    public async Task DeleteAsync(short modelCode, int currentUserId)
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
                var assignments = new List<string> { "[is_deleted] = @isDeleted" };
                AddParameter(command, "@isDeleted", DbType.Boolean, true);

                if (availableColumns.Contains("date_updated"))
                {
                    assignments.Add("[date_updated] = @dateUpdated");
                    AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
                }

                if (availableColumns.Contains("modified_by_user_code"))
                {
                    assignments.Add("[modified_by_user_code] = @modifiedByUserCode");
                    AddParameter(command, "@modifiedByUserCode", DbType.Int32, currentUserId > 0 ? currentUserId : null);
                }

                command.CommandText = $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", assignments)} WHERE [model_code] = @modelCode";
            }
            else
            {
                command.CommandText = $"DELETE FROM [dbo].[{TableName}] WHERE [model_code] = @modelCode";
            }

            AddParameter(command, "@modelCode", DbType.Int16, modelCode);
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

    private static List<WriteValue> BuildLegacyValues(Model model)
        =>
        [
            new("make_code", "@makeCode", DbType.Int16, model.make_code),
            new("unit_of_measure_code", "@unitOfMeasureCode", DbType.Int16, model.unit_of_measure_code),
            new("fuel_type_code", "@fuelTypeCode", DbType.Int16, model.fuel_type_code),
            new("licence_code", "@licenceCode", DbType.Int16, model.licence_code),
            new("maint_trigger_code", "@maintTriggerCode", DbType.Int16, model.maint_trigger_code),
            new("class_code", "@classCode", DbType.Int16, model.class_code),
            new("model_description", "@modelDescription", DbType.String, model.model_description),
            new("engine_type", "@engineType", DbType.String, model.engine_type),
            new("engine_capacity", "@engineCapacity", DbType.Int16, model.engine_capacity),
            new("rated_power", "@ratedPower", DbType.Int16, model.rated_power),
            new("fuel_tank_capacity", "@fuelTankCapacity", DbType.Int16, model.fuel_tank_capacity),
            new("target_consumption", "@targetConsumption", DbType.Decimal, model.target_consumption),
            new("target_tyre_life", "@targetTyreLife", DbType.Int32, model.target_tyre_life),
            new("service_interval", "@serviceInterval", DbType.Int32, model.service_interval),
            new("vemm_code", "@vemmCode", DbType.String, model.vemm_code),
            new("licence_fee_code", "@licenceFeeCode", DbType.Int16, model.licence_fee_code),
            new("gvm", "@gvm", DbType.Int32, model.gvm),
            new("transmission", "@transmission", DbType.String, model.transmission),
            new("wesbank_kilos_per_litre", "@wesbankKilosPerLitre", DbType.Decimal, model.wesbank_kilos_per_litre)
        ];

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SELECT list is composed only from fixed legacy columns and allowlisted optional columns; predicates and values are parameterized.")]
    private async Task<List<Model>> QueryAsync(string? predicate = null, Action<DbCommand>? configure = null)
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
            var projection = RequiredColumns
                .Select(column => $"[model].[{column}] AS [{column}]")
                .Concat(OptionalColumns.Select(column => GetOptionalProjection(availableColumns, column)))
                .Append("[make].[make_description] AS [make_description]")
                .ToArray();
            var conditions = new List<string> { GetNotDeletedFilter(availableColumns) };
            if (!string.IsNullOrWhiteSpace(predicate))
            {
                conditions.Add(predicate);
            }

            command.CommandText = $"SELECT {string.Join(", ", projection)} FROM [dbo].[{TableName}] AS [model] LEFT JOIN [dbo].[make] AS [make] ON [make].[make_code] = [model].[make_code] WHERE {string.Join(" AND ", conditions)} ORDER BY [make].[make_description], [model].[model_description], [model].[model_code]";
            configure?.Invoke(command);

            var results = new List<Model>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapModel(reader, availableColumns));
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
        Justification = "The INSERT statement is composed from fixed legacy and allowlisted optional columns; all values are parameters.")]
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
            command.CommandText = $"INSERT INTO [dbo].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[model_code] VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
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
        Justification = "The UPDATE statement is composed from fixed legacy and allowlisted optional columns; all values are parameters.")]
    private async Task ExecuteUpdateAsync(short modelCode, IReadOnlyList<WriteValue> values)
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
            command.CommandText = $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [model_code] = @modelCode";
            AddParameters(command, values);
            AddParameter(command, "@modelCode", DbType.Int16, modelCode);
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

            var missingColumns = RequiredColumns.Where(column => !columns.Contains(column)).ToArray();
            if (missingColumns.Length > 0)
            {
                throw new InvalidOperationException($"The required model compatibility columns are not available: {string.Join(", ", missingColumns)}");
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

    private static void AddCreateAuditValues(ICollection<WriteValue> values, IReadOnlySet<string> columns, int currentUserId, DateTime now)
    {
        AddOptionalValue(values, columns, "date_created", "@dateCreated", DbType.DateTime2, now);
        AddOptionalValue(values, columns, "created_by_user_code", "@createdByUserCode", DbType.Int32, currentUserId > 0 ? currentUserId : null);
        AddOptionalValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);
    }

    private static void AddUpdateAuditValues(ICollection<WriteValue> values, IReadOnlySet<string> columns, int currentUserId, DateTime now)
    {
        AddOptionalValue(values, columns, "date_updated", "@dateUpdated", DbType.DateTime2, now);
        AddOptionalValue(values, columns, "modified_by_user_code", "@modifiedByUserCode", DbType.Int32, currentUserId > 0 ? currentUserId : null);
    }

    private static void AddOptionalValue(ICollection<WriteValue> values, IReadOnlySet<string> columns, string column, string parameter, DbType type, object? value)
    {
        if (columns.Contains(column))
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

    private static Model MapModel(DbDataReader reader, IReadOnlySet<string> availableColumns)
    {
        var modelCode = ReadInt16(reader, "model_code") ?? 0;
        var makeCode = ReadInt16(reader, "make_code") ?? 0;
        var makeDescription = ReadString(reader, "make_description");

        return new Model
        {
            model_code = modelCode,
            make_code = makeCode,
            unit_of_measure_code = ReadInt16(reader, "unit_of_measure_code") ?? 0,
            fuel_type_code = ReadInt16(reader, "fuel_type_code") ?? 0,
            licence_code = ReadInt16(reader, "licence_code") ?? 0,
            maint_trigger_code = ReadInt16(reader, "maint_trigger_code"),
            class_code = ReadInt16(reader, "class_code") ?? 0,
            type_code = ReadInt16IfAvailable(reader, availableColumns, "type_code"),
            model_description = ReadString(reader, "model_description") ?? string.Empty,
            engine_type = ReadString(reader, "engine_type"),
            engine_capacity = ReadInt16(reader, "engine_capacity"),
            rated_power = ReadInt16(reader, "rated_power"),
            fuel_tank_capacity = ReadInt16(reader, "fuel_tank_capacity"),
            target_consumption = ReadDecimal(reader, "target_consumption"),
            target_tyre_life = ReadInt32(reader, "target_tyre_life"),
            service_interval = ReadInt32(reader, "service_interval"),
            vemm_code = ReadString(reader, "vemm_code"),
            licence_fee_code = ReadInt16(reader, "licence_fee_code"),
            gvm = ReadInt32(reader, "gvm"),
            transmission = ReadString(reader, "transmission"),
            wesbank_kilos_per_litre = ReadDecimal(reader, "wesbank_kilos_per_litre"),
            date_created = ReadDateTimeIfAvailable(reader, availableColumns, "date_created") ?? DateTime.MinValue,
            date_updated = ReadDateTimeIfAvailable(reader, availableColumns, "date_updated"),
            created_by_user_code = ReadInt32IfAvailable(reader, availableColumns, "created_by_user_code"),
            modified_by_user_code = ReadInt32IfAvailable(reader, availableColumns, "modified_by_user_code"),
            is_deleted = ReadBooleanIfAvailable(reader, availableColumns, "is_deleted") ?? false,
            Make = makeDescription is null
                ? null
                : new Make { make_code = makeCode, make_description = makeDescription }
        };
    }

    private static string GetOptionalProjection(IReadOnlySet<string> columns, string column)
    {
        if (columns.Contains(column))
        {
            return $"[model].[{column}] AS [{column}]";
        }

        var sqlType = column switch
        {
            "type_code" => "smallint",
            "date_created" or "date_updated" => "datetime2",
            "created_by_user_code" or "modified_by_user_code" => "int",
            "is_deleted" => "bit",
            _ => "varchar(1)"
        };
        return $"CAST(NULL AS {sqlType}) AS [{column}]";
    }

    private static string GetNotDeletedFilter(IReadOnlySet<string> columns)
        => columns.Contains("is_deleted") ? "([model].[is_deleted] = 0 OR [model].[is_deleted] IS NULL)" : "1 = 1";

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The count query is a fixed repository statement and the model code is parameterized.")]
    private static async Task<int> CountAsync(DbConnection connection, string sql, short modelCode, DbTransaction? transaction)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        AddParameter(command, "@modelCode", DbType.Int16, modelCode);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string schema, string table, DbTransaction? transaction)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT COUNT(1)
            FROM [INFORMATION_SCHEMA].[TABLES]
            WHERE [TABLE_SCHEMA] = @schema
              AND [TABLE_NAME] = @table
            """;
        AddParameter(command, "@schema", DbType.String, schema);
        AddParameter(command, "@table", DbType.String, table);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    private static string? ReadString(DbDataReader reader, string column) => reader[column] is DBNull ? null : reader[column]?.ToString();

    private static short? ReadInt16(DbDataReader reader, string column) => reader[column] is DBNull ? null : Convert.ToInt16(reader[column]);

    private static int? ReadInt32(DbDataReader reader, string column) => reader[column] is DBNull ? null : Convert.ToInt32(reader[column]);

    private static decimal? ReadDecimal(DbDataReader reader, string column) => reader[column] is DBNull ? null : Convert.ToDecimal(reader[column]);

    private static short? ReadInt16IfAvailable(DbDataReader reader, IReadOnlySet<string> columns, string column)
        => columns.Contains(column) && reader[column] is not DBNull ? Convert.ToInt16(reader[column]) : null;

    private static bool? ReadBooleanIfAvailable(DbDataReader reader, IReadOnlySet<string> columns, string column)
        => columns.Contains(column) && reader[column] is not DBNull ? Convert.ToBoolean(reader[column]) : null;

    private static int? ReadInt32IfAvailable(DbDataReader reader, IReadOnlySet<string> columns, string column)
        => columns.Contains(column) && reader[column] is not DBNull ? Convert.ToInt32(reader[column]) : null;

    private static DateTime? ReadDateTimeIfAvailable(DbDataReader reader, IReadOnlySet<string> columns, string column)
        => columns.Contains(column) && reader[column] is not DBNull ? Convert.ToDateTime(reader[column]) : null;

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}
