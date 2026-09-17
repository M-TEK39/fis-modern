using FIS.Core.Application.Interfaces;
using Hangfire;

namespace FIS.Api.Services;

/// <summary>
/// Daily Hangfire job that scans active contracts and sends expiry reminder emails
/// at milestone intervals: 90, 60, 30, 14, and 7 days before target_return_date.
///
/// Recipients per reminder:
///   - Site contact (net_address / res_person) — the "client" / hiring department
///   - Original capturer (internal fleet management awareness)
///
/// The message instructs clients to submit a formal letter of extension
/// if they intend to keep the vehicle beyond the contract end date.
/// </summary>
public class ContractExpiryReminderJob
{
    // Days before expiry at which reminders are sent (mirrors legacy behaviour: 3 months → 7 days)
    private static readonly int[] ReminderMilestones = { 90, 60, 30, 14, 7 };

    private readonly IContractRepository _contractRepository;
    private readonly IEmailNotificationService _emailNotification;
    private readonly ILogger<ContractExpiryReminderJob> _logger;

    public ContractExpiryReminderJob(
        IContractRepository contractRepository,
        IEmailNotificationService emailNotification,
        ILogger<ContractExpiryReminderJob> logger
    )
    {
        _contractRepository = contractRepository;
        _emailNotification = emailNotification;
        _logger = logger;
    }

    /// <summary>
    /// Entry point called by Hangfire daily at 7:00 AM.
    /// Finds all active contracts whose target_return_date falls on a reminder milestone
    /// and dispatches the expiry reminder email.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 3600)]
    public async Task RunAsync()
    {
        _logger.LogInformation("ContractExpiryReminderJob: starting daily check");

        var today = DateTime.Today;
        int sent = 0,
            skipped = 0,
            errors = 0;

        // Load active contracts that have a target return date
        // ContractRepository negotiates the original legacy projection and
        // optional audit columns at runtime. Do not use the EF Contracts
        // DbSet here: the restored client table predates those optional
        // columns, and a static projection would stop every reminder run.
        var contracts = (await _contractRepository.GetActiveContractsAsync())
            .Where(contract => contract.target_return_date.HasValue)
            .Select(contract => new { contract.contract_code, contract.target_return_date })
            .ToList();

        foreach (var c in contracts)
        {
            // Days remaining (positive = future, negative = overdue)
            var daysRemaining = (c.target_return_date!.Value.Date - today).Days;

            if (!ReminderMilestones.Contains(daysRemaining))
            {
                skipped++;
                continue;
            }

            try
            {
                var ok = await _emailNotification.SendContractExpiryReminderAsync(
                    c.contract_code,
                    daysRemaining
                );
                if (ok)
                    sent++;
                else
                    errors++;
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogError(
                    ex,
                    "ContractExpiryReminderJob: error sending reminder for Contract {ContractId} ({Days} days)",
                    c.contract_code,
                    daysRemaining
                );
            }
        }

        _logger.LogInformation(
            "ContractExpiryReminderJob: complete — {Sent} sent, {Errors} errors, {Skipped} not at milestone",
            sent,
            errors,
            skipped
        );
    }
}
