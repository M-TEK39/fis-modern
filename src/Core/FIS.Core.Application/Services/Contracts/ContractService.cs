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
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<ContractService> _logger;

    public ContractService(
        IContractRepository contractRepository,
        IVehicleRepository vehicleRepository,
        ISiteRepository siteRepository,
        IContractValidationService validationService,
        IJournalDetailService journalDetailService,
        ICurrentUserContext currentUserContext,
        ILogger<ContractService> logger
    )
    {
        _contractRepository =
            contractRepository ?? throw new ArgumentNullException(nameof(contractRepository));
        _vehicleRepository =
            vehicleRepository ?? throw new ArgumentNullException(nameof(vehicleRepository));
        _siteRepository = siteRepository ?? throw new ArgumentNullException(nameof(siteRepository));
        _validationService =
            validationService ?? throw new ArgumentNullException(nameof(validationService));
        _journalDetailService =
            journalDetailService ?? throw new ArgumentNullException(nameof(journalDetailService));
        _currentUserContext =
            currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
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
                _logger.LogWarning(
                    "Contract validation failed for vehicle {VmfCode}: {Errors}",
                    contract.vmf_code,
                    string.Join(", ", validationResult.Errors)
                );
                return ContractOperationResult.ValidationFailed(validationResult);
            }

            // The validation service uses the legacy capture input flag. The
            // approval procedure itself owns the persisted pending-row status
            // (`still_current = 'N'`, status 1), contract group, status
            // history, and journal-trigger transaction.
            contract.still_current = "Y";
            contract.start_time = contract.start_date;
            contract.end_odometer = 0;
            contract.locked_for_transfer = false;
            // Charged_Until is intentionally left to the legacy procedure;
            // it derives the billing boundary from the target-return period.

            // Create contract through the archived approval procedure. This
            // intentionally fails when the client database does not expose a
            // compatible procedure; a direct-DML fallback would lose legacy
            // transaction, trigger, and status-history behavior.
            var createdContract = await _contractRepository.CreateForApprovalAsync(
                contract,
                _currentUserContext.GetCurrentUserIdOrDefault()
            );

            _logger.LogInformation(
                "Contract created successfully: {ContractCode} for vehicle {VmfCode}",
                createdContract.contract_code,
                createdContract.vmf_code
            );

            // The legacy approval procedure's contract insert fires the
            // database journal trigger. The application-level journal
            // approximation is intentionally not run here, because it would
            // duplicate or diverge from the database-owned transaction.

            return ContractOperationResult.Success(createdContract);
        }
        catch (Exception ex)
            when (
                ex is NotSupportedException
                || (
                    ex is InvalidOperationException
                    && ex.Message.Contains("legacy", StringComparison.OrdinalIgnoreCase)
                    && ex.Message.Contains("procedure", StringComparison.OrdinalIgnoreCase)
                )
            )
        {
            _logger.LogError(ex, "Legacy contract creation workflow unavailable for vehicle: {VmfCode}", contract.vmf_code);
            throw;
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
    public async Task<ContractOperationResult> CloseContractAsync(
        int contractCode,
        DateTime endDate,
        int endOdometer,
        string? notes = null,
        int currentUserId = 0
    )
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
                _logger.LogWarning(
                    "Contract close validation failed for {ContractCode}: {Errors}",
                    contractCode,
                    string.Join(", ", validationResult.Errors)
                );
                return ContractOperationResult.ValidationFailed(validationResult);
            }

            // Close contract
            var resolvedUserId =
                currentUserId > 0 ? currentUserId : _currentUserContext.GetCurrentUserIdOrDefault();
            await _contractRepository.EndContractAsync(
                contractCode,
                endDate,
                resolvedUserId,
                endOdometer,
                notes
            );

            _logger.LogInformation("Contract closed successfully: {ContractCode}", contractCode);

            return ContractOperationResult.Success(contract);
        }
        catch (Exception ex)
            when (
                (ex is NotSupportedException
                    && ex.Message.Contains("contract-closure", StringComparison.OrdinalIgnoreCase))
                || ex.Message.Contains("billing boundary", StringComparison.OrdinalIgnoreCase)
            )
        {
            _logger.LogError(ex, "Legacy contract closure workflow unavailable for contract: {ContractCode}", contractCode);
            throw;
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
    public async Task<ContractOperationResult> ModifyContractAsync(
        Contract contract,
        ContractChangeTracker changeTracker
    )
    {
        try
        {
            _logger.LogInformation("Modifying contract: {ContractCode}", contract.contract_code);

            // Validate modification
            var validationResult = await _validationService.ValidateContractModifyAsync(contract);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning(
                    "Contract modify validation failed for {ContractCode}: {Errors}",
                    contract.contract_code,
                    string.Join(", ", validationResult.Errors)
                );
                return ContractOperationResult.ValidationFailed(validationResult);
            }

            // Process based on change tracker flags (legacy logic)
            if (changeTracker.DoInsert)
            {
                _logger.LogInformation(
                    "Contract modification requires insert: {ContractCode}",
                    contract.contract_code
                );
                return await AddContractAsync(contract);
            }
            else if (changeTracker.DoRebill)
            {
                _logger.LogInformation(
                    "Contract modification requires rebill: {ContractCode}",
                    contract.contract_code
                );
                // Contract UPDATE is intentionally the only application write.
                // The legacy instead-of-update trigger owns reversal/rebill
                // creation and its transaction boundary; creating journal rows
                // here would double-bill or bypass posted-journal rules.
                await _contractRepository.UpdateAsync(
                    contract,
                    _currentUserContext.GetCurrentUserIdOrDefault()
                );
            }
            else if (changeTracker.DoUpdate)
            {
                _logger.LogInformation(
                    "Contract modification requires update: {ContractCode}",
                    contract.contract_code
                );

                // The legacy instead-of-update trigger recalculates the
                // journal detail or creates the required reversal/rebill.
                // Do not update journal_detail separately from the contract.
                await _contractRepository.UpdateAsync(
                    contract,
                    _currentUserContext.GetCurrentUserIdOrDefault()
                );
            }
            else if (changeTracker.DoReversal)
            {
                _logger.LogInformation(
                    "Contract modification requires reversal: {ContractCode}",
                    contract.contract_code
                );
                // The archived contract trigger creates reversal/rebill rows
                // while applying the contract update. There is no verified
                // standalone reversal procedure, so do not manufacture a
                // journal row through the modern repository.
                throw new NotSupportedException(
                    "The legacy contract reversal workflow requires a trigger-owned contract update; no standalone reversal procedure is available."
                );
            }

            _logger.LogInformation(
                "Contract modified successfully: {ContractCode}",
                contract.contract_code
            );

            return ContractOperationResult.Success(contract);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error modifying contract: {ContractCode}",
                contract.contract_code
            );
            return ContractOperationResult.Failed($"Error modifying contract: {ex.Message}");
        }
    }

    /// <summary>
    /// Extend contract target return date (Legacy: ExtendContractTargetReturnDate)
    /// </summary>
    public async Task<ContractOperationResult> ExtendContractTargetReturnDateAsync(
        int contractCode,
        DateTime newTargetReturnDate
    )
    {
        try
        {
            _logger.LogInformation(
                "Extending contract {ContractCode} target return date to {TargetDate}",
                contractCode,
                newTargetReturnDate
            );

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

            await _contractRepository.ExtendExistingAsync(
                contract,
                _currentUserContext.GetCurrentUserIdOrDefault()
            );

            _logger.LogInformation(
                "Contract target return date extended successfully: {ContractCode}",
                contractCode
            );

            return ContractOperationResult.Success(contract);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error extending contract target return date: {ContractCode}",
                contractCode
            );
            return ContractOperationResult.Failed($"Error extending contract: {ex.Message}");
        }
    }

    /// <summary>
    /// Split contract (Legacy: SplitContract method)
    /// Creates new child contract related to parent
    /// </summary>
    public async Task<ContractOperationResult> SplitContractAsync(
        int parentContractCode,
        Contract newContract
    )
    {
        try
        {
            _logger.LogInformation("Splitting contract: {ParentContractCode}", parentContractCode);

            var parentContract = await _contractRepository.GetByIdAsync(parentContractCode);
            if (parentContract == null)
            {
                return ContractOperationResult.Failed(
                    $"Parent contract {parentContractCode} not found"
                );
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

            _logger.LogInformation(
                "Contract split successfully: Parent {ParentContractCode}, Child {ChildContractCode}",
                parentContractCode,
                result.Contract?.contract_code
            );

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error splitting contract: {ParentContractCode}",
                parentContractCode
            );
            return ContractOperationResult.Failed($"Error splitting contract: {ex.Message}");
        }
    }

    /// <summary>
    /// Cancel contract (Legacy: CancelContract method)
    /// </summary>
    public async Task<ContractOperationResult> CancelContractAsync(
        int contractCode,
        string? cancellationReason = null
    )
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
            var wasActive = string.Equals(
                contract.still_current,
                "Y",
                StringComparison.OrdinalIgnoreCase
            );

            contract.end_date = DateTime.Now;
            contract.end_time = DateTime.Now;
            contract.end_odometer = currentOdometer;
            contract.still_current = "N";
            contract.Notes = $"CANCELLED: {cancellationReason ?? "No reason provided"}";

            var resolvedUserId = _currentUserContext.GetCurrentUserIdOrDefault();
            if (!wasActive)
            {
                // The legacy Cancel Loaded Contract button is a pending-row
                // decision (status 6), not an active billing close. Keep that
                // path on the approval/decision procedure and reserve the
                // trigger-owned close below for genuinely active contracts.
                contract.contract_status_code = 6;
                contract.contract_status_date = DateTime.Now;
                await _contractRepository.UpdatePendingDecisionAsync(contract, resolvedUserId);
            }
            else
            {
                await _contractRepository.EndContractAsync(
                    contractCode,
                    contract.end_date ?? DateTime.Now,
                    resolvedUserId,
                    currentOdometer,
                    contract.Notes
                );
            }

            _logger.LogInformation("Contract cancelled successfully: {ContractCode}", contractCode);

            return ContractOperationResult.Success(contract);
        }
        catch (Exception ex)
            when (
                (ex is NotSupportedException
                    && ex.Message.Contains("contract-closure", StringComparison.OrdinalIgnoreCase))
                || ex.Message.Contains("billing boundary", StringComparison.OrdinalIgnoreCase)
            )
        {
            _logger.LogError(ex, "Legacy contract cancellation workflow unavailable for contract: {ContractCode}", contractCode);
            throw;
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
                date_created = DateTime.Now,
            };

            var result = await AddContractAsync(contract);
            return result.IsSuccess ? result.Contract : null;
        }
        catch (Exception ex)
            when (
                ex is NotSupportedException
                || (
                    ex is InvalidOperationException
                    && ex.Message.Contains("legacy", StringComparison.OrdinalIgnoreCase)
                    && ex.Message.Contains("procedure", StringComparison.OrdinalIgnoreCase)
                )
            )
        {
            _logger.LogError(ex, "Legacy contract creation workflow unavailable for vehicle: {VmfCode}", request.VmfCode);
            throw;
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
    public async Task<bool> EndContractByVmfCodeAsync(
        int vmfCode,
        int? endOdometer = null,
        string? notes = null,
        int currentUserId = 0
    )
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
            int finalOdometer =
                endOdometer ?? vehicle?.current_odo ?? activeContract.start_odometer;

            var result = await CloseContractAsync(
                activeContract.contract_code,
                DateTime.Now,
                finalOdometer,
                notes,
                currentUserId
            );
            return result.IsSuccess;
        }
        catch (Exception ex)
            when (
                (ex is NotSupportedException
                    && ex.Message.Contains("contract-closure", StringComparison.OrdinalIgnoreCase))
                || ex.Message.Contains("billing boundary", StringComparison.OrdinalIgnoreCase)
            )
        {
            _logger.LogError(ex, "Legacy contract closure workflow unavailable for vehicle: {VmfCode}", vmfCode);
            throw;
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

    public static ContractOperationResult ValidationFailed(
        ContractValidationResult validationResult
    ) =>
        new()
        {
            IsSuccess = false,
            ErrorMessage = validationResult.GetErrorMessage(),
            ValidationResult = validationResult,
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
