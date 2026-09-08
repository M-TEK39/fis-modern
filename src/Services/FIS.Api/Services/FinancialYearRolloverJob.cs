using FIS.Core.Domain.Entities.Financial;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Services;

/// <summary>
/// Hangfire job that automatically creates the next financial year record
/// on 1 April each year (the start of the new South African fiscal year).
///
/// Financial year convention:
///   The FY is named after the calendar year it ENDS in.
///   e.g. 1 April 2025 – 31 March 2026 = FY2026 (financial_year_code = 2026).
///
/// Runs at 00:05 on 1 April every year (cron: "5 0 1 4 *").
/// Idempotent: if the record already exists the job logs and exits cleanly.
/// </summary>
public class FinancialYearRolloverJob
{
    private readonly FisDbContext _context;
    private readonly ILogger<FinancialYearRolloverJob> _logger;

    public FinancialYearRolloverJob(FisDbContext context, ILogger<FinancialYearRolloverJob> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        var today = DateTime.Today;

        // The new FY that just started today (1 April YYYY) ends on 31 March YYYY+1.
        // Its code is YYYY+1 (the year it ends).
        var newFyCode = (short)(today.Year + 1);
        var startDate = new DateTime(today.Year, 4, 1);
        var endDate = new DateTime(today.Year + 1, 3, 31);
        var fyName = $"{today.Year}/{today.Year + 1}";

        _logger.LogInformation(
            "FinancialYearRolloverJob: checking for FY{Code} ({Name})",
            newFyCode,
            fyName
        );

        // Idempotency: do nothing if the record already exists
        var exists = await _context.FinancialYears.AnyAsync(y =>
            y.financial_year_code == newFyCode && !y.is_deleted
        );

        if (exists)
        {
            _logger.LogInformation(
                "FinancialYearRolloverJob: FY{Code} already exists — no action needed.",
                newFyCode
            );
            return;
        }

        var newYear = new FinancialYear
        {
            financial_year_code = newFyCode,
            financial_year_name = fyName,
            start_date = startDate,
            end_date = endDate,
            date_created = DateTime.UtcNow,
            is_deleted = false,
        };

        _context.FinancialYears.Add(newYear);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "FinancialYearRolloverJob: FY{Code} ({Name}) created — {Start:yyyy-MM-dd} to {End:yyyy-MM-dd}",
            newFyCode,
            fyName,
            startDate,
            endDate
        );
    }
}
