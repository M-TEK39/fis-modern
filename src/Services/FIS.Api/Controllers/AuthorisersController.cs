using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

/// <summary>
/// Authoriser management over the legacy-compatible dbo.approvers table.
///
/// The client database shape contains PersalNumber, TelephoneNumber and
/// approver_active. The expanded shape may instead contain the shared audit
/// columns and is_deleted. The route deliberately discovers the available
/// columns before building SQL so either database can remain operational.
/// </summary>
[ApiController]
[Authorize]
[Route("api/authorisers")]
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "All interpolated SQL identifiers come from fixed schema constants or the fixed column set returned by INFORMATION_SCHEMA; request values are parameters.")]
public sealed class AuthorisersController : BaseApiController
{
    private const string ApproversTable = "approvers";
    private const string RanksTable = "ranks";

    private readonly FisDbContext _context;
    private readonly ILogger<AuthorisersController> _logger;

    public AuthorisersController(FisDbContext context, ILogger<AuthorisersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AuthoriserDto>>> GetAuthorisers([FromQuery] short? siteCode)
    {
        try
        {
            var authorisers = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, ApproversTable);
                EnsureApproverTable(schema);

                var siteFilter = siteCode.HasValue ? " AND [site_code] = @siteCode" : string.Empty;
                await using var command = connection.CreateCommand();
                command.CommandText = $"{BuildApproverSelect(schema)} WHERE {BuildActivePredicate(schema)}{siteFilter} ORDER BY [Surname], [Firstname], [approver_code]";
                if (siteCode.HasValue)
                {
                    AddParameter(command, "@siteCode", siteCode.Value);
                }

                return await ReadAuthorisersAsync(command, schema);
            });

            return Ok(authorisers.Select(MapToDto));
        }
        catch (Exception ex)
        {
            return HandleFailure(ex, "retrieving authorisers");
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AuthoriserDto>> GetAuthoriser(int id)
    {
        try
        {
            var authoriser = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, ApproversTable);
                EnsureApproverTable(schema);

                await using var command = connection.CreateCommand();
                command.CommandText = $"{BuildApproverSelect(schema)} WHERE [approver_code] = @id";
                AddParameter(command, "@id", id);

                return await ReadAuthoriserAsync(command, schema);
            });

            return authoriser is null ? NotFound(new { message = $"Authoriser not found with code: {id}" }) : Ok(MapToDto(authoriser));
        }
        catch (Exception ex)
        {
            return HandleFailure(ex, $"retrieving authoriser {id}");
        }
    }

    [HttpGet("ranks")]
    public async Task<ActionResult<IEnumerable<AuthoriserRankDto>>> GetRanks()
    {
        try
        {
            var ranks = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, RanksTable);
                if (!schema.Has("rank_code") || !schema.Has("description"))
                {
                    return new List<AuthoriserRankDto>();
                }

                var activePredicate = schema.Has("is_deleted") ? "([is_deleted] = 0 OR [is_deleted] IS NULL)" : "1 = 1";
                await using var command = connection.CreateCommand();
                command.CommandText = $"SELECT [rank_code] AS [RankCode], [description] AS [Description] FROM [dbo].[{RanksTable}] WHERE {activePredicate} ORDER BY [description], [rank_code]";

                var result = new List<AuthoriserRankDto>();
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var rankCode = ReadInt(reader, "RankCode");
                    if (rankCode.HasValue)
                    {
                        result.Add(new AuthoriserRankDto
                        {
                            Id = rankCode.Value,
                            Description = ReadString(reader, "Description")
                        });
                    }
                }

                return result;
            });

            return Ok(ranks);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to retrieve authoriser ranks; returning an empty lookup");
            return Ok(Array.Empty<AuthoriserRankDto>());
        }
    }

    [HttpPost]
    public async Task<ActionResult<AuthoriserDto>> CreateAuthoriser([FromBody] CreateAuthoriserDto dto)
    {
        try
        {
            var created = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, ApproversTable);
                EnsureApproverTable(schema);
                ValidateAuthoriser(dto, schema);

                var columns = new List<string>
                {
                    "site_code",
                    "department_code",
                    "rank_code",
                    "Surname",
                    "Firstname"
                };
                var values = new List<(string Name, object? Value)>
                {
                    ("@siteCode", dto.SiteCode),
                    ("@departmentCode", dto.DepartmentCode),
                    ("@rankCode", dto.RankCode),
                    ("@surname", dto.Surname.Trim()),
                    ("@firstname", dto.Firstname.Trim())
                };

                AddOptionalAuthoriserFields(schema, columns, values, dto, includeCreatedAudit: true, currentUserId: GetCurrentUserId());

                await using var command = connection.CreateCommand();
                command.CommandText = $"INSERT INTO [dbo].[{ApproversTable}] ({string.Join(", ", columns.Select(QuoteIdentifier))}) OUTPUT INSERTED.[approver_code] VALUES ({string.Join(", ", values.Select(item => item.Name))})";
                foreach (var (name, value) in values)
                {
                    AddParameter(command, name, value);
                }

                var createdId = Convert.ToInt32(await command.ExecuteScalarAsync());
                return await ReadAuthoriserByIdAsync(connection, schema, createdId);
            });

            return created is null
                ? StatusCode(500, new { message = "The authoriser was created but could not be reloaded." })
                : CreatedAtAction(nameof(GetAuthoriser), new { id = created.AuthoriserCode }, MapToDto(created));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return HandleFailure(ex, "creating an authoriser");
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AuthoriserDto>> UpdateAuthoriser(int id, [FromBody] UpdateAuthoriserDto dto)
    {
        try
        {
            var updated = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, ApproversTable);
                EnsureApproverTable(schema);
                ValidateAuthoriser(dto, schema);

                var existing = await ReadAuthoriserByIdAsync(connection, schema, id);
                if (existing is null)
                {
                    return null;
                }

                var assignments = new List<string>
                {
                    "[site_code] = @siteCode",
                    "[department_code] = @departmentCode",
                    "[rank_code] = @rankCode",
                    "[Surname] = @surname",
                    "[Firstname] = @firstname"
                };
                var values = new List<(string Name, object? Value)>
                {
                    ("@siteCode", dto.SiteCode),
                    ("@departmentCode", dto.DepartmentCode),
                    ("@rankCode", dto.RankCode),
                    ("@surname", dto.Surname.Trim()),
                    ("@firstname", dto.Firstname.Trim())
                };

                AddOptionalAuthoriserFields(schema, assignments, values, dto, includeCreatedAudit: false, currentUserId: GetCurrentUserId());
                if (schema.Has("date_updated"))
                {
                    assignments.Add("[date_updated] = @dateUpdated");
                    values.Add(("@dateUpdated", DateTime.UtcNow));
                }
                if (schema.Has("modified_by_user_code"))
                {
                    assignments.Add("[modified_by_user_code] = @modifiedByUserCode");
                    values.Add(("@modifiedByUserCode", GetCurrentUserId()));
                }

                await using var command = connection.CreateCommand();
                command.CommandText = $"UPDATE [dbo].[{ApproversTable}] SET {string.Join(", ", assignments)} WHERE [approver_code] = @id";
                foreach (var (name, value) in values)
                {
                    AddParameter(command, name, value);
                }
                AddParameter(command, "@id", id);
                await command.ExecuteNonQueryAsync();

                return await ReadAuthoriserByIdAsync(connection, schema, id);
            });

            return updated is null
                ? NotFound(new { message = $"Authoriser not found with code: {id}" })
                : Ok(MapToDto(updated));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return HandleFailure(ex, $"updating authoriser {id}");
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteAuthoriser(int id)
    {
        try
        {
            var deleted = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, ApproversTable);
                EnsureApproverTable(schema);

                var existing = await ReadAuthoriserByIdAsync(connection, schema, id);
                if (existing is null)
                {
                    return false;
                }

                var assignments = new List<string>();
                if (schema.Has("approver_active"))
                {
                    assignments.Add("[approver_active] = @active");
                }
                else if (schema.Has("IsActive"))
                {
                    assignments.Add("[IsActive] = @active");
                }
                if (schema.Has("is_deleted"))
                {
                    assignments.Add("[is_deleted] = @isDeleted");
                }
                if (schema.Has("date_updated"))
                {
                    assignments.Add("[date_updated] = @dateUpdated");
                }
                if (schema.Has("modified_by_user_code"))
                {
                    assignments.Add("[modified_by_user_code] = @modifiedByUserCode");
                }

                if (assignments.Count == 0)
                {
                    throw new InvalidOperationException("The authoriser table has no supported active-state column.");
                }

                await using var command = connection.CreateCommand();
                command.CommandText = $"UPDATE [dbo].[{ApproversTable}] SET {string.Join(", ", assignments)} WHERE [approver_code] = @id";
                AddParameter(command, "@active", false);
                AddParameter(command, "@isDeleted", true);
                AddParameter(command, "@dateUpdated", DateTime.UtcNow);
                AddParameter(command, "@modifiedByUserCode", GetCurrentUserId());
                AddParameter(command, "@id", id);
                await command.ExecuteNonQueryAsync();
                return true;
            });

            return deleted ? NoContent() : NotFound(new { message = $"Authoriser not found with code: {id}" });
        }
        catch (Exception ex)
        {
            return HandleFailure(ex, $"deleting authoriser {id}");
        }
    }

    private async Task<T> WithConnectionAsync<T>(Func<DbConnection, Task<T>> operation)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            return await operation(connection);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<TableSchema> ReadTableSchemaAsync(DbConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table";
        AddParameter(command, "@schema", "dbo");
        AddParameter(command, "@table", tableName);

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.IsDBNull(0) ? null : reader.GetString(0);
            if (!string.IsNullOrWhiteSpace(name))
            {
                columns.Add(name);
            }
        }

        return new TableSchema(tableName, columns);
    }

    private static void EnsureApproverTable(TableSchema schema)
    {
        string[] requiredColumns = ["approver_code", "site_code", "department_code", "rank_code", "Surname", "Firstname"];
        if (requiredColumns.Any(column => !schema.Has(column)))
        {
            throw new InvalidOperationException("The dbo.approvers table is missing one or more required legacy columns.");
        }
    }

    private static string BuildApproverSelect(TableSchema schema)
    {
        var persal = schema.Has("PersalNumber") ? "CAST([PersalNumber] AS nvarchar(50))" : "CAST(NULL AS nvarchar(50))";
        var telephone = schema.Has("TelephoneNumber") ? "CAST([TelephoneNumber] AS nvarchar(50))" : "CAST(NULL AS nvarchar(50))";
        var active = BuildActiveExpression(schema);
        var legacyFieldsAvailable = schema.Has("PersalNumber") && schema.Has("TelephoneNumber");

        return $"SELECT [approver_code] AS [AuthoriserCode], [site_code] AS [SiteCode], [department_code] AS [DepartmentCode], [rank_code] AS [RankCode], [Surname] AS [Surname], [Firstname] AS [Firstname], {persal} AS [PersalNumber], {telephone} AS [TelephoneNumber], {active} AS [IsActive], CAST({(legacyFieldsAvailable ? 1 : 0)} AS bit) AS [LegacyFieldsAvailable] FROM [dbo].[{ApproversTable}]";
    }

    private static string BuildActiveExpression(TableSchema schema)
    {
        if (schema.Has("approver_active"))
        {
            return "COALESCE([approver_active], 0)";
        }

        if (schema.Has("IsActive"))
        {
            return "COALESCE([IsActive], 0)";
        }

        if (schema.Has("is_deleted"))
        {
            return "CASE WHEN COALESCE([is_deleted], 0) = 1 THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END";
        }

        return "CAST(1 AS bit)";
    }

    private static string BuildActivePredicate(TableSchema schema)
    {
        if (schema.Has("approver_active"))
        {
            return "COALESCE([approver_active], 0) = 1";
        }

        if (schema.Has("IsActive"))
        {
            return "COALESCE([IsActive], 0) = 1";
        }

        return schema.Has("is_deleted") ? "COALESCE([is_deleted], 0) = 0" : "1 = 1";
    }

    private static void ValidateAuthoriser(CreateAuthoriserDto dto, TableSchema schema)
    {
        if (string.IsNullOrWhiteSpace(dto.Firstname))
        {
            throw new ArgumentException("The first name is required.");
        }
        if (string.IsNullOrWhiteSpace(dto.Surname))
        {
            throw new ArgumentException("The surname is required.");
        }
        if (dto.RankCode <= 0)
        {
            throw new ArgumentException("The rank is required.");
        }
        if (dto.DepartmentCode < short.MinValue || dto.DepartmentCode > short.MaxValue)
        {
            throw new ArgumentException("The department code must be a valid legacy smallint.");
        }
        var nameLimit = schema.Has("PersalNumber") ? 50 : 255;
        if (dto.Firstname.Trim().Length > nameLimit)
        {
            throw new ArgumentException("The first name is too long for the database.");
        }
        if (dto.Surname.Trim().Length > nameLimit)
        {
            throw new ArgumentException("The surname is too long for the database.");
        }
        if (schema.Has("PersalNumber") && dto.PersalNumber?.Length > 10)
        {
            throw new ArgumentException("The Persal Number must be 10 characters or fewer.");
        }
        if (schema.Has("TelephoneNumber") && dto.TelephoneNumber?.Length > 20)
        {
            throw new ArgumentException("The Telephone Number must be 20 characters or fewer.");
        }
    }

    private static void AddOptionalAuthoriserFields(
        TableSchema schema,
        ICollection<string> sqlParts,
        ICollection<(string Name, object? Value)> values,
        CreateAuthoriserDto dto,
        bool includeCreatedAudit,
        int currentUserId)
    {
        if (schema.Has("PersalNumber"))
        {
            sqlParts.Add(includeCreatedAudit ? "PersalNumber" : "[PersalNumber] = @persalNumber");
            values.Add(("@persalNumber", dto.PersalNumber?.Trim()));
        }
        if (schema.Has("TelephoneNumber"))
        {
            sqlParts.Add(includeCreatedAudit ? "TelephoneNumber" : "[TelephoneNumber] = @telephoneNumber");
            values.Add(("@telephoneNumber", dto.TelephoneNumber?.Trim()));
        }
        if (schema.Has("approver_active"))
        {
            sqlParts.Add(includeCreatedAudit ? "approver_active" : "[approver_active] = @active");
            values.Add(("@active", dto.IsActive));
        }
        else if (schema.Has("IsActive"))
        {
            sqlParts.Add(includeCreatedAudit ? "IsActive" : "[IsActive] = @active");
            values.Add(("@active", dto.IsActive));
        }
        if (schema.Has("is_deleted"))
        {
            sqlParts.Add(includeCreatedAudit ? "is_deleted" : "[is_deleted] = @isDeleted");
            values.Add(("@isDeleted", !dto.IsActive));
        }
        if (includeCreatedAudit && schema.Has("date_created"))
        {
            sqlParts.Add("date_created");
            values.Add(("@dateCreated", DateTime.UtcNow));
        }
        if (includeCreatedAudit && schema.Has("created_by_user_code"))
        {
            sqlParts.Add("created_by_user_code");
            values.Add(("@createdByUserCode", currentUserId));
        }
    }

    private static async Task<List<AuthoriserRecord>> ReadAuthorisersAsync(DbCommand command, TableSchema schema)
    {
        var result = new List<AuthoriserRecord>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(ReadAuthoriser(reader, schema));
        }

        return result;
    }

    private static async Task<AuthoriserRecord?> ReadAuthoriserAsync(DbCommand command, TableSchema schema)
    {
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadAuthoriser(reader, schema) : null;
    }

    private static async Task<AuthoriserRecord?> ReadAuthoriserByIdAsync(DbConnection connection, TableSchema schema, int id)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"{BuildApproverSelect(schema)} WHERE [approver_code] = @id";
        AddParameter(command, "@id", id);
        return await ReadAuthoriserAsync(command, schema);
    }

    private static AuthoriserRecord ReadAuthoriser(DbDataReader reader, TableSchema schema)
    {
        return new AuthoriserRecord
        {
            AuthoriserCode = ReadInt(reader, "AuthoriserCode") ?? 0,
            SiteCode = ReadNullableShort(reader, "SiteCode"),
            DepartmentCode = ReadInt(reader, "DepartmentCode") ?? 0,
            RankCode = ReadInt(reader, "RankCode") ?? 0,
            Surname = ReadString(reader, "Surname"),
            Firstname = ReadString(reader, "Firstname"),
            PersalNumber = ReadString(reader, "PersalNumber"),
            TelephoneNumber = ReadString(reader, "TelephoneNumber"),
            IsActive = ReadBool(reader, "IsActive"),
            LegacyFieldsAvailable = ReadBool(reader, "LegacyFieldsAvailable")
        };
    }

    private static AuthoriserDto MapToDto(AuthoriserRecord authoriser)
    {
        return new AuthoriserDto
        {
            AuthoriserCode = authoriser.AuthoriserCode,
            RankCode = authoriser.RankCode,
            Surname = authoriser.Surname ?? string.Empty,
            Firstname = authoriser.Firstname ?? string.Empty,
            PersalNumber = authoriser.PersalNumber,
            TelephoneNumber = authoriser.TelephoneNumber,
            IsActive = authoriser.IsActive,
            SiteCode = authoriser.SiteCode,
            DepartmentCode = authoriser.DepartmentCode,
            LegacyFieldsAvailable = authoriser.LegacyFieldsAvailable
        };
    }

    private ActionResult HandleFailure(Exception ex, string operation)
    {
        _logger.LogError(ex, "Error {Operation}", operation);
        return ex is DbException
            ? StatusCode(503, new { message = "The authoriser database is unavailable." })
            : StatusCode(500, new { message = $"Error {operation}." });
    }

    private static string QuoteIdentifier(string identifier) => $"[{identifier}]";

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static int? ReadInt(DbDataReader reader, string name)
    {
        var value = reader[name];
        return value is DBNull ? null : Convert.ToInt32(value);
    }

    private static short? ReadNullableShort(DbDataReader reader, string name)
    {
        var value = reader[name];
        return value is DBNull ? null : Convert.ToInt16(value);
    }

    private static string? ReadString(DbDataReader reader, string name)
    {
        var value = reader[name];
        return value is DBNull ? null : value.ToString()?.Trim();
    }

    private static bool ReadBool(DbDataReader reader, string name)
    {
        var value = reader[name];
        return value is not DBNull && Convert.ToBoolean(value);
    }

    private sealed record TableSchema(string Name, IReadOnlySet<string> Columns)
    {
        public bool Has(string column) => Columns.Contains(column);
    }

    private sealed class AuthoriserRecord
    {
        public int AuthoriserCode { get; init; }
        public short? SiteCode { get; init; }
        public int DepartmentCode { get; init; }
        public int RankCode { get; init; }
        public string? Surname { get; init; }
        public string? Firstname { get; init; }
        public string? PersalNumber { get; init; }
        public string? TelephoneNumber { get; init; }
        public bool IsActive { get; init; }
        public bool LegacyFieldsAvailable { get; init; }
    }
}

public class CreateAuthoriserDto
{
    public int RankCode { get; set; }
    public string Surname { get; set; } = string.Empty;
    public string Firstname { get; set; } = string.Empty;
    public string? PersalNumber { get; set; }
    public string? TelephoneNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public short? SiteCode { get; set; }
    public int DepartmentCode { get; set; }
}

public class UpdateAuthoriserDto : CreateAuthoriserDto
{
}

public sealed class AuthoriserDto : CreateAuthoriserDto
{
    public int AuthoriserCode { get; set; }
    public bool LegacyFieldsAvailable { get; set; }
}

public sealed class AuthoriserRankDto
{
    public int Id { get; set; }
    public string? Description { get; set; }
}
