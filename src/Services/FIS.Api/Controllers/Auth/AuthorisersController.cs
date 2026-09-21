using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Infrastructure.Repositories;
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
[Authorize(Roles = "Driver and Authoriser Management")]
[Route("api/authorisers")]
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "All interpolated SQL identifiers come from fixed schema constants or the fixed column set returned by INFORMATION_SCHEMA; request values are parameters."
)]
public sealed class AuthorisersController : BaseApiController
{
    private const int DefaultPageSize = 24;
    private const int MaximumPageSize = 100;
    private const string ApproversTable = "approvers";
    private const string RanksTable = "ranks";
    private const string InsertApproverProcedure = "DEV_INS_Approvers";
    private const string UpdateApproverProcedure = "DEV_UPD_Approvers";
    private const string DeleteApproverProcedure = "DEV_DEL_Approvers";

    private static readonly string[][] InsertApproverParameterSets =
    [
        [
            "@Approver_Code",
            "@Site_code",
            "@Department_code",
            "@Surname",
            "@FirstName",
            "@Rank_Code",
            "@TelephoneNumber",
        ],
        [
            "@ApproverCode",
            "@SiteCode",
            "@DepartmentCode",
            "@RankCode",
            "@Surname",
            "@Firstname",
            "@TelephoneNumber",
        ],
    ];

    private static readonly string[][] UpdateApproverParameterSets =
    [
        [
            "@Approver_Code",
            "@Site_code",
            "@Department_code",
            "@Surname",
            "@FirstName",
            "@Rank_Code",
            "@TelephoneNumber",
        ],
        [
            "@ApproverCode",
            "@SiteCode",
            "@DepartmentCode",
            "@RankCode",
            "@Surname",
            "@Firstname",
            "@TelephoneNumber",
        ],
        [
            "@Approver_Code",
            "@Site_code",
            "@Department_code",
            "@Surname",
            "@Firstname",
            "@Rank_Code",
            "@TelephoneNumber",
        ],
    ];

    private static readonly string[][] DeleteApproverParameterSets = [["@ID"]];

    private readonly FisDbContext _context;
    private readonly AuthoriserLookupOverlay _lookupOverlay;
    private readonly ILogger<AuthorisersController> _logger;

    public AuthorisersController(
        FisDbContext context,
        AuthoriserLookupOverlay lookupOverlay,
        ILogger<AuthorisersController> logger
    )
    {
        _context = context;
        _lookupOverlay = lookupOverlay;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AuthoriserDto>>> GetAuthorisers(
        [FromQuery] short? siteCode,
        [FromQuery] int? departmentCode
    )
    {
        try
        {
            var overlayRows =
                siteCode is > 0 && departmentCode is > 0
                    ? await _lookupOverlay.ReadApproversForEditRowsAsync(
                        siteCode.Value,
                        departmentCode.Value
                    )
                    : null;
            var leftover = await QueryLeftoverAuthorisersAsync(
                overlayRows is null ? siteCode : null,
                activeOnly: overlayRows is null
            );
            if (overlayRows is not null)
            {
                var overlaid = OverlayAuthorisers(overlayRows, leftover);
                if (overlaid is not null)
                {
                    return Ok(overlaid.Select(MapToDto));
                }
            }

            return Ok(leftover.Select(MapToDto));
        }
        catch (Exception ex)
        {
            return HandleFailure(ex, "retrieving authorisers");
        }
    }

    [HttpGet("page")]
    public async Task<ActionResult> GetAuthorisersPage(
        [FromQuery] short? siteCode,
        [FromQuery] int? departmentCode,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize
    )
    {
        if (siteCode is null)
        {
            return BadRequest(new { message = "siteCode is required." });
        }

        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaximumPageSize);

        try
        {
            var overlayRows =
                departmentCode is > 0
                    ? await _lookupOverlay.ReadApproversForEditRowsAsync(
                        siteCode.Value,
                        departmentCode.Value
                    )
                    : null;
            if (overlayRows is not null)
            {
                var leftover = await QueryLeftoverAuthorisersAsync(siteCode: null, activeOnly: false);
                var overlaid = OverlayAuthorisers(overlayRows, leftover);
                if (overlaid is not null)
                {
                    return Ok(PageAuthorisers(overlaid, normalizedPage, normalizedPageSize));
                }
            }

            var result = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, ApproversTable);
                EnsureApproverTable(schema);

                var filter = $"{BuildActivePredicate(schema)} AND [site_code] = @siteCode";
                var total = await CountAuthorisersAsync(connection, filter, siteCode.Value);
                var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)normalizedPageSize));
                var currentPage = Math.Min(normalizedPage, totalPages);
                var offset = checked((long)(currentPage - 1) * normalizedPageSize);

                await using var command = connection.CreateCommand();
                command.CommandText =
                    $"{BuildApproverSelect(schema)} WHERE {filter} ORDER BY [Surname], [Firstname], [approver_code] OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";
                AddParameter(command, "@siteCode", siteCode.Value);
                AddParameter(command, "@offset", offset);
                AddParameter(command, "@pageSize", normalizedPageSize);

                var items = (await ReadAuthorisersAsync(command, schema)).Select(MapToDto).ToList();
                return (
                    Items: items,
                    Page: currentPage,
                    PageSize: normalizedPageSize,
                    Total: total
                );
            });

            return Ok(
                new
                {
                    items = result.Items,
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.Total,
                    totalPages = Math.Max(
                        1,
                        (int)Math.Ceiling(result.Total / (double)result.PageSize)
                    ),
                }
            );
        }
        catch (Exception ex)
        {
            return HandleFailure(ex, "retrieving paged authorisers");
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AuthoriserDto>> GetAuthoriser(int id)
    {
        try
        {
            var overlayRows = await _lookupOverlay.ReadSingleApproverRowsAsync(id);
            AuthoriserRecord? leftover = null;
            if (overlayRows is null || overlayRows.Count > 0)
            {
                try
                {
                    leftover = await WithConnectionAsync(async connection =>
                    {
                        var schema = await ReadTableSchemaAsync(connection, ApproversTable);
                        EnsureApproverTable(schema);
                        return await ReadAuthoriserByIdAsync(connection, schema, id);
                    });
                }
                catch (Exception ex) when (overlayRows is not null)
                {
                    _logger.LogWarning(ex, "Leftover authoriser {AuthoriserCode} could not be hydrated", id);
                }
            }

            AuthoriserRecord? authoriser;
            if (overlayRows is null)
            {
                authoriser = leftover;
            }
            else if (overlayRows.Count == 0)
            {
                authoriser = null;
            }
            else
            {
                authoriser = leftover ?? MapAuthoriserFromOverlay(overlayRows[0]);
            }

            return authoriser is null
                ? NotFound(new { message = $"Authoriser not found with code: {id}" })
                : Ok(MapToDto(authoriser));
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
            var overlayRows = await _lookupOverlay.ReadRankRowsAsync();
            var leftover = await QueryLeftoverRanksAsync();
            if (overlayRows is not null)
            {
                var overlaid = OverlayRanks(overlayRows, leftover);
                if (overlaid is not null)
                {
                    return Ok(overlaid);
                }
            }

            return Ok(leftover);
        }
        catch (Exception ex)
        {
            if (IsLegacySelectorContractException(ex))
            {
                return HandleFailure(ex, "retrieving authoriser ranks");
            }

            _logger.LogWarning(
                ex,
                "Unable to retrieve authoriser ranks; returning an empty lookup"
            );
            return Ok(Array.Empty<AuthoriserRankDto>());
        }
    }

    [HttpGet("ranks/page")]
    public async Task<ActionResult> GetRanksPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize
    )
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
        try
        {
            var result = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, RanksTable);
                if (!schema.Has("rank_code") || !schema.Has("description"))
                {
                    return (
                        Items: new List<AuthoriserRankDto>(),
                        Page: 1,
                        PageSize: normalizedPageSize,
                        Total: 0
                    );
                }

                var activePredicate = schema.Has("is_deleted")
                    ? "([is_deleted] = 0 OR [is_deleted] IS NULL)"
                    : "1 = 1";
                await using var countCommand = connection.CreateCommand();
                countCommand.CommandText =
                    $"SELECT COUNT(1) FROM [dbo].[{RanksTable}] WHERE {activePredicate}";
                var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
                var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)normalizedPageSize));
                var currentPage = Math.Min(normalizedPage, totalPages);
                var offset = checked((long)(currentPage - 1) * normalizedPageSize);

                await using var command = connection.CreateCommand();
                command.CommandText =
                    $"SELECT [rank_code] AS [RankCode], [description] AS [Description] FROM [dbo].[{RanksTable}] WHERE {activePredicate} ORDER BY [description], [rank_code] OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";
                AddParameter(command, "@offset", offset);
                AddParameter(command, "@pageSize", normalizedPageSize);
                return (
                    Items: await ReadRanksFromCommandAsync(command),
                    Page: currentPage,
                    PageSize: normalizedPageSize,
                    Total: total
                );
            });
            return Ok(
                new
                {
                    items = result.Items,
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.Total,
                    totalPages = Math.Max(
                        1,
                        (int)Math.Ceiling(result.Total / (double)result.PageSize)
                    ),
                }
            );
        }
        catch (Exception ex)
        {
            return HandleFailure(ex, "retrieving paged authoriser ranks");
        }
    }

    [HttpPost("ranks")]
    public async Task<ActionResult<IEnumerable<AuthoriserRankDto>>> SaveRanks(
        [FromBody] List<AuthoriserRankDto> ranks
    )
    {
        try
        {
            if (ranks is null)
            {
                return BadRequest(new { message = "At least one rank is required." });
            }

            var savedRanks = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, RanksTable);
                EnsureRankTable(schema);

                foreach (var rank in ranks)
                {
                    var description = rank.RankName ?? rank.Description;
                    ValidateRank(description, schema);

                    if (rank.Id <= 0)
                    {
                        var columns = new List<string> { "description" };
                        var values = new List<(string Name, object? Value)>
                        {
                            ("@description", description!.Trim()),
                        };
                        AddOptionalRankInsertFields(schema, columns, values, GetCurrentUserId());

                        await using var insertCommand = connection.CreateCommand();
                        insertCommand.CommandText =
                            $"INSERT INTO [dbo].[{RanksTable}] ({string.Join(", ", columns.Select(QuoteIdentifier))}) OUTPUT INSERTED.[rank_code] VALUES ({string.Join(", ", values.Select(item => item.Name))})";
                        AddParameters(insertCommand, values);
                        await insertCommand.ExecuteScalarAsync();
                        continue;
                    }

                    if (!await RankExistsAsync(connection, rank.Id))
                    {
                        continue;
                    }

                    var assignments = new List<string> { "[description] = @description" };
                    var updateValues = new List<(string Name, object? Value)>
                    {
                        ("@description", description!.Trim()),
                    };
                    if (schema.Has("is_deleted"))
                    {
                        assignments.Add("[is_deleted] = @isDeleted");
                        updateValues.Add(("@isDeleted", false));
                    }
                    AddOptionalRankUpdateFields(
                        schema,
                        assignments,
                        updateValues,
                        GetCurrentUserId()
                    );

                    await using var updateCommand = connection.CreateCommand();
                    updateCommand.CommandText =
                        $"UPDATE [dbo].[{RanksTable}] SET {string.Join(", ", assignments)} WHERE [rank_code] = @id";
                    AddParameters(updateCommand, updateValues);
                    AddParameter(updateCommand, "@id", rank.Id);
                    await updateCommand.ExecuteNonQueryAsync();
                }

                return await ReadRanksAsync(connection, schema);
            });

            return Ok(savedRanks);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return HandleFailure(ex, "saving authoriser ranks");
        }
    }

    [HttpPost]
    public async Task<ActionResult<AuthoriserDto>> CreateAuthoriser(
        [FromBody] CreateAuthoriserDto dto
    )
    {
        try
        {
            var created = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, ApproversTable);
                EnsureApproverTable(schema);
                ValidateAuthoriser(dto, schema);

                var procedureParameters = await _lookupOverlay.TryGetProcedureParametersAsync(
                    InsertApproverProcedure
                );
                if (procedureParameters is not null)
                {
                    if (
                        !AuthoriserLookupOverlay.MatchesParameterSet(
                            procedureParameters,
                            InsertApproverParameterSets
                        )
                    )
                    {
                        throw new LegacyAuthoriserProcedureContractException(
                            $"The deployed legacy procedure {InsertApproverProcedure} does not match its verified parameter contract. No direct-DML fallback was run."
                        );
                    }

                    var createdId = await ExecuteInsertApproverProcedureAsync(
                        connection,
                        procedureParameters,
                        dto
                    );
                    return await ReadAuthoriserByIdAsync(connection, schema, createdId);
                }

                var columns = new List<string>
                {
                    "site_code",
                    "department_code",
                    "rank_code",
                    "Surname",
                    "Firstname",
                };
                var values = new List<(string Name, object? Value)>
                {
                    ("@siteCode", dto.SiteCode),
                    ("@departmentCode", dto.DepartmentCode),
                    ("@rankCode", dto.RankCode),
                    ("@surname", dto.Surname.Trim()),
                    ("@firstname", dto.Firstname.Trim()),
                };

                AddOptionalAuthoriserFields(
                    schema,
                    columns,
                    values,
                    dto,
                    includeCreatedAudit: true,
                    currentUserId: GetCurrentUserId()
                );

                await using var command = connection.CreateCommand();
                command.CommandText =
                    $"INSERT INTO [dbo].[{ApproversTable}] ({string.Join(", ", columns.Select(QuoteIdentifier))}) OUTPUT INSERTED.[approver_code] VALUES ({string.Join(", ", values.Select(item => item.Name))})";
                foreach (var (name, value) in values)
                {
                    AddParameter(command, name, value);
                }

                var leftoverCreatedId = Convert.ToInt32(await command.ExecuteScalarAsync());
                return await ReadAuthoriserByIdAsync(connection, schema, leftoverCreatedId);
            });

            return created is null
                ? StatusCode(
                    500,
                    new { message = "The authoriser was created but could not be reloaded." }
                )
                : CreatedAtAction(
                    nameof(GetAuthoriser),
                    new { id = created.AuthoriserCode },
                    MapToDto(created)
                );
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
    public async Task<ActionResult<AuthoriserDto>> UpdateAuthoriser(
        int id,
        [FromBody] UpdateAuthoriserDto dto
    )
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

                var procedureParameters = await _lookupOverlay.TryGetProcedureParametersAsync(
                    UpdateApproverProcedure
                );
                if (procedureParameters is not null)
                {
                    if (
                        !AuthoriserLookupOverlay.MatchesParameterSet(
                            procedureParameters,
                            UpdateApproverParameterSets
                        )
                    )
                    {
                        throw new LegacyAuthoriserProcedureContractException(
                            $"The deployed legacy procedure {UpdateApproverProcedure} does not match its verified parameter contract. No direct-DML fallback was run."
                        );
                    }

                    await ExecuteUpdateApproverProcedureAsync(
                        connection,
                        procedureParameters,
                        id,
                        dto
                    );
                    return await ReadAuthoriserByIdAsync(connection, schema, id);
                }

                var assignments = new List<string>
                {
                    "[site_code] = @siteCode",
                    "[department_code] = @departmentCode",
                    "[rank_code] = @rankCode",
                    "[Surname] = @surname",
                    "[Firstname] = @firstname",
                };
                var values = new List<(string Name, object? Value)>
                {
                    ("@siteCode", dto.SiteCode),
                    ("@departmentCode", dto.DepartmentCode),
                    ("@rankCode", dto.RankCode),
                    ("@surname", dto.Surname.Trim()),
                    ("@firstname", dto.Firstname.Trim()),
                };

                AddOptionalAuthoriserFields(
                    schema,
                    assignments,
                    values,
                    dto,
                    includeCreatedAudit: false,
                    currentUserId: GetCurrentUserId()
                );
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
                command.CommandText =
                    $"UPDATE [dbo].[{ApproversTable}] SET {string.Join(", ", assignments)} WHERE [approver_code] = @id";
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

                var procedureParameters = await _lookupOverlay.TryGetProcedureParametersAsync(
                    DeleteApproverProcedure
                );
                if (procedureParameters is not null)
                {
                    if (
                        !AuthoriserLookupOverlay.MatchesParameterSet(
                            procedureParameters,
                            DeleteApproverParameterSets
                        )
                    )
                    {
                        throw new LegacyAuthoriserProcedureContractException(
                            $"The deployed legacy procedure {DeleteApproverProcedure} does not match its verified parameter contract. No direct-DML fallback was run."
                        );
                    }

                    await ExecuteDeleteApproverProcedureAsync(connection, id);
                    return true;
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
                    throw new InvalidOperationException(
                        "The authoriser table has no supported active-state column."
                    );
                }

                await using var command = connection.CreateCommand();
                command.CommandText =
                    $"UPDATE [dbo].[{ApproversTable}] SET {string.Join(", ", assignments)} WHERE [approver_code] = @id";
                AddParameter(command, "@active", false);
                AddParameter(command, "@isDeleted", true);
                AddParameter(command, "@dateUpdated", DateTime.UtcNow);
                AddParameter(command, "@modifiedByUserCode", GetCurrentUserId());
                AddParameter(command, "@id", id);
                await command.ExecuteNonQueryAsync();
                return true;
            });

            return deleted
                ? NoContent()
                : NotFound(new { message = $"Authoriser not found with code: {id}" });
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

    private static async Task<TableSchema> ReadTableSchemaAsync(
        DbConnection connection,
        string tableName
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table";
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
        string[] requiredColumns =
        [
            "approver_code",
            "site_code",
            "department_code",
            "rank_code",
            "Surname",
            "Firstname",
        ];
        if (requiredColumns.Any(column => !schema.Has(column)))
        {
            throw new InvalidOperationException(
                "The dbo.approvers table is missing one or more required legacy columns."
            );
        }
    }

    private static void EnsureRankTable(TableSchema schema)
    {
        if (!schema.Has("rank_code") || !schema.Has("description"))
        {
            throw new InvalidOperationException(
                "The dbo.ranks table is missing one or more required legacy columns."
            );
        }
    }

    private static void ValidateRank(string? description, TableSchema schema)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("The rank description is required.");
        }

        var maxLength = schema.Has("is_deleted") ? 255 : 50;
        if (description.Trim().Length > maxLength)
        {
            throw new ArgumentException(
                $"The rank description must be {maxLength} characters or fewer."
            );
        }
    }

    private static async Task<bool> RankExistsAsync(DbConnection connection, int id)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT 1 FROM [dbo].[{RanksTable}] WHERE [rank_code] = @id";
        AddParameter(command, "@id", id);
        return await command.ExecuteScalarAsync() is not null;
    }

    private static async Task<List<AuthoriserRankDto>> ReadRanksAsync(
        DbConnection connection,
        TableSchema schema
    )
    {
        var activePredicate = schema.Has("is_deleted")
            ? "([is_deleted] = 0 OR [is_deleted] IS NULL)"
            : "1 = 1";
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT [rank_code] AS [RankCode], [description] AS [Description] FROM [dbo].[{RanksTable}] WHERE {activePredicate} ORDER BY [description], [rank_code]";

        return await ReadRanksFromCommandAsync(command);
    }

    private static async Task<List<AuthoriserRankDto>> ReadRanksFromCommandAsync(DbCommand command)
    {
        var result = new List<AuthoriserRankDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var rankCode = ReadInt(reader, "RankCode");
            if (rankCode.HasValue)
            {
                var description = ReadString(reader, "Description");
                result.Add(
                    new AuthoriserRankDto
                    {
                        Id = rankCode.Value,
                        RankName = description,
                        Description = description,
                    }
                );
            }
        }

        return result;
    }

    private static void AddOptionalRankInsertFields(
        TableSchema schema,
        ICollection<string> columns,
        ICollection<(string Name, object? Value)> values,
        int currentUserId
    )
    {
        if (schema.Has("date_created"))
        {
            columns.Add("date_created");
            values.Add(("@dateCreated", DateTime.UtcNow));
        }
        if (schema.Has("created_by_user_code"))
        {
            columns.Add("created_by_user_code");
            values.Add(("@createdByUserCode", currentUserId));
        }
        if (schema.Has("is_deleted"))
        {
            columns.Add("is_deleted");
            values.Add(("@isDeleted", false));
        }
    }

    private static void AddOptionalRankUpdateFields(
        TableSchema schema,
        ICollection<string> assignments,
        ICollection<(string Name, object? Value)> values,
        int currentUserId
    )
    {
        if (schema.Has("date_updated"))
        {
            assignments.Add("[date_updated] = @dateUpdated");
            values.Add(("@dateUpdated", DateTime.UtcNow));
        }
        if (schema.Has("modified_by_user_code"))
        {
            assignments.Add("[modified_by_user_code] = @modifiedByUserCode");
            values.Add(("@modifiedByUserCode", currentUserId));
        }
    }

    private static string BuildApproverSelect(TableSchema schema)
    {
        var persal = schema.Has("PersalNumber")
            ? "CAST([PersalNumber] AS nvarchar(50))"
            : "CAST(NULL AS nvarchar(50))";
        var telephone = schema.Has("TelephoneNumber")
            ? "CAST([TelephoneNumber] AS nvarchar(50))"
            : "CAST(NULL AS nvarchar(50))";
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

    private static async Task<int> CountAuthorisersAsync(
        DbConnection connection,
        string filter,
        short siteCode
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM [dbo].[{ApproversTable}] WHERE {filter}";
        AddParameter(command, "@siteCode", siteCode);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
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
        int currentUserId
    )
    {
        if (schema.Has("PersalNumber"))
        {
            sqlParts.Add(includeCreatedAudit ? "PersalNumber" : "[PersalNumber] = @persalNumber");
            values.Add(("@persalNumber", dto.PersalNumber?.Trim()));
        }
        if (schema.Has("TelephoneNumber"))
        {
            sqlParts.Add(
                includeCreatedAudit ? "TelephoneNumber" : "[TelephoneNumber] = @telephoneNumber"
            );
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

    private static async Task<List<AuthoriserRecord>> ReadAuthorisersAsync(
        DbCommand command,
        TableSchema schema
    )
    {
        var result = new List<AuthoriserRecord>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(ReadAuthoriser(reader, schema));
        }

        return result;
    }

    private static async Task<AuthoriserRecord?> ReadAuthoriserAsync(
        DbCommand command,
        TableSchema schema
    )
    {
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadAuthoriser(reader, schema) : null;
    }

    private static async Task<AuthoriserRecord?> ReadAuthoriserByIdAsync(
        DbConnection connection,
        TableSchema schema,
        int id
    )
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
            LegacyFieldsAvailable = ReadBool(reader, "LegacyFieldsAvailable"),
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
            LegacyFieldsAvailable = authoriser.LegacyFieldsAvailable,
        };
    }

    private async Task<List<AuthoriserRecord>> QueryLeftoverAuthorisersAsync(
        short? siteCode,
        bool activeOnly
    )
    {
        return await WithConnectionAsync(async connection =>
        {
            var schema = await ReadTableSchemaAsync(connection, ApproversTable);
            EnsureApproverTable(schema);
            var filter = activeOnly ? BuildActivePredicate(schema) : "1 = 1";
            if (siteCode.HasValue)
            {
                filter += " AND [site_code] = @siteCode";
            }

            await using var command = connection.CreateCommand();
            command.CommandText =
                $"{BuildApproverSelect(schema)} WHERE {filter} ORDER BY [Surname], [Firstname], [approver_code]";
            if (siteCode.HasValue)
            {
                AddParameter(command, "@siteCode", siteCode.Value);
            }

            return await ReadAuthorisersAsync(command, schema);
        });
    }

    private async Task<List<AuthoriserRankDto>> QueryLeftoverRanksAsync()
    {
        try
        {
            return await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, RanksTable);
                if (!schema.Has("rank_code") || !schema.Has("description"))
                {
                    return new List<AuthoriserRankDto>();
                }

                return await ReadRanksAsync(connection, schema);
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Leftover authoriser ranks could not be hydrated");
            return [];
        }
    }

    private static List<AuthoriserRecord>? OverlayAuthorisers(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> overlayRows,
        IReadOnlyList<AuthoriserRecord> leftover
    )
    {
        if (overlayRows.Count == 0)
        {
            return [];
        }

        var leftoverByCode = leftover
            .Where(item => item.AuthoriserCode > 0)
            .GroupBy(item => item.AuthoriserCode)
            .ToDictionary(group => group.Key, group => group.First());
        var mapped = new List<AuthoriserRecord>();
        var seen = new HashSet<int>();
        foreach (var row in overlayRows)
        {
            var code = AuthoriserLookupOverlay.ReadInt32(
                row,
                "Code",
                "Approver_Code",
                "ApproverCode",
                "AuthoriserCode"
            );
            if (code is null or <= 0 || !seen.Add(code.Value))
            {
                continue;
            }

            leftoverByCode.TryGetValue(code.Value, out var leftoverRecord);
            var mappedRecord = leftoverRecord ?? MapAuthoriserFromOverlay(row);
            if (mappedRecord is null)
            {
                continue;
            }

            mapped.Add(mappedRecord);
        }

        return mapped.Count == 0 ? null : mapped;
    }

    private static List<AuthoriserRankDto>? OverlayRanks(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> overlayRows,
        IReadOnlyList<AuthoriserRankDto> leftover
    )
    {
        if (overlayRows.Count == 0)
        {
            return [];
        }

        var leftoverById = leftover
            .Where(item => item.Id > 0)
            .GroupBy(item => item.Id)
            .ToDictionary(group => group.Key, group => group.First());
        var mapped = new List<AuthoriserRankDto>();
        var seen = new HashSet<int>();
        foreach (var row in overlayRows)
        {
            var id = AuthoriserLookupOverlay.ReadInt32(row, "Rank_Code", "rank_code", "RankCode", "Id");
            if (id is null or <= 0 || !seen.Add(id.Value))
            {
                continue;
            }

            leftoverById.TryGetValue(id.Value, out var leftoverRank);
            var description = AuthoriserLookupOverlay.ReadString(
                row,
                "Rank Name",
                "description",
                "Description",
                "RankName"
            );
            mapped.Add(
                leftoverRank is null
                    ? new AuthoriserRankDto
                    {
                        Id = id.Value,
                        RankName = description,
                        Description = description,
                    }
                    : leftoverRank
            );
        }

        return mapped.Count == 0 ? null : mapped;
    }

    private static object PageAuthorisers(
        IReadOnlyList<AuthoriserRecord> authorisers,
        int page,
        int pageSize
    )
    {
        var total = authorisers.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        var currentPage = Math.Min(Math.Max(1, page), totalPages);
        var skip = (currentPage - 1) * pageSize;
        return new
        {
            items = authorisers.Skip(skip).Take(pageSize).Select(MapToDto).ToList(),
            page = currentPage,
            pageSize,
            total,
            totalPages,
        };
    }

    private static AuthoriserRecord? MapAuthoriserFromOverlay(
        IReadOnlyDictionary<string, object?> row
    )
    {
        var code = AuthoriserLookupOverlay.ReadInt32(
            row,
            "Approver_Code",
            "ApproverCode",
            "Code",
            "AuthoriserCode"
        );
        if (code is null or <= 0)
        {
            return null;
        }

        var siteCode = AuthoriserLookupOverlay.ReadInt32(row, "Site_code", "SiteCode", "site_code");
        return new AuthoriserRecord
        {
            AuthoriserCode = code.Value,
            SiteCode =
                siteCode is > 0 and <= short.MaxValue ? (short)siteCode.Value : null,
            DepartmentCode =
                AuthoriserLookupOverlay.ReadInt32(
                    row,
                    "Department_code",
                    "DepartmentCode",
                    "department_code"
                ) ?? 0,
            RankCode =
                AuthoriserLookupOverlay.ReadInt32(row, "Rank_Code", "RankCode", "rank_code") ?? 0,
            Surname = AuthoriserLookupOverlay.ReadString(row, "Surname"),
            Firstname = AuthoriserLookupOverlay.ReadString(row, "Firstname", "FirstName"),
            TelephoneNumber = AuthoriserLookupOverlay.ReadString(row, "TelephoneNumber"),
            IsActive = true,
            LegacyFieldsAvailable = !string.IsNullOrWhiteSpace(
                AuthoriserLookupOverlay.ReadString(row, "TelephoneNumber")
            ),
        };
    }

    private static async Task<int> ExecuteInsertApproverProcedureAsync(
        DbConnection connection,
        IReadOnlyList<string> actualParameters,
        CreateAuthoriserDto dto
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = $"[dbo].[{InsertApproverProcedure}]";
        BindApproverProcedureParameters(command, actualParameters, dto, outputApproverCode: true);
        await command.ExecuteNonQueryAsync();
        var output = command
            .Parameters.Cast<DbParameter>()
            .FirstOrDefault(parameter => parameter.Direction != ParameterDirection.Input);
        if (
            output?.Value is null or DBNull
            || !int.TryParse(output.Value.ToString(), out var id)
            || id <= 0
        )
        {
            throw new LegacyAuthoriserProcedureContractException(
                $"The deployed legacy procedure {InsertApproverProcedure} did not return an authoriser code."
            );
        }

        return id;
    }

    private static async Task ExecuteUpdateApproverProcedureAsync(
        DbConnection connection,
        IReadOnlyList<string> actualParameters,
        int approverCode,
        CreateAuthoriserDto dto
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = $"[dbo].[{UpdateApproverProcedure}]";
        BindApproverProcedureParameters(
            command,
            actualParameters,
            dto,
            outputApproverCode: false,
            approverCode
        );
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteDeleteApproverProcedureAsync(DbConnection connection, int id)
    {
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = $"[dbo].[{DeleteApproverProcedure}]";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@ID";
        parameter.DbType = DbType.Int32;
        parameter.Value = id;
        command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync();
    }

    private static void BindApproverProcedureParameters(
        DbCommand command,
        IReadOnlyList<string> actualParameters,
        CreateAuthoriserDto dto,
        bool outputApproverCode,
        int? approverCode = null
    )
    {
        foreach (var name in actualParameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            if (MatchesName(name, "@Approver_Code", "@ApproverCode"))
            {
                parameter.DbType = DbType.Int32;
                if (outputApproverCode)
                {
                    parameter.Direction = ParameterDirection.Output;
                    parameter.Value = DBNull.Value;
                }
                else
                {
                    parameter.Value = approverCode ?? 0;
                }
            }
            else if (MatchesName(name, "@Site_code", "@SiteCode"))
            {
                parameter.DbType = DbType.Int32;
                parameter.Value = dto.SiteCode.HasValue ? dto.SiteCode.Value : DBNull.Value;
            }
            else if (MatchesName(name, "@Department_code", "@DepartmentCode"))
            {
                parameter.DbType = DbType.Int32;
                parameter.Value = dto.DepartmentCode;
            }
            else if (MatchesName(name, "@Rank_Code", "@RankCode"))
            {
                parameter.DbType = DbType.Int32;
                parameter.Value = dto.RankCode;
            }
            else if (MatchesName(name, "@Surname"))
            {
                parameter.DbType = DbType.String;
                parameter.Value = dto.Surname.Trim();
            }
            else if (MatchesName(name, "@FirstName", "@Firstname"))
            {
                parameter.DbType = DbType.String;
                parameter.Value = dto.Firstname.Trim();
            }
            else if (MatchesName(name, "@TelephoneNumber"))
            {
                parameter.DbType = DbType.String;
                parameter.Value = string.IsNullOrWhiteSpace(dto.TelephoneNumber)
                    ? DBNull.Value
                    : dto.TelephoneNumber.Trim();
            }
            else
            {
                throw new LegacyAuthoriserProcedureContractException(
                    $"The deployed legacy procedure uses unverified parameter {name}."
                );
            }

            command.Parameters.Add(parameter);
        }
    }

    private static bool MatchesName(string actual, params string[] expected)
    {
        return expected.Any(name =>
            string.Equals(actual, name, StringComparison.OrdinalIgnoreCase)
        );
    }

    private static bool IsLegacySelectorContractException(Exception ex)
    {
        return ex is LegacyAuthoriserProcedureContractException
            || (
                ex is InvalidOperationException
                && ex.Message.Contains(
                    "does not match its verified parameter contract",
                    StringComparison.Ordinal
                )
            );
    }

    private ActionResult HandleFailure(Exception ex, string operation)
    {
        _logger.LogError(ex, "Error {Operation}", operation);
        if (IsLegacySelectorContractException(ex))
        {
            return StatusCode(503, new { message = ex.Message });
        }
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

    private static void AddParameters(
        DbCommand command,
        IEnumerable<(string Name, object? Value)> values
    )
    {
        foreach (var (name, value) in values)
        {
            AddParameter(command, name, value);
        }
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

public class UpdateAuthoriserDto : CreateAuthoriserDto { }

public sealed class AuthoriserDto : CreateAuthoriserDto
{
    public int AuthoriserCode { get; set; }
    public bool LegacyFieldsAvailable { get; set; }
}

public sealed class AuthoriserRankDto
{
    public int Id { get; set; }
    public string? RankName { get; set; }
    public string? Description { get; set; }
}

public sealed class LegacyAuthoriserProcedureContractException : InvalidOperationException
{
    public LegacyAuthoriserProcedureContractException(string message)
        : base(message) { }
}
