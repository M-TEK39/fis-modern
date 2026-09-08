using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.ReferenceData;
using FIS.Data.SqlServer;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/troubleshoot")]
[Authorize]
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "All interpolated SQL identifiers come from fixed table and column names discovered from INFORMATION_SCHEMA; request values are parameters.")]
public class TroubleshootController : BaseApiController
{
    private const string TripsWithoutRoutesBackupTable = "TripsWithoutRoutes_Backup";
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
        return Ok(new
        {
            module = "Troubleshoot",
            endpoints = new[]
            {
                "users",
                "departmentsites",
                "log/search",
                "log/update",
                "reports/general",
                "odometer/search",
                "remove-trips-no-routes",
                "approver-ranks",
                "vehicle-master-edit",
                "update-recovered-gg"
            }
        });
    }

    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<TroubleshootUserDto>>> GetUsers()
    {
        var users = await _context.UserAccessOlds
            .AsNoTracking()
            .Where(x => x.user_active)
            .OrderBy(x => x.name)
            .Take(500)
            .Select(x => new TroubleshootUserDto
            {
                UserAccessCode = x.user_access_code,
                Name = x.name ?? string.Empty,
                SiteDescription = _context.Sites.Where(s => s.Site_code == x.Site_code).Select(s => s.description).FirstOrDefault(),
                FirstName = x.FirstName,
                LastName = x.LastName
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("departmentsites")]
    public async Task<ActionResult<IEnumerable<TroubleshootSiteUserDto>>> GetDepartmentSites()
    {
        var users = await _context.UserAccessOlds
            .AsNoTracking()
            .Where(x => x.user_active)
            .Join(
                _context.Sites.AsNoTracking(),
                u => u.Site_code,
                s => s.Site_code,
                (u, s) => new TroubleshootSiteUserDto
                {
                    UserAccessCode = u.user_access_code,
                    Name = u.name ?? string.Empty,
                    SiteDescription = s.description,
                    SiteCode = s.Site_code
                })
            .OrderBy(x => x.Name)
            .Take(1000)
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost("log/search")]
    public async Task<ActionResult<IEnumerable<TroubleshootLogEntryDto>>> SearchLogs([FromBody] TroubleshootLogSearchRequest request)
    {
        var rows = await QueryLogs(request.UserAccessCode, null, null, null).ToListAsync();
        return Ok(rows);
    }

    [HttpPost("log/update")]
    public async Task<ActionResult> UpdateLogs()
    {
        var now = DateTime.UtcNow;
        var rows = await _context.TSLogs.Where(x => !x.is_deleted && x.date_updated == null).ToListAsync();
        foreach (var row in rows)
        {
            row.date_updated = now;
            row.modified_by_user_code = GetCurrentUserId();
        }

        await _context.SaveChangesAsync();
        return Ok(new { updated = rows.Count });
    }

    [HttpPost("reports/general")]
    public async Task<ActionResult<IEnumerable<TroubleshootLogEntryDto>>> GetGeneralReports([FromBody] TroubleshootReportFilter filter)
    {
        var rows = await QueryLogs(filter.UserAccessCode, filter.ProblemKeyword, filter.FromDate, filter.ToDate).ToListAsync();
        return Ok(rows);
    }

    [HttpPost("odometer/search")]
    public async Task<ActionResult<IEnumerable<OdometerCorrectionResultDto>>> SearchOdometerCorrections([FromBody] OdometerCorrectionSearchRequest request)
    {
        var mode = (request.SearchMode ?? string.Empty).Trim().ToUpperInvariant();
        var searchValue = (request.SearchValue ?? string.Empty).Trim();

        if (mode == "TA")
        {
            IQueryable<Trip> tripQuery = _context.Trips
                .AsNoTracking()
                .Where(t => !t.is_deleted);

            if (int.TryParse(searchValue, out var taCode))
            {
                tripQuery = tripQuery.Where(t => t.trip_authority_code == taCode);
            }
            else
            {
                tripQuery = tripQuery.Where(t => t.trip_authority_code.ToString().Contains(searchValue));
            }

            var taRows = await tripQuery
                .Join(_context.Contracts.AsNoTracking().Where(c => !c.is_deleted),
                    t => t.contract_code,
                    c => c.contract_code,
                    (t, c) => new { t, c })
                .Join(_context.Vehicles.AsNoTracking().Where(v => !v.is_deleted),
                    tc => tc.c.vmf_code,
                    v => v.vmf_code,
                    (tc, v) => new OdometerCorrectionResultDto
                    {
                        VehicleIdentifier = string.IsNullOrWhiteSpace(v.fleet_number) ? v.registration_number : v.fleet_number,
                        TripAuthorityNumber = tc.t.trip_authority_code.ToString(),
                        CurrentOdometer = v.current_odo,
                        LastOdometer = tc.t.end_odo_meter ?? tc.c.end_odometer
                    })
                .OrderByDescending(x => x.TripAuthorityNumber)
                .Take(200)
                .ToListAsync();

            return Ok(taRows);
        }

        IQueryable<Vehicle> vehicleQuery = _context.Vehicles.AsNoTracking().Where(v => !v.is_deleted);
        if (!string.IsNullOrWhiteSpace(searchValue))
        {
            vehicleQuery = mode switch
            {
                "VMF" => vehicleQuery.Where(v => v.vmf_code.ToString() == searchValue),
                "REG" => vehicleQuery.Where(v => v.registration_number != null && v.registration_number.Contains(searchValue)),
                _ => vehicleQuery.Where(v => v.fleet_number != null && v.fleet_number.Contains(searchValue))
            };
        }

        var rows = await vehicleQuery
            .OrderBy(v => v.fleet_number)
            .Take(200)
            .Select(v => new OdometerCorrectionResultDto
            {
                VehicleIdentifier = string.IsNullOrWhiteSpace(v.fleet_number) ? v.registration_number : v.fleet_number,
                TripAuthorityNumber = null,
                CurrentOdometer = v.current_odo,
                LastOdometer = v.highest_km.HasValue ? (int?)Math.Round(v.highest_km.Value) : null
            })
            .ToListAsync();

        return Ok(rows);
    }

    [HttpGet("trips-without-routes")]
    public async Task<ActionResult<IEnumerable<TripsWithoutRoutesDto>>> GetTripsWithoutRoutes()
    {
        try
        {
            var rows = await WithConnectionAsync(async connection =>
            {
                var schema = await ReadTableSchemaAsync(connection, TripsWithoutRoutesBackupTable);
                if (schema.Count > 0)
                {
                    return await ReadBackupTripsWithoutRoutesAsync(connection, schema);
                }

                return await ReadStoredProcedureTripsWithoutRoutesAsync(connection);
            });

            return Ok(rows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving trips without routes");
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

                return await ExecuteStoredProcedureDeleteAsync(connection, "ADM_DEL_TripsWithoutRoutes");
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
    public async Task<ActionResult<IEnumerable<ApproverRankDto>>> SaveApproverRanks([FromBody] List<ApproverRankDto> ranks)
    {
        var userId = GetCurrentUserId();
        var now = DateTime.UtcNow;

        foreach (var dto in ranks)
        {
            if (dto.Id <= 0)
            {
                var created = new Rank
                {
                    description = dto.RankName ?? dto.Description,
                    date_created = now,
                    created_by_user_code = userId,
                    is_deleted = false
                };
                _context.Ranks.Add(created);
                continue;
            }

            var existing = await _context.Ranks.FirstOrDefaultAsync(x => x.rank_code == dto.Id);
            if (existing == null)
            {
                continue;
            }

            existing.description = dto.RankName ?? dto.Description;
            existing.date_updated = now;
            existing.modified_by_user_code = userId;
            existing.is_deleted = false;
        }

        await _context.SaveChangesAsync();
        return Ok(await GetApproverRanksData());
    }

    [HttpPost("vehicle-master-edit")]
    public async Task<ActionResult<IEnumerable<VehicleLookupDto>>> GetVehicleMasterEdit([FromBody] VehicleMasterEditRequest request)
    {
        var id = (request.VehicleIdentifier ?? string.Empty).Trim();
        var rows = await _context.Vehicles
            .AsNoTracking()
            .Where(v => !v.is_deleted && (
                v.vmf_code.ToString() == id ||
                (v.fleet_number != null && v.fleet_number.Contains(id)) ||
                (v.registration_number != null && v.registration_number.Contains(id))))
            .OrderBy(v => v.fleet_number)
            .Take(50)
            .Select(v => new VehicleLookupDto
            {
                VmfCode = v.vmf_code,
                FleetNumber = v.fleet_number,
                RegistrationNumber = v.registration_number,
                CurrentOdometer = v.current_odo,
                RecoveredGg = v.derived_odo
            })
            .ToListAsync();

        return Ok(rows);
    }

    [HttpPost("update-recovered-gg")]
    public async Task<ActionResult> UpdateRecoveredGg([FromBody] UpdateRecoveredGgRequest request)
    {
        var id = (request.VehicleIdentifier ?? string.Empty).Trim();
        var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v =>
            !v.is_deleted && (
                v.vmf_code.ToString() == id ||
                v.fleet_number == id ||
                v.registration_number == id));

        if (vehicle == null)
        {
            return NotFound(new { message = "Vehicle not found" });
        }

        vehicle.derived_odo = request.Notes;
        vehicle.date_updated = DateTime.UtcNow;
        vehicle.modified_by_user_code = GetCurrentUserId();

        await _context.SaveChangesAsync();
        return Ok(new { vmfCode = vehicle.vmf_code, updated = true });
    }

    private IQueryable<TroubleshootLogEntryDto> QueryLogs(int? userAccessCode, string? keyword, DateTime? fromDate, DateTime? toDate)
    {
        var query = _context.TSLogs
            .AsNoTracking()
            .Where(x => !x.is_deleted)
            .AsQueryable();

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
            query = query.Where(x => x.TSDate.HasValue && x.TSDate.Value.Date >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            query = query.Where(x => x.TSDate.HasValue && x.TSDate.Value.Date <= toDate.Value.Date);
        }

        return query
            .OrderByDescending(x => x.TSDate)
            .ThenByDescending(x => x.TSTime)
            .Take(1000)
            .Select(x => new TroubleshootLogEntryDto
            {
                Id = x.ErrorID,
                VehicleIdentifier = x.ErrorCode,
                ProblemDescription = x.ErrorCode,
                Status = x.is_deleted ? "Deleted" : "Active",
                LoggedDate = x.TSDate,
                LoggedBy = x.user_access_code == null ? null : x.user_access_code.ToString()
            });
    }

    private async Task<List<ApproverRankDto>> GetApproverRanksData()
    {
        return await _context.Ranks
            .AsNoTracking()
            .Where(x => !x.is_deleted)
            .OrderBy(x => x.description)
            .Select(x => new ApproverRankDto
            {
                Id = x.rank_code,
                RankName = x.description,
                Description = x.description
            })
            .ToListAsync();
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

    private static async Task<HashSet<string>> ReadTableSchemaAsync(DbConnection connection, string tableName)
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

        return columns;
    }

    private static async Task<List<TripsWithoutRoutesDto>> ReadBackupTripsWithoutRoutesAsync(DbConnection connection, HashSet<string> schema)
    {
        if (!schema.Contains("trip_authority_code") || !schema.Contains("contract_code") || !schema.Contains("issue_date"))
        {
            return [];
        }

        var selections = new List<(string Column, string Alias)>
        {
            ("trip_authority_code", "TripAuthorityCode"),
            ("contract_code", "ContractCode"),
            ("issue_date", "IssueDate")
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
        command.CommandText = $"SELECT {string.Join(", ", selections.Select(item => $"[{item.Column}] AS [{item.Alias}]"))} FROM [dbo].[{TripsWithoutRoutesBackupTable}] WHERE {string.Join(" AND ", predicates.Count == 0 ? ["1 = 1"] : predicates)} ORDER BY [issue_date], [trip_authority_code]";
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<TripsWithoutRoutesDto>();
        while (await reader.ReadAsync())
        {
            rows.Add(ReadTripsWithoutRoutesRow(reader));
        }

        return rows;
    }

    private static async Task<List<TripsWithoutRoutesDto>> ReadStoredProcedureTripsWithoutRoutesAsync(DbConnection connection)
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

    private async Task<int> DeleteBackupTripsWithoutRoutesAsync(DbConnection connection, HashSet<string> schema, RemoveTripsRequest request)
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

            command.CommandText = $"UPDATE [dbo].[{TripsWithoutRoutesBackupTable}] SET {string.Join(", ", assignments)} WHERE {string.Join(" AND ", predicates.Count == 0 ? ["1 = 1"] : predicates)}";
        }
        else
        {
            command.CommandText = $"DELETE FROM [dbo].[{TripsWithoutRoutesBackupTable}] WHERE {string.Join(" AND ", predicates.Count == 0 ? ["1 = 1"] : predicates)}";
        }

        foreach (var (name, value) in values)
        {
            AddParameter(command, name, value);
        }

        var affected = await command.ExecuteNonQueryAsync();
        return affected < 0 ? 0 : affected;
    }

    private static async Task<int> ExecuteStoredProcedureDeleteAsync(DbConnection connection, string procedureName)
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

    private static async Task<bool> StoredProcedureExistsAsync(DbConnection connection, string procedureName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.objects WHERE schema_id = SCHEMA_ID(@schema) AND name = @name AND type IN ('P', 'PC')) THEN 1 ELSE 0 END";
        AddParameter(command, "@schema", "dbo");
        AddParameter(command, "@name", procedureName);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    private static void AddSelection(HashSet<string> schema, ICollection<(string Column, string Alias)> selections, string column, string alias)
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
            ApproverName = ReadString(reader, "ApproverName", "approver_name")
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
    }

    public class OdometerCorrectionSearchRequest
    {
        public string SearchMode { get; set; } = "GG";
        public string SearchValue { get; set; } = string.Empty;
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
    }

    public class VehicleLookupDto
    {
        public int VmfCode { get; set; }
        public string? FleetNumber { get; set; }
        public string? RegistrationNumber { get; set; }
        public int? CurrentOdometer { get; set; }
        public string? RecoveredGg { get; set; }
    }

    public class UpdateRecoveredGgRequest
    {
        public string? VehicleIdentifier { get; set; }
        public string? Notes { get; set; }
    }
}
