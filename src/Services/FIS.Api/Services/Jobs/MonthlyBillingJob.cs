using System.Data;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Financial;
using FIS.Data.SqlServer;
using Hangfire;
using Microsoft.EntityFrameworkCore;

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
    private readonly IContractRepository _contractRepository;
    private readonly IJournalDetailService _journalDetailService;
    private readonly ILogger<MonthlyBillingJob> _logger;

    public MonthlyBillingJob(
        FisDbContext context,
        IContractRepository contractRepository,
        IJournalDetailService journalDetailService,
        ILogger<MonthlyBillingJob> logger
    )
    {
        _context = context;
        _contractRepository = contractRepository;
        _journalDetailService = journalDetailService;
        _logger = logger;
    }

    /// <summary>
    /// Entry point called by Hangfire on the 1st of each month at 06:00.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 3600)]
    public async Task RunAsync()
    {
        var today = DateTime.Today;
        _logger.LogInformation("MonthlyBillingJob: starting for period ending {Today}", today);

        int billed = 0,
            skipped = 0,
            errors = 0;

        // Load all active contracts — still_current = 'Y' is the ONLY stop condition.
        // end_date is intentionally NOT filtered here.
        var contracts = (await _contractRepository.GetActiveContractsAsync()).ToList();

        _logger.LogInformation(
            "MonthlyBillingJob: found {Count} active contracts to evaluate",
            contracts.Count
        );

        foreach (var listedContract in contracts)
        {
            try
            {
                // Billing starts from where we last left off, or from contract start_date
                var billingFrom =
                    listedContract.Charged_Until?.Date ?? listedContract.start_date.Date;

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

                // Resolve department from the compatibility repository projection.
                var departmentCode = listedContract.Site?.Depatrment_code ?? 150;

                // Calculate tariff-based amount for the billing period
                decimal amount;
                try
                {
                    amount = await _journalDetailService.CalculateJournalAmountAsync(
                        billingFrom,
                        billingTo,
                        listedContract.start_odometer,
                        listedContract.end_odometer ?? 0,
                        listedContract.vmf_code,
                        listedContract.site_code,
                        departmentCode,
                        listedContract.contract_type ?? "H",
                        today
                    );

                    // Negative values indicate tariff lookup failure — fall back to 0 and log
                    if (amount < 0)
                    {
                        _logger.LogWarning(
                            "MonthlyBillingJob: tariff lookup failed (code {Code}) for contract {ContractCode}, vehicle {VmfCode}. Billing at R0.",
                            amount,
                            listedContract.contract_code,
                            listedContract.vmf_code
                        );
                        amount = 0m;
                    }
                }
                catch (Exception tariffEx)
                {
                    _logger.LogError(
                        tariffEx,
                        "MonthlyBillingJob: tariff calculation error for contract {ContractCode}. Billing at R0.",
                        listedContract.contract_code
                    );
                    amount = 0m;
                }

                // Keep the short serializable section to the journal insert and
                // Charged_Until update. Tariff calculation can perform several
                // reads, so holding a contract lock while it runs would cause
                // unnecessary production blocking.
                await using var transaction = await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable
                );
                var contract = await _contractRepository.GetByIdAsync(listedContract.contract_code);
                var currentBillingFrom = contract?.Charged_Until?.Date ?? contract?.start_date.Date;
                var currentDepartmentCode = contract?.Site?.Depatrment_code ?? 150;
                if (
                    contract is null
                    || contract.is_deleted
                    || contract.still_current != "Y"
                    || currentBillingFrom != billingFrom
                    || currentDepartmentCode != departmentCode
                    || !MatchesBillingInputs(contract, listedContract)
                )
                {
                    skipped++;
                    await transaction.CommitAsync();
                    continue;
                }

                // The locked contract still matches the data used for tariff
                // calculation, so this journal and watermark advance are atomic.
                var journalDetail = new JournalDetail
                {
                    vmf_code = contract.vmf_code,
                    site_code = contract.site_code,
                    department_code = departmentCode,
                    journal_detail_quantity = days,
                    journal_detail_amount = amount,
                    journal_detail_isdebit = true,
                    journal_detail_type_code = 1, // Fixed
                    journal_detail_description =
                        $"Monthly billing: Contract {contract.contract_code} | {billingFrom:yyyy-MM-dd} – {billingTo:yyyy-MM-dd} | {days} days",
                };

                await _journalDetailService.CreateJournalDetailAsync(journalDetail);

                // Advance Charged_Until so the next run picks up from today
                contract.Charged_Until = today;
                await _contractRepository.UpdateAsync(contract, 0);
                await transaction.CommitAsync();

                billed++;

                _logger.LogInformation(
                    "MonthlyBillingJob: billed Contract {ContractCode} (vehicle {VmfCode}) {Days} days @ R{Amount}",
                    contract.contract_code,
                    contract.vmf_code,
                    days,
                    amount
                );
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogError(
                    ex,
                    "MonthlyBillingJob: unhandled error processing contract {ContractCode}",
                    listedContract.contract_code
                );
            }
        }

        _logger.LogInformation(
            "MonthlyBillingJob: complete — {Billed} billed, {Skipped} already current, {Errors} errors",
            billed,
            skipped,
            errors
        );
    }

    private static bool MatchesBillingInputs(Contract current, Contract listed) =>
        current.vmf_code == listed.vmf_code
        && current.site_code == listed.site_code
        && current.start_odometer == listed.start_odometer
        && current.end_odometer == listed.end_odometer
        && string.Equals(current.contract_type, listed.contract_type, StringComparison.Ordinal);
}
