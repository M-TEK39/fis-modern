using FIS.Core.Application.Interfaces;
using FIS.Core.Application.Services.Validation;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Financial;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Application.Services;

/// <summary>
/// Contract business service implementing all legacy contract operations
/// Maps legacy GGFleet.Provider.ContractProvider functionality to modern patterns
/// Handles complex contract operations: Add, Close, Modify, Split, Extend, Cancel
/// Implements change tracking and reversal/rebill logic from legacy system
/// </summary>
public class ContractService : IContractService
{
    private readonly IContractRepository _contractRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ISiteRepository _siteRepository;
    private readonly IContractValidationService _validationService;
    private readonly IJournalDetailService _journalDetailService;
    private readonly ILogger<ContractService> _logger;

    public ContractService(
        IContractRepository contractRepository,
        IVehicleRepository vehicleRepository,
        ISiteRepository siteRepository,
        IContractValidationService validationService,
        IJournalDetailService journalDetailService,
        ILogger<ContractService> logger)
    {
        _contractRepository = contractRepository ?? throw new ArgumentNullException(nameof(contractRepository));
        _vehicleRepository = vehicleRepository ?? throw new ArgumentNullException(nameof(vehicleRepository));
        _siteRepository = siteRepository ?? throw new ArgumentNullException(nameof(siteRepository));
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        _journalDetailService = journalDetailService ?? throw new ArgumentNullException(nameof(journalDetailService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Legacy Contract Operations

    /// <summary>
    /// Add a new contract (Legacy: AddContract method)
    /// Creates contract with validation and optional journal detail integration
    /// </summary>
    public async Task<ContractOperationResult> AddContractAsync(Contract contract)
    {
        try
        {
            _logger.LogInformation("Adding new contract for vehicle: {VmfCode}", contract.vmf_code);

            // Validate contract
            var validationResult = await _validationService.ValidateContractAddAsync(contract);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Contract validation failed for vehicle {VmfCode}: {Errors}",
                    contract.vmf_code, string.Join(", ", validationResult.Errors));
                return ContractOperationResult.ValidationFailed(validationResult);
            }

            // Set default values for new contract
            contract.still_current = "Y";
            contract.start_time = contract.start_date;
            contract.end_odometer = 0;
            contract.locked_for_transfer = false;
            // Initialise Charged_Until to start_date so the monthly billing job
            // knows the correct starting point for this contract's first billing run.
            contract.Charged_Until = contract.start_date;

            // Create contract
            var createdContract = await _contractRepository.CreateAsync(contract, 1);

            _logger.LogInformation("Contract created successfully: {ContractCode} for vehicle {VmfCode}",
                createdContract.contract_code, createdContract.vmf_code);

            // Create journal detail entry (Legacy: JournalDetailProvider.AddJournalDetail)
            try
            {
                var site = await _siteRepository.GetByIdAsync(createdContract.site_code);
                var departmentCode = site?.Depatrment_code ?? 150; // Default to 150 if not found

                // Calculate journal amount
                var journalAmount = await _journalDetailService.CalculateJournalAmountAsync(
                    createdContract.start_date,
                    createdContract.end_date ?? DateTime.Now.AddMonths(1),
                    createdContract.start_odometer,
                    0, // End odometer not set yet
                    createdContract.vmf_code,
                    createdContract.site_code,
                    departmentCode,
                    createdContract.contract_type ?? "H",
                    DateTime.Now);

                // Create journal detail
                var daysQuantity = createdContract.end_date.HasValue
                    ? (int)(createdContract.end_date.Value - createdContract.start_date).TotalDays
                    : 30; // Default to 30 days if no end date

                var journalDetail = new JournalDetail
                {
                    vmf_code = createdContract.vmf_code,
                    site_code = createdContract.site_code,
                    department_code = departmentCode,
                    journal_detail_quantity = daysQuantity,
                    journal_detail_amount = journalAmount,
                    journal_detail_description = $"Contract {createdContract.contract_code} - Initial billing",
                    journal_detail_isdebit = true,
                    journal_detail_type_code = 1
                };

                await _journalDetailService.CreateJournalDetailAsync(journalDetail);

                _logger.LogInformation("Journal detail created for contract {ContractCode}, Amount: {Amount}",
                    createdContract.contract_code, journalAmount);
            }
            catch (Exception journalEx)
            {
                _logger.LogError(journalEx, "Error creating journal detail for contract {ContractCode}. Contract created but journal failed.",
                    createdContract.contract_code);
                // Contract is still created - journal failure doesn't roll back contract
            }

            return ContractOperationResult.Success(createdContract);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding contract for vehicle: {VmfCode}", contract.vmf_code);
            return ContractOperationResult.Failed($"Error adding contract: {ex.Message}");
        }
    }

    /// <summary>
    /// Close an active contract (Legacy: CloseContract method)
    /// Sets end date, end odometer, and still_current to 'N'
    /// </summary>
    public async Task<ContractOperationResult> CloseContractAsync(int contractCode, DateTime endDate, int endOdometer, string? notes = null, int currentUserId = 0)
    {
        try
        {
            _logger.LogInformation("Closing contract: {ContractCode}", contractCode);

            var contract = await _contractRepository.GetByIdAsync(contractCode);
            if (contract == null)
            {
                return ContractOperationResult.Failed($"Contract {contractCode} not found");
            }

            // Update contract fields
            contract.end_date = endDate;
            contract.end_time = endDate;
            contract.end_odometer = endOdometer;
            contract.still_current = "N";
            if (!string.IsNullOrWhiteSpace(notes))
            {
                contract.Notes = notes;
            }

            // Validate closure
            var validationResult = await _validationService.ValidateContractCloseAsync(contract);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Contract close validation failed for {ContractCode}: {Errors}",
                    contractCode, string.Join(", ", validationResult.Errors));
                return ContractOperationResult.ValidationFailed(validationResult);
            }

            // Close contract
            await _contractRepository.EndContractAsync(contractCode, endDate, currentUserId, endOdometer, notes);

            _logger.LogInformation("Contract closed successfully: {ContractCode}", contractCode);

            return ContractOperationResult.Success(contract);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing contract: {ContractCode}", contractCode);
            return ContractOperationResult.Failed($"Error closing contract: {ex.Message}");
        }
    }

    /// <summary>
    /// Modify a contract (Legacy: ModifyContract method)
    /// Handles complex change tracking with DoInsert, DoRebill, DoUpdate, DoReversal flags
    /// </summary>
    public async Task<ContractOperationResult> ModifyContractAsync(Contract contract, ContractChangeTracker changeTracker)
    {
        try
        {
            _logger.LogInformation("Modifying contract: {ContractCode}", contract.contract_code);

            // Validate modification
            var validationResult = await _validationService.ValidateContractModifyAsync(contract);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Contract modify validation failed for {ContractCode}: {Errors}",
                    contract.contract_code, string.Join(", ", validationResult.Errors));
                return ContractOperationResult.ValidationFailed(validationResult);
            }

            // Process based on change tracker flags (legacy logic)
            if (changeTracker.DoInsert)
            {
                _logger.LogInformation("Contract modification requires insert: {ContractCode}", contract.contract_code);
                return await AddContractAsync(contract);
            }
            else if (changeTracker.DoRebill)
            {
                _logger.LogInformation("Contract modification requires rebill: {ContractCode}", contract.contract_code);

                // Rebill logic: Generate reversal + create new journal (Legacy: RebillContract)
                try
                {
                    // Get existing journal entries for this vehicle
                    var existingJournals = await _journalDetailService.GetJournalDetailsByVehicleAsync(contract.vmf_code);
                    var latestJournal = existingJournals.FirstOrDefault();

                    if (latestJournal != null)
                    {
                        // Generate reversal for old entry
                        await _journalDetailService.GenerateReversalAsync(latestJournal.journal_detail_code);
                        _logger.LogInformation("Generated reversal for journal {JournalCode}", latestJournal.journal_detail_code);
                    }

                    // Create new journal entry with updated values
                    var site = await _siteRepository.GetByIdAsync(contract.site_code);
                    var departmentCode = site?.Depatrment_code ?? 150;

                    var newJournalAmount = await _journalDetailService.CalculateJournalAmountAsync(
                        contract.start_date,
                        contract.end_date ?? DateTime.Now.AddMonths(1),
                        contract.start_odometer,
                        contract.end_odometer ?? 0,
                        contract.vmf_code,
                        contract.site_code,
                        departmentCode,
                        contract.contract_type ?? "H",
                        DateTime.Now);

                    var newJournal = new JournalDetail
                    {
                        vmf_code = contract.vmf_code,
                        site_code = contract.site_code,
                        department_code = departmentCode,
                        journal_detail_amount = newJournalAmount,
                        journal_detail_description = $"Contract {contract.contract_code} - Rebilled",
                        journal_detail_rebill_code = latestJournal?.journal_detail_code ?? Guid.NewGuid()
                    };

                    await _journalDetailService.CreateJournalDetailAsync(newJournal);
                    _logger.LogInformation("Created rebill journal for contract {ContractCode}", contract.contract_code);
                }
                catch (Exception journalEx)
                {
                    _logger.LogError(journalEx, "Error processing journal rebill for contract {ContractCode}", contract.contract_code);
                }

                await _contractRepository.UpdateAsync(contract, 1);
            }
            else if (changeTracker.DoUpdate)
            {
                _logger.LogInformation("Contract modification requires update: {ContractCode}", contract.contract_code);

                // Update journal detail (Legacy: JournalDetailProvider.UpdateJournalDetail)
                try
                {
                    var existingJournals = await _journalDetailService.GetJournalDetailsByVehicleAsync(contract.vmf_code);
                    var latestJournal = existingJournals.FirstOrDefault();

                    if (latestJournal != null)
                    {
                        // Recalculate amount
                        var site = await _siteRepository.GetByIdAsync(contract.site_code);
                        var departmentCode = site?.Depatrment_code ?? 150;

                        latestJournal.journal_detail_amount = await _journalDetailService.CalculateJournalAmountAsync(
                            contract.start_date,
                            contract.end_date ?? DateTime.Now.AddMonths(1),
                            contract.start_odometer,
                            contract.end_odometer ?? 0,
                            contract.vmf_code,
                            contract.site_code,
                            departmentCode,
                            contract.contract_type ?? "H",
                            DateTime.Now);

                        latestJournal.journal_detail_description = $"Contract {contract.contract_code} - Updated";

                        await _journalDetailService.UpdateJournalDetailAsync(latestJournal);
                        _logger.LogInformation("Updated journal for contract {ContractCode}", contract.contract_code);
                    }
                }
                catch (Exception journalEx)
                {
                    _logger.LogError(journalEx, "Error updating journal for contract {ContractCode}", contract.contract_code);
                }

                await _contractRepository.UpdateAsync(contract, 1);
            }
            else if (changeTracker.DoReversal)
            {
                _logger.LogInformation("Contract modification requires reversal: {ContractCode}", contract.contract_code);

                // Generate reversals (Legacy: JournalDetailProvider.GenerateReversals)
                try
                {
                    var existingJournals = await _journalDetailService.GetJournalDetailsByVehicleAsync(contract.vmf_code);
                    var latestJournal = existingJournals.FirstOrDefault();

                    if (latestJournal != null)
                    {
                        await _journalDetailService.GenerateReversalAsync(latestJournal.journal_detail_code);
                        _logger.LogInformation("Generated reversal for journal {JournalCode}", latestJournal.journal_detail_code);
                    }
                }
                catch (Exception journalEx)
                {
                    _logger.LogError(journalEx, "Error generating reversal for contract {ContractCode}", contract.contract_code);
                }
            }

            _logger.LogInformation("Contract modified successfully: {ContractCode}", contract.contract_code);

            return ContractOperationResult.Success(contract);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error modifying contract: {ContractCode}", contract.contract_code);
            return ContractOperationResult.Failed($"Error modifying contract: {ex.Message}");
        }
    }

    /// <summary>
    /// Extend contract target return date (Legacy: ExtendContractTargetReturnDate)
    /// </summary>
    public async Task<ContractOperationResult> ExtendContractTargetReturnDateAsync(int contractCode, DateTime newTargetReturnDate)
    {
        try
        {
            _logger.LogInformation("Extending contract {ContractCode} target return date to {TargetDate}",
                contractCode, newTargetReturnDate);

            var contract = await _contractRepository.GetByIdAsync(contractCode);
            if (contract == null)
            {
                return ContractOperationResult.Failed($"Contract {contractCode} not found");
            }

            contract.target_return_date = newTargetReturnDate;

            // Validate extension
            var validationResult = await _validationService.ValidateContractExtendAsync(contract);
            if (!validationResult.IsValid)
            {
                return ContractOperationResult.ValidationFailed(validationResult);
            }

            await _contractRepository.UpdateAsync(contract, 1);

            _logger.LogInformation("Contract target return date extended successfully: {ContractCode}", contractCode);

            return ContractOperationResult.Success(contract);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extending contract target return date: {ContractCode}", contractCode);
            return ContractOperationResult.Failed($"Error extending contract: {ex.Message}");
        }
    }

    /// <summary>
    /// Split contract (Legacy: SplitContract method)
    /// Creates new child contract related to parent
    /// </summary>
    public async Task<ContractOperationResult> SplitContractAsync(int parentContractCode, Contract newContract)
    {
        try
        {
            _logger.LogInformation("Splitting contract: {ParentContractCode}", parentContractCode);

            var parentContract = await _contractRepository.GetByIdAsync(parentContractCode);
            if (parentContract == null)
            {
                return ContractOperationResult.Failed($"Parent contract {parentContractCode} not found");
            }

            // Set parent relationship
            newContract.parent_contract_code = parentContractCode;
            newContract.contract_group_code = parentContract.contract_group_code;

            // Validate split
            var validationResult = await _validationService.ValidateContractSplitAsync(newContract);
            if (!validationResult.IsValid)
            {
                return ContractOperationResult.ValidationFailed(validationResult);
            }

            // Create new contract
            var result = await AddContractAsync(newContract);

            _logger.LogInformation("Contract split successfully: Parent {ParentContractCode}, Child {ChildContractCode}",
                parentContractCode, result.Contract?.contract_code);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error splitting contract: {ParentContractCode}", parentContractCode);
            return ContractOperationResult.Failed($"Error splitting contract: {ex.Message}");
        }
    }

    /// <summary>
    /// Cancel contract (Legacy: CancelContract method)
    /// </summary>
    public async Task<ContractOperationResult> CancelContractAsync(int contractCode, string? cancellationReason = null)
    {
        try
        {
            _logger.LogInformation("Cancelling contract: {ContractCode}", contractCode);

            var contract = await _contractRepository.GetByIdAsync(contractCode);
            if (contract == null)
            {
                return ContractOperationResult.Failed($"Contract {contractCode} not found");
            }

            // Validate cancellation
            var validationResult = await _validationService.ValidateContractCancelAsync(contract);
            if (!validationResult.IsValid)
            {
                return ContractOperationResult.ValidationFailed(validationResult);
            }

            // Close contract with current date and odometer
            var vehicle = await _vehicleRepository.GetByIdAsync(contract.vmf_code);
            int currentOdometer = vehicle?.current_odo ?? contract.start_odometer;

            contract.end_date = DateTime.Now;
            contract.end_time = DateTime.Now;
            contract.end_odometer = currentOdometer;
            contract.still_current = "N";
            contract.Notes = $"CANCELLED: {cancellationReason ?? "No reason provided"}";

            await _contractRepository.UpdateAsync(contract, 1);

            _logger.LogInformation("Contract cancelled successfully: {ContractCode}", contractCode);

            return ContractOperationResult.Success(contract);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling contract: {ContractCode}", contractCode);
            return ContractOperationResult.Failed($"Error cancelling contract: {ex.Message}");
        }
    }

    #endregion

    #region IContractService Implementation (Simple Interface Methods)

    /// <summary>
    /// Hire a vehicle - creates a new contract (simple interface method)
    /// </summary>
    public async Task<Contract?> HireVehicleAsync(HireContractRequest request)
    {
        try
        {
            var vehicle = await _vehicleRepository.GetByIdAsync(request.VmfCode);
            if (vehicle == null)
            {
                _logger.LogWarning("Vehicle not found: {VmfCode}", request.VmfCode);
                return null;
            }

            var site = await _siteRepository.GetByIdAsync(request.SiteCode);
            if (site == null)
            {
                _logger.LogWarning("Site not found: {SiteCode}", request.SiteCode);
                return null;
            }

            var contract = new Contract
            {
                vmf_code = request.VmfCode,
                site_code = request.SiteCode,
                start_date = DateTime.Now,
                start_time = DateTime.Now,
                start_odometer = request.StartOdometer ?? vehicle.current_odo,
                still_current = "Y",
                Driver_id = request.DriverId,
                Driver_name = request.DriverName,
                site_driver_code = request.SiteDriverCode,
                user_code = request.UserCode,
                Authorisation = request.Authorisation,
                Notes = request.Notes,
                target_return_date = request.TargetReturnDate,
                contract_type = "H", // H = Hire
                end_odometer = 0,
                locked_for_transfer = false,
                contract_status_code = 0, // 0 = Draft (not yet submitted for approval)
                created_by_user_code = request.CreatedByUserId,
                date_created = DateTime.Now
            };

            var result = await AddContractAsync(contract);
            return result.IsSuccess ? result.Contract : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error hiring vehicle: {VmfCode}", request.VmfCode);
            return null;
        }
    }

    /// <summary>
    /// End contract by vehicle code (simple interface method)
    /// </summary>
    public async Task<bool> EndContractByVmfCodeAsync(int vmfCode, int? endOdometer = null, string? notes = null)
    {
        try
        {
            var activeContract = await _contractRepository.GetActiveContractByVehicleAsync(vmfCode);
            if (activeContract == null)
            {
                _logger.LogWarning("No active contract found for vehicle: {VmfCode}", vmfCode);
                return false;
            }

            var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode);
            int finalOdometer = endOdometer ?? vehicle?.current_odo ?? activeContract.start_odometer;

            var result = await CloseContractAsync(activeContract.contract_code, DateTime.Now, finalOdometer, notes);
            return result.IsSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending contract for vehicle: {VmfCode}", vmfCode);
            return false;
        }
    }

    /// <summary>
    /// Get active contracts (simple interface method)
    /// </summary>
    public async Task<IEnumerable<Contract>> GetActiveContractsAsync()
    {
        return await _contractRepository.GetActiveContractsAsync();
    }

    #endregion
}

/// <summary>
/// Contract operation result wrapper
/// </summary>
public class ContractOperationResult
{
    public bool IsSuccess { get; set; }
    public Contract? Contract { get; set; }
    public string? ErrorMessage { get; set; }
    public ContractValidationResult? ValidationResult { get; set; }

    public static ContractOperationResult Success(Contract contract) =>
        new() { IsSuccess = true, Contract = contract };

    public static ContractOperationResult Failed(string error) =>
        new() { IsSuccess = false, ErrorMessage = error };

    public static ContractOperationResult ValidationFailed(ContractValidationResult validationResult) =>
        new()
        {
            IsSuccess = false,
            ErrorMessage = validationResult.GetErrorMessage(),
            ValidationResult = validationResult
        };
}

/// <summary>
/// Contract change tracker (Legacy: DoInsert, DoRebill, DoUpdate, DoReversal flags)
/// Used to track what type of operation is needed during contract modification
/// </summary>
public class ContractChangeTracker
{
    public bool DoInsert { get; set; }
    public bool DoRebill { get; set; }
    public bool DoUpdate { get; set; }
    public bool DoReversal { get; set; }

    public static ContractChangeTracker ForUpdate() => new() { DoUpdate = true };
    public static ContractChangeTracker ForRebill() => new() { DoRebill = true, DoReversal = true };
    public static ContractChangeTracker ForInsert() => new() { DoInsert = true };
    public static ContractChangeTracker ForReversal() => new() { DoReversal = true };
}
