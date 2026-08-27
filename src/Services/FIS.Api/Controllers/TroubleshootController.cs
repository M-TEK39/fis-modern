using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.ReferenceData;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/troubleshoot")]
[Authorize]
public class TroubleshootController : BaseApiController
{
    private readonly FisDbContext _context;

    public TroubleshootController(FisDbContext context)
    {
        _context = context;
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
            .Where(x => !x.is_deleted)
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
            .Where(x => !x.is_deleted)
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

    [HttpPost("remove-trips-no-routes")]
    public async Task<ActionResult> RemoveTripsWithoutRoutes([FromBody] RemoveTripsRequest request)
    {
        var from = request.FromDate?.Date ?? DateTime.MinValue.Date;
        var to = request.ToDate?.Date ?? DateTime.MaxValue.Date;

        var rows = await _context.TripWithoutRouteBackups
            .Where(x => !x.is_deleted && x.issue_date.Date >= from && x.issue_date.Date <= to)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var userId = GetCurrentUserId();
        foreach (var row in rows)
        {
            row.is_deleted = true;
            row.date_updated = now;
            row.modified_by_user_code = userId;
        }

        await _context.SaveChangesAsync();
        return Ok(new { removed = rows.Count });
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
