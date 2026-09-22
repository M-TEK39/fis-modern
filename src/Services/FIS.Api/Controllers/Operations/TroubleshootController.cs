using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.ReferenceData;
using FIS.Core.Domain.Entities.System;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/troubleshoot")]
[Authorize(Roles = "Trouble Shooting")]
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "All interpolated SQL identifiers come from fixed table and column names discovered from INFORMATION_SCHEMA; request values are parameters."
)]
public class TroubleshootController : BaseApiController
{
    private const string TripsWithoutRoutesBackupTable = "TripsWithoutRoutes_Backup";
    private const int DefaultPageSize = 24;
    private const int MaximumPageSize = 100;
    private readonly FisDbContext _context;
    private readonly ILogger<TroubleshootController> _logger;

    public TroubleshootController(FisDbContext context, ILogger<TroubleshootController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult GetRoot()
    {
        return Ok(
            new
            {
                module = "Troubleshoot",
                endpoints = new[]
                {
                    "users",
                    "users/page",
                    "departmentsites",
                    "log/search",
                    "log/search/page",
                    "log/update",
                    "reports/general",
                    "reports/general/page",
                    "odometer/search",
                    "odometer/search/page",
                    "trips-without-routes/page",
                    "remove-trips-no-routes",
                    "approver-ranks",
                    "vehicle-master-edit",
                    "vehicle-master-edit/page",
                },
            }
        );
    }

    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<TroubleshootUserDto>>> GetUsers()
    {
        var users = await _context
            .UserAccessOlds.AsNoTracking()
            .Where(x => x.user_active)
            .OrderBy(x => x.name)
            .Take(500)
            .Select(x => new TroubleshootUserDto
            {
                UserAccessCode = x.user_access_code,
                Name = x.name ?? string.Empty,
                SiteDescription = _context
                    .Sites.Where(s => s.Site_code == x.Site_code)
                    .Select(s => s.description)
                    .FirstOrDefault(),
                FirstName = x.FirstName,
                LastName = x.LastName,
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("users/page")]
    public async Task<ActionResult> GetUsersPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize
    )
    {
        var (normalizedPage, normalizedPageSize) = NormalizePaging(page, pageSize);
        var activeUsers = _context.UserAccessOlds.AsNoTracking().Where(x => x.user_active);
        var total = await activeUsers.CountAsync();
        var totalPages = CalculateTotalPages(total, normalizedPageSize);
        normalizedPage = Math.Min(normalizedPage, totalPages);

        var users = await activeUsers
            .OrderBy(x => x.name)
            .ThenBy(x => x.user_access_code)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new TroubleshootUserDto
            {
                UserAccessCode = x.user_access_code,
                Name = x.name ?? string.Empty,
                SiteDescription = _context
                    .Sites.Where(s => s.Site_code == x.Site_code)
                    .Select(s => s.description)
                    .FirstOrDefault(),
                FirstName = x.FirstName,
                LastName = x.LastName,
            })
            .ToListAsync();

        return Ok(
            new
            {
                items = users,
                page = normalizedPage,
                pageSize = normalizedPageSize,
                total,
                totalPages,
            }
        );
    }

    [HttpGet("departmentsites")]
    public async Task<ActionResult<IEnumerable<TroubleshootSiteUserDto>>> GetDepartmentSites()
    {
        var users = await _context
            .UserAccessOlds.AsNoTracking()
            .Where(x => x.user_active)
            .Join(
                _context.Sites.AsNoTracking(),
                u => u.Site_code,
                s => s.Site_code,
                (u, s) =>
                    new TroubleshootSiteUserDto
                    {
                        UserAccessCode = u.user_access_code,
                        Name = u.name ?? string.Empty,
                        SiteDescription = s.description,
                        SiteCode = s.Site_code,
                    }
            )
            .OrderBy(x => x.Name)
            .Take(1000)
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost("log/search")]
    public async Task<ActionResult<IEnumerable<TroubleshootLogEntryDto>>> SearchLogs(
        [FromBody] TroubleshootLogSearchRequest request
    )
    {
        var rows = await QueryLogs(request.UserAccessCode, null, null, null).ToListAsync();
        return Ok(rows);
    }

    [HttpPost("log/search/page")]
    public async Task<ActionResult> SearchLogsPaged([FromBody] TroubleshootLogSearchRequest request)
    {
        var (page, pageSize) = NormalizePaging(request.Page, request.PageSize);
        var filtered = FilterLogs(request.UserAccessCode, null, null, null);
        var total = await filtered.CountAsync();
        var totalPages = CalculateTotalPages(total, pageSize);
        page = Math.Min(page, totalPages);

        var items = await ProjectLogs(
                OrderPagedLogs(filtered).Skip(CalculateSkip(page, pageSize)).Take(pageSize)
            )
            .ToListAsync();

        return Ok(
            new
            {
                items,
                page,
                pageSize,
                total,
                totalPages,
            }
        );
    }

    [HttpPost("log/update")]
    public async Task<ActionResult> UpdateLogs()
    {
        var now = DateTime.UtcNow;
        var rows = await _context
            .TSLogs.Where(x => x.date_updated == null)
            .ToListAsync();
        foreach (var row in rows)
        {
            row.date_updated = now;
            row.modified_by_user_code = GetCurrentUserId();
        }

        await _context.SaveChangesAsync();
        return Ok(new { updated = rows.Count });
    }

    [HttpPost("reports/general")]
    public async Task<ActionResult<IEnumerable<TroubleshootLogEntryDto>>> GetGeneralReports(
        [FromBody] TroubleshootReportFilter filter
    )
    {
        var rows = await QueryLogs(
                filter.UserAccessCode,
                filter.ProblemKeyword,
                filter.FromDate,
                filter.ToDate
            )
            .ToListAsync();
        return Ok(rows);
    }

    [HttpPost("reports/general/page")]
    public async Task<ActionResult> GetGeneralReportsPaged(
        [FromBody] TroubleshootReportFilter filter
    )
    {
        var (page, pageSize) = NormalizePaging(filter.Page, filter.PageSize);
        var filtered = FilterLogs(
            filter.UserAccessCode,
            filter.ProblemKeyword,
            filter.FromDate,
            filter.ToDate
        );
        var total = await filtered.CountAsync();
        var totalPages = CalculateTotalPages(total, pageSize);
        page = Math.Min(page, totalPages);

        var items = await ProjectLogs(
                OrderPagedLogs(filtered).Skip(CalculateSkip(page, pageSize)).Take(pageSize)
            )
            .ToListAsync();

        return Ok(
            new
            {
                items,
                page,
                pageSize,
                total,
                totalPages,
            }
        );
    }

    [HttpPost("odometer/search")]
    public async Task<
        ActionResult<IEnumerable<OdometerCorrectionResultDto>>
    > SearchOdometerCorrections([FromBody] OdometerCorrectionSearchRequest request)
    {
        var mode = (request.SearchMode ?? string.Empty).Trim().ToUpperInvariant();
        var searchValue = (request.SearchValue ?? string.Empty).Trim();
        var overlay = await TryReadOdometerTripDetailsAsync(mode, searchValue);
        if (overlay is not null)
        {
            return Ok(overlay);
        }

        if (mode == "TA")
        {
            IQueryable<Trip> tripQuery = _context.Trips.AsNoTracking();

            if (int.TryParse(searchValue, out var taCode))
            {
                tripQuery = tripQuery.Where(t => t.trip_authority_code == taCode);
            }
            else
            {
                tripQuery = tripQuery.Where(t =>
                    t.trip_authority_code.ToString().Contains(searchValue)
                );
            }

            var taRows = await tripQuery
                .Join(
                    _context.Contracts.AsNoTracking(),
                    t => t.contract_code,
                    c => c.contract_code,
                    (t, c) => new { t, c }
                )
                .Join(
                    _context.Vehicles.AsNoTracking(),
                    tc => tc.c.vmf_code,
                    v => v.vmf_code,
                    (tc, v) =>
                        new OdometerCorrectionResultDto
                        {
                            VehicleIdentifier = string.IsNullOrWhiteSpace(v.fleet_number)
                                ? v.registration_number
                                : v.fleet_number,
                            TripAuthorityNumber = tc.t.trip_authority_code.ToString(),
                            CurrentOdometer = v.current_odo,
                            LastOdometer = tc.t.end_odo_meter ?? tc.c.end_odometer,
                        }
                )
                .OrderByDescending(x => x.TripAuthorityNumber)
                .Take(200)
                .ToListAsync();

            return Ok(taRows);
        }

        var vehicleQuery = FilterOdometerVehicles(mode, searchValue);

        var rows = await vehicleQuery
            .OrderBy(v => v.fleet_number)
            .Take(200)
            .Select(v => new OdometerCorrectionResultDto
            {
                VehicleIdentifier = string.IsNullOrWhiteSpace(v.fleet_number)
                    ? v.registration_number
                    : v.fleet_number,
                TripAuthorityNumber = null,
                CurrentOdometer = v.current_odo,
                LastOdometer = v.highest_km.HasValue ? (int?)Math.Round(v.highest_km.Value) : null,
            })
            .ToListAsync();

        return Ok(rows);
    }

    [HttpPost("odometer/search/page")]
    public async Task<ActionResult> SearchOdometerCorrectionsPaged(
        [FromBody] OdometerCorrectionSearchRequest request
    )
    {
        var mode = (request.SearchMode ?? string.Empty).Trim().ToUpperInvariant();
        var searchValue = (request.SearchValue ?? string.Empty).Trim();
        var (page, pageSize) = NormalizePaging(request.Page, request.PageSize);
        var overlay = await TryReadOdometerTripDetailsAsync(mode, searchValue);
        if (overlay is not null)
        {
            var total = overlay.Count;
            var totalPages = CalculateTotalPages(total, pageSize);
            page = Math.Min(page, totalPages);
            var items = overlay.Skip(CalculateSkip(page, pageSize)).Take(pageSize).ToList();
            return Ok(
                new
                {
                    items,
                    page,
                    pageSize,
                    total,
                    totalPages,
                }
            );
        }

        if (mode == "TA")
        {
            var filtered = QueryOdometerTaRows(searchValue);
            var total = await filtered.CountAsync();
            var totalPages = CalculateTotalPages(total, pageSize);
            page = Math.Min(page, totalPages);

            var items = await filtered
                .OrderByDescending(x => x.TripAuthorityNumber)
                .ThenByDescending(x => x.TripAuthorityCode)
                .Skip(CalculateSkip(page, pageSize))
                .Take(pageSize)
                .Select(x => new OdometerCorrectionResultDto
                {
                    VehicleIdentifier = x.VehicleIdentifier,
                    TripAuthorityNumber = x.TripAuthorityNumber,
                    CurrentOdometer = x.CurrentOdometer,
                    LastOdometer = x.LastOdometer,
                })
                .ToListAsync();

            return Ok(
                new
                {
                    items,
                    page,
                    pageSize,
                    total,
                    totalPages,
                }
            );
        }

        var vehicleQuery = FilterOdometerVehicles(mode, searchValue);
        var vehicleTotal = await vehicleQuery.CountAsync();
        var vehicleTotalPages = CalculateTotalPages(vehicleTotal, pageSize);
        page = Math.Min(page, vehicleTotalPages);

        var vehicleItems = await vehicleQuery
            .OrderBy(v => v.fleet_number)
            .ThenBy(v => v.vmf_code)
            .Skip(CalculateSkip(page, pageSize))
            .Take(pageSize)
            .Select(v => new OdometerCorrectionResultDto
            {
                VehicleIdentifier = string.IsNullOrWhiteSpace(v.fleet_number)
                    ? v.registration_number
                    : v.fleet_number,
                TripAuthorityNumber = null,
                CurrentOdometer = v.current_odo,
                LastOdometer = v.highest_km.HasValue ? (int?)Math.Round(v.highest_km.Value) : null,
            })
            .ToListAsync();

        return Ok(
            new
            {
                items = vehicleItems,
                page,
                pageSize,
                total = vehicleTotal,
                totalPages = vehicleTotalPages,
            }
        );
    }

    [HttpGet("trips-without-routes")]
    public async Task<ActionResult<IEnumerable<TripsWithoutRoutesDto>>> GetTripsWithoutRoutes()
    {
        try
        {
            return Ok(await GetTripsWithoutRoutesData());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving trips without routes");
            return StatusCode(500, new { message = "Unable to retrieve trips without routes." });
        }
    }

    [HttpGet("trips-without-routes/page")]
    public async Task<ActionResult> GetTripsWithoutRoutesPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize
    )
    {
        try
        {
            var (requestedPage, normalizedPageSize) = NormalizePaging(page, pageSize);
            // Some legacy deployments only expose the stored procedure. Its
            // result has no paging parameters, so it is materialized within
            // the protected API process and only the requested page crosses
            // the HTTP boundary.
            var rows = await GetTripsWithoutRoutesData();
            var total = rows.Count;
            var totalPages = CalculateTotalPages(total, normalizedPageSize);
            var resolvedPage = Math.Min(requestedPage, totalPages);
            var items = rows.Skip(CalculateSkip(resolvedPage, normalizedPageSize))
                .Take(normalizedPageSize)
                .ToList();
            return Ok(
                new
                {
                    items,
                    page = resolvedPage,
                    pageSize = normalizedPageSize,
                    total,
                    totalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged trips without routes");
            return StatusCode(500, new { message = "Unable to retrieve trips without routes." });
        }
    }

    [HttpPost("remove-trips-no-routes")]
    public async Task<ActionResult> RemoveTripsWithoutRoutes([FromBody] RemoveTripsRequest request)
    {
        try
        {
            var removed = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, TripsWithoutRoutesBackupTable);
                if (schema.Count > 0)
                {
                    return await DeleteBackupTripsWithoutRoutesAsync(connection, schema, request);
                }

                return await ExecuteStoredProcedureDeleteAsync(
                    connection,
                    "ADM_DEL_TripsWithoutRoutes"
                );
            });

            return Ok(new { removed });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing trips without routes");
            return StatusCode(500, new { message = "Unable to remove trips without routes." });
        }
    }

    [HttpGet("approver-ranks")]
    public async Task<ActionResult<IEnumerable<ApproverRankDto>>> GetApproverRanks()
    {
        return Ok(await GetApproverRanksData());
    }

    [HttpPost("approver-ranks")]
    public async Task<ActionResult<IEnumerable<ApproverRankDto>>> SaveApproverRanks(
        [FromBody] List<ApproverRankDto> ranks
    )
    {
        foreach (var dto in ranks)
        {
            if (dto.Id <= 0)
            {
                var created = new Rank { description = dto.RankName ?? dto.Description };
                _context.Ranks.Add(created);
                continue;
            }

            var existing = await _context.Ranks.FirstOrDefaultAsync(x => x.rank_code == dto.Id);
            if (existing == null)
            {
                continue;
            }

            existing.description = dto.RankName ?? dto.Description;
        }

        await _context.SaveChangesAsync();
        return Ok(await GetApproverRanksData());
    }

    [HttpPost("vehicle-master-edit")]
    public async Task<ActionResult<IEnumerable<VehicleLookupDto>>> GetVehicleMasterEdit(
        [FromBody] VehicleMasterEditRequest request
    )
    {
        var id = (request.VehicleIdentifier ?? string.Empty).Trim();
        var rows = await FilterVehicleMasterEdit(id)
            .OrderBy(v => v.fleet_number)
            .Take(50)
            .Select(v => new VehicleLookupDto
            {
                VmfCode = v.vmf_code,
                FleetNumber = v.fleet_number,
                RegistrationNumber = v.registration_number,
                CurrentOdometer = v.current_odo,
                RecoveredGg = v.derived_odo,
            })
            .ToListAsync();

        return Ok(rows);
    }

    [HttpPost("vehicle-master-edit/page")]
    public async Task<ActionResult> GetVehicleMasterEditPaged(
        [FromBody] VehicleMasterEditRequest request
    )
    {
        var id = (request.VehicleIdentifier ?? string.Empty).Trim();
        var (page, pageSize) = NormalizePaging(request.Page, request.PageSize);
        var filtered = FilterVehicleMasterEdit(id);
        var total = await filtered.CountAsync();
        var totalPages = CalculateTotalPages(total, pageSize);
        page = Math.Min(page, totalPages);

        var items = await filtered
            .OrderBy(v => v.fleet_number)
            .ThenBy(v => v.vmf_code)
            .Skip(CalculateSkip(page, pageSize))
            .Take(pageSize)
            .Select(v => new VehicleLookupDto
            {
                VmfCode = v.vmf_code,
                FleetNumber = v.fleet_number,
                RegistrationNumber = v.registration_number,
                CurrentOdometer = v.current_odo,
                RecoveredGg = v.derived_odo,
            })
            .ToListAsync();

        return Ok(
            new
            {
                items,
                page,
                pageSize,
                total,
                totalPages,
            }
        );
    }

    private IQueryable<TroubleshootLogEntryDto> QueryLogs(
        int? userAccessCode,
        string? keyword,
        DateTime? fromDate,
        DateTime? toDate
    )
    {
        return ProjectLogs(
            OrderLogs(FilterLogs(userAccessCode, keyword, fromDate, toDate)).Take(1000)
        );
    }

    private IQueryable<TSLog> FilterLogs(
        int? userAccessCode,
        string? keyword,
        DateTime? fromDate,
        DateTime? toDate
    )
    {
        var query = _context.TSLogs.AsNoTracking().AsQueryable();

        if (userAccessCode.HasValue && userAccessCode.Value > 0)
        {
            query = query.Where(x => x.user_access_code == userAccessCode.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.ErrorCode != null && x.ErrorCode.Contains(keyword));
        }

        if (fromDate.HasValue)
        {
            query = query.Where(x =>
                x.TSDate.HasValue && x.TSDate.Value.Date >= fromDate.Value.Date
            );
        }

        if (toDate.HasValue)
        {
            query = query.Where(x => x.TSDate.HasValue && x.TSDate.Value.Date <= toDate.Value.Date);
        }

        return query;
    }

    private static IOrderedQueryable<TSLog> OrderLogs(IQueryable<TSLog> query)
    {
        return query.OrderByDescending(x => x.TSDate).ThenByDescending(x => x.TSTime);
    }

    private static IOrderedQueryable<TSLog> OrderPagedLogs(IQueryable<TSLog> query)
    {
        return OrderLogs(query).ThenByDescending(x => x.ErrorID);
    }

    private static IQueryable<TroubleshootLogEntryDto> ProjectLogs(IQueryable<TSLog> query)
    {
        return query.Select(x => new TroubleshootLogEntryDto
        {
            Id = x.ErrorID,
            VehicleIdentifier = x.ErrorCode,
            ProblemDescription = x.ErrorCode,
            Status = x.date_updated == null ? "Active" : "Updated",
            LoggedDate = x.TSDate,
            LoggedBy = x.user_access_code == null ? null : x.user_access_code.ToString(),
        });
    }

    private IQueryable<OdometerCorrectionQueryRow> QueryOdometerTaRows(string searchValue)
    {
        IQueryable<Trip> tripQuery = _context.Trips.AsNoTracking();

        if (int.TryParse(searchValue, out var taCode))
        {
            tripQuery = tripQuery.Where(t => t.trip_authority_code == taCode);
        }
        else
        {
            tripQuery = tripQuery.Where(t =>
                t.trip_authority_code.ToString().Contains(searchValue)
            );
        }

        return tripQuery
            .Join(
                _context.Contracts.AsNoTracking(),
                t => t.contract_code,
                c => c.contract_code,
                (t, c) => new { t, c }
            )
            .Join(
                _context.Vehicles.AsNoTracking(),
                tc => tc.c.vmf_code,
                v => v.vmf_code,
                (tc, v) =>
                    new OdometerCorrectionQueryRow
                    {
                        TripAuthorityCode = tc.t.trip_authority_code,
                        VehicleIdentifier = string.IsNullOrWhiteSpace(v.fleet_number)
                            ? v.registration_number
                            : v.fleet_number,
                        TripAuthorityNumber = tc.t.trip_authority_code.ToString(),
                        CurrentOdometer = v.current_odo,
                        LastOdometer = tc.t.end_odo_meter ?? tc.c.end_odometer,
                    }
            );
    }

    private async Task<IReadOnlyList<OdometerCorrectionResultDto>?> TryReadOdometerTripDetailsAsync(
        string mode,
        string searchValue
    )
    {
        if (string.IsNullOrWhiteSpace(searchValue))
        {
            return null;
        }

        var selectedId = mode is "GG" or "REG" or "TA" ? mode : "GG";
        return await WithConnectionAsync(connection =>
            ReadOdometerTripDetailsAsync(connection, selectedId, searchValue.Trim())
        );
    }

    private static async Task<
        IReadOnlyList<OdometerCorrectionResultDto>?
    > ReadOdometerTripDetailsAsync(DbConnection connection, string mode, string searchValue)
    {
        var actualParameters = await GetProcedureParametersAsync(
            connection,
            "DEV_SEL_GetTripDetails"
        );
        if (actualParameters is null)
        {
            return null;
        }

        if (
            !actualParameters.SequenceEqual(["@Value", "@Mode"], StringComparer.OrdinalIgnoreCase)
        )
        {
            throw new InvalidOperationException(
                "The deployed legacy procedure DEV_SEL_GetTripDetails does not match its verified parameter contract. No direct-DML fallback was run."
            );
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "DEV_SEL_GetTripDetails";
        command.CommandType = CommandType.StoredProcedure;
        AddParameter(command, "@Value", searchValue);
        AddParameter(command, "@Mode", mode);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<OdometerCorrectionResultDto>();
        while (await reader.ReadAsync())
        {
            var mapped = MapOdometerTripDetailsRow(reader);
            if (mapped is not null)
            {
                rows.Add(mapped);
            }
        }

        return rows;
    }

    private static OdometerCorrectionResultDto? MapOdometerTripDetailsRow(DbDataReader reader)
    {
        var vehicleIdentifier =
            ReadString(
                reader,
                "fleet_number",
                "GG_Number",
                "GG Number",
                "GGNo",
                "registration_number"
            ) ?? ReadOrdinalString(reader, 0);
        var tripAuthority =
            ReadString(
                reader,
                "trip_authority_code",
                "Trip_Authority_Code",
                "Trip Auth.",
                "TripAuth"
            ) ?? ReadOrdinalString(reader, 2);
        var startOdo =
            ReadInt(reader, "start_odo_meter", "start_odo", "StartODO", "Start ODO")
            ?? ReadOrdinalInt(reader, 5);
        var endOdo =
            ReadInt(reader, "end_odo_meter", "end_odo", "EndODO", "End ODO")
            ?? ReadOrdinalInt(reader, 6);

        if (
            string.IsNullOrWhiteSpace(vehicleIdentifier)
            && string.IsNullOrWhiteSpace(tripAuthority)
            && startOdo is null
            && endOdo is null
        )
        {
            return null;
        }

        return new OdometerCorrectionResultDto
        {
            VehicleIdentifier = vehicleIdentifier,
            TripAuthorityNumber = tripAuthority,
            CurrentOdometer = startOdo,
            LastOdometer = endOdo,
        };
    }

    private static async Task<List<string>?> GetProcedureParametersAsync(
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
               AND [parameterObject].[parameter_id] > 0
            WHERE [schemaObject].[name] = @schemaName
              AND [procedureObject].[name] = @procedureName
            ORDER BY [parameterObject].[parameter_id]
            """;
        AddParameter(command, "@schemaName", "dbo");
        AddParameter(command, "@procedureName", procedureName);

        var actualParameters = new List<string>();
        var procedureFound = false;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            procedureFound = true;
            if (!reader.IsDBNull(0))
            {
                actualParameters.Add(reader.GetString(0));
            }
        }

        return procedureFound ? actualParameters : null;
    }

    private static string? ReadOrdinalString(DbDataReader reader, int ordinal)
    {
        if (ordinal < 0 || ordinal >= reader.FieldCount || reader.IsDBNull(ordinal))
        {
            return null;
        }

        return Convert.ToString(reader.GetValue(ordinal))?.Trim();
    }

    private static int? ReadOrdinalInt(DbDataReader reader, int ordinal)
    {
        if (ordinal < 0 || ordinal >= reader.FieldCount || reader.IsDBNull(ordinal))
        {
            return null;
        }

        return int.TryParse(Convert.ToString(reader.GetValue(ordinal)), out var parsed)
            ? parsed
            : null;
    }

    private IQueryable<Vehicle> FilterOdometerVehicles(string mode, string searchValue)
    {
        var vehicleQuery = _context.Vehicles.AsNoTracking();
        if (string.IsNullOrWhiteSpace(searchValue))
        {
            return vehicleQuery;
        }

        return mode switch
        {
            "VMF" => vehicleQuery.Where(v => v.vmf_code.ToString() == searchValue),
            "REG" => vehicleQuery.Where(v =>
                v.registration_number != null && v.registration_number.Contains(searchValue)
            ),
            _ => vehicleQuery.Where(v =>
                v.fleet_number != null && v.fleet_number.Contains(searchValue)
            ),
        };
    }

    private IQueryable<Vehicle> FilterVehicleMasterEdit(string id)
    {
        return _context
            .Vehicles.AsNoTracking()
            .Where(v =>
                v.vmf_code.ToString() == id
                || (v.fleet_number != null && v.fleet_number.Contains(id))
                || (v.registration_number != null && v.registration_number.Contains(id))
            );
    }

    private static (int Page, int PageSize) NormalizePaging(int? page, int? pageSize)
    {
        return (
            Math.Max(1, page ?? 1),
            Math.Clamp(pageSize ?? DefaultPageSize, 1, MaximumPageSize)
        );
    }

    private static int CalculateTotalPages(int total, int pageSize)
    {
        return Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
    }

    private static int CalculateSkip(int page, int pageSize)
    {
        return checked((page - 1) * pageSize);
    }

    private async Task<List<ApproverRankDto>> GetApproverRanksData()
    {
        return await _context
            .Ranks.AsNoTracking()
            .OrderBy(x => x.description)
            .Select(x => new ApproverRankDto
            {
                Id = x.rank_code,
                RankName = x.description,
                Description = x.description,
            })
            .ToListAsync();
    }

    private Task<List<TripsWithoutRoutesDto>> GetTripsWithoutRoutesData() =>
        WithConnectionAsync(async connection =>
        {
            var schema = await ReadTableSchemaAsync(connection, TripsWithoutRoutesBackupTable);
            return schema.Count > 0
                ? await ReadBackupTripsWithoutRoutesAsync(connection, schema)
                : await ReadStoredProcedureTripsWithoutRoutesAsync(connection);
        });

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

    private static async Task<HashSet<string>> ReadTableSchemaAsync(
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

        return columns;
    }

    private static async Task<List<TripsWithoutRoutesDto>> ReadBackupTripsWithoutRoutesAsync(
        DbConnection connection,
        HashSet<string> schema
    )
    {
        if (
            !schema.Contains("trip_authority_code")
            || !schema.Contains("contract_code")
            || !schema.Contains("issue_date")
        )
        {
            return [];
        }

        var selections = new List<(string Column, string Alias)>
        {
            ("trip_authority_code", "TripAuthorityCode"),
            ("contract_code", "ContractCode"),
            ("issue_date", "IssueDate"),
        };
        AddSelection(schema, selections, "trip_reason", "TripReason");
        AddSelection(schema, selections, "trip_request_number", "TripRequestNumber");
        AddSelection(schema, selections, "approver_name", "ApproverName");

        var predicates = new List<string>();
        if (schema.Contains("is_deleted"))
        {
            predicates.Add("COALESCE([is_deleted], 0) = 0");
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT {string.Join(", ", selections.Select(item => $"[{item.Column}] AS [{item.Alias}]"))} FROM [dbo].[{TripsWithoutRoutesBackupTable}] WHERE {string.Join(" AND ", predicates.Count == 0 ? ["1 = 1"] : predicates)} ORDER BY [issue_date], [trip_authority_code]";
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<TripsWithoutRoutesDto>();
        while (await reader.ReadAsync())
        {
            rows.Add(ReadTripsWithoutRoutesRow(reader));
        }

        return rows;
    }

    private static async Task<
        List<TripsWithoutRoutesDto>
    > ReadStoredProcedureTripsWithoutRoutesAsync(DbConnection connection)
    {
        if (!await StoredProcedureExistsAsync(connection, "DEV_SEL_TripsWithoutRoutes"))
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "DEV_SEL_TripsWithoutRoutes";
        command.CommandType = CommandType.StoredProcedure;
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<TripsWithoutRoutesDto>();
        while (await reader.ReadAsync())
        {
            rows.Add(ReadTripsWithoutRoutesRow(reader));
        }

        return rows;
    }

    private async Task<int> DeleteBackupTripsWithoutRoutesAsync(
        DbConnection connection,
        HashSet<string> schema,
        RemoveTripsRequest request
    )
    {
        var predicates = new List<string>();
        if (schema.Contains("is_deleted"))
        {
            predicates.Add("COALESCE([is_deleted], 0) = 0");
        }

        var values = new List<(string Name, object? Value)>();
        if (schema.Contains("issue_date") && request.FromDate.HasValue)
        {
            predicates.Add("[issue_date] >= @fromDate");
            values.Add(("@fromDate", request.FromDate.Value.Date));
        }
        if (schema.Contains("issue_date") && request.ToDate.HasValue)
        {
            predicates.Add("[issue_date] < @toDateExclusive");
            values.Add(("@toDateExclusive", request.ToDate.Value.Date.AddDays(1)));
        }

        await using var command = connection.CreateCommand();
        if (schema.Contains("is_deleted"))
        {
            var assignments = new List<string> { "[is_deleted] = @isDeleted" };
            values.Add(("@isDeleted", true));
            if (schema.Contains("date_updated"))
            {
                assignments.Add("[date_updated] = @dateUpdated");
                values.Add(("@dateUpdated", DateTime.UtcNow));
            }
            if (schema.Contains("modified_by_user_code"))
            {
                assignments.Add("[modified_by_user_code] = @modifiedByUserCode");
                values.Add(("@modifiedByUserCode", GetCurrentUserId()));
            }

            command.CommandText =
                $"UPDATE [dbo].[{TripsWithoutRoutesBackupTable}] SET {string.Join(", ", assignments)} WHERE {string.Join(" AND ", predicates.Count == 0 ? ["1 = 1"] : predicates)}";
        }
        else
        {
            command.CommandText =
                $"DELETE FROM [dbo].[{TripsWithoutRoutesBackupTable}] WHERE {string.Join(" AND ", predicates.Count == 0 ? ["1 = 1"] : predicates)}";
        }

        foreach (var (name, value) in values)
        {
            AddParameter(command, name, value);
        }

        var affected = await command.ExecuteNonQueryAsync();
        return affected < 0 ? 0 : affected;
    }

    private static async Task<int> ExecuteStoredProcedureDeleteAsync(
        DbConnection connection,
        string procedureName
    )
    {
        if (!await StoredProcedureExistsAsync(connection, procedureName))
        {
            return 0;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = procedureName;
        command.CommandType = CommandType.StoredProcedure;
        var affected = await command.ExecuteNonQueryAsync();
        return affected < 0 ? 0 : affected;
    }

    private static async Task<bool> StoredProcedureExistsAsync(
        DbConnection connection,
        string procedureName
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.objects WHERE schema_id = SCHEMA_ID(@schema) AND name = @name AND type IN ('P', 'PC')) THEN 1 ELSE 0 END";
        AddParameter(command, "@schema", "dbo");
        AddParameter(command, "@name", procedureName);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    private static void AddSelection(
        HashSet<string> schema,
        ICollection<(string Column, string Alias)> selections,
        string column,
        string alias
    )
    {
        if (schema.Contains(column))
        {
            selections.Add((column, alias));
        }
    }

    private static TripsWithoutRoutesDto ReadTripsWithoutRoutesRow(DbDataReader reader)
    {
        return new TripsWithoutRoutesDto
        {
            TripAuthorityCode = ReadInt(reader, "TripAuthorityCode", "trip_authority_code"),
            ContractCode = ReadInt(reader, "ContractCode", "contract_code"),
            IssueDate = ReadDateTime(reader, "IssueDate", "issue_date"),
            TripReason = ReadString(reader, "TripReason", "trip_reason"),
            TripRequestNumber = ReadString(reader, "TripRequestNumber", "trip_request_number"),
            ApproverName = ReadString(reader, "ApproverName", "approver_name"),
        };
    }

    private static int? ReadInt(DbDataReader reader, params string[] names)
    {
        var value = ReadValue(reader, names);
        return value is null ? null : Convert.ToInt32(value);
    }

    private static DateTime? ReadDateTime(DbDataReader reader, params string[] names)
    {
        var value = ReadValue(reader, names);
        return value is null ? null : Convert.ToDateTime(value);
    }

    private static string? ReadString(DbDataReader reader, params string[] names)
    {
        var value = ReadValue(reader, names);
        return value?.ToString()?.Trim();
    }

    private static object? ReadValue(DbDataReader reader, params string[] names)
    {
        foreach (var name in names)
        {
            try
            {
                var ordinal = reader.GetOrdinal(name);
                return reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
            }
            catch (IndexOutOfRangeException)
            {
                // Stored procedure column names vary between legacy database copies.
            }
        }

        return null;
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    public class TroubleshootUserDto
    {
        public int UserAccessCode { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? SiteDescription { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }

    public class TroubleshootSiteUserDto
    {
        public int UserAccessCode { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? SiteDescription { get; set; }
        public short? SiteCode { get; set; }
    }

    public class TroubleshootLogSearchRequest
    {
        public int UserAccessCode { get; set; }
        public int? Page { get; set; }
        public int? PageSize { get; set; }
    }

    public class TroubleshootLogEntryDto
    {
        public int Id { get; set; }
        public string? VehicleIdentifier { get; set; }
        public string? ProblemDescription { get; set; }
        public string? Status { get; set; }
        public DateTime? LoggedDate { get; set; }
        public string? LoggedBy { get; set; }
    }

    public class TroubleshootReportFilter
    {
        public string? ProblemKeyword { get; set; }
        public int? UserAccessCode { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public bool OpenInExcel { get; set; }
        public int? Page { get; set; }
        public int? PageSize { get; set; }
    }

    public class OdometerCorrectionSearchRequest
    {
        public string SearchMode { get; set; } = "GG";
        public string SearchValue { get; set; } = string.Empty;
        public int? Page { get; set; }
        public int? PageSize { get; set; }
    }

    public class OdometerCorrectionResultDto
    {
        public string? VehicleIdentifier { get; set; }
        public string? TripAuthorityNumber { get; set; }
        public int? CurrentOdometer { get; set; }
        public int? LastOdometer { get; set; }
    }

    public class RemoveTripsRequest
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    public class TripsWithoutRoutesDto
    {
        public int? TripAuthorityCode { get; set; }
        public int? ContractCode { get; set; }
        public DateTime? IssueDate { get; set; }
        public string? TripReason { get; set; }
        public string? TripRequestNumber { get; set; }
        public string? ApproverName { get; set; }
    }

    public class ApproverRankDto
    {
        public int Id { get; set; }
        public string? RankName { get; set; }
        public string? Description { get; set; }
    }

    public class VehicleMasterEditRequest
    {
        public string? VehicleIdentifier { get; set; }
        public int? Page { get; set; }
        public int? PageSize { get; set; }
    }

    public class VehicleLookupDto
    {
        public int VmfCode { get; set; }
        public string? FleetNumber { get; set; }
        public string? RegistrationNumber { get; set; }
        public int? CurrentOdometer { get; set; }
        public string? RecoveredGg { get; set; }
    }

    private sealed class OdometerCorrectionQueryRow
    {
        public int TripAuthorityCode { get; set; }
        public string? VehicleIdentifier { get; set; }
        public string? TripAuthorityNumber { get; set; }
        public int? CurrentOdometer { get; set; }
        public int? LastOdometer { get; set; }
    }
}
