using FIS.Core.Application.Interfaces;
using FIS.Core.Application.Services.Billing;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Financial;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Application.Services;

/// <summary>
/// Journal Detail Service implementation
/// Maps legacy GGFleet.Provider.JournalDetailProvider and GGFleet.BLL.JournalDetail
/// Handles financial journal operations: creation, updates, reversals, and calculations
/// </summary>
public class JournalDetailService : IJournalDetailService
{
    private readonly IJournalDetailRepository _journalDetailRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ISiteRepository _siteRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ITariffCalculationService _tariffCalculationService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<JournalDetailService> _logger;

    public JournalDetailService(
        IJournalDetailRepository journalDetailRepository,
        IVehicleRepository vehicleRepository,
        ISiteRepository siteRepository,
        IDepartmentRepository departmentRepository,
        ITariffCalculationService tariffCalculationService,
        ICurrentUserContext currentUserContext,
        ILogger<JournalDetailService> logger
    )
    {
        _journalDetailRepository =
            journalDetailRepository
            ?? throw new ArgumentNullException(nameof(journalDetailRepository));
        _vehicleRepository =
            vehicleRepository ?? throw new ArgumentNullException(nameof(vehicleRepository));
        _siteRepository = siteRepository ?? throw new ArgumentNullException(nameof(siteRepository));
        _departmentRepository =
            departmentRepository ?? throw new ArgumentNullException(nameof(departmentRepository));
        _tariffCalculationService =
            tariffCalculationService
            ?? throw new ArgumentNullException(nameof(tariffCalculationService));
        _currentUserContext =
            currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Create new journal detail entry
    /// Legacy: AddJournalDetail method (stored procedure: NEW_DEV_INS_JournalDetail)
    /// </summary>
    public async Task<JournalDetail> CreateJournalDetailAsync(JournalDetail journalDetail)
    {
        try
        {
            _logger.LogInformation(
                "Creating journal detail for vehicle: {VmfCode}, Site: {SiteCode}",
                journalDetail.vmf_code,
                journalDetail.site_code
            );

            // Set defaults
            if (journalDetail.journal_detail_code == Guid.Empty)
            {
                journalDetail.journal_detail_code = Guid.NewGuid();
            }

            journalDetail.journal_detail_date_created = DateTime.Now;
            journalDetail.journal_detail_date = DateTime.Now;
            journalDetail.journal_detail_isaccepted = false;
            journalDetail.journal_detail_isdebit = true; // Default to debit
            journalDetail.journal_detail_type_code = 1; // Default type

            // Calculate financial year
            journalDetail.journal_detail_financial_year = CalculateFinancialYear(
                journalDetail.journal_detail_date
            );

            var created = await _journalDetailRepository.CreateAsync(
                journalDetail,
                _currentUserContext.GetCurrentUserIdOrDefault()
            );

            _logger.LogInformation(
                "Journal detail created: {JournalDetailCode}, Amount: {Amount}",
                created.journal_detail_code,
                created.journal_detail_amount
            );

            return created;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creating journal detail for vehicle: {VmfCode}",
                journalDetail.vmf_code
            );
            throw;
        }
    }

    /// <summary>
    /// Update existing journal detail entry
    /// Legacy: UpdateJournalDetail method (stored procedure: NEW_DEV_UPD_JournalDetail)
    /// </summary>
    public async Task UpdateJournalDetailAsync(JournalDetail journalDetail)
    {
        try
        {
            _logger.LogInformation(
                "Updating journal detail: {JournalDetailCode}",
                journalDetail.journal_detail_code
            );

            journalDetail.journal_detail_date_updated = DateTime.Now;

            // Recalculate financial year if date changed
            journalDetail.journal_detail_financial_year = CalculateFinancialYear(
                journalDetail.journal_detail_date
            );

            await _journalDetailRepository.UpdateAsync(
                journalDetail,
                _currentUserContext.GetCurrentUserIdOrDefault()
            );

            _logger.LogInformation(
                "Journal detail updated: {JournalDetailCode}",
                journalDetail.journal_detail_code
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error updating journal detail: {JournalDetailCode}",
                journalDetail.journal_detail_code
            );
            throw;
        }
    }

    /// <summary>
    /// Get journal detail by ID
    /// </summary>
    public async Task<JournalDetail?> GetJournalDetailByIdAsync(int journalDetailId)
    {
        return await _journalDetailRepository.GetByIdAsync(journalDetailId);
    }

    /// <summary>
    /// Get journal detail by code (GUID)
    /// Legacy: ListJournalDetail method (stored procedure: NEW_DEV_SEL_JournalDetail)
    /// </summary>
    public async Task<JournalDetail?> GetJournalDetailByCodeAsync(Guid journalDetailCode)
    {
        return await _journalDetailRepository.GetByCodeAsync(journalDetailCode);
    }

    /// <summary>
    /// Generate reversal entry for a journal detail
    /// Legacy: GenerateReversals method (stored procedure: NEW_DEV_UPD_JournalDetailReversal)
    /// Creates an offsetting entry to reverse a transaction
    /// </summary>
    public async Task<JournalDetail> GenerateReversalAsync(Guid journalDetailCode)
    {
        try
        {
            _logger.LogInformation(
                "Generating reversal for journal detail: {JournalDetailCode}",
                journalDetailCode
            );

            // Get original journal detail
            var originalJournal = await _journalDetailRepository.GetByCodeAsync(journalDetailCode);
            if (originalJournal == null)
            {
                throw new ArgumentException($"Journal detail {journalDetailCode} not found");
            }

            // Check if already reversed
            var existingReversals = await _journalDetailRepository.GetReversalsForJournalAsync(
                journalDetailCode
            );
            if (existingReversals.Any())
            {
                _logger.LogWarning(
                    "Journal detail {JournalDetailCode} already has reversals",
                    journalDetailCode
                );
                throw new InvalidOperationException(
                    $"Journal detail {journalDetailCode} already has reversal entries"
                );
            }

            // Create reversal entry (opposite debit/credit)
            var reversal = new JournalDetail
            {
                journal_detail_code = Guid.NewGuid(),
                journal_code = originalJournal.journal_code,
                department_code = originalJournal.department_code,
                site_code = originalJournal.site_code,
                vmf_code = originalJournal.vmf_code,
                journal_detail_type_code = originalJournal.journal_detail_type_code,
                journal_detail_isdebit = !originalJournal.journal_detail_isdebit, // Opposite direction
                journal_detail_quantity = originalJournal.journal_detail_quantity,
                journal_detail_tariff = originalJournal.journal_detail_tariff,
                journal_detail_amount = -originalJournal.journal_detail_amount, // Negative amount
                journal_detail_description = $"REVERSAL of {originalJournal.journal_detail_code}",
                journal_detail_reversalof = journalDetailCode, // Link to original
                journal_detail_date_created = DateTime.Now,
                journal_detail_date = DateTime.Now,
                journal_detail_isaccepted = false,
                journal_detail_financial_year = CalculateFinancialYear(DateTime.Now),
                journal_detail_isreversaldenied = false,
            };

            var createdReversal = await _journalDetailRepository.CreateAsync(
                reversal,
                _currentUserContext.GetCurrentUserIdOrDefault()
            );

            _logger.LogInformation(
                "Reversal created: {ReversalCode} for original {OriginalCode}",
                createdReversal.journal_detail_code,
                journalDetailCode
            );

            return createdReversal;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error generating reversal for journal detail: {JournalDetailCode}",
                journalDetailCode
            );
            throw;
        }
    }

    /// <summary>
    /// Calculate journal detail amount for a contract period
    /// Legacy: CalculateJournalDetailAmount method
    /// Formula: Quantity (days) * Tariff = Amount
    /// </summary>
    public async Task<decimal> CalculateJournalAmountAsync(
        DateTime startDate,
        DateTime endDate,
        int startOdometer,
        int endOdometer,
        int vmfCode,
        short siteCode,
        int departmentCode,
        string contractType,
        DateTime checkDate
    )
    {
        try
        {
            _logger.LogInformation(
                "Calculating journal amount for vehicle {VmfCode}, period {StartDate} to {EndDate}",
                vmfCode,
                startDate,
                endDate
            );

            // Get tariff rate
            var tariff = await GetVehicleTariffAsync(
                startDate,
                endDate,
                startOdometer,
                endOdometer,
                vmfCode,
                siteCode,
                departmentCode,
                contractType,
                checkDate,
                "FIXED"
            );

            // Calculate quantity (number of days)
            int quantity = (endDate - startDate).Days;
            if (quantity < 0)
                quantity = 0;

            // Calculate amount
            decimal amount = Math.Round(quantity * tariff, 2);

            _logger.LogInformation(
                "Calculated amount: {Amount} (Quantity: {Quantity} days * Tariff: {Tariff})",
                amount,
                quantity,
                tariff
            );

            return amount;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error calculating journal amount for vehicle: {VmfCode}",
                vmfCode
            );
            throw;
        }
    }

    /// <summary>
    /// Get vehicle tariff (rate) for billing calculation
    /// Legacy: GetVehicleTariff method (stored procedure: NEW_DEV_SEL_VehicleTarrif)
    /// Now uses TariffCalculationService with full legacy business logic.
    /// </summary>
    public async Task<decimal> GetVehicleTariffAsync(
        DateTime startDate,
        DateTime endDate,
        int startOdometer,
        int endOdometer,
        int vmfCode,
        short siteCode,
        int departmentCode,
        string contractType,
        DateTime checkDate,
        string tariffType = "FIXED"
    )
    {
        try
        {
            _logger.LogInformation(
                "Getting tariff for vehicle {VmfCode}, type {TariffType}",
                vmfCode,
                tariffType
            );

            // Use the comprehensive TariffCalculationService
            var tariffTypeEnum =
                tariffType.ToUpper() == "KILOS" ? TariffType.Kilos : TariffType.Fixed;

            var result = await _tariffCalculationService.GetVehicleTariffAsync(
                startDate,
                endDate,
                startOdometer,
                endOdometer,
                vmfCode,
                siteCode,
                departmentCode,
                contractType,
                checkDate,
                tariffTypeEnum
            );

            if (result.Status != TariffStatus.Valid)
            {
                _logger.LogWarning(
                    "Tariff calculation returned status {Status}: {Message}",
                    result.Status,
                    result.Message
                );

                // Return error code or 0.00 based on status
                return result.Status switch
                {
                    TariffStatus.YearNotFound => -1m,
                    TariffStatus.NoMatch => -2m,
                    TariffStatus.Incomplete => -3m,
                    _ => 0.00m,
                };
            }

            _logger.LogInformation(
                "Tariff retrieved: {Tariff} for vehicle {VmfCode} from {Source}",
                result.Amount,
                vmfCode,
                result.Source
            );

            return result.Amount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tariff for vehicle: {VmfCode}", vmfCode);
            throw;
        }
    }

    /// <summary>
    /// Get journal details for a vehicle
    /// </summary>
    public async Task<IEnumerable<JournalDetail>> GetJournalDetailsByVehicleAsync(int vmfCode)
    {
        return await _journalDetailRepository.GetByVehicleAsync(vmfCode);
    }

    /// <summary>
    /// Get journal details for a site
    /// </summary>
    public async Task<IEnumerable<JournalDetail>> GetJournalDetailsBySiteAsync(short siteCode)
    {
        return await _journalDetailRepository.GetBySiteAsync(siteCode);
    }

    /// <summary>
    /// Get journal details by financial year
    /// </summary>
    public async Task<IEnumerable<JournalDetail>> GetJournalDetailsByFinancialYearAsync(
        string financialYear
    )
    {
        return await _journalDetailRepository.GetByFinancialYearAsync(financialYear);
    }

    /// <summary>
    /// Calculate financial year from a date
    /// Legacy: Helper.CurrentFinancialYear method
    /// Financial year starts in March (month 3)
    /// Example: 2024-02-15 = FY2024, 2024-03-01 = FY2025
    /// </summary>
    public string CalculateFinancialYear(DateTime date)
    {
        // Financial year starts 1 April, ends 31 March.
        // Convention: the FY is named after the calendar year it ends in.
        // e.g. April 2025 – March 2026 = FY2026.
        int financialYear = date.Month >= 4 ? date.Year + 1 : date.Year;
        return financialYear.ToString();
    }

    /// <summary>
    /// Delete a journal detail entry
    /// </summary>
    public async Task DeleteJournalDetailAsync(int journalDetailId)
    {
        try
        {
            _logger.LogInformation("Deleting journal detail: {JournalDetailId}", journalDetailId);

            await _journalDetailRepository.DeleteAsync(
                journalDetailId,
                _currentUserContext.GetCurrentUserIdOrDefault()
            );

            _logger.LogInformation("Journal detail deleted: {JournalDetailId}", journalDetailId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error deleting journal detail: {JournalDetailId}",
                journalDetailId
            );
            throw;
        }
    }
}
