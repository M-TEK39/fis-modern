using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Persists vehicle sources against both the original legacy table and the
/// expanded table. Optional modern audit columns and legacy contact fields
/// are negotiated at runtime so a missing column cannot break a query.
/// </summary>
public sealed class VehicleSourceRepository : IVehicleSourceRepository
{
    private const string TableName = "vehicle_source";

    private static readonly string[] RequiredColumns =
    [
        "vs_code",
        "name",
        "physical_address",
        "postal_address",
        "tel_number",
        "fax_number"
    ];

    private static readonly string[] OptionalColumns =
    [
        "email_address",
        "contact_person",
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted"
    ];

    private readonly FisDbContext _context;

    public VehicleSourceRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<VehicleSourcePage> GetPageAsync()
    {
        var schema = await GetSchemaAsync();
        var items = await QueryAsync(schema);
        return new(items, new VehicleSourceCapabilities(
            schema.Columns.Contains("email_address"),
            schema.Columns.Contains("contact_person")));
    }

    public async Task<VehicleSourceCapabilities> GetCapabilitiesAsync()
    {
        var schema = await GetSchemaAsync();
        return new(
            schema.Columns.Contains("email_address"),
            schema.Columns.Contains("contact_person"));
    }

    public async Task<VehicleSourceRecord?> GetByIdAsync(byte sourceCode)
    {
        var schema = await GetSchemaAsync();
        return (await QueryAsync(schema, sourceCode)).SingleOrDefault();
    }

    public async Task<VehicleSourceRecord> CreateAsync(VehicleSourceInput source, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(source);

        var schema = await GetSchemaAsync();
        EnsureOptionalFieldsAvailable(source, schema);

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        DbTransaction? transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        DbTransaction? ownedTransaction = null;
        var committed = false;
        try
        {
            if (transaction is null && !schema.IsIdentity)
            {
                ownedTransaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable);
                transaction = ownedTransaction;
            }

            var values = BuildValues(source, schema, currentUserId, DateTime.UtcNow);
            byte sourceCode;
            if (schema.IsIdentity)
            {
                sourceCode = await ExecuteInsertAsync(connection, transaction, values);
            }
            else
            {
                sourceCode = await GetNextSourceCodeAsync(connection, transaction);
                values.Insert(0, new WriteValue("vs_code", "@sourceCode", DbType.Byte, sourceCode));
                await ExecuteInsertWithoutOutputAsync(connection, transaction, values);
            }

            if (ownedTransaction is not null)
            {
                await ownedTransaction.CommitAsync();
                committed = true;
            }

            return (await QueryAsync(schema, sourceCode)).Single();
        }
        catch
        {
            if (ownedTransaction is not null && !committed)
            {
                await ownedTransaction.RollbackAsync();
            }

            throw;
        }
        finally
        {
            if (ownedTransaction is not null)
            {
                await ownedTransaction.DisposeAsync();
            }
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<VehicleSourceRecord> UpdateAsync(byte sourceCode, VehicleSourceInput source, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(source);

        var schema = await GetSchemaAsync();
        var existing = (await QueryAsync(schema, sourceCode)).SingleOrDefault();
        if (existing is null)
        {
            throw new KeyNotFoundException($"Vehicle source with vs_code {sourceCode} was not found.");
        }

        EnsureOptionalFieldsAvailable(source, schema);
        var values = BuildValues(source, schema, currentUserId, DateTime.UtcNow, includeCreateAudit: false);
        await ExecuteUpdateAsync(sourceCode, values);
        return (await QueryAsync(schema, sourceCode)).Single();
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The table, columns, and predicates are selected only from fixed compatibility branches; values are parameterized.")]
    private async Task<List<VehicleSourceRecord>> QueryAsync(SourceSchema schema, byte? sourceCode = null)
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
            var projection = RequiredColumns
                .Select(column => $"[{column}] AS [{column}]")
                .Concat(OptionalColumns.Select(column => GetOptionalProjection(schema.Columns, column)))
                .ToArray();
            var conditions = new List<string> { GetNotDeletedFilter(schema.Columns) };
            if (sourceCode.HasValue)
            {
                conditions.Add("[vs_code] = @sourceCode");
                AddParameter(command, "@sourceCode", DbType.Byte, sourceCode.Value);
            }

            command.CommandText = $"SELECT {string.Join(", ", projection)} FROM [dbo].[{TableName}] WHERE {string.Join(" AND ", conditions)} ORDER BY COALESCE([name], ''), [vs_code]";

            var results = new List<VehicleSourceRecord>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapRecord(reader, schema.Columns));
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
        Justification = "The INSERT statement is composed only from fixed compatibility columns and all values are parameters.")]
    private static async Task<byte> ExecuteInsertAsync(
        DbConnection connection,
        DbTransaction? transaction,
        IReadOnlyList<WriteValue> values)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"INSERT INTO [dbo].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[vs_code] VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
        AddParameters(command, values);
        return Convert.ToByte(await command.ExecuteScalarAsync());
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The INSERT statement is composed only from fixed compatibility columns and all values are parameters.")]
    private static async Task ExecuteInsertWithoutOutputAsync(
        DbConnection connection,
        DbTransaction? transaction,
        IReadOnlyList<WriteValue> values)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"INSERT INTO [dbo].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
        AddParameters(command, values);
        await command.ExecuteNonQueryAsync();
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The UPDATE statement is composed only from fixed compatibility columns and all values are parameters.")]
    private async Task ExecuteUpdateAsync(byte sourceCode, IReadOnlyList<WriteValue> values)
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
            command.CommandText = $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [vs_code] = @sourceCode";
            AddParameters(command, values);
            AddParameter(command, "@sourceCode", DbType.Byte, sourceCode);
            if (await command.ExecuteNonQueryAsync() == 0)
            {
                throw new KeyNotFoundException($"Vehicle source with vs_code {sourceCode} was not found.");
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

    private async Task<SourceSchema> GetSchemaAsync()
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
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    columns.Add(reader.GetString(0));
                }
            }

            var missingColumns = RequiredColumns.Where(column => !columns.Contains(column)).ToArray();
            if (missingColumns.Length > 0)
            {
                throw new InvalidOperationException($"The required vehicle source compatibility columns are not available: {string.Join(", ", missingColumns)}");
            }

            await using var identityCommand = connection.CreateCommand();
            identityCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            identityCommand.CommandText = "SELECT COLUMNPROPERTY(OBJECT_ID(N'[dbo].[vehicle_source]'), N'vs_code', 'IsIdentity')";
            var identityValue = await identityCommand.ExecuteScalarAsync();
            return new SourceSchema(columns, Convert.ToInt32(identityValue) == 1);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<byte> GetNextSourceCodeAsync(DbConnection connection, DbTransaction? transaction)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COALESCE(MAX(CAST([vs_code] AS int)), 0) + 1 FROM [dbo].[vehicle_source] WITH (UPDLOCK, HOLDLOCK)";
        var nextValue = Convert.ToInt32(await command.ExecuteScalarAsync());
        if (nextValue > byte.MaxValue)
        {
            throw new InvalidOperationException("The vehicle source code range is full.");
        }

        return Convert.ToByte(nextValue);
    }

    private static List<WriteValue> BuildValues(
        VehicleSourceInput source,
        SourceSchema schema,
        int currentUserId,
        DateTime now,
        bool includeCreateAudit = true)
    {
        var values = new List<WriteValue>
        {
            new("name", "@name", DbType.String, source.Name),
            new("physical_address", "@physicalAddress", DbType.String, source.PhysicalAddress),
            new("postal_address", "@postalAddress", DbType.String, source.PostalAddress),
            new("tel_number", "@telephoneNumber", DbType.String, source.TelephoneNumber),
            new("fax_number", "@faxNumber", DbType.String, source.FaxNumber)
        };

        AddOptionalStringValue(values, schema.Columns, "email_address", "@emailAddress", source.EmailAddress);
        AddOptionalStringValue(values, schema.Columns, "contact_person", "@contactPerson", source.ContactPerson);

        if (includeCreateAudit)
        {
            AddOptionalValue(values, schema.Columns, "date_created", "@dateCreated", DbType.DateTime2, now);
            AddOptionalValue(values, schema.Columns, "created_by_user_code", "@createdByUserCode", DbType.Int32, currentUserId > 0 ? currentUserId : null);
            AddOptionalValue(values, schema.Columns, "is_deleted", "@isDeleted", DbType.Boolean, false);
        }
        else
        {
            AddOptionalValue(values, schema.Columns, "date_updated", "@dateUpdated", DbType.DateTime2, now);
            AddOptionalValue(values, schema.Columns, "modified_by_user_code", "@modifiedByUserCode", DbType.Int32, currentUserId > 0 ? currentUserId : null);
        }

        return values;
    }

    private static void EnsureOptionalFieldsAvailable(VehicleSourceInput source, SourceSchema schema)
    {
        if (!schema.Columns.Contains("email_address") && !string.IsNullOrWhiteSpace(source.EmailAddress))
        {
            throw new VehicleSourceFieldUnavailableException("email_address");
        }

        if (!schema.Columns.Contains("contact_person") && !string.IsNullOrWhiteSpace(source.ContactPerson))
        {
            throw new VehicleSourceFieldUnavailableException("contact_person");
        }
    }

    private static void AddOptionalStringValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> columns,
        string column,
        string parameter,
        string value)
    {
        if (columns.Contains(column))
        {
            values.Add(new WriteValue(column, parameter, DbType.String, value));
        }
    }

    private static void AddOptionalValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> columns,
        string column,
        string parameter,
        DbType type,
        object? value)
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

    private static VehicleSourceRecord MapRecord(DbDataReader reader, IReadOnlySet<string> columns)
        => new(
            ReadByte(reader, "vs_code"),
            ReadString(reader, "name"),
            ReadString(reader, "physical_address"),
            ReadString(reader, "postal_address"),
            ReadString(reader, "tel_number"),
            ReadString(reader, "fax_number"),
            ReadStringIfAvailable(reader, columns, "email_address"),
            ReadStringIfAvailable(reader, columns, "contact_person"),
            ReadDateTimeIfAvailable(reader, columns, "date_created"),
            ReadDateTimeIfAvailable(reader, columns, "date_updated"),
            ReadInt32IfAvailable(reader, columns, "created_by_user_code"),
            ReadInt32IfAvailable(reader, columns, "modified_by_user_code"));

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
            _ => "nvarchar(255)"
        };
        return $"CAST(NULL AS {sqlType}) AS [{column}]";
    }

    private static string GetNotDeletedFilter(IReadOnlySet<string> columns)
        => columns.Contains("is_deleted") ? "([is_deleted] = 0 OR [is_deleted] IS NULL)" : "1 = 1";

    private static string? ReadString(DbDataReader reader, string column)
        => reader[column] is DBNull ? null : reader[column]?.ToString();

    private static string? ReadStringIfAvailable(DbDataReader reader, IReadOnlySet<string> columns, string column)
        => columns.Contains(column) ? ReadString(reader, column) : null;

    private static byte ReadByte(DbDataReader reader, string column)
        => reader[column] is DBNull ? (byte)0 : Convert.ToByte(reader[column]);

    private static DateTime? ReadDateTimeIfAvailable(DbDataReader reader, IReadOnlySet<string> columns, string column)
        => columns.Contains(column) && reader[column] is not DBNull ? Convert.ToDateTime(reader[column]) : null;

    private static int? ReadInt32IfAvailable(DbDataReader reader, IReadOnlySet<string> columns, string column)
        => columns.Contains(column) && reader[column] is not DBNull ? Convert.ToInt32(reader[column]) : null;

    private sealed record SourceSchema(IReadOnlySet<string> Columns, bool IsIdentity);

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}
