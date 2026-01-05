using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for JournalDetail entity
/// Provides data access for financial journal transactions
/// </summary>
public class JournalDetailRepository : IJournalDetailRepository
{
    private readonly FisDbContext _context;
    private readonly ILogger<JournalDetailRepository> _logger;

    public JournalDetailRepository(FisDbContext context, ILogger<JournalDetailRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<JournalDetail?> GetByIdAsync(int journalDetailId)
    {
        try
        {
            return await _context.Set<JournalDetail>()
                .Include(j => j.Vehicle)
                .Include(j => j.Site)
                .Include(j => j.Department)
                .FirstOrDefaultAsync(j => j.journal_detail_id == journalDetailId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting journal detail by ID: {JournalDetailId}", journalDetailId);
            throw;
        }
    }

    public async Task<JournalDetail?> GetByCodeAsync(Guid journalDetailCode)
    {
        try
        {
            return await _context.Set<JournalDetail>()
                .Include(j => j.Vehicle)
                .Include(j => j.Site)
                .Include(j => j.Department)
                .FirstOrDefaultAsync(j => j.journal_detail_code == journalDetailCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting journal detail by code: {JournalDetailCode}", journalDetailCode);
            throw;
        }
    }

    public async Task<IEnumerable<JournalDetail>> GetByVehicleAsync(int vmfCode)
    {
        try
        {
            return await _context.Set<JournalDetail>()
                .Where(j => j.vmf_code == vmfCode)
                .Include(j => j.Vehicle)
                .Include(j => j.Site)
                .Include(j => j.Department)
                .OrderByDescending(j => j.journal_detail_date)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting journal details for vehicle: {VmfCode}", vmfCode);
            throw;
        }
    }

    public async Task<IEnumerable<JournalDetail>> GetBySiteAsync(short siteCode)
    {
        try
        {
            return await _context.Set<JournalDetail>()
                .Where(j => j.site_code == siteCode)
                .Include(j => j.Vehicle)
                .Include(j => j.Site)
                .Include(j => j.Department)
                .OrderByDescending(j => j.journal_detail_date)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting journal details for site: {SiteCode}", siteCode);
            throw;
        }
    }

    public async Task<IEnumerable<JournalDetail>> GetByDepartmentAsync(int departmentCode)
    {
        try
        {
            return await _context.Set<JournalDetail>()
                .Where(j => j.department_code == departmentCode)
                .Include(j => j.Vehicle)
                .Include(j => j.Site)
                .Include(j => j.Department)
                .OrderByDescending(j => j.journal_detail_date)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting journal details for department: {DepartmentCode}", departmentCode);
            throw;
        }
    }

    public async Task<IEnumerable<JournalDetail>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            return await _context.Set<JournalDetail>()
                .Where(j => j.journal_detail_date >= startDate && j.journal_detail_date <= endDate)
                .Include(j => j.Vehicle)
                .Include(j => j.Site)
                .Include(j => j.Department)
                .OrderByDescending(j => j.journal_detail_date)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting journal details for date range: {StartDate} to {EndDate}", startDate, endDate);
            throw;
        }
    }

    public async Task<IEnumerable<JournalDetail>> GetByFinancialYearAsync(string financialYear)
    {
        try
        {
            return await _context.Set<JournalDetail>()
                .Where(j => j.journal_detail_financial_year == financialYear)
                .Include(j => j.Vehicle)
                .Include(j => j.Site)
                .Include(j => j.Department)
                .OrderByDescending(j => j.journal_detail_date)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting journal details for financial year: {FinancialYear}", financialYear);
            throw;
        }
    }

    public async Task<IEnumerable<JournalDetail>> GetReversalsForJournalAsync(Guid journalDetailCode)
    {
        try
        {
            return await _context.Set<JournalDetail>()
                .Where(j => j.journal_detail_reversalof == journalDetailCode)
                .Include(j => j.Vehicle)
                .Include(j => j.Site)
                .Include(j => j.Department)
                .OrderByDescending(j => j.journal_detail_date)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reversals for journal: {JournalDetailCode}", journalDetailCode);
            throw;
        }
    }

    public async Task<JournalDetail> CreateAsync(JournalDetail journalDetail)
    {
        try
        {
            // Generate new GUID if not set
            if (journalDetail.journal_detail_code == Guid.Empty)
            {
                journalDetail.journal_detail_code = Guid.NewGuid();
            }

            // Set created date
            journalDetail.journal_detail_date_created = DateTime.Now;
            journalDetail.journal_detail_date = DateTime.Now;

            _context.Set<JournalDetail>().Add(journalDetail);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created journal detail: {JournalDetailCode}", journalDetail.journal_detail_code);

            return journalDetail;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating journal detail");
            throw;
        }
    }

    public async Task UpdateAsync(JournalDetail journalDetail)
    {
        try
        {
            journalDetail.journal_detail_date_updated = DateTime.Now;
            _context.Set<JournalDetail>().Update(journalDetail);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated journal detail: {JournalDetailCode}", journalDetail.journal_detail_code);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating journal detail: {JournalDetailId}", journalDetail.journal_detail_id);
            throw;
        }
    }

    public async Task DeleteAsync(int journalDetailId)
    {
        try
        {
            var journalDetail = await GetByIdAsync(journalDetailId);
            if (journalDetail != null)
            {
                _context.Set<JournalDetail>().Remove(journalDetail);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted journal detail: {JournalDetailId}", journalDetailId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting journal detail: {JournalDetailId}", journalDetailId);
            throw;
        }
    }
}
