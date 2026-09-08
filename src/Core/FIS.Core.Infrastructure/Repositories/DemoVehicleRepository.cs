using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Reads and writes Demo_vehicles through the legacy columns only. Expanded
/// audit fields are included only when the connected database exposes them,
/// keeping the original client database usable during rollout.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Table and column identifiers come from fixed compatibility allowlists; submitted values are parameters."
)]
public sealed class DemoVehicleRepository : IDemoVehicleRepository
{
    private const string TableName = "Demo_vehicles";
    private const string SiteTableName = "site";

    private static readonly string[] RequiredColumns =
    [
        "demo_vehicle_code",
        "gg_number",
        "reg_number",
        "model_description",
        "year_mnf",
        "site_code",
        "bank_code",
        "tank",
        "colour",
        "engine_number",
        "chassis_number",
    ];

    private static readonly string[] OptionalColumns =
    [
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private readonly FisDbContext _context;

    public DemoVehicleRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<DemoVehicleRecord>> GetAllAsync()
    {
        var schema = await GetSchemaAsync();
        return await QueryAsync(schema);
    }

    public async Task<IReadOnlyList<DemoVehicleRecord>> SearchAsync(
        string searchTerm,
        bool byRegistration
    )
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return [];
        }

        var schema = await GetSchemaAsync();
        return await QueryAsync(schema, searchTerm.Trim(), byRegistration);
    }

    public async Task<DemoVehicleRecord?> GetByIdAsync(int demoVehicleCode)
    {
        var schema = await GetSchemaAsync();
        return (await QueryAsync(schema, demoVehicleCode)).SingleOrDefault();
    }

    public async Task<DemoVehicleRecord> CreateAsync(DemoVehicleInput input, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(input);

        var schema = await GetSchemaAsync();
        var values = BuildValues(input, schema.Columns, currentUserId, includeCreateAudit: true);
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        DbTransaction? transaction = null;
        var committed = false;
        try
        {
            if (!schema.IsIdentity)
            {
                transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable);
                var nextCode = await GetNextCodeAsync(connection, transaction);
                values.Insert(
                    0,
                    new WriteValue("demo_vehicle_code", "@demoVehicleCode", DbType.Int16, nextCode)
                );
                await ExecuteInsertWithoutOutputAsync(connection, transaction, values);
                await transaction.CommitAsync();
                committed = true;
                return (await QueryAsync(schema, nextCode)).Single();
            }

            var code = await ExecuteInsertAsync(connection, transaction, values);
            return (await QueryAsync(schema, code)).Single();
        }
        catch
        {
            if (transaction is not null && !committed)
            {
                await transaction.RollbackAsync();
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }

            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<DemoVehicleRecord> UpdateAsync(
        int demoVehicleCode,
        DemoVehicleInput input,
        int currentUserId
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        var schema = await GetSchemaAsync();
        if (await GetByIdAsync(demoVehicleCode) is null)
        {
            throw new KeyNotFoundException(
                $"Demo vehicle with code {demoVehicleCode} was not found."
            );
        }

        var values = BuildValues(input, schema.Columns, currentUserId, includeCreateAudit: false);
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
            command.CommandText =
                $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [demo_vehicle_code] = @demoVehicleCode AND {GetNotDeletedFilter(string.Empty, schema.Columns)}";
            AddParameters(command, values);
            AddParameter(command, "@demoVehicleCode", DbType.Int32, demoVehicleCode);
            if (await command.ExecuteNonQueryAsync() == 0)
            {
                throw new KeyNotFoundException(
                    $"Demo vehicle with code {demoVehicleCode} was not found."
                );
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        return (await QueryAsync(schema, demoVehicleCode)).Single();
    }

    public async Task DeleteAsync(int demoVehicleCode, int currentUserId)
    {
        var schema = await GetSchemaAsync();
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
            if (schema.Columns.Contains("is_deleted"))
            {
                var assignments = new List<string> { "[is_deleted] = @isDeleted" };
                AddParameter(command, "@isDeleted", DbType.Boolean, true);
                if (schema.Columns.Contains("date_updated"))
                {
                    assignments.Add("[date_updated] = @dateUpdated");
                    AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
                }

                if (schema.Columns.Contains("modified_by_user_code"))
                {
                    assignments.Add("[modified_by_user_code] = @modifiedByUserCode");
                    AddParameter(
                        command,
                        "@modifiedByUserCode",
                        DbType.Int32,
                        currentUserId > 0 ? currentUserId : null
                    );
                }

                command.CommandText =
                    $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", assignments)} WHERE [demo_vehicle_code] = @demoVehicleCode AND {GetNotDeletedFilter(string.Empty, schema.Columns)}";
            }
            else
            {
                command.CommandText =
                    $"DELETE FROM [dbo].[{TableName}] WHERE [demo_vehicle_code] = @demoVehicleCode";
            }

            AddParameter(command, "@demoVehicleCode", DbType.Int32, demoVehicleCode);
            if (await command.ExecuteNonQueryAsync() == 0)
            {
                throw new KeyNotFoundException(
                    $"Demo vehicle with code {demoVehicleCode} was not found."
                );
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

    private async Task<DemoVehicleSchema> GetSchemaAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var columns = await GetColumnsAsync(connection, TableName);
            var missingColumns = RequiredColumns
                .Where(column => !columns.Contains(column))
                .ToArray();
            if (missingColumns.Length > 0)
            {
                throw new InvalidOperationException(
                    $"The required Demo_vehicles compatibility columns are not available: {string.Join(", ", missingColumns)}"
                );
            }

            var siteColumns = await GetColumnsAsync(connection, SiteTableName);
            var hasSiteDescription =
                siteColumns.Contains("site_code") && siteColumns.Contains("description");
            var identityCommand = connection.CreateCommand();
            identityCommand.CommandText =
                $"SELECT COLUMNPROPERTY(OBJECT_ID(N'[dbo].[{TableName}]'), N'demo_vehicle_code', 'IsIdentity')";
            var identityValue = await identityCommand.ExecuteScalarAsync();
            return new DemoVehicleSchema(
                columns,
                Convert.ToInt32(identityValue) == 1,
                hasSiteDescription,
                siteColumns
            );
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<HashSet<string>> GetColumnsAsync(
        DbConnection connection,
        string tableName
    )
    {
        await using var command = connection.CreateCommand();
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

    private async Task<List<DemoVehicleRecord>> QueryAsync(DemoVehicleSchema schema) =>
        await QueryAsync(schema, null, false, null);

    private async Task<List<DemoVehicleRecord>> QueryAsync(
        DemoVehicleSchema schema,
        int? demoVehicleCode
    ) => await QueryAsync(schema, null, false, demoVehicleCode);

    private async Task<List<DemoVehicleRecord>> QueryAsync(
        DemoVehicleSchema schema,
        string? searchTerm,
        bool byRegistration
    ) => await QueryAsync(schema, searchTerm, byRegistration, null);

    private async Task<List<DemoVehicleRecord>> QueryAsync(
        DemoVehicleSchema schema,
        string? searchTerm,
        bool byRegistration,
        int? demoVehicleCode,
        bool _ = true
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
            var siteProjection = schema.HasSiteDescription
                ? "s.[description] AS [site_description]"
                : "CAST(NULL AS nvarchar(255)) AS [site_description]";
            var siteJoin = schema.HasSiteDescription
                ? $"LEFT JOIN [dbo].[{SiteTableName}] AS s ON d.[site_code] = s.[site_code] AND {GetNotDeletedFilter("s", schema.SiteColumns)}"
                : string.Empty;
            var conditions = new List<string> { GetNotDeletedFilter("d", schema.Columns) };

            if (searchTerm is not null)
            {
                var searchColumn = byRegistration ? "reg_number" : "gg_number";
                conditions.Add($"LOWER(COALESCE(d.[{searchColumn}], '')) LIKE @search");
                AddParameter(
                    command,
                    "@search",
                    DbType.String,
                    $"%{searchTerm.ToLowerInvariant()}%"
                );
            }

            if (demoVehicleCode.HasValue)
            {
                conditions.Add("d.[demo_vehicle_code] = @demoVehicleCode");
                AddParameter(command, "@demoVehicleCode", DbType.Int32, demoVehicleCode.Value);
            }

            var projection = string.Join(
                ", ",
                RequiredColumns
                    .Select(column => $"d.[{column}] AS [{column}]")
                    .Append(siteProjection)
                    .Concat(
                        OptionalColumns.Select(column =>
                            GetOptionalProjection("d", schema.Columns, column)
                        )
                    )
            );
            command.CommandText =
                $"SELECT {projection} FROM [dbo].[{TableName}] AS d {siteJoin} WHERE {string.Join(" AND ", conditions)} ORDER BY COALESCE(d.[gg_number], ''), d.[demo_vehicle_code]";

            var results = new List<DemoVehicleRecord>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapRecord(reader));
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

    private static List<WriteValue> BuildValues(
        DemoVehicleInput input,
        IReadOnlySet<string> columns,
        int currentUserId,
        bool includeCreateAudit
    )
    {
        var values = new List<WriteValue>
        {
            new("gg_number", "@ggNumber", DbType.String, input.GgNumber),
            new("reg_number", "@registrationNumber", DbType.String, input.RegistrationNumber),
            new("model_description", "@modelDescription", DbType.String, input.ModelDescription),
            new("site_code", "@siteCode", DbType.Int16, input.SiteCode),
            new("year_mnf", "@yearManufactured", DbType.Int32, input.YearManufactured),
            new("bank_code", "@bankCode", DbType.String, input.BankCode),
            new("colour", "@colour", DbType.String, input.Colour),
            new("tank", "@tank", DbType.Int16, input.Tank),
            new("engine_number", "@engineNumber", DbType.String, input.EngineNumber),
            new("chassis_number", "@chassisNumber", DbType.String, input.ChassisNumber),
        };

        if (includeCreateAudit)
        {
            AddOptionalValue(
                values,
                columns,
                "date_created",
                "@dateCreated",
                DbType.DateTime2,
                DateTime.UtcNow
            );
            AddOptionalValue(
                values,
                columns,
                "created_by_user_code",
                "@createdByUserCode",
                DbType.Int32,
                currentUserId > 0 ? currentUserId : null
            );
            AddOptionalValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);
        }
        else
        {
            AddOptionalValue(
                values,
                columns,
                "date_updated",
                "@dateUpdated",
                DbType.DateTime2,
                DateTime.UtcNow
            );
            AddOptionalValue(
                values,
                columns,
                "modified_by_user_code",
                "@modifiedByUserCode",
                DbType.Int32,
                currentUserId > 0 ? currentUserId : null
            );
        }

        return values;
    }

    private static async Task<int> ExecuteInsertAsync(
        DbConnection connection,
        DbTransaction? transaction,
        IReadOnlyList<WriteValue> values
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"INSERT INTO [dbo].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[demo_vehicle_code] VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
        AddParameters(command, values);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task ExecuteInsertWithoutOutputAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlyList<WriteValue> values
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"INSERT INTO [dbo].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
        AddParameters(command, values);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> GetNextCodeAsync(
        DbConnection connection,
        DbTransaction transaction
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"SELECT COALESCE(MAX(CAST([demo_vehicle_code] AS int)), 0) + 1 FROM [dbo].[{TableName}] WITH (UPDLOCK, HOLDLOCK)";
        var nextCode = Convert.ToInt32(await command.ExecuteScalarAsync());
        if (nextCode > short.MaxValue)
        {
            throw new InvalidOperationException("The demo vehicle code range is full.");
        }

        return nextCode;
    }

    private static string GetOptionalProjection(
        string alias,
        IReadOnlySet<string> columns,
        string column
    )
    {
        if (columns.Contains(column))
        {
            return $"{alias}.[{column}] AS [{column}]";
        }

        var sqlType = column switch
        {
            "date_created" or "date_updated" => "datetime2",
            "created_by_user_code" or "modified_by_user_code" => "int",
            "is_deleted" => "bit",
            _ => "nvarchar(255)",
        };
        return $"CAST(NULL AS {sqlType}) AS [{column}]";
    }

    private static string GetNotDeletedFilter(string alias, IReadOnlySet<string> columns)
    {
        if (!columns.Contains("is_deleted"))
        {
            return "1 = 1";
        }

        var prefix = string.IsNullOrWhiteSpace(alias) ? string.Empty : $"{alias}.";
        return $"({prefix}[is_deleted] = 0 OR {prefix}[is_deleted] IS NULL)";
    }

    private static DemoVehicleRecord MapRecord(DbDataReader reader) =>
        new(
            ReadInt32(reader, "demo_vehicle_code") ?? 0,
            ReadString(reader, "gg_number"),
            ReadString(reader, "reg_number"),
            ReadString(reader, "model_description"),
            ReadInt32(reader, "year_mnf"),
            ReadInt16(reader, "site_code"),
            ReadString(reader, "site_description"),
            ReadString(reader, "bank_code"),
            ReadInt16(reader, "tank"),
            ReadString(reader, "colour"),
            ReadString(reader, "engine_number"),
            ReadString(reader, "chassis_number"),
            ReadDateTime(reader, "date_created"),
            ReadDateTime(reader, "date_updated"),
            ReadInt32(reader, "created_by_user_code"),
            ReadInt32(reader, "modified_by_user_code")
        );

    private static void AddOptionalValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> columns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
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

    private static string? ReadString(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : reader[column]?.ToString();

    private static int? ReadInt32(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToInt32(reader[column]);

    private static short? ReadInt16(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToInt16(reader[column]);

    private static DateTime? ReadDateTime(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToDateTime(reader[column]);

    private sealed record DemoVehicleSchema(
        IReadOnlySet<string> Columns,
        bool IsIdentity,
        bool HasSiteDescription,
        IReadOnlySet<string> SiteColumns
    );

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}
