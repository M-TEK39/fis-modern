using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

/// <summary>
/// Legacy-compatible site-driver API.
///
/// The client database exposes the original site_drivers and
/// driver_licence_types columns. Expanded databases may add the shared audit
/// columns. The controller only references those optional columns after
/// discovering them, keeping both database shapes usable without migrations.
/// </summary>
[ApiController]
[Authorize]
[Route("api/site-drivers")]
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "All interpolated SQL identifiers come from fixed table and column names; request values are parameters.")]
public sealed class SiteDriversController : BaseApiController
{
    private const string DriversTable = "site_drivers";
    private const string LicenceTypesTable = "driver_licence_types";

    private readonly FisDbContext _context;
    private readonly ILogger<SiteDriversController> _logger;

    public SiteDriversController(FisDbContext context, ILogger<SiteDriversController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DriverDto>>> GetDrivers([FromQuery] int? siteCode)
    {
        try
        {
            var drivers = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, DriversTable);
                EnsureDriverTable(schema);

                var siteFilter = siteCode.HasValue ? " AND [site_code] = @siteCode" : string.Empty;
                await using var command = connection.CreateCommand();
                command.CommandText = $"{BuildDriverSelect(schema)} WHERE {BuildDriverPredicate(schema)}{siteFilter} ORDER BY [driver_surname], [driver_firstname], [site_driver_code]";
                if (siteCode.HasValue)
                {
                    AddParameter(command, "@siteCode", siteCode.Value);
                }

                return await ReadDriversAsync(command);
            });

            return Ok(drivers);
        }
        catch (Exception ex)
        {
            return HandleFailure(ex, "retrieving site drivers");
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DriverDto>> GetDriver(int id)
    {
        try
        {
            var driver = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, DriversTable);
                EnsureDriverTable(schema);
                return await ReadDriverByIdAsync(connection, schema, id);
            });

            return driver is null ? NotFound(new { message = $"Site driver not found with code: {id}" }) : Ok(driver);
        }
        catch (Exception ex)
        {
            return HandleFailure(ex, $"retrieving site driver {id}");
        }
    }

    [HttpGet("licence-types")]
    public async Task<ActionResult<IEnumerable<SiteDriverLicenceTypeDto>>> GetLicenceTypes()
    {
        try
        {
            var types = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, LicenceTypesTable);
                if (!schema.Has("driver_licence_type_id")
                    || !schema.Has("driver_licence_type_code")
                    || !schema.Has("driver_licence_type_description"))
                {
                    return new List<SiteDriverLicenceTypeDto>();
                }

                var activePredicate = schema.Has("is_deleted") ? "COALESCE([is_deleted], 0) = 0" : "1 = 1";
                await using var command = connection.CreateCommand();
                command.CommandText = $"SELECT [driver_licence_type_id] AS [Id], [driver_licence_type_code] AS [Code], [driver_licence_type_description] AS [Description] FROM [dbo].[{LicenceTypesTable}] WHERE {activePredicate} ORDER BY [driver_licence_type_description], [driver_licence_type_id]";

                var result = new List<SiteDriverLicenceTypeDto>();
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new SiteDriverLicenceTypeDto
                    {
                        Id = ReadInt(reader, "Id") ?? 0,
                        Code = ReadString(reader, "Code"),
                        Description = ReadString(reader, "Description")
                    });
                }

                return result.Where(item => item.Id > 0).ToList();
            });

            return Ok(types);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to retrieve site-driver licence types; returning an empty lookup");
            return Ok(Array.Empty<SiteDriverLicenceTypeDto>());
        }
    }

    [HttpPost]
    public async Task<ActionResult<DriverDto>> CreateDriver([FromBody] CreateDriverDto dto)
    {
        try
        {
            var created = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, DriversTable);
                EnsureDriverTable(schema);
                ValidateDriver(dto);

                var columns = DriverColumns.ToList();
                var values = DriverValues(dto);
                AddOptionalAuditInsertFields(schema, columns, values, GetCurrentUserId());

                await using var command = connection.CreateCommand();
                command.CommandText = $"INSERT INTO [dbo].[{DriversTable}] ({string.Join(", ", columns.Select(QuoteIdentifier))}) OUTPUT INSERTED.[site_driver_code] VALUES ({string.Join(", ", values.Select(item => item.Name))})";
                AddParameters(command, values);

                var driverId = Convert.ToInt32(await command.ExecuteScalarAsync());
                return await ReadDriverByIdAsync(connection, schema, driverId);
            });

            return created is null
                ? StatusCode(500, new { message = "The site driver was created but could not be reloaded." })
                : CreatedAtAction(nameof(GetDriver), new { id = created.SiteDriverCode }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return HandleFailure(ex, "creating a site driver");
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<DriverDto>> UpdateDriver(int id, [FromBody] UpdateDriverDto dto)
    {
        try
        {
            var updated = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, DriversTable);
                EnsureDriverTable(schema);
                ValidateDriver(dto);

                if (await ReadDriverByIdAsync(connection, schema, id) is null)
                {
                    return null;
                }

                var assignments = DriverColumns.Select(column => $"{QuoteIdentifier(column)} = @{ParameterName(column)}").ToList();
                var values = DriverValues(dto);
                AddOptionalAuditUpdateFields(schema, assignments, values, GetCurrentUserId());

                await using var command = connection.CreateCommand();
                command.CommandText = $"UPDATE [dbo].[{DriversTable}] SET {string.Join(", ", assignments)} WHERE [site_driver_code] = @siteDriverCode";
                AddParameters(command, values);
                AddParameter(command, "@siteDriverCode", id);
                await command.ExecuteNonQueryAsync();

                return await ReadDriverByIdAsync(connection, schema, id);
            });

            return updated is null
                ? NotFound(new { message = $"Site driver not found with code: {id}" })
                : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return HandleFailure(ex, $"updating site driver {id}");
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteDriver(int id)
    {
        try
        {
            var deleted = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, DriversTable);
                EnsureDriverTable(schema);

                if (await ReadDriverByIdAsync(connection, schema, id) is null)
                {
                    return false;
                }

                var assignments = new List<string> { "[driver_active] = @driverActive" };
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

                await using var command = connection.CreateCommand();
                command.CommandText = $"UPDATE [dbo].[{DriversTable}] SET {string.Join(", ", assignments)} WHERE [site_driver_code] = @siteDriverCode";
                AddParameter(command, "@driverActive", false);
                AddParameter(command, "@isDeleted", true);
                AddParameter(command, "@dateUpdated", DateTime.UtcNow);
                AddParameter(command, "@modifiedByUserCode", GetCurrentUserId());
                AddParameter(command, "@siteDriverCode", id);
                await command.ExecuteNonQueryAsync();
                return true;
            });

            return deleted ? NoContent() : NotFound(new { message = $"Site driver not found with code: {id}" });
        }
        catch (Exception ex)
        {
            return HandleFailure(ex, $"deleting site driver {id}");
        }
    }

    private static readonly string[] DriverColumns =
    [
        "site_code",
        "driver_licence_type_id",
        "driver_surname",
        "driver_firstname",
        "driver_SA_id",
        "driver_passportnumber",
        "driver_persalnumber",
        "driver_contractnumber",
        "driver_licence_number",
        "driver_licence_issuedate",
        "driver_licence_lastVerifiedDate",
        "driver_hasPDP",
        "driver_PDP_ExpiryDate",
        "driver_licence_ExpiryDate",
        "driver_active"
    ];

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
            if (!reader.IsDBNull(0))
            {
                columns.Add(reader.GetString(0));
            }
        }

        return new TableSchema(columns);
    }

    private static void EnsureDriverTable(TableSchema schema)
    {
        if (DriverColumns.Any(column => !schema.Has(column)))
        {
            throw new InvalidOperationException("The dbo.site_drivers table is missing one or more required legacy columns.");
        }
    }

    private static string BuildDriverSelect(TableSchema schema)
    {
        var columns = string.Join(", ", DriverColumns.Select(column => $"{QuoteIdentifier(column)} AS {QuoteIdentifier(column)}"));
        return $"SELECT [site_driver_code] AS [site_driver_code], {columns} FROM [dbo].[{DriversTable}]";
    }

    private static string BuildDriverPredicate(TableSchema schema)
    {
        var deletedPredicate = schema.Has("is_deleted") ? " AND COALESCE([is_deleted], 0) = 0" : string.Empty;
        return $"[driver_active] = 1{deletedPredicate}";
    }

    private static async Task<List<DriverDto>> ReadDriversAsync(DbCommand command)
    {
        var result = new List<DriverDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(ReadDriver(reader));
        }

        return result;
    }

    private static async Task<DriverDto?> ReadDriverByIdAsync(DbConnection connection, TableSchema schema, int id)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"{BuildDriverSelect(schema)} WHERE [site_driver_code] = @siteDriverCode";
        AddParameter(command, "@siteDriverCode", id);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadDriver(reader) : null;
    }

    private static DriverDto ReadDriver(DbDataReader reader)
    {
        return new DriverDto
        {
            SiteDriverCode = ReadInt(reader, "site_driver_code") ?? 0,
            SiteCode = ReadInt(reader, "site_code") ?? 0,
            DriverLicenceTypeId = ReadInt(reader, "driver_licence_type_id") ?? 0,
            DriverSurname = ReadString(reader, "driver_surname"),
            DriverFirstname = ReadString(reader, "driver_firstname"),
            DriverSAId = ReadString(reader, "driver_SA_id"),
            DriverPassportNumber = ReadString(reader, "driver_passportnumber"),
            DriverPersonalNumber = ReadString(reader, "driver_persalnumber"),
            DriverContractNumber = ReadString(reader, "driver_contractnumber"),
            DriverLicenceNumber = ReadString(reader, "driver_licence_number"),
            DriverLicenceIssueDate = ReadDateTime(reader, "driver_licence_issuedate"),
            DriverLicenceLastVerifiedDate = ReadDateTime(reader, "driver_licence_lastVerifiedDate"),
            DriverHasPDP = ReadBool(reader, "driver_hasPDP"),
            DriverPDPExpiryDate = ReadNullableDateTime(reader, "driver_PDP_ExpiryDate"),
            DriverLicenceExpiryDate = ReadNullableDateTime(reader, "driver_licence_ExpiryDate"),
            DriverActive = ReadBool(reader, "driver_active")
        };
    }

    private static void ValidateDriver(CreateDriverDto dto)
    {
        if (dto.SiteCode <= 0)
        {
            throw new ArgumentException("The site is required.");
        }
        if (dto.DriverLicenceTypeId <= 0)
        {
            throw new ArgumentException("The driver licence type is required.");
        }
        ValidateLength(dto.DriverSurname, 50, "The surname");
        ValidateLength(dto.DriverFirstname, 50, "The first name");
        ValidateLength(dto.DriverSAId, 13, "The South African ID");
        ValidateLength(dto.DriverPassportNumber, 20, "The passport number");
        ValidateLength(dto.DriverPersonalNumber, 10, "The Persal number");
        ValidateLength(dto.DriverContractNumber, 10, "The contract number");
        ValidateLength(dto.DriverLicenceNumber, 20, "The licence number");
        if (string.IsNullOrWhiteSpace(dto.DriverSurname) || string.IsNullOrWhiteSpace(dto.DriverFirstname))
        {
            throw new ArgumentException("The first name and surname are required.");
        }
        if (string.IsNullOrWhiteSpace(dto.DriverLicenceNumber))
        {
            throw new ArgumentException("The licence number is required.");
        }
        if (dto.DriverLicenceIssueDate == default || dto.DriverLicenceLastVerifiedDate == default)
        {
            throw new ArgumentException("The licence issue and last verified dates are required.");
        }
        if (dto.DriverHasPDP && dto.DriverPDPExpiryDate is null)
        {
            throw new ArgumentException("The PDP expiry date is required when the driver has a PDP.");
        }
    }

    private static void ValidateLength(string? value, int maxLength, string label)
    {
        if (value is not null && value.Trim().Length > maxLength)
        {
            throw new ArgumentException($"{label} must be {maxLength} characters or fewer.");
        }
    }

    private static List<(string Name, object? Value)> DriverValues(CreateDriverDto dto)
    {
        return
        [
            ("@site_code", dto.SiteCode),
            ("@driver_licence_type_id", dto.DriverLicenceTypeId),
            ("@driver_surname", dto.DriverSurname.Trim()),
            ("@driver_firstname", dto.DriverFirstname.Trim()),
            ("@driver_SA_id", NullIfWhiteSpace(dto.DriverSAId)),
            ("@driver_passportnumber", NullIfWhiteSpace(dto.DriverPassportNumber)),
            ("@driver_persalnumber", NullIfWhiteSpace(dto.DriverPersonalNumber)),
            ("@driver_contractnumber", NullIfWhiteSpace(dto.DriverContractNumber)),
            ("@driver_licence_number", dto.DriverLicenceNumber!.Trim()),
            ("@driver_licence_issuedate", dto.DriverLicenceIssueDate),
            ("@driver_licence_lastVerifiedDate", dto.DriverLicenceLastVerifiedDate),
            ("@driver_hasPDP", dto.DriverHasPDP),
            ("@driver_PDP_ExpiryDate", dto.DriverPDPExpiryDate),
            ("@driver_licence_ExpiryDate", dto.DriverLicenceExpiryDate),
            ("@driver_active", dto.DriverActive)
        ];
    }

    private static void AddOptionalAuditInsertFields(TableSchema schema, ICollection<string> columns, ICollection<(string Name, object? Value)> values, int currentUserId)
    {
        if (schema.Has("date_created"))
        {
            columns.Add("date_created");
            values.Add(("@date_created", DateTime.UtcNow));
        }
        if (schema.Has("created_by_user_code"))
        {
            columns.Add("created_by_user_code");
            values.Add(("@created_by_user_code", currentUserId));
        }
        if (schema.Has("is_deleted"))
        {
            columns.Add("is_deleted");
            values.Add(("@is_deleted", false));
        }
    }

    private static void AddOptionalAuditUpdateFields(TableSchema schema, ICollection<string> assignments, ICollection<(string Name, object? Value)> values, int currentUserId)
    {
        if (schema.Has("date_updated"))
        {
            assignments.Add("[date_updated] = @date_updated");
            values.Add(("@date_updated", DateTime.UtcNow));
        }
        if (schema.Has("modified_by_user_code"))
        {
            assignments.Add("[modified_by_user_code] = @modified_by_user_code");
            values.Add(("@modified_by_user_code", currentUserId));
        }
    }

    private static string ParameterName(string column) => column;

    private static void AddParameters(DbCommand command, IEnumerable<(string Name, object? Value)> values)
    {
        foreach (var (name, value) in values)
        {
            AddParameter(command, name, value);
        }
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static object? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int? ReadInt(DbDataReader reader, string name)
    {
        var value = reader[name];
        return value is DBNull ? null : Convert.ToInt32(value);
    }

    private static string? ReadString(DbDataReader reader, string name)
    {
        var value = reader[name];
        return value is DBNull ? null : value.ToString()?.Trim();
    }

    private static DateTime ReadDateTime(DbDataReader reader, string name)
    {
        var value = reader[name];
        return value is DBNull ? default : Convert.ToDateTime(value);
    }

    private static DateTime? ReadNullableDateTime(DbDataReader reader, string name)
    {
        var value = reader[name];
        return value is DBNull ? null : Convert.ToDateTime(value);
    }

    private static bool ReadBool(DbDataReader reader, string name)
    {
        var value = reader[name];
        return value is not DBNull && Convert.ToBoolean(value);
    }

    private ActionResult HandleFailure(Exception ex, string operation)
    {
        _logger.LogError(ex, "Error {Operation}", operation);
        return ex is DbException
            ? StatusCode(503, new { message = "The site-driver database is unavailable." })
            : StatusCode(500, new { message = $"Error {operation}." });
    }

    private static string QuoteIdentifier(string identifier) => $"[{identifier}]";

    private sealed record TableSchema(IReadOnlySet<string> Columns)
    {
        public bool Has(string column) => Columns.Contains(column);
    }
}

public sealed class SiteDriverLicenceTypeDto
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
}
