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
/// Persists the site table against the original legacy schema and the
/// expanded schema. The stable legacy columns are always selected and written;
/// columns that are absent from older client databases are negotiated at runtime.
/// </summary>
public sealed class SiteRepository : ISiteRepository
{
    private const string TableName = "site";

    private static readonly string[] RequiredColumns =
    [
        "Site_code",
        "Depatrment_code",
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
        "Map_reference",
        "Map_description",
        "cell_number",
        "site_active",
        "telephone2",
        "fax1",
        "financial_system_code",
        "financial_system_active",
        "date_created",
        "date_updated",
        "modified_by_user_code",
    ];

    private static readonly string[] OptionalColumns =
    [
        "financial_system_activate_date",
        "export_is_active",
        "date_last_exported",
        "Service_Kilometres",
        "Service_Years",
        "Overhead_Percentage",
        "province_code",
        "notes",
        "user_access_code",
        "created_by_user_code",
        "is_deleted",
    ];

    private readonly FisDbContext _context;

    public SiteRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Site?> GetByIdAsync(int siteCode) =>
        (
            await QueryAsync(
                "[Site_code] = @siteCode",
                command => AddParameter(command, "@siteCode", DbType.Int32, siteCode)
            )
        ).SingleOrDefault();

    public async Task<Site?> GetByNameAsync(string siteName)
    {
        if (string.IsNullOrWhiteSpace(siteName))
        {
            return null;
        }

        return (
            await QueryAsync(
                "[description] = @description",
                command => AddParameter(command, "@description", DbType.String, siteName.Trim())
            )
        ).SingleOrDefault();
    }

    public async Task<IEnumerable<Site>> GetActiveSitesAsync() =>
        await QueryAsync("[site_active] = 1");

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The page queries use fixed compatibility columns, filters, ordering, and parameterized pagination values."
    )]
    public async Task<SitePage> GetPageAsync(int page = 1, int pageSize = 24)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var availableColumns = await GetAvailableColumnsAsync();
        var notDeletedFilter = GetNotDeletedFilter(availableColumns);
        var whereClause = $"[site_active] = 1 AND {notDeletedFilter}";
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            int total;
            await using (var countCommand = connection.CreateCommand())
            {
                countCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
                countCommand.CommandText =
                    $"SELECT COUNT(1) FROM [dbo].[{TableName}] WHERE {whereClause}";
                total = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
            }

            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            page = Math.Min(page, totalPages);
            var skip = checked((long)(page - 1) * pageSize);

            var projection = RequiredColumns
                .Select(column => $"[{column}] AS [{column}]")
                .Concat(
                    OptionalColumns.Select(column =>
                        GetOptionalProjection(availableColumns, column)
                    )
                )
                .ToArray();
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText =
                $"SELECT {string.Join(", ", projection)} FROM [dbo].[{TableName}] WHERE {whereClause} ORDER BY [Department_number], [description], [Site_code] OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY";
            AddParameter(command, "@skip", DbType.Int64, skip);
            AddParameter(command, "@pageSize", DbType.Int32, pageSize);

            var items = new List<Site>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(MapSite(reader, availableColumns));
            }

            return new SitePage(items, page, pageSize, total);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<IEnumerable<Site>> SearchSitesAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetActiveSitesAsync();
        }

        return await QueryAsync(
            "(LOWER(COALESCE([description], '')) LIKE @search OR "
                + "LOWER(COALESCE([res_person], '')) LIKE @search OR "
                + "LOWER(COALESCE([address1], '')) LIKE @search OR "
                + "LOWER(COALESCE([address2], '')) LIKE @search OR "
                + "LOWER(COALESCE([Department_number], '')) LIKE @search)",
            command =>
                AddParameter(
                    command,
                    "@search",
                    DbType.String,
                    $"%{searchTerm.Trim().ToLowerInvariant()}%"
                )
        );
    }

    public async Task<SiteDeleteCheck> GetDeleteCheckAsync(int siteCode)
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
            var contractCount = await TableExistsAsync(connection, "dbo", "contract", transaction)
                ? await CountAsync(
                    connection,
                    "SELECT COUNT(1) FROM [dbo].[contract] WHERE [site_code] = @siteCode",
                    siteCode,
                    transaction
                )
                : 0;

            return new SiteDeleteCheck(contractCount);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<bool> HasActiveContractsAsync(int siteCode)
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
            if (!await TableExistsAsync(connection, "dbo", "contract", transaction))
            {
                return false;
            }

            return await CountAsync(
                    connection,
                    "SELECT COUNT(1) FROM [dbo].[contract] WHERE [site_code] = @siteCode AND [still_current] = 'Y'",
                    siteCode,
                    transaction
                ) > 0;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<Site> CreateAsync(Site site, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(site);

        var availableColumns = await GetAvailableColumnsAsync();
        var now = DateTime.UtcNow;
        site.date_created = now;
        site.date_updated = now;
        site.modified_by_user_code = currentUserId > 0 ? currentUserId : null;
        site.is_deleted = false;

        var values = BuildLegacyValues(site);
        AddOptionalSiteValues(values, availableColumns, site);
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
        site.Site_code = await ExecuteInsertAsync(values);
        return site;
    }

    public async Task UpdateAsync(Site site, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(site);

        var existing =
            await GetByIdAsync(site.Site_code)
            ?? throw new InvalidOperationException(
                $"Site with Site_code {site.Site_code} not found"
            );
        var availableColumns = await GetAvailableColumnsAsync();
        var now = DateTime.UtcNow;
        site.date_created = existing.date_created;
        site.created_by_user_code = existing.created_by_user_code;
        site.date_updated = now;
        site.modified_by_user_code =
            currentUserId > 0 ? currentUserId : existing.modified_by_user_code;
        site.is_deleted = existing.is_deleted;

        var values = BuildLegacyValues(site);
        AddOptionalSiteValues(values, availableColumns, site);
        AddOptionalValue(
            values,
            availableColumns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            site.created_by_user_code
        );
        AddOptionalValue(
            values,
            availableColumns,
            "is_deleted",
            "@isDeleted",
            DbType.Boolean,
            site.is_deleted
        );

        IDbContextTransaction? transaction = null;
        if (_context.Database.CurrentTransaction is null)
        {
            transaction = await _context.Database.BeginTransactionAsync();
        }

        try
        {
            if (
                !string.Equals(
                    existing.res_person?.Trim(),
                    site.res_person?.Trim(),
                    StringComparison.Ordinal
                )
            )
            {
                await AddResponsiblePersonHistoryAsync(existing, site);
            }

            if (existing.site_active != site.site_active)
            {
                await AddSiteStatusAuditAsync(existing, site, currentUserId);
            }

            await ExecuteUpdateAsync(site.Site_code, values);

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
        Justification = "The soft-delete statement is selected from fixed compatibility branches and the site code is parameterized."
    )]
    public async Task DeleteAsync(int siteCode, int currentUserId)
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
            var assignments = new List<string>
            {
                "[site_active] = @siteActive",
                "[date_updated] = @dateUpdated",
                "[modified_by_user_code] = @modifiedByUserCode",
            };
            AddParameter(command, "@siteActive", DbType.Boolean, false);
            AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
            AddParameter(
                command,
                "@modifiedByUserCode",
                DbType.Int32,
                currentUserId > 0 ? currentUserId : null
            );

            if (availableColumns.Contains("is_deleted"))
            {
                assignments.Insert(0, "[is_deleted] = @isDeleted");
                AddParameter(command, "@isDeleted", DbType.Boolean, true);
            }

            command.CommandText =
                $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", assignments)} WHERE [Site_code] = @siteCode";
            AddParameter(command, "@siteCode", DbType.Int32, siteCode);
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
        Justification = "The SELECT list is composed only from fixed legacy columns and allowlisted runtime optional columns; predicates and values are parameterized."
    )]
    private async Task<List<Site>> QueryAsync(
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
            var projection = RequiredColumns
                .Select(column => $"[{column}] AS [{column}]")
                .Concat(
                    OptionalColumns.Select(column =>
                        GetOptionalProjection(availableColumns, column)
                    )
                )
                .ToArray();
            var conditions = new List<string> { GetNotDeletedFilter(availableColumns) };
            if (!string.IsNullOrWhiteSpace(predicate))
            {
                conditions.Add(predicate);
            }

            command.CommandText =
                $"SELECT {string.Join(", ", projection)} FROM [dbo].[{TableName}] WHERE {string.Join(" AND ", conditions)} ORDER BY [Department_number], [description], [Site_code]";
            configure?.Invoke(command);

            var results = new List<Site>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapSite(reader, availableColumns));
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
        Justification = "The INSERT statement is composed from fixed legacy columns and allowlisted optional columns; all values are parameters."
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
            command.CommandText =
                $"INSERT INTO [dbo].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[Site_code] VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
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
        Justification = "The UPDATE statement is composed from fixed legacy columns and allowlisted optional columns; all values are parameters."
    )]
    private async Task ExecuteUpdateAsync(int siteCode, IReadOnlyList<WriteValue> values)
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
            command.CommandText =
                $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [Site_code] = @siteCode";
            AddParameters(command, values);
            AddParameter(command, "@siteCode", DbType.Int32, siteCode);
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

    private async Task AddResponsiblePersonHistoryAsync(Site existing, Site updated)
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
                ([department_code], [department_number], [site_code], [res_person], [telephone], [net_address], [date_changed])
            VALUES
                (@departmentCode, @departmentNumber, @siteCode, @responsiblePerson, @telephone, @netAddress, @dateChanged)
            """;
        AddParameter(command, "@departmentCode", DbType.Int32, updated.Depatrment_code);
        AddParameter(command, "@departmentNumber", DbType.String, updated.Department_number);
        AddParameter(command, "@siteCode", DbType.Int32, existing.Site_code);
        AddParameter(command, "@responsiblePerson", DbType.String, existing.res_person);
        AddParameter(command, "@telephone", DbType.String, existing.telephone);
        AddParameter(command, "@netAddress", DbType.String, existing.net_address);
        AddParameter(command, "@dateChanged", DbType.DateTime2, DateTime.UtcNow);
        await command.ExecuteNonQueryAsync();
    }

    private async Task AddSiteStatusAuditAsync(Site existing, Site updated, int currentUserId)
    {
        var connection = _context.Database.GetDbConnection();
        var transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        if (!await TableExistsAsync(connection, "Audit", "Audit_Site_Status", transaction))
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO [Audit].[Audit_Site_Status]
                ([site_code], [updated_by_user_code], [notes], [action_date], [site_active], [created_by_user_code], [date_site_created])
            VALUES
                (@siteCode, @updatedByUserCode, @notes, @actionDate, @siteActive, @createdByUserCode, @dateSiteCreated)
            """;
        AddParameter(command, "@siteCode", DbType.Int32, existing.Site_code);
        AddParameter(
            command,
            "@updatedByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );
        AddParameter(command, "@notes", DbType.String, updated.notes);
        AddParameter(command, "@actionDate", DbType.DateTime2, DateTime.UtcNow);
        AddParameter(command, "@siteActive", DbType.Boolean, updated.site_active);
        AddParameter(command, "@createdByUserCode", DbType.Int32, existing.user_access_code);
        AddParameter(command, "@dateSiteCreated", DbType.DateTime2, existing.date_created);
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
                columns.Add(Convert.ToString(reader.GetValue(0)) ?? string.Empty);
            }

            var missing = RequiredColumns.Where(column => !columns.Contains(column)).ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException(
                    $"The site table is missing stable legacy columns: {string.Join(", ", missing)}"
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

    private static List<WriteValue> BuildLegacyValues(Site site) =>
        [
            new("Depatrment_code", "@departmentCode", DbType.Int16, site.Depatrment_code),
            new("description", "@description", DbType.String, site.description),
            new("res_person", "@responsiblePerson", DbType.String, site.res_person),
            new("address1", "@address1", DbType.String, site.address1),
            new("address2", "@address2", DbType.String, site.address2),
            new("address3", "@address3", DbType.String, site.address3),
            new("postal_code", "@postalCode", DbType.String, site.postal_code),
            new("telephone", "@telephone", DbType.String, site.telephone),
            new("fax", "@fax", DbType.String, site.fax),
            new("net_address", "@netAddress", DbType.String, site.net_address),
            new("Department_number", "@departmentNumber", DbType.String, site.Department_number),
            new("Map_reference", "@mapReference", DbType.String, site.Map_reference),
            new("Map_description", "@mapDescription", DbType.String, site.Map_description),
            new("cell_number", "@cellNumber", DbType.String, site.cell_number),
            new("site_active", "@siteActive", DbType.Boolean, site.site_active),
            new("telephone2", "@telephone2", DbType.String, site.telephone2),
            new("fax1", "@fax1", DbType.String, site.fax1),
            new(
                "financial_system_code",
                "@financialSystemCode",
                DbType.Byte,
                site.financial_system_code
            ),
            new(
                "financial_system_active",
                "@financialSystemActive",
                DbType.Boolean,
                site.financial_system_active
            ),
            new("date_created", "@dateCreated", DbType.DateTime2, site.date_created),
            new("date_updated", "@dateUpdated", DbType.DateTime2, site.date_updated),
            new(
                "modified_by_user_code",
                "@modifiedByUserCode",
                DbType.Int32,
                site.modified_by_user_code
            ),
        ];

    private static void AddOptionalSiteValues(
        ICollection<WriteValue> values,
        IReadOnlySet<string> availableColumns,
        Site site
    )
    {
        AddOptionalValue(
            values,
            availableColumns,
            "financial_system_activate_date",
            "@financialSystemActivateDate",
            DbType.DateTime2,
            site.financial_system_activate_date
        );
        AddOptionalValue(
            values,
            availableColumns,
            "export_is_active",
            "@exportIsActive",
            DbType.Boolean,
            site.export_is_active
        );
        AddOptionalValue(
            values,
            availableColumns,
            "date_last_exported",
            "@dateLastExported",
            DbType.DateTime2,
            site.date_last_exported
        );
        AddOptionalValue(
            values,
            availableColumns,
            "Service_Kilometres",
            "@serviceKilometres",
            DbType.Int32,
            site.Service_Kilometres
        );
        AddOptionalValue(
            values,
            availableColumns,
            "Service_Years",
            "@serviceYears",
            DbType.Byte,
            site.Service_Years
        );
        AddOptionalValue(
            values,
            availableColumns,
            "Overhead_Percentage",
            "@overheadPercentage",
            DbType.Decimal,
            site.Overhead_Percentage
        );
        AddOptionalValue(
            values,
            availableColumns,
            "province_code",
            "@provinceCode",
            DbType.Byte,
            site.province_code
        );
        AddOptionalValue(values, availableColumns, "notes", "@notes", DbType.String, site.notes);
        AddOptionalValue(
            values,
            availableColumns,
            "user_access_code",
            "@userAccessCode",
            DbType.Int32,
            site.user_access_code
        );
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

    private static Site MapSite(DbDataReader reader, IReadOnlySet<string> availableColumns) =>
        new()
        {
            Site_code = ReadInt16(reader, "Site_code") ?? 0,
            Depatrment_code = ReadInt16(reader, "Depatrment_code"),
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
            Map_reference = ReadString(reader, "Map_reference"),
            Map_description = ReadString(reader, "Map_description"),
            cell_number = ReadString(reader, "cell_number"),
            site_active = ReadBoolean(reader, "site_active") ?? false,
            telephone2 = ReadString(reader, "telephone2"),
            fax1 = ReadString(reader, "fax1"),
            financial_system_code = ReadByte(reader, "financial_system_code"),
            financial_system_active = ReadBoolean(reader, "financial_system_active"),
            financial_system_activate_date = ReadDateTime(reader, "financial_system_activate_date"),
            export_is_active = ReadBoolean(reader, "export_is_active"),
            date_last_exported = ReadDateTime(reader, "date_last_exported"),
            Service_Kilometres = ReadInt32(reader, "Service_Kilometres") ?? 0,
            Service_Years = ReadByte(reader, "Service_Years") ?? 0,
            Overhead_Percentage = ReadDecimal(reader, "Overhead_Percentage") ?? 0,
            date_created = ReadDateTime(reader, "date_created") ?? DateTime.MinValue,
            date_updated = ReadDateTime(reader, "date_updated"),
            province_code = ReadByte(reader, "province_code"),
            notes = ReadString(reader, "notes"),
            user_access_code = ReadInt32(reader, "user_access_code"),
            modified_by_user_code = ReadInt32(reader, "modified_by_user_code"),
            created_by_user_code = ReadInt32IfAvailable(
                reader,
                availableColumns,
                "created_by_user_code"
            ),
            is_deleted = ReadBooleanIfAvailable(reader, availableColumns, "is_deleted") ?? false,
        };

    private static string GetOptionalProjection(IReadOnlySet<string> columns, string column)
    {
        if (columns.Contains(column))
        {
            return $"[{column}] AS [{column}]";
        }

        var sqlType = column switch
        {
            "financial_system_activate_date" or "date_last_exported" => "datetime2",
            "export_is_active" => "bit",
            "Service_Kilometres" => "int",
            "Service_Years" => "tinyint",
            "Overhead_Percentage" => "decimal(18, 3)",
            "province_code" => "tinyint",
            "user_access_code" => "int",
            "is_deleted" => "bit",
            "created_by_user_code" => "int",
            _ => "varchar(1)",
        };
        return $"CAST(NULL AS {sqlType}) AS [{column}]";
    }

    private static string GetNotDeletedFilter(IReadOnlySet<string> columns) =>
        columns.Contains("is_deleted") ? "([is_deleted] = 0 OR [is_deleted] IS NULL)" : "1 = 1";

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The count query is selected from a fixed repository statement and the site code is parameterized."
    )]
    private static async Task<int> CountAsync(
        DbConnection connection,
        string sql,
        int siteCode,
        DbTransaction? transaction
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        AddParameter(command, "@siteCode", DbType.Int32, siteCode);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<bool> TableExistsAsync(
        DbConnection connection,
        string schema,
        string table,
        DbTransaction? transaction
    )
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

    private static int? ReadInt32IfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column
    ) => columns.Contains(column) ? ReadInt32(reader, column) : null;

    private static byte? ReadByte(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToByte(reader.GetValue(ordinal));
    }

    private static bool? ReadBoolean(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToBoolean(reader.GetValue(ordinal));
    }

    private static bool? ReadBooleanIfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column
    ) => columns.Contains(column) ? ReadBoolean(reader, column) : null;

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }

    private static decimal? ReadDecimal(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal));
    }
}
