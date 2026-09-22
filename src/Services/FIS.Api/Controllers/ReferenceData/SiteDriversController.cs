using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Claims;
using FIS.Core.Application.Interfaces;
using FIS.Core.Infrastructure.Repositories;
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
    Justification = "All interpolated SQL identifiers come from fixed table and column names; request values are parameters."
)]
public sealed class SiteDriversController : BaseApiController
{
    private const int DefaultPageSize = 24;
    private const int MaximumPageSize = 100;
    private const string DriversTable = "site_drivers";
    private const string LicenceTypesTable = "driver_licence_types";
    private const string InsertDriverProcedure = "DEV_INS_SiteDrivers";
    private const string UpdateDriverProcedure = "DEV_UPD_SiteDrivers";
    private const string DeleteDriverProcedure = "DEV_DEL_SiteDrivers";

    private static readonly string[] InsertDriverParameters =
    [
        "@SiteDriverCode",
        "@SiteCode",
        "@DriverLicenceTypeID",
        "@Surname",
        "@FirstName",
        "@SAIDNumber",
        "@PassportNumber",
        "@PersalNumber",
        "@DriverContractNumber",
        "@DriverLicenceNumber",
        "@DriverLicenceIssueDate",
        "@DriverLicenceLastVerifiedDate",
        "@HasPDP",
        "@PDPExpiryDate",
        "@LicenceExpiryDate",
        "@Active",
    ];

    private static readonly string[] UpdateDriverParameters =
    [
        "@SiteDriverCode",
        "@SiteCode",
        "@DriverLicenceTypeID",
        "@Surname",
        "@FirstName",
        "@SAIDNumber",
        "@PassportNumber",
        "@PersalNumber",
        "@DriverContractNumber",
        "@DriverLicenceNumber",
        "@DriverLicenceIssueDate",
        "@DriverLicenceLastVerifiedDate",
        "@HasPDP",
        "@PDPExpiryDate",
        "@LicenceExpiryDate",
        "@Active",
    ];

    private static readonly string[] DeleteDriverParameters = ["@ID"];

    private readonly FisDbContext _context;
    private readonly ISiteRepository _siteRepository;
    private readonly SiteDriverLookupOverlay _lookupOverlay;
    private readonly ILogger<SiteDriversController> _logger;

    public SiteDriversController(
        FisDbContext context,
        ISiteRepository siteRepository,
        SiteDriverLookupOverlay lookupOverlay,
        ILogger<SiteDriversController> logger
    )
    {
        _context = context;
        _siteRepository = siteRepository;
        _lookupOverlay = lookupOverlay;
        _logger = logger;
    }

    private bool HasRole(params string[] expectedRoles)
    {
        if (expectedRoles.Any(User.IsInRole))
            return true;

        return User.Claims
            .Where(claim =>
                claim.Type == ClaimTypes.Role
                || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
                || claim.Type.Equals("roles", StringComparison.OrdinalIgnoreCase)
            )
            .SelectMany(claim =>
                claim.Value.Split(
                    ',',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                )
            )
            .Any(role => expectedRoles.Any(expected =>
                string.Equals(role, expected, StringComparison.OrdinalIgnoreCase)));
    }

    private bool HasDriverMaintenanceRole() =>
        HasRole(
            "Driver and Authoriser Management",
            "SystemAdministrator",
            "System Administrator"
        );

    // Trip Authorities could open the legacy trip capture and driver-licence
    // pages (TripsFilter.aspx.vb:459-463) and those pages had no inner role
    // gate, so the read endpoints they call must accept the same role.
    private bool HasDriverWorkflowReadRole() =>
        HasDriverMaintenanceRole()
        || HasRole(
            "Contracts",
            "Contract",
            "Contract (load and manage)",
            "Contracts (load and manage)",
            "Contract (approver)",
            "Contracts approver",
            "Contract (cancel and close)",
            "Contracts (cancel and close)",
            "Trip Authorities",
            "TripAuthorities"
        );

    private async Task<IReadOnlySet<int>?> ResolveAllowedSiteCodesAsync()
    {
        if (HasRole("SystemAdministrator", "System Administrator"))
            return null;

        var userId = GetCurrentUserId();
        var profileSiteCode = await _context.UserAccessOlds.AsNoTracking()
            .Where(user => user.user_access_code == userId)
            .Select(user => user.Site_code)
            .SingleOrDefaultAsync(HttpContext.RequestAborted);
        if (profileSiteCode is not > 0)
            return new HashSet<int>();

        var profileSite = await _siteRepository.GetByIdAsync(profileSiteCode.Value);
        if (profileSite is null)
            return new HashSet<int>();

        var sites = await _siteRepository.GetActiveSitesAsync();
        if (HasRole("Vehicle List for All Departments in Province") && profileSite.province_code.HasValue)
            sites = sites.Where(site => site.province_code == profileSite.province_code.Value);
        else if (HasRole("Vehicle List for All Sites in Department") && profileSite.Depatrment_code.HasValue)
            sites = sites.Where(site => site.Depatrment_code == profileSite.Depatrment_code.Value);
        else
            sites = sites.Where(site => site.Site_code == profileSite.Site_code);

        return sites.Select(site => (int)site.Site_code).ToHashSet();
    }

    private async Task<bool> IsSiteAllowedAsync(int siteCode)
    {
        var allowedSites = await ResolveAllowedSiteCodesAsync();
        return allowedSites is null || allowedSites.Contains(siteCode);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DriverDto>>> GetDrivers([FromQuery] int? siteCode)
    {
        if (!HasDriverWorkflowReadRole())
            return Forbid();

        try
        {
            var allowedSites = await ResolveAllowedSiteCodesAsync();
            if (siteCode.HasValue && allowedSites is not null && !allowedSites.Contains(siteCode.Value))
                return Forbid();

            var drivers = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, DriversTable);
                EnsureDriverTable(schema);

                var siteFilter = siteCode.HasValue ? " AND [site_code] = @siteCode" : string.Empty;
                await using var command = connection.CreateCommand();
                command.CommandText =
                    $"{BuildDriverSelect(schema)} WHERE {BuildDriverPredicate(schema)}{siteFilter} ORDER BY [driver_surname], [driver_firstname], [site_driver_code]";
                if (siteCode.HasValue)
                {
                    AddParameter(command, "@siteCode", siteCode.Value);
                }

                var rows = await ReadDriversAsync(command);
                return allowedSites is null
                    ? rows
                    : rows.Where(driver => allowedSites.Contains(driver.SiteCode)).ToList();
            });

            return Ok(drivers);
        }
        catch (Exception ex)
        {
            return HandleFailure(ex, "retrieving site drivers");
        }
    }

    [HttpGet("page")]
    public async Task<ActionResult> GetDriversPage(
        [FromQuery] int? siteCode,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize
    )
    {
        if (!HasDriverWorkflowReadRole())
            return Forbid();

        if (siteCode is null)
        {
            return BadRequest(new { message = "siteCode is required." });
        }

        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaximumPageSize);

        try
        {
            var allowedSites = await ResolveAllowedSiteCodesAsync();
            if (allowedSites is not null && !allowedSites.Contains(siteCode.Value))
                return Forbid();

            var result = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, DriversTable);
                EnsureDriverTable(schema);

                var filter = $"{BuildDriverPredicate(schema)} AND [site_code] = @siteCode";
                var total = await CountDriversAsync(connection, filter, siteCode.Value);
                var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)normalizedPageSize));
                var currentPage = Math.Min(normalizedPage, totalPages);
                var offset = checked((long)(currentPage - 1) * normalizedPageSize);

                await using var command = connection.CreateCommand();
                command.CommandText =
                    $"{BuildDriverSelect(schema)} WHERE {filter} ORDER BY [driver_surname], [driver_firstname], [site_driver_code] OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";
                AddParameter(command, "@siteCode", siteCode.Value);
                AddParameter(command, "@offset", offset);
                AddParameter(command, "@pageSize", normalizedPageSize);

                var items = await ReadDriversAsync(command);
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
            return HandleFailure(ex, "retrieving paged site drivers");
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DriverDto>> GetDriver(int id)
    {
        if (!HasDriverWorkflowReadRole())
            return Forbid();

        try
        {
            var overlayRows = await _lookupOverlay.ReadSingleSiteDriverRowsAsync(id);
            DriverDto? leftover = null;
            if (overlayRows is null || overlayRows.Count > 0)
            {
                try
                {
                    leftover = await WithConnectionAsync(async connection =>
                    {
                        var schema = await ReadTableSchemaAsync(connection, DriversTable);
                        EnsureDriverTable(schema);
                        return await ReadDriverByIdAsync(connection, schema, id);
                    });
                }
                catch (Exception ex) when (overlayRows is not null)
                {
                    _logger.LogWarning(ex, "Leftover site driver {SiteDriverCode} could not be hydrated", id);
                }
            }

            DriverDto? driver;
            if (overlayRows is null)
            {
                driver = leftover;
            }
            else if (overlayRows.Count == 0)
            {
                driver = null;
            }
            else
            {
                driver = leftover ?? MapDriverFromOverlay(overlayRows[0]);
            }

            if (driver is not null && !await IsSiteAllowedAsync(driver.SiteCode))
                return Forbid();

            return driver is null
                ? NotFound(new { message = $"Site driver not found with code: {id}" })
                : Ok(driver);
        }
        catch (Exception ex)
        {
            return HandleFailure(ex, $"retrieving site driver {id}");
        }
    }

    [HttpGet("licence-types")]
    public async Task<ActionResult<IEnumerable<SiteDriverLicenceTypeDto>>> GetLicenceTypes()
    {
        if (!HasDriverWorkflowReadRole())
            return Forbid();

        try
        {
            var overlayRows = await _lookupOverlay.ReadLicenceTypeRowsAsync();
            var leftover = await QueryLeftoverLicenceTypesAsync();
            if (overlayRows is not null)
            {
                var overlaid = OverlayLicenceTypes(overlayRows, leftover);
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
                return HandleFailure(ex, "retrieving site-driver licence types");
            }

            _logger.LogWarning(
                ex,
                "Unable to retrieve site-driver licence types; returning an empty lookup"
            );
            return Ok(Array.Empty<SiteDriverLicenceTypeDto>());
        }
    }

    [HttpGet("sites")]
    public async Task<ActionResult<IEnumerable<DriverManagementSiteLookupDto>>> GetSitesForEdit(
        [FromQuery] int departmentId
    )
    {
        if (!HasDriverWorkflowReadRole())
            return Forbid();

        if (departmentId <= 0)
        {
            return BadRequest(new { message = "departmentId is required." });
        }

        try
        {
            var overlayRows = await _lookupOverlay.ReadSitesForEditRowsAsync(departmentId);
            var leftoverAll = (await _siteRepository.GetActiveSitesAsync())
                .Select(MapSiteLookup)
                .Where(site => site.SiteCode > 0)
                .ToList();
            var leftover = leftoverAll
                .Where(site => site.DepartmentCode == departmentId)
                .ToList();
            var allowedSites = await ResolveAllowedSiteCodesAsync();
            if (overlayRows is not null)
            {
                var overlaid = OverlaySitesForEdit(overlayRows, leftoverAll);
                if (overlaid is not null)
                {
                    return Ok(FilterAllowedSites(overlaid, allowedSites));
                }
            }

            return Ok(FilterAllowedSites(leftover, allowedSites));
        }
        catch (Exception ex)
        {
            return HandleFailure(ex, "retrieving sites for driver maintenance");
        }
    }

    [HttpPost("licence-types")]
    public async Task<ActionResult<SiteDriverLicenceTypeDto>> CreateLicenceType(
        [FromBody] SiteDriverLicenceTypeWriteDto dto
    )
    {
        if (!HasDriverMaintenanceRole())
            return Forbid();

        try
        {
            var created = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, LicenceTypesTable);
                EnsureLicenceTypeTable(schema);
                ValidateLicenceType(dto, schema);

                var columns = new List<string>
                {
                    "driver_licence_type_code",
                    "driver_licence_type_description",
                };
                var values = new List<(string Name, object? Value)>
                {
                    ("@code", NullIfWhiteSpace(dto.Code)),
                    ("@description", dto.Description!.Trim()),
                };
                AddOptionalLicenceTypeInsertFields(schema, columns, values, GetCurrentUserId());

                await using var command = connection.CreateCommand();
                command.CommandText =
                    $"INSERT INTO [dbo].[{LicenceTypesTable}] ({string.Join(", ", columns.Select(QuoteIdentifier))}) OUTPUT INSERTED.[driver_licence_type_id] VALUES ({string.Join(", ", values.Select(item => item.Name))})";
                AddParameters(command, values);

                var id = Convert.ToInt32(await command.ExecuteScalarAsync());
                return await ReadLicenceTypeByIdAsync(connection, schema, id);
            });

            return created is null
                ? StatusCode(
                    500,
                    new
                    {
                        message = "The driver licence type was created but could not be reloaded.",
                    }
                )
                : CreatedAtAction(nameof(GetLicenceTypes), created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return HandleFailure(ex, "creating a driver licence type");
        }
    }

    [HttpPut("licence-types/{id:int}")]
    public async Task<ActionResult<SiteDriverLicenceTypeDto>> UpdateLicenceType(
        int id,
        [FromBody] SiteDriverLicenceTypeWriteDto dto
    )
    {
        if (!HasDriverMaintenanceRole())
            return Forbid();

        try
        {
            var updated = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, LicenceTypesTable);
                EnsureLicenceTypeTable(schema);
                ValidateLicenceType(dto, schema);

                if (await ReadLicenceTypeByIdAsync(connection, schema, id) is null)
                {
                    return null;
                }

                var assignments = new List<string>
                {
                    "[driver_licence_type_description] = @description",
                };
                var values = new List<(string Name, object? Value)>
                {
                    ("@description", dto.Description!.Trim()),
                };

                // An omitted code must not erase a legacy code that the current
                // workflow does not edit. The code remains available through the
                // read DTO and can be updated when a caller supplies it.
                if (schema.Has("driver_licence_type_code") && !string.IsNullOrWhiteSpace(dto.Code))
                {
                    assignments.Add("[driver_licence_type_code] = @code");
                    values.Add(("@code", dto.Code.Trim()));
                }
                AddOptionalLicenceTypeUpdateFields(schema, assignments, values, GetCurrentUserId());

                await using var command = connection.CreateCommand();
                command.CommandText =
                    $"UPDATE [dbo].[{LicenceTypesTable}] SET {string.Join(", ", assignments)} WHERE [driver_licence_type_id] = @id";
                AddParameters(command, values);
                AddParameter(command, "@id", id);
                await command.ExecuteNonQueryAsync();

                return await ReadLicenceTypeByIdAsync(connection, schema, id);
            });

            return updated is null
                ? NotFound(new { message = $"Driver licence type not found with code: {id}" })
                : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return HandleFailure(ex, $"updating driver licence type {id}");
        }
    }

    [HttpDelete("licence-types/{id:int}")]
    public async Task<ActionResult> DeleteLicenceType(int id)
    {
        if (!HasDriverMaintenanceRole())
            return Forbid();

        try
        {
            var deleted = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, LicenceTypesTable);
                EnsureLicenceTypeTable(schema);

                if (await ReadLicenceTypeByIdAsync(connection, schema, id) is null)
                {
                    return false;
                }

                await using var command = connection.CreateCommand();
                if (schema.Has("is_deleted"))
                {
                    var assignments = new List<string> { "[is_deleted] = @isDeleted" };
                    var values = new List<(string Name, object? Value)> { ("@isDeleted", true) };
                    AddOptionalLicenceTypeUpdateFields(
                        schema,
                        assignments,
                        values,
                        GetCurrentUserId()
                    );
                    command.CommandText =
                        $"UPDATE [dbo].[{LicenceTypesTable}] SET {string.Join(", ", assignments)} WHERE [driver_licence_type_id] = @id";
                    AddParameters(command, values);
                }
                else
                {
                    // The legacy table is NonActivateableEntityBase in the old
                    // API, so deletion is intentionally a physical delete there.
                    // SQL Server still protects rows referenced by site_drivers.
                    command.CommandText =
                        $"DELETE FROM [dbo].[{LicenceTypesTable}] WHERE [driver_licence_type_id] = @id";
                }

                AddParameter(command, "@id", id);
                await command.ExecuteNonQueryAsync();
                return true;
            });

            return deleted
                ? NoContent()
                : NotFound(new { message = $"Driver licence type not found with code: {id}" });
        }
        catch (Exception ex)
        {
            return HandleFailure(ex, $"deleting driver licence type {id}");
        }
    }

    [HttpPost]
    public async Task<ActionResult<DriverDto>> CreateDriver([FromBody] CreateDriverDto dto)
    {
        if (!HasDriverMaintenanceRole())
            return Forbid();

        try
        {
            var created = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, DriversTable);
                EnsureDriverTable(schema);
                ValidateDriver(dto);
                if (!await IsSiteAllowedAsync(dto.SiteCode))
                    throw new SiteDriverScopeException();
                NormalizeDriverLicenceFlags(dto);
                    await EnsureNoDuplicateIdentityAsync(connection, schema, dto);

                var procedureParameters = await ReadProcedureParametersAsync(
                    connection,
                    InsertDriverProcedure
                );
                int driverId;
                if (procedureParameters is not null)
                {
                    EnsureProcedureContract(
                        InsertDriverProcedure,
                        procedureParameters,
                        InsertDriverParameters
                    );
                    driverId = await ExecuteInsertDriverProcedureAsync(connection, dto);
                }
                else
                {
                    // The direct write is a compatibility path only for databases
                    // where the legacy procedure genuinely does not exist.
                    var columns = DriverColumns.ToList();
                    var values = DriverValues(dto);
                    AddOptionalAuditInsertFields(schema, columns, values, GetCurrentUserId());

                    await using var command = connection.CreateCommand();
                    command.CommandText =
                        $"INSERT INTO [dbo].[{DriversTable}] ({string.Join(", ", columns.Select(QuoteIdentifier))}) OUTPUT INSERTED.[site_driver_code] VALUES ({string.Join(", ", values.Select(item => item.Name))})";
                    AddParameters(command, values);
                    driverId = Convert.ToInt32(await command.ExecuteScalarAsync());
                }

                return await ReadDriverByIdAsync(connection, schema, driverId);
            });

            return created is null
                ? StatusCode(
                    500,
                    new { message = "The site driver was created but could not be reloaded." }
                )
                : CreatedAtAction(nameof(GetDriver), new { id = created.SiteDriverCode }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (SiteDriverDuplicateException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (SiteDriverScopeException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return HandleFailure(ex, "creating a site driver");
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<DriverDto>> UpdateDriver(int id, [FromBody] UpdateDriverDto dto)
    {
        if (!HasDriverMaintenanceRole())
            return Forbid();

        try
        {
            var updated = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, DriversTable);
                EnsureDriverTable(schema);
                ValidateDriver(dto);
                NormalizeDriverLicenceFlags(dto);

                var existing = await ReadDriverByIdAsync(connection, schema, id);
                if (existing is null)
                {
                    return null;
                }
                if (!await IsSiteAllowedAsync(existing.SiteCode) || !await IsSiteAllowedAsync(dto.SiteCode))
                    throw new SiteDriverScopeException();
                await EnsureNoDuplicateIdentityAsync(connection, schema, dto, id);

                var procedureParameters = await ReadProcedureParametersAsync(
                    connection,
                    UpdateDriverProcedure
                );
                if (procedureParameters is not null)
                {
                    EnsureProcedureContract(
                        UpdateDriverProcedure,
                        procedureParameters,
                        UpdateDriverParameters
                    );
                    await ExecuteUpdateDriverProcedureAsync(connection, id, dto);
                }
                else
                {
                    // The direct write is a compatibility path only for databases
                    // where the legacy procedure genuinely does not exist.
                    var assignments = DriverColumns
                        .Select(column => $"{QuoteIdentifier(column)} = @{ParameterName(column)}")
                        .ToList();
                    var values = DriverValues(dto);
                    AddOptionalAuditUpdateFields(schema, assignments, values, GetCurrentUserId());

                    await using var command = connection.CreateCommand();
                    command.CommandText =
                        $"UPDATE [dbo].[{DriversTable}] SET {string.Join(", ", assignments)} WHERE [site_driver_code] = @siteDriverCode";
                    AddParameters(command, values);
                    AddParameter(command, "@siteDriverCode", id);
                    await command.ExecuteNonQueryAsync();
                }

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
        catch (SiteDriverDuplicateException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (SiteDriverScopeException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return HandleFailure(ex, $"updating site driver {id}");
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteDriver(int id)
    {
        if (!HasDriverMaintenanceRole())
            return Forbid();

        try
        {
            var deleted = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, DriversTable);
                EnsureDriverTable(schema);

                var existing = await ReadDriverByIdAsync(connection, schema, id);
                if (existing is null)
                {
                    return false;
                }
                if (!await IsSiteAllowedAsync(existing.SiteCode))
                    throw new SiteDriverScopeException();

                var procedureParameters = await ReadProcedureParametersAsync(
                    connection,
                    DeleteDriverProcedure
                );
                if (procedureParameters is not null)
                {
                    EnsureProcedureContract(
                        DeleteDriverProcedure,
                        procedureParameters,
                        DeleteDriverParameters
                    );
                    await ExecuteDeleteDriverProcedureAsync(connection, id);
                }
                else
                {
                    // The direct write is a compatibility path only for databases
                    // where the legacy procedure genuinely does not exist. Legacy
                    // deletion is deactivation, never physical deletion.
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
                    command.CommandText =
                        $"UPDATE [dbo].[{DriversTable}] SET {string.Join(", ", assignments)} WHERE [site_driver_code] = @siteDriverCode";
                    AddParameter(command, "@driverActive", false);
                    AddParameter(command, "@isDeleted", true);
                    AddParameter(command, "@dateUpdated", DateTime.UtcNow);
                    AddParameter(command, "@modifiedByUserCode", GetCurrentUserId());
                    AddParameter(command, "@siteDriverCode", id);
                    await command.ExecuteNonQueryAsync();
                }
                return true;
            });

            return deleted
                ? NoContent()
                : NotFound(new { message = $"Site driver not found with code: {id}" });
        }
        catch (SiteDriverScopeException)
        {
            return Forbid();
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
        "driver_active",
    ];

    private static void EnsureLicenceTypeTable(TableSchema schema)
    {
        string[] requiredColumns =
        [
            "driver_licence_type_id",
            "driver_licence_type_code",
            "driver_licence_type_description",
        ];
        if (requiredColumns.Any(column => !schema.Has(column)))
        {
            throw new InvalidOperationException(
                "The dbo.driver_licence_types table is missing one or more required legacy columns."
            );
        }
    }

    private static void ValidateLicenceType(SiteDriverLicenceTypeWriteDto dto, TableSchema schema)
    {
        if (string.IsNullOrWhiteSpace(dto.Description))
        {
            throw new ArgumentException("The driver licence type description is required.");
        }

        var codeLength = schema.Has("is_deleted") ? 50 : 3;
        var descriptionLength = schema.Has("is_deleted") ? 255 : 100;
        if (dto.Code?.Trim().Length > codeLength)
        {
            throw new ArgumentException(
                $"The driver licence type code must be {codeLength} characters or fewer."
            );
        }
        if (dto.Description.Trim().Length > descriptionLength)
        {
            throw new ArgumentException(
                $"The driver licence type description must be {descriptionLength} characters or fewer."
            );
        }
    }

    private static void AddOptionalLicenceTypeInsertFields(
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

    private static void AddOptionalLicenceTypeUpdateFields(
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

    private static string BuildLicenceTypeSelect(TableSchema schema)
    {
        return $"SELECT [driver_licence_type_id] AS [Id], [driver_licence_type_code] AS [Code], [driver_licence_type_description] AS [Description] FROM [dbo].[{LicenceTypesTable}]";
    }

    private static async Task<SiteDriverLicenceTypeDto?> ReadLicenceTypeByIdAsync(
        DbConnection connection,
        TableSchema schema,
        int id
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"{BuildLicenceTypeSelect(schema)} WHERE [driver_licence_type_id] = @id";
        AddParameter(command, "@id", id);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new SiteDriverLicenceTypeDto
        {
            Id = ReadInt(reader, "Id") ?? 0,
            Code = ReadString(reader, "Code"),
            Description = ReadString(reader, "Description"),
        };
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
            if (!reader.IsDBNull(0))
            {
                columns.Add(reader.GetString(0));
            }
        }

        return new TableSchema(columns);
    }

    private static void EnsureDriverTable(TableSchema schema)
    {
        if (!schema.Has("site_driver_code") || DriverColumns.Any(column => !schema.Has(column)))
        {
            throw new InvalidOperationException(
                "The dbo.site_drivers table is missing one or more required legacy columns."
            );
        }
    }

    private static string BuildDriverSelect(TableSchema schema)
    {
        var columns = string.Join(
            ", ",
            DriverColumns.Select(column =>
                $"{QuoteIdentifier(column)} AS {QuoteIdentifier(column)}"
            )
        );
        return $"SELECT [site_driver_code] AS [site_driver_code], {columns} FROM [dbo].[{DriversTable}]";
    }

    private static string BuildDriverPredicate(TableSchema schema)
    {
        var deletedPredicate = schema.Has("is_deleted")
            ? " AND COALESCE([is_deleted], 0) = 0"
            : string.Empty;
        return $"[driver_active] = 1{deletedPredicate}";
    }

    private static async Task<int> CountDriversAsync(
        DbConnection connection,
        string filter,
        int siteCode
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM [dbo].[{DriversTable}] WHERE {filter}";
        AddParameter(command, "@siteCode", siteCode);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
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

    private static async Task<DriverDto?> ReadDriverByIdAsync(
        DbConnection connection,
        TableSchema schema,
        int id
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"{BuildDriverSelect(schema)} WHERE [site_driver_code] = @siteDriverCode";
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
            DriverActive = ReadBool(reader, "driver_active"),
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
        if (
            string.IsNullOrWhiteSpace(dto.DriverSurname)
            || string.IsNullOrWhiteSpace(dto.DriverFirstname)
        )
        {
            throw new ArgumentException("The first name and surname are required.");
        }
        if (string.IsNullOrWhiteSpace(dto.DriverLicenceNumber))
        {
            throw new ArgumentException("The licence number is required.");
        }
        if (string.IsNullOrWhiteSpace(dto.DriverSAId) && string.IsNullOrWhiteSpace(dto.DriverPassportNumber))
        {
            throw new ArgumentException(
                "Either a South African ID number or a passport number is required."
            );
        }
        if (!string.IsNullOrWhiteSpace(dto.DriverSAId))
        {
            var southAfricanId = dto.DriverSAId.Trim();
            if (southAfricanId.Length != 13 || !southAfricanId.All(char.IsDigit))
            {
                throw new ArgumentException("The South African ID number must contain 13 digits.");
            }
            if (!IsValidSouthAfricanId(southAfricanId))
            {
                throw new ArgumentException("The South African ID number is not valid.");
            }
        }
        if (string.IsNullOrWhiteSpace(dto.DriverPersonalNumber) && string.IsNullOrWhiteSpace(dto.DriverContractNumber))
        {
            throw new ArgumentException(
                "A Persal number or driver contract number is required."
            );
        }
        if (dto.DriverLicenceIssueDate == default || dto.DriverLicenceLastVerifiedDate == default)
        {
            throw new ArgumentException("The licence issue and last verified dates are required.");
        }
        if (dto.DriverLicenceExpiryDate is null)
        {
            throw new ArgumentException("The licence expiry date is required.");
        }
        if (IsPdpLicenceType(dto.DriverLicenceTypeId) && dto.DriverPDPExpiryDate is null)
        {
            throw new ArgumentException(
                "The PDP expiry date is required when the driver has a PDP."
            );
        }
    }

    private static void NormalizeDriverLicenceFlags(CreateDriverDto dto)
    {
        // The legacy page derives PDP eligibility from the licence type (3, 5,
        // 9, or 13); it does not trust a separate checkbox value. Preserve
        // that database contract and clear an accidentally supplied PDP date
        // for a non-PDP licence.
        dto.DriverHasPDP = IsPdpLicenceType(dto.DriverLicenceTypeId);
        if (!dto.DriverHasPDP)
        {
            dto.DriverPDPExpiryDate = null;
        }
    }

    private static bool IsPdpLicenceType(int licenceTypeId) =>
        licenceTypeId is 3 or 5 or 9 or 13;

    private static bool IsValidSouthAfricanId(string value)
    {
        var currentYear = DateTime.Today.Year % 100;
        var year = int.Parse(value[..2]);
        var month = int.Parse(value.Substring(2, 2));
        var day = int.Parse(value.Substring(4, 2));
        var fullYear = year <= currentYear ? 2000 + year : 1900 + year;
        if (!DateTime.TryParse(
                $"{fullYear:0000}-{month:00}-{day:00}",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var birthDate
            )
            || birthDate.Year != fullYear
            || birthDate.Month != month
            || birthDate.Day != day)
        {
            return false;
        }

        var sum = 0;
        var doubleDigit = false;
        for (var index = value.Length - 1; index >= 0; index--)
        {
            var digit = value[index] - '0';
            if (doubleDigit)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            doubleDigit = !doubleDigit;
        }

        return sum % 10 == 0;
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
            ("@driver_active", dto.DriverActive),
        ];
    }

    private static void AddOptionalAuditInsertFields(
        TableSchema schema,
        ICollection<string> columns,
        ICollection<(string Name, object? Value)> values,
        int currentUserId
    )
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

    private static void AddOptionalAuditUpdateFields(
        TableSchema schema,
        ICollection<string> assignments,
        ICollection<(string Name, object? Value)> values,
        int currentUserId
    )
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

    private static async Task<int> ExecuteInsertDriverProcedureAsync(
        DbConnection connection,
        CreateDriverDto dto
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = $"[dbo].[{InsertDriverProcedure}]";
        var output = command.CreateParameter();
        output.ParameterName = "@SiteDriverCode";
        output.DbType = DbType.Int32;
        output.Direction = ParameterDirection.Output;
        command.Parameters.Add(output);
        AddProcedureDriverParameters(command, dto);
        await command.ExecuteNonQueryAsync();

        if (output.Value is null or DBNull || !int.TryParse(output.Value.ToString(), out var id) || id <= 0)
        {
            throw new LegacySiteDriverProcedureContractException(
                $"The deployed legacy procedure {InsertDriverProcedure} did not return a site-driver code."
            );
        }

        return id;
    }

    private static async Task ExecuteUpdateDriverProcedureAsync(
        DbConnection connection,
        int id,
        UpdateDriverDto dto
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = $"[dbo].[{UpdateDriverProcedure}]";
        AddParameter(command, "@SiteDriverCode", id);
        AddProcedureDriverParameters(command, dto);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteDeleteDriverProcedureAsync(DbConnection connection, int id)
    {
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = $"[dbo].[{DeleteDriverProcedure}]";
        AddParameter(command, "@ID", id);
        await command.ExecuteNonQueryAsync();
    }

    private static void AddProcedureDriverParameters(DbCommand command, CreateDriverDto dto)
    {
        AddParameter(command, "@SiteCode", dto.SiteCode);
        AddParameter(command, "@DriverLicenceTypeID", dto.DriverLicenceTypeId);
        AddParameter(command, "@Surname", dto.DriverSurname.Trim());
        AddParameter(command, "@FirstName", dto.DriverFirstname.Trim());
        AddParameter(command, "@SAIDNumber", NullIfWhiteSpace(dto.DriverSAId));
        AddParameter(command, "@PassportNumber", NullIfWhiteSpace(dto.DriverPassportNumber));
        AddParameter(command, "@PersalNumber", NullIfWhiteSpace(dto.DriverPersonalNumber));
        AddParameter(command, "@DriverContractNumber", NullIfWhiteSpace(dto.DriverContractNumber));
        AddParameter(command, "@DriverLicenceNumber", dto.DriverLicenceNumber!.Trim());
        AddParameter(command, "@DriverLicenceIssueDate", dto.DriverLicenceIssueDate);
        AddParameter(command, "@DriverLicenceLastVerifiedDate", dto.DriverLicenceLastVerifiedDate);
        AddParameter(command, "@HasPDP", dto.DriverHasPDP);
        AddParameter(command, "@PDPExpiryDate", dto.DriverPDPExpiryDate);
        AddParameter(command, "@LicenceExpiryDate", dto.DriverLicenceExpiryDate);
        AddParameter(command, "@Active", dto.DriverActive);
    }

    private static async Task EnsureNoDuplicateIdentityAsync(
        DbConnection connection,
        TableSchema schema,
        CreateDriverDto dto,
        int? excludedId = null
    )
    {
        var identities = new (string Column, string? Value)[]
        {
            ("driver_SA_id", NullIfWhiteSpace(dto.DriverSAId)),
            ("driver_passportnumber", NullIfWhiteSpace(dto.DriverPassportNumber)),
            ("driver_persalnumber", NullIfWhiteSpace(dto.DriverPersonalNumber)),
            ("driver_contractnumber", NullIfWhiteSpace(dto.DriverContractNumber)),
            ("driver_licence_number", NullIfWhiteSpace(dto.DriverLicenceNumber)),
        };
        var populated = identities.Where(item => item.Value is not null).ToArray();
        if (populated.Length == 0)
        {
            return;
        }

        var identityPredicates = populated
            .Select((item, index) =>
                $"NULLIF(LTRIM(RTRIM([{item.Column}])), '') = @identity{index}")
            .ToArray();
        // A driver identity is global across sites, including historical
        // inactive rows. Re-capturing the same person at another site would
        // make contracts and vehicle allocation ambiguous after reactivation.
        var excludedPredicate = excludedId.HasValue
            ? " AND [site_driver_code] <> @excludedId"
            : string.Empty;

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT TOP (1) [site_driver_code], [site_code], [driver_firstname], [driver_surname]
            FROM [dbo].[{DriversTable}]
            WHERE 1 = 1{excludedPredicate}
              AND ({string.Join(" OR ", identityPredicates)})
            """;
        for (var index = 0; index < populated.Length; index++)
        {
            AddParameter(command, $"@identity{index}", populated[index].Value);
        }
        if (excludedId.HasValue)
        {
            AddParameter(command, "@excludedId", excludedId.Value);
        }

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return;
        }

        var existingId = Convert.ToInt32(reader["site_driver_code"]);
        var existingSite = Convert.ToInt32(reader["site_code"]);
        var firstName = reader["driver_firstname"] is DBNull
            ? string.Empty
            : reader["driver_firstname"].ToString()?.Trim() ?? string.Empty;
        var surname = reader["driver_surname"] is DBNull
            ? string.Empty
            : reader["driver_surname"].ToString()?.Trim() ?? string.Empty;
        throw new SiteDriverDuplicateException(
            $"This driver already exists at site {existingSite} (site-driver {existingId}, {firstName} {surname}). A driver cannot be captured or assigned to more than one site."
        );
    }

    private static async Task<IReadOnlyList<string>?> ReadProcedureParametersAsync(
        DbConnection connection,
        string procedureName
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT [parameterObject].[name]
            FROM [sys].[procedures] AS [procedureObject]
            INNER JOIN [sys].[schemas] AS [schemaObject]
                ON [schemaObject].[schema_id] = [procedureObject].[schema_id]
            LEFT JOIN [sys].[parameters] AS [parameterObject]
                ON [parameterObject].[object_id] = [procedureObject].[object_id]
            WHERE [schemaObject].[name] = N'dbo'
              AND [procedureObject].[name] = @procedureName
              AND [parameterObject].[parameter_id] > 0
            ORDER BY [parameterObject].[parameter_id]
            """;
        AddParameter(command, "@procedureName", procedureName);
        var parameters = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                parameters.Add(reader.GetString(0));
            }
        }

        if (parameters.Count > 0)
        {
            return parameters;
        }

        await using var existsCommand = connection.CreateCommand();
        existsCommand.CommandText = "SELECT OBJECT_ID(@procedureName, 'P');";
        AddParameter(existsCommand, "@procedureName", $"dbo.{procedureName}");
        var objectId = await existsCommand.ExecuteScalarAsync();
        return objectId is null or DBNull ? null : parameters;
    }

    private static void EnsureProcedureContract(
        string procedureName,
        IReadOnlyList<string> actualParameters,
        IReadOnlyList<string> expectedParameters
    )
    {
        if (!actualParameters.SequenceEqual(expectedParameters, StringComparer.OrdinalIgnoreCase))
        {
            throw new LegacySiteDriverProcedureContractException(
                $"The deployed legacy procedure {procedureName} does not match its verified parameter contract. No direct-DML fallback was run."
            );
        }
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

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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

    private async Task<List<SiteDriverLicenceTypeDto>> QueryLeftoverLicenceTypesAsync()
    {
        try
        {
            return await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, LicenceTypesTable);
                if (
                    !schema.Has("driver_licence_type_id")
                    || !schema.Has("driver_licence_type_code")
                    || !schema.Has("driver_licence_type_description")
                )
                {
                    return new List<SiteDriverLicenceTypeDto>();
                }

                var activePredicate = schema.Has("is_deleted")
                    ? "COALESCE([is_deleted], 0) = 0"
                    : "1 = 1";
                await using var command = connection.CreateCommand();
                command.CommandText =
                    $"SELECT [driver_licence_type_id] AS [Id], [driver_licence_type_code] AS [Code], [driver_licence_type_description] AS [Description] FROM [dbo].[{LicenceTypesTable}] WHERE {activePredicate} ORDER BY [driver_licence_type_description], [driver_licence_type_id]";

                var result = new List<SiteDriverLicenceTypeDto>();
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(
                        new SiteDriverLicenceTypeDto
                        {
                            Id = ReadInt(reader, "Id") ?? 0,
                            Code = ReadString(reader, "Code"),
                            Description = ReadString(reader, "Description"),
                        }
                    );
                }

                return result.Where(item => item.Id > 0).ToList();
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Leftover driver licence types could not be hydrated");
            return [];
        }
    }

    private static List<SiteDriverLicenceTypeDto>? OverlayLicenceTypes(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> overlayRows,
        IReadOnlyList<SiteDriverLicenceTypeDto> leftover
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
        var leftoverByCode = leftover
            .Where(item => !string.IsNullOrWhiteSpace(item.Code))
            .GroupBy(item => item.Code!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var mapped = new List<SiteDriverLicenceTypeDto>();
        var seen = new HashSet<int>();
        foreach (var row in overlayRows)
        {
            var id = SiteDriverLookupOverlay.ReadInt32(
                row,
                "DriverLicenceTypeID",
                "driver_licence_type_id",
                "Id"
            );
            var code = SiteDriverLookupOverlay.ReadString(
                row,
                "driver_licence_type_code",
                "Code"
            );
            leftoverById.TryGetValue(id ?? 0, out var leftoverByKey);
            if (
                leftoverByKey is null
                && !string.IsNullOrWhiteSpace(code)
                && leftoverByCode.TryGetValue(code, out var leftoverMatch)
            )
            {
                leftoverByKey = leftoverMatch;
                id = leftoverMatch.Id;
            }

            if (id is null or <= 0 || !seen.Add(id.Value))
            {
                continue;
            }

            var description = SiteDriverLookupOverlay.ReadString(
                row,
                "FullDescription",
                "driver_licence_type_description",
                "Description"
            );
            mapped.Add(
                leftoverByKey is null
                    ? new SiteDriverLicenceTypeDto
                    {
                        Id = id.Value,
                        Code = code,
                        Description = description,
                    }
                    : new SiteDriverLicenceTypeDto
                    {
                        Id = leftoverByKey.Id,
                        Code = leftoverByKey.Code ?? code,
                        Description = description ?? leftoverByKey.Description,
                    }
            );
        }

        return mapped.Count == 0 ? null : mapped;
    }

    private static List<DriverManagementSiteLookupDto>? OverlaySitesForEdit(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> overlayRows,
        IReadOnlyList<DriverManagementSiteLookupDto> leftover
    )
    {
        if (overlayRows.Count == 0)
        {
            return [];
        }

        var leftoverByCode = leftover
            .Where(site => site.SiteCode > 0)
            .GroupBy(site => site.SiteCode)
            .ToDictionary(group => group.Key, group => group.First());
        var mapped = new List<DriverManagementSiteLookupDto>();
        var seen = new HashSet<int>();
        foreach (var row in overlayRows)
        {
            var siteCode = SiteDriverLookupOverlay.ReadInt32(
                row,
                "Site_code",
                "SiteCode",
                "site_code"
            );
            if (siteCode is null or <= 0 || !seen.Add(siteCode.Value))
            {
                continue;
            }

            leftoverByCode.TryGetValue(siteCode.Value, out var leftoverSite);
            var description = SiteDriverLookupOverlay.ReadString(row, "Description", "description");
            var departmentCode = SiteDriverLookupOverlay.ReadInt32(
                row,
                "Depatrment_code",
                "DepartmentCode",
                "department_code"
            );
            mapped.Add(
                leftoverSite is null
                    ? new DriverManagementSiteLookupDto
                    {
                        SiteCode = siteCode.Value,
                        DepartmentCode = departmentCode,
                        Description = description,
                    }
                    : leftoverSite
            );
        }

        return mapped.Count == 0 ? null : mapped;
    }

    private static IReadOnlyList<DriverManagementSiteLookupDto> FilterAllowedSites(
        IReadOnlyList<DriverManagementSiteLookupDto> sites,
        IReadOnlySet<int>? allowedSites
    )
    {
        return allowedSites is null
            ? sites
            : sites.Where(site => allowedSites.Contains(site.SiteCode)).ToList();
    }

    private static DriverManagementSiteLookupDto MapSiteLookup(FIS.Core.Domain.Entities.Site site)
    {
        return new DriverManagementSiteLookupDto
        {
            SiteCode = site.Site_code,
            DepartmentCode = site.Depatrment_code,
            Description = site.description,
        };
    }

    private static DriverDto? MapDriverFromOverlay(IReadOnlyDictionary<string, object?> row)
    {
        var siteDriverCode = SiteDriverLookupOverlay.ReadInt32(
            row,
            "SiteDriverCode",
            "site_driver_code"
        );
        if (siteDriverCode is null or <= 0)
        {
            return null;
        }

        return new DriverDto
        {
            SiteDriverCode = siteDriverCode.Value,
            SiteCode = SiteDriverLookupOverlay.ReadInt32(row, "SiteCode", "site_code") ?? 0,
            DriverLicenceTypeId =
                SiteDriverLookupOverlay.ReadInt32(
                    row,
                    "DriverLicenceTypeID",
                    "driver_licence_type_id"
                ) ?? 0,
            DriverSurname = SiteDriverLookupOverlay.ReadString(row, "Surname", "driver_surname"),
            DriverFirstname = SiteDriverLookupOverlay.ReadString(
                row,
                "FirstName",
                "driver_firstname"
            ),
            DriverSAId = SiteDriverLookupOverlay.ReadString(row, "SAIDNumber", "driver_SA_id"),
            DriverPassportNumber = SiteDriverLookupOverlay.ReadString(
                row,
                "PassportNumber",
                "driver_passportnumber"
            ),
            DriverPersonalNumber = SiteDriverLookupOverlay.ReadString(
                row,
                "PersalNumber",
                "driver_persalnumber"
            ),
            DriverContractNumber = SiteDriverLookupOverlay.ReadString(
                row,
                "DriverContractNumber",
                "driver_contractnumber"
            ),
            DriverLicenceNumber = SiteDriverLookupOverlay.ReadString(
                row,
                "DriverLicenceNumber",
                "driver_licence_number"
            ),
            DriverLicenceIssueDate =
                SiteDriverLookupOverlay.ReadDateTime(
                    row,
                    "DriverLicenceIssueDate",
                    "driver_licence_issuedate"
                ) ?? default,
            DriverLicenceLastVerifiedDate =
                SiteDriverLookupOverlay.ReadDateTime(
                    row,
                    "DriverLicenceLastVerifiedDate",
                    "driver_licence_lastVerifiedDate"
                ) ?? default,
            DriverHasPDP =
                SiteDriverLookupOverlay.ReadBool(row, "HasPDP", "driver_hasPDP") ?? false,
            DriverPDPExpiryDate = SiteDriverLookupOverlay.ReadDateTime(
                row,
                "PDPExpiryDate",
                "driver_PDP_ExpiryDate"
            ),
            DriverLicenceExpiryDate = SiteDriverLookupOverlay.ReadDateTime(
                row,
                "LicenceExpiryDate",
                "driver_licence_ExpiryDate"
            ),
            DriverActive = SiteDriverLookupOverlay.ReadBool(row, "Active", "driver_active") ?? true,
        };
    }

    private static bool IsLegacySelectorContractException(Exception ex)
    {
        return ex is LegacySiteDriverProcedureContractException
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
            ? StatusCode(503, new { message = "The site-driver database is unavailable." })
            : StatusCode(500, new { message = $"Error {operation}." });
    }

    private static string QuoteIdentifier(string identifier) => $"[{identifier}]";

    private sealed record TableSchema(IReadOnlySet<string> Columns)
    {
        public bool Has(string column) => Columns.Contains(column);
    }
}

public sealed class LegacySiteDriverProcedureContractException : InvalidOperationException
{
    public LegacySiteDriverProcedureContractException(string message)
        : base(message) { }
}

public sealed class SiteDriverScopeException : InvalidOperationException { }

public sealed class SiteDriverDuplicateException : InvalidOperationException
{
    public SiteDriverDuplicateException(string message)
        : base(message) { }
}

public sealed class SiteDriverLicenceTypeDto
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
}

public sealed class DriverManagementSiteLookupDto
{
    public int SiteCode { get; set; }
    public int? DepartmentCode { get; set; }
    public string? Description { get; set; }
}

public sealed class SiteDriverLicenceTypeWriteDto
{
    public string? Code { get; set; }
    public string? Description { get; set; }
}
