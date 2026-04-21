using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Financial;
using Microsoft.EntityFrameworkCore;
using FIS.Data.SqlServer;

namespace FIS.Api.Services;

/// <summary>
/// Monthly Hangfire job that generates billing journal entries for all active contracts.
///
/// Billing rule: bill every contract where still_current = 'Y', regardless of end_date.
/// End_date passing does NOT stop billing. Only a contract close/reassign/extend
/// (which sets still_current = 'N' or creates a new contract) stops billing.
///
/// Per-run logic per contract:
///   1. Determine billing period: Charged_Until (or start_date) → today
///   2. Skip if already billed up to today (Charged_Until >= today)
///   3. Calculate days × tariff = amount
///   4. Insert JournalDetail record
///   5. Update contract.Charged_Until = today
///
/// Runs on the 1st of each month at 06:00.
/// </summary>
public class MonthlyBillingJob
{
    private readonly FisDbContext _context;
    private readonly IJournalDetailService _journalDetailService;
    private readonly ILogger<MonthlyBillingJob> _logger;

    public MonthlyBillingJob(
        FisDbContext context,
        IJournalDetailService journalDetailService,
        ILogger<MonthlyBillingJob> logger)
    {
        _context = context;
        _journalDetailService = journalDetailService;
        _logger = logger;
    }

    /// <summary>
    /// Entry point called by Hangfire on the 1st of each month at 06:00.
    /// </summary>
    public async Task RunAsync()
    {
        var today = DateTime.Today;
        _logger.LogInformation("MonthlyBillingJob: starting for period ending {Today}", today);

        int billed = 0, skipped = 0, errors = 0;

        // Load all active contracts — still_current = 'Y' is the ONLY stop condition.
        // end_date is intentionally NOT filtered here.
        var contracts = await _context.Contracts
            .Where(c =>
                !c.is_deleted &&
                c.still_current == "Y")
            .ToListAsync();

        _logger.LogInformation("MonthlyBillingJob: found {Count} active contracts to evaluate", contracts.Count);

        foreach (var contract in contracts)
        {
            try
            {
                // Billing starts from where we last left off, or from contract start_date
                var billingFrom = contract.Charged_Until?.Date ?? contract.start_date.Date;

                // Skip if already billed up to today
                if (billingFrom >= today)
                {
                    skipped++;
                    continue;
                }

                var billingTo = today;
                int days = (billingTo - billingFrom).Days;

                if (days <= 0)
                {
                    skipped++;
                    continue;
                }

                // Resolve department from site
                var site = await _context.Sites.FindAsync(contract.site_code);
                var departmentCode = site?.Depatrment_code ?? 150;

                // Calculate tariff-based amount for the billing period
                decimal amount;
                try
                {
                    amount = await _journalDetailService.CalculateJournalAmountAsync(
                        billingFrom,
                        billingTo,
                        contract.start_odometer,
                        contract.end_odometer ?? 0,
                        contract.vmf_code,
                        contract.site_code,
                        departmentCode,
                        contract.contract_type ?? "H",
                        today);

                    // Negative values indicate tariff lookup failure — fall back to 0 and log
                    if (amount < 0)
                    {
                        _logger.LogWarning(
                            "MonthlyBillingJob: tariff lookup failed (code {Code}) for contract {ContractCode}, vehicle {VmfCode}. Billing at R0.",
                            amount, contract.contract_code, contract.vmf_code);
                        amount = 0m;
                    }
                }
                catch (Exception tariffEx)
                {
                    _logger.LogError(tariffEx,
                        "MonthlyBillingJob: tariff calculation error for contract {ContractCode}. Billing at R0.",
                        contract.contract_code);
                    amount = 0m;
                }

                // Create journal detail
                var journalDetail = new JournalDetail
                {
                    vmf_code            = contract.vmf_code,
                    site_code           = contract.site_code,
                    department_code     = departmentCode,
                    journal_detail_quantity = days,
                    journal_detail_amount   = amount,
                    journal_detail_isdebit  = true,
                    journal_detail_type_code = 1, // Fixed
                    journal_detail_description =
                        $"Monthly billing: Contract {contract.contract_code} | {billingFrom:yyyy-MM-dd} – {billingTo:yyyy-MM-dd} | {days} days"
                };

                await _journalDetailService.CreateJournalDetailAsync(journalDetail);

                // Advance Charged_Until so the next run picks up from today
                contract.Charged_Until = today;
                _context.Contracts.Update(contract);
                await _context.SaveChangesAsync();

                billed++;

                _logger.LogInformation(
                    "MonthlyBillingJob: billed Contract {ContractCode} (vehicle {VmfCode}) {Days} days @ R{Amount}",
                    contract.contract_code, contract.vmf_code, days, amount);
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogError(ex,
                    "MonthlyBillingJob: unhandled error processing contract {ContractCode}",
                    contract.contract_code);
            }
        }

        _logger.LogInformation(
            "MonthlyBillingJob: complete — {Billed} billed, {Skipped} already current, {Errors} errors",
            billed, skipped, errors);
    }
}
