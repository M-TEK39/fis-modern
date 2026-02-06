using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
namespace FIS.Core.Infrastructure.Repositories;
public class AccidentRepository : IAccidentRepository
{
    private readonly FisDbContext _context;
    public AccidentRepository(FisDbContext context) { _context = context; }
    public async Task<Accident?> GetByIdAsync(int accidentCode) { return await _context.Set<Accident>().Include(a => a.Vehicle).FirstOrDefaultAsync(a => a.accident_code == accidentCode); }
    public async Task<IEnumerable<Accident>> GetAllAsync() { return await _context.Set<Accident>().Include(a => a.Vehicle).ToListAsync(); }
    public async Task<IEnumerable<Accident>> GetByVehicleAsync(int vmfCode) { return await _context.Set<Accident>().Where(a => a.vmf_code == vmfCode).Include(a => a.Vehicle).ToListAsync(); }
    public async Task<IEnumerable<Accident>> GetByDateRangeAsync(DateTime startDate, DateTime endDate) { return await _context.Set<Accident>().Where(a => a.occurence_date >= startDate && a.occurence_date <= endDate).Include(a => a.Vehicle).ToListAsync(); }

    public async Task<AccidentClaimsSummary> GetClaimsSummaryAsync()
    {
        var accidents = await _context.Set<Accident>().ToListAsync();
        var totalClaims = accidents.Sum(a => a.claim_amount ?? 0);
        var openClaims = accidents.Count(a => a.claim_amount > 0);
        var closedClaims = accidents.Count(a => a.claim_amount == 0 || !a.claim_amount.HasValue);
        var pendingAmount = accidents.Where(a => a.claim_amount > 0).Sum(a => a.claim_amount ?? 0);

        return new AccidentClaimsSummary
        {
            total_accidents = accidents.Count,
            total_claims_value = totalClaims,
            open_claims = openClaims,
            closed_claims = closedClaims,
            pending_claim_amount = pendingAmount
        };
    }

    public async Task<IEnumerable<AccidentReport>> GetRecentReportsAsync(int limit = 10)
    {
        return await _context.Set<Accident>()
            .Include(a => a.Vehicle)
            .OrderByDescending(a => a.occurence_date)
            .Take(limit)
            .Select(a => new AccidentReport
            {
                accident_code = a.accident_code,
                accident_reference = a.gg_reference ?? a.hq_reference ?? "",
                accident_date = a.occurence_date ?? DateTime.MinValue,
                vehicle_registration = a.Vehicle != null ? a.Vehicle.fleet_number ?? "" : "",
                severity = "Unknown",
                status = (a.claim_amount ?? 0) > 0 ? "Open" : "Closed"
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<AccidentOutstandingClaim>> GetOutstandingClaimsAsync()
    {
        var today = DateTime.UtcNow;
        return await _context.Set<Accident>()
            .Include(a => a.Vehicle)
            .Where(a => (a.claim_amount ?? 0) > 0)
            .Select(a => new AccidentOutstandingClaim
            {
                accident_id = a.accident_code,
                accident_reference = a.gg_reference ?? a.hq_reference ?? "",
                vehicle_registration = a.Vehicle != null ? a.Vehicle.fleet_number ?? "" : "",
                accident_date = a.occurence_date ?? DateTime.MinValue,
                claim_type = "Claim",
                claim_amount = a.claim_amount,
                status = "Outstanding",
                days_outstanding = (int)(today - (a.occurence_date ?? DateTime.MinValue)).TotalDays
            })
            .ToListAsync();
    }

    public async Task<AccidentStatistics> GetStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.Set<Accident>().Include(a => a.Vehicle).AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(a => a.occurence_date >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(a => a.occurence_date <= toDate.Value);

        var accidents = await query.ToListAsync();
        var totalClaims = accidents.Sum(a => a.claim_amount ?? 0);

        var thisMonth = DateTime.UtcNow.Month;
        var thisYear = DateTime.UtcNow.Year;
        var accidentsThisMonth = accidents.Count(a => a.occurence_date?.Month == thisMonth && a.occurence_date?.Year == thisYear);
        var accidentsThisYear = accidents.Count(a => a.occurence_date?.Year == thisYear);

        var avgCost = accidents.Count > 0 ? totalClaims / accidents.Count : 0;

        return new AccidentStatistics
        {
            total_accidents = accidents.Count,
            total_repair_costs = 0,
            total_third_party_claims = totalClaims,
            total_department_claims = 0,
            accidents_this_month = accidentsThisMonth,
            accidents_this_year = accidentsThisYear,
            average_cost_per_accident = avgCost,
            most_common_severity = "Unknown",
            department_breakdown = new List<DepartmentAccidentSummary>(),
            monthly_breakdown = new List<MonthlySummary>()
        };
    }

    public async Task<Accident> CreateAsync(Accident accident, int currentUserId) { _context.Set<Accident>().Add(accident); await _context.SaveChangesAsync(); return accident; }
    public async Task<Accident> UpdateAsync(Accident accident, int currentUserId) { if (accident == null) throw new ArgumentNullException(nameof(accident)); var existing = await _context.Set<Accident>().FindAsync(accident.accident_code); if (existing == null) throw new InvalidOperationException($"Accident with accident_code {accident.accident_code} not found"); _context.Entry(existing).CurrentValues.SetValues(accident); await _context.SaveChangesAsync(); return existing; }
    public async Task DeleteAsync(int accidentCode, int currentUserId) { var accident = await GetByIdAsync(accidentCode); if (accident != null) { _context.Set<Accident>().Remove(accident); await _context.SaveChangesAsync(); } }
}
