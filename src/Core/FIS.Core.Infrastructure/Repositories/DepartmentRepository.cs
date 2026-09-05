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
/// Persists the legacy department table against both the client schema and
/// the expanded schema. The legacy columns are always selected and written;
/// newer audit and configuration columns are negotiated at runtime.
/// </summary>
public sealed class DepartmentRepository : IDepartmentRepository
{
    private const string TableName = "department";

    private static readonly string[] RequiredColumns =
    [
        "department_code",
        "company_code",
        "description",
        "res_person",
        "address1",
        "address2",
        "address3",
        "postal_code",
        "telephone",
        "fax",
        "net_address",
        "Department_number",
        "cell_number",
        "notes",
        "dept_active",
        "user_access_code"
    ];

    private static readonly string[] OptionalColumns =
    [
        "department_abbr",
        "bas_installation_code",
        "clo_email",
        "telephone2",
        "fax2",
        "financial_system_code",
        "financial_system_active",
        "financial_system_activate_date",
        "default_site",
        "export_is_active",
        "date_last_exported",
        "Service_Kilometres",
        "Service_Years",
        "Overhead_Percentage",
        "comments",
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted"
    ];

    private readonly FisDbContext _context;

    public DepartmentRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Department?> GetByIdAsync(int departmentCode)
        => (await QueryAsync(
            "[department_code] = @departmentCode",
            command => AddParameter(command, "@departmentCode", DbType.Int32, departmentCode)))
            .SingleOrDefault();

    public async Task<Department?> GetByNameAsync(string departmentName)
    {
        if (string.IsNullOrWhiteSpace(departmentName))
        {
            return null;
        }

        return (await QueryAsync(
            "[description] = @description",
            command => AddParameter(command, "@description", DbType.String, departmentName)))
            .SingleOrDefault();
    }

    public async Task<IEnumerable<Department>> GetAllAsync()
        => await QueryAsync();

    public async Task<IEnumerable<Department>> GetActiveDepartmentsAsync()
        => await QueryAsync("[dept_active] = 1");

    public async Task<IEnumerable<Department>> GetByCompanyAsync(int companyCode)
        => await QueryAsync(
            "[company_code] = @companyCode",
            command => AddParameter(command, "@companyCode", DbType.Int32, companyCode));

    public async Task<IEnumerable<Department>> SearchDepartmentsAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetActiveDepartmentsAsync();
        }

        var availableColumns = await GetAvailableColumnsAsync();
        var searchPredicate = availableColumns.Contains("department_abbr")
            ? "(LOWER([description]) LIKE @search OR LOWER([department_abbr]) LIKE @search OR LOWER([notes]) LIKE @search)"
            : "(LOWER([description]) LIKE @search OR LOWER([notes]) LIKE @search)";

        return await QueryAsync(
            searchPredicate,
            command => AddParameter(command, "@search", DbType.String, $"%{searchTerm.Trim().ToLowerInvariant()}%"));
    }

    public async Task<DepartmentDeleteCheck> GetDeleteCheckAsync(int departmentCode)
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
            var siteCount = await CountAsync(
                connection,
                "SELECT COUNT(1) FROM [dbo].[site] WHERE [Depatrment_code] = @departmentCode",
                departmentCode,
                transaction);

            var logsheetCount = await TableExistsAsync(connection, "dbo", "logsheets", transaction)
                ? await CountAsync(
                    connection,
                    """
                    SELECT COUNT(1)
                    FROM [dbo].[logsheets] AS l
                    INNER JOIN [dbo].[site] AS s ON s.[Site_code] = l.[site_code]
                    WHERE s.[Depatrment_code] = @departmentCode
                    """,
                    departmentCode,
                    transaction)
                : 0;

            return new DepartmentDeleteCheck(siteCount, logsheetCount);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<bool> HasActiveContractsAsync(int departmentCode)
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
            if (!await TableExistsAsync(connection, "dbo", "contract", transaction) ||
                !await TableExistsAsync(connection, "dbo", "site", transaction))
            {
                return false;
            }

            return await CountAsync(
                connection,
                """
                SELECT COUNT(1)
                FROM [dbo].[contract] AS c
                INNER JOIN [dbo].[site] AS s ON s.[Site_code] = c.[site_code]
                WHERE c.[still_current] = 'Y'
                  AND s.[Depatrment_code] = @departmentCode
                """,
                departmentCode,
                transaction) > 0;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<Department> CreateAsync(Department department, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(department);

        var availableColumns = await GetAvailableColumnsAsync();
        var now = DateTime.UtcNow;
        var values = BuildCommonWriteValues(department);
        AddOptionalWriteValues(values, availableColumns, department, currentUserId, now, isCreate: true);

        department.date_created = now;
        department.created_by_user_code = currentUserId > 0 ? currentUserId : null;
        department.is_deleted = false;
        department.department_code = await ExecuteInsertAsync(values);
        return department;
    }

    public async Task UpdateAsync(Department department, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(department);

        var existing = await GetByIdAsync(department.department_code)
            ?? throw new InvalidOperationException($"Department with department_code {department.department_code} not found");
        var availableColumns = await GetAvailableColumnsAsync();
        var now = DateTime.UtcNow;
        var values = BuildCommonWriteValues(department);
        AddOptionalWriteValues(values, availableColumns, department, currentUserId, now, isCreate: false);

        IDbContextTransaction? transaction = null;
        if (_context.Database.CurrentTransaction is null)
        {
            transaction = await _context.Database.BeginTransactionAsync();
        }

        try
        {
            if (!string.Equals(existing.res_person?.Trim(), department.res_person?.Trim(), StringComparison.Ordinal))
            {
                await AddResponsiblePersonHistoryAsync(existing, department);
            }

            if (existing.dept_active != department.dept_active)
            {
                await AddDepartmentStatusAuditAsync(existing, department, currentUserId);
            }

            await ExecuteUpdateAsync(department.department_code, values);
            department.date_created = existing.date_created;
            department.created_by_user_code = existing.created_by_user_code;
            department.date_updated = now;
            department.modified_by_user_code = currentUserId > 0 ? currentUserId : null;
            department.is_deleted = existing.is_deleted;

            if (transaction is not null)
            {
                await transaction.CommitAsync();
            }
        }
        catch
        {
            if (transaction is not null)
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
        }
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The DELETE or soft-delete statement is selected from fixed compatibility branches and the department code is parameterized.")]
    public async Task DeleteAsync(int departmentCode, int currentUserId)
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

                command.CommandText = $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", assignments)} WHERE [department_code] = @departmentCode";
            }
            else
            {
                command.CommandText = $"DELETE FROM [dbo].[{TableName}] WHERE [department_code] = @departmentCode";
            }

            AddParameter(command, "@departmentCode", DbType.Int32, departmentCode);
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
        Justification = "The SELECT list is composed only from fixed legacy columns and the allowlisted runtime optional columns; predicates and values are parameterized.")]
    private async Task<List<Department>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null)
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
                .Select(column => $"[{column}] AS [{column}]")
                .Concat(OptionalColumns.Select(column => GetOptionalProjection(availableColumns, column)))
                .ToArray();
            var conditions = new List<string> { GetNotDeletedFilter(availableColumns) };
            if (!string.IsNullOrWhiteSpace(predicate))
            {
                conditions.Add(predicate);
            }

            command.CommandText = $"SELECT {string.Join(", ", projection)} FROM [dbo].[{TableName}] WHERE {string.Join(" AND ", conditions)} ORDER BY [description], [department_code]";
            configure?.Invoke(command);

            var results = new List<Department>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapDepartment(reader, availableColumns));
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
        Justification = "The INSERT statement is composed from fixed legacy columns and allowlisted optional columns; all values are parameters.")]
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
            command.CommandText = $"INSERT INTO [dbo].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[department_code] VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
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
        Justification = "The UPDATE statement is composed from fixed legacy columns and allowlisted optional columns; all values are parameters.")]
    private async Task ExecuteUpdateAsync(int departmentCode, IReadOnlyList<WriteValue> values)
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
            command.CommandText = $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [department_code] = @departmentCode";
            AddParameters(command, values);
            AddParameter(command, "@departmentCode", DbType.Int32, departmentCode);
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

    private async Task AddResponsiblePersonHistoryAsync(Department existing, Department updated)
    {
        var connection = _context.Database.GetDbConnection();
        var transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        if (!await TableExistsAsync(connection, "dbo", "res_person_history", transaction))
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO [dbo].[res_person_history]
                ([department_code], [department_number], [res_person], [telephone], [net_address], [date_changed])
            VALUES
                (@departmentCode, @departmentNumber, @responsiblePerson, @telephone, @netAddress, @dateChanged)
            """;
        AddParameter(command, "@departmentCode", DbType.Int32, existing.department_code);
        AddParameter(command, "@departmentNumber", DbType.String, updated.Department_number);
        AddParameter(command, "@responsiblePerson", DbType.String, existing.res_person);
        AddParameter(command, "@telephone", DbType.String, existing.telephone);
        AddParameter(command, "@netAddress", DbType.String, existing.net_address);
        AddParameter(command, "@dateChanged", DbType.DateTime2, DateTime.UtcNow);
        await command.ExecuteNonQueryAsync();
    }

    private async Task AddDepartmentStatusAuditAsync(Department existing, Department updated, int currentUserId)
    {
        var connection = _context.Database.GetDbConnection();
        var transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        if (!await TableExistsAsync(connection, "Audit", "Audit_Department_Status", transaction))
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO [Audit].[Audit_Department_Status]
                ([department_code], [updated_by_user_code], [notes], [action_date], [Department_active], [created_by_user_code], [date_department_created])
            VALUES
                (@departmentCode, @updatedByUserCode, @notes, @actionDate, @departmentActive, @createdByUserCode, @dateDepartmentCreated)
            """;
        AddParameter(command, "@departmentCode", DbType.Int32, existing.department_code);
        AddParameter(command, "@updatedByUserCode", DbType.Int32, currentUserId > 0 ? currentUserId : null);
        AddParameter(command, "@notes", DbType.String, updated.notes);
        AddParameter(command, "@actionDate", DbType.DateTime2, DateTime.UtcNow);
        AddParameter(command, "@departmentActive", DbType.Boolean, updated.dept_active);
        AddParameter(command, "@createdByUserCode", DbType.Int32, existing.user_access_code);
        AddParameter(command, "@dateDepartmentCreated", DbType.DateTime2, existing.date_created);
        await command.ExecuteNonQueryAsync();
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
                throw new InvalidOperationException($"The required department compatibility columns are not available: {string.Join(", ", missingColumns)}");
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

    private static List<WriteValue> BuildCommonWriteValues(Department department)
        =>
        [
            new("company_code", "@companyCode", DbType.Int16, department.company_code),
            new("description", "@description", DbType.String, department.description),
            new("res_person", "@responsiblePerson", DbType.String, department.res_person),
            new("address1", "@address1", DbType.String, department.address1),
            new("address2", "@address2", DbType.String, department.address2),
            new("address3", "@address3", DbType.String, department.address3),
            new("postal_code", "@postalCode", DbType.String, department.postal_code),
            new("telephone", "@telephone", DbType.String, department.telephone),
            new("fax", "@fax", DbType.String, department.fax),
            new("net_address", "@netAddress", DbType.String, department.net_address),
            new("Department_number", "@departmentNumber", DbType.String, department.Department_number),
            new("cell_number", "@cellNumber", DbType.String, department.cell_number),
            new("notes", "@notes", DbType.String, department.notes),
            new("dept_active", "@departmentActive", DbType.Boolean, department.dept_active),
            new("user_access_code", "@userAccessCode", DbType.Int16, department.user_access_code)
        ];

    private static void AddOptionalWriteValues(
        ICollection<WriteValue> values,
        IReadOnlySet<string> availableColumns,
        Department department,
        int currentUserId,
        DateTime now,
        bool isCreate)
    {
        AddOptionalValue(values, availableColumns, "department_abbr", "@departmentAbbr", DbType.String, department.department_abbr);
        AddOptionalValue(values, availableColumns, "bas_installation_code", "@basInstallationCode", DbType.String, department.bas_installation_code);
        AddOptionalValue(values, availableColumns, "clo_email", "@cloEmail", DbType.String, department.clo_email);
        AddOptionalValue(values, availableColumns, "telephone2", "@telephone2", DbType.String, department.telephone2);
        AddOptionalValue(values, availableColumns, "fax2", "@fax2", DbType.String, department.fax2);
        AddOptionalValue(values, availableColumns, "financial_system_code", "@financialSystemCode", DbType.Byte, department.financial_system_code);
        AddOptionalValue(values, availableColumns, "financial_system_active", "@financialSystemActive", DbType.Boolean, department.financial_system_active);
        AddOptionalValue(values, availableColumns, "financial_system_activate_date", "@financialSystemActivateDate", DbType.DateTime2, department.financial_system_activate_date);
        AddOptionalValue(values, availableColumns, "default_site", "@defaultSite", DbType.Int16, department.default_site);
        AddOptionalValue(values, availableColumns, "export_is_active", "@exportIsActive", DbType.Boolean, department.export_is_active);
        AddOptionalValue(values, availableColumns, "Service_Kilometres", "@serviceKilometres", DbType.Int32, department.Service_Kilometres);
        AddOptionalValue(values, availableColumns, "Service_Years", "@serviceYears", DbType.Byte, department.Service_Years);
        AddOptionalValue(values, availableColumns, "Overhead_Percentage", "@overheadPercentage", DbType.Decimal, department.Overhead_Percentage);
        AddOptionalValue(values, availableColumns, "comments", "@comments", DbType.String, department.comments);

        if (isCreate)
        {
            AddOptionalValue(values, availableColumns, "date_created", "@dateCreated", DbType.DateTime2, now);
            AddOptionalValue(values, availableColumns, "created_by_user_code", "@createdByUserCode", DbType.Int32, currentUserId > 0 ? currentUserId : null);
            AddOptionalValue(values, availableColumns, "is_deleted", "@isDeleted", DbType.Boolean, false);
        }
        else
        {
            AddOptionalValue(values, availableColumns, "date_updated", "@dateUpdated", DbType.DateTime2, now);
            AddOptionalValue(values, availableColumns, "modified_by_user_code", "@modifiedByUserCode", DbType.Int32, currentUserId > 0 ? currentUserId : null);
            AddOptionalValue(values, availableColumns, "is_deleted", "@isDeleted", DbType.Boolean, department.is_deleted);
        }
    }

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

    private static Department MapDepartment(DbDataReader reader, IReadOnlySet<string> availableColumns)
        => new()
        {
            department_code = ReadInt16(reader, "department_code") ?? 0,
            company_code = ReadInt16(reader, "company_code") ?? 0,
            description = ReadString(reader, "description"),
            res_person = ReadString(reader, "res_person"),
            address1 = ReadString(reader, "address1"),
            address2 = ReadString(reader, "address2"),
            address3 = ReadString(reader, "address3"),
            postal_code = ReadString(reader, "postal_code"),
            telephone = ReadString(reader, "telephone"),
            fax = ReadString(reader, "fax"),
            net_address = ReadString(reader, "net_address"),
            Department_number = ReadString(reader, "Department_number"),
            cell_number = ReadString(reader, "cell_number"),
            notes = ReadString(reader, "notes"),
            dept_active = ReadBoolean(reader, "dept_active") ?? false,
            user_access_code = ReadInt16(reader, "user_access_code"),
            department_abbr = ReadStringIfAvailable(reader, availableColumns, "department_abbr"),
            bas_installation_code = ReadStringIfAvailable(reader, availableColumns, "bas_installation_code"),
            clo_email = ReadStringIfAvailable(reader, availableColumns, "clo_email"),
            telephone2 = ReadStringIfAvailable(reader, availableColumns, "telephone2"),
            fax2 = ReadStringIfAvailable(reader, availableColumns, "fax2"),
            financial_system_code = ReadByteIfAvailable(reader, availableColumns, "financial_system_code"),
            financial_system_active = ReadBooleanIfAvailable(reader, availableColumns, "financial_system_active"),
            financial_system_activate_date = ReadDateTimeIfAvailable(reader, availableColumns, "financial_system_activate_date"),
            default_site = ReadInt16IfAvailable(reader, availableColumns, "default_site"),
            export_is_active = ReadBooleanIfAvailable(reader, availableColumns, "export_is_active"),
            date_last_exported = ReadDateTimeIfAvailable(reader, availableColumns, "date_last_exported"),
            Service_Kilometres = ReadInt32IfAvailable(reader, availableColumns, "Service_Kilometres") ?? 0,
            Service_Years = ReadByteIfAvailable(reader, availableColumns, "Service_Years") ?? 0,
            Overhead_Percentage = ReadDecimalIfAvailable(reader, availableColumns, "Overhead_Percentage") ?? 0,
            comments = ReadStringIfAvailable(reader, availableColumns, "comments"),
            date_created = ReadDateTimeIfAvailable(reader, availableColumns, "date_created") ?? DateTime.MinValue,
            date_updated = ReadDateTimeIfAvailable(reader, availableColumns, "date_updated"),
            created_by_user_code = ReadInt32IfAvailable(reader, availableColumns, "created_by_user_code"),
            modified_by_user_code = ReadInt32IfAvailable(reader, availableColumns, "modified_by_user_code"),
            is_deleted = ReadBooleanIfAvailable(reader, availableColumns, "is_deleted") ?? false
        };

    private static string GetOptionalProjection(IReadOnlySet<string> columns, string column)
    {
        if (columns.Contains(column))
        {
            return $"[{column}] AS [{column}]";
        }

        var sqlType = column switch
        {
            "financial_system_code" or "Service_Years" => "tinyint",
            "financial_system_active" or "export_is_active" or "is_deleted" => "bit",
            "financial_system_activate_date" or "date_last_exported" or "date_created" or "date_updated" => "datetime2",
            "default_site" => "smallint",
            "Service_Kilometres" => "int",
            "Overhead_Percentage" => "decimal(18, 2)",
            "created_by_user_code" or "modified_by_user_code" => "int",
            _ => "varchar(1)"
        };
        return $"CAST(NULL AS {sqlType}) AS [{column}]";
    }

    private static string GetNotDeletedFilter(IReadOnlySet<string> columns)
        => columns.Contains("is_deleted") ? "([is_deleted] = 0 OR [is_deleted] IS NULL)" : "1 = 1";

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The count query is selected from fixed repository statements and the department code is parameterized.")]
    private static async Task<int> CountAsync(
        DbConnection connection,
        string sql,
        int departmentCode,
        DbTransaction? transaction)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        AddParameter(command, "@departmentCode", DbType.Int32, departmentCode);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<bool> TableExistsAsync(
        DbConnection connection,
        string schema,
        string table,
        DbTransaction? transaction)
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

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal))?.TrimEnd();
    }

    private static string? ReadStringIfAvailable(DbDataReader reader, IReadOnlySet<string> columns, string column)
        => columns.Contains(column) ? ReadString(reader, column) : null;

    private static short? ReadInt16(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt16(reader.GetValue(ordinal));
    }

    private static short? ReadInt16IfAvailable(DbDataReader reader, IReadOnlySet<string> columns, string column)
        => columns.Contains(column) ? ReadInt16(reader, column) : null;

    private static int? ReadInt32IfAvailable(DbDataReader reader, IReadOnlySet<string> columns, string column)
    {
        if (!columns.Contains(column))
        {
            return null;
        }

        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static byte? ReadByteIfAvailable(DbDataReader reader, IReadOnlySet<string> columns, string column)
    {
        if (!columns.Contains(column))
        {
            return null;
        }

        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToByte(reader.GetValue(ordinal));
    }

    private static bool? ReadBoolean(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToBoolean(reader.GetValue(ordinal));
    }

    private static bool? ReadBooleanIfAvailable(DbDataReader reader, IReadOnlySet<string> columns, string column)
        => columns.Contains(column) ? ReadBoolean(reader, column) : null;

    private static DateTime? ReadDateTimeIfAvailable(DbDataReader reader, IReadOnlySet<string> columns, string column)
    {
        if (!columns.Contains(column))
        {
            return null;
        }

        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }

    private static decimal? ReadDecimalIfAvailable(DbDataReader reader, IReadOnlySet<string> columns, string column)
    {
        if (!columns.Contains(column))
        {
            return null;
        }

        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal));
    }
}
