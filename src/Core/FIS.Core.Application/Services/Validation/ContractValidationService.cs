using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Application.Services.Validation;

/// <summary>
/// Contract validation service implementing all legacy validation rules
/// Maps 20+ legacy stored procedures to modern EF Core queries
/// Maintains 100% compatibility with legacy GGFleet.BLL.Contract validation logic
/// </summary>
public class ContractValidationService : IContractValidationService
{
    private readonly IContractRepository _contractRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IModelRepository _modelRepository;
    private readonly ISiteRepository _siteRepository;
    private readonly ITariffRepository _tariffRepository;
    private readonly ITripRepository _tripRepository;
    private readonly ILogger<ContractValidationService> _logger;

    public ContractValidationService(
        IContractRepository contractRepository,
        IVehicleRepository vehicleRepository,
        IModelRepository modelRepository,
        ISiteRepository siteRepository,
        ITariffRepository tariffRepository,
        ITripRepository tripRepository,
        ILogger<ContractValidationService> logger
    )
    {
        _contractRepository =
            contractRepository ?? throw new ArgumentNullException(nameof(contractRepository));
        _vehicleRepository =
            vehicleRepository ?? throw new ArgumentNullException(nameof(vehicleRepository));
        _modelRepository =
            modelRepository ?? throw new ArgumentNullException(nameof(modelRepository));
        _siteRepository = siteRepository ?? throw new ArgumentNullException(nameof(siteRepository));
        _tariffRepository =
            tariffRepository ?? throw new ArgumentNullException(nameof(tariffRepository));
        _tripRepository = tripRepository ?? throw new ArgumentNullException(nameof(tripRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Individual Validation Methods

    /// <summary>
    /// Legacy: NEW_DEV_SEL_ContractByReg
    /// </summary>
    public async Task<ContractValidationResult> ValidateContractRegistrationNumberAsync(
        string registrationNumber
    )
    {
        try
        {
            var vehicle = await _vehicleRepository.GetByRegistrationNumberAsync(registrationNumber);
            if (vehicle == null)
            {
                return ContractValidationResult.Failed("No contract linked to this registration");
            }

            var hasContract = await _contractRepository.HasActiveContractAsync(vehicle.vmf_code);
            if (!hasContract)
            {
                return ContractValidationResult.Failed("No contract linked to this registration");
            }

            return ContractValidationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error validating contract registration number: {Registration}",
                registrationNumber
            );
            return ContractValidationResult.Failed($"Validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Legacy: NEW_DEV_VAL_Registration
    /// </summary>
    public async Task<ContractValidationResult> ValidateVehicleRegistrationNumberAsync(
        string registrationNumber
    )
    {
        try
        {
            var vehicle = await _vehicleRepository.GetByRegistrationNumberAsync(registrationNumber);
            if (vehicle == null)
            {
                return ContractValidationResult.Failed("Invalid registration no");
            }

            return ContractValidationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error validating vehicle registration number: {Registration}",
                registrationNumber
            );
            return ContractValidationResult.Failed($"Validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Legacy: NEW_DEV_VAL_DuplicateContract
    /// Critical business rule: No duplicate open contracts per vehicle
    /// </summary>
    public async Task<ContractValidationResult> ValidateDuplicateContractAsync(int vmfCode)
    {
        try
        {
            var activeContract = await _contractRepository.GetActiveContractByVehicleAsync(vmfCode);
            if (activeContract != null)
            {
                var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode);
                var site = activeContract.Site;

                var errorMessage =
                    $"There is already an open Contract for this vehicle {vehicle?.registration_number ?? "Unknown"} "
                    + $"at this site {site?.description ?? "Unknown"}. "
                    + $"The open contract number at this site is {activeContract.contract_code}. "
                    + $"No duplicate open contracts are allowed for a given vehicle.";

                return ContractValidationResult.Failed(errorMessage);
            }

            return ContractValidationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error checking duplicate contract for vehicle: {VmfCode}",
                vmfCode
            );
            return ContractValidationResult.Failed($"Validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Legacy: NEW_DEV_VAL_OpenTripAuthority
    /// Prevents closing contracts with active trip authorities
    /// </summary>
    public async Task<ContractValidationResult> ValidateNoOpenTripAuthoritiesAsync(int contractCode)
    {
        try
        {
            var contract = await _contractRepository.GetByIdAsync(contractCode);
            if (contract == null)
            {
                return ContractValidationResult.Failed($"Contract {contractCode} not found");
            }

            // Get all trips for this contract
            var trips = await _tripRepository.GetTripsByVehicleAsync(contract.vmf_code);

            // Check for any trips with expiry dates in the future
            var openTripAuthorities = trips
                .Where(t =>
                    t.contract_code == contractCode
                    && t.expiry_date.HasValue
                    && t.expiry_date.Value > DateTime.Now
                )
                .ToList();

            if (openTripAuthorities.Any())
            {
                return ContractValidationResult.Failed(
                    "The contract has an open trip authority and cannot be closed"
                );
            }

            return ContractValidationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error checking open trip authorities for contract: {ContractCode}",
                contractCode
            );
            return ContractValidationResult.Failed($"Validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Legacy: NEW_DEV_VAL_Site
    /// Validates site code and ensures department code is set
    /// </summary>
    public async Task<ContractValidationResult> ValidateSiteCodeAsync(short siteCode)
    {
        try
        {
            var site = await _siteRepository.GetByIdAsync(siteCode);
            if (site == null)
            {
                return ContractValidationResult.Failed("The site code is invalid");
            }

            // Check if department code is set (legacy had typo "Depatrment_code")
            if (site.Depatrment_code == null || site.Depatrment_code == 0)
            {
                return ContractValidationResult.Failed("The department code is invalid");
            }

            return ContractValidationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating site code: {SiteCode}", siteCode);
            return ContractValidationResult.Failed($"Validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Legacy: NEW_DEV_VAL_Contracts (for start date on add)
    /// Ensures new contract start date doesn't overlap with previous contract
    /// </summary>
    public async Task<ContractValidationResult> ValidateStartDateAsync(
        int vmfCode,
        DateTime startDate
    )
    {
        try
        {
            var contracts = await _contractRepository.GetContractsByVehicleAsync(vmfCode);
            var previousContract = contracts
                .Where(c => c.end_date.HasValue)
                .OrderByDescending(c => c.end_date)
                .FirstOrDefault();

            if (previousContract != null)
            {
                if (startDate < previousContract.end_date!.Value)
                {
                    return ContractValidationResult.Failed(
                        $"The previous contract's end date was {previousContract.end_date.Value:yyyy-MM-dd}. "
                            + $"Please select a start date that does not overlap with the previous contract's end date"
                    );
                }
            }

            // Start date must be today or earlier
            if (startDate > DateTime.Now)
            {
                return ContractValidationResult.Failed("The start date has to be today or earlier");
            }

            return ContractValidationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating start date for vehicle: {VmfCode}", vmfCode);
            return ContractValidationResult.Failed($"Validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Legacy: NEW_DEV_VAL_Contracts (for start date on modify - less restrictive)
    /// </summary>
    public async Task<ContractValidationResult> ValidateStartDateModifyAsync(
        int vmfCode,
        DateTime startDate
    )
    {
        try
        {
            var contracts = await _contractRepository.GetContractsByVehicleAsync(vmfCode);
            var previousContract = contracts
                .Where(c => c.end_date.HasValue)
                .OrderByDescending(c => c.end_date)
                .FirstOrDefault();

            if (previousContract != null)
            {
                if (startDate < previousContract.end_date!.Value)
                {
                    return ContractValidationResult.Failed(
                        $"The previous contract's end date was {previousContract.end_date.Value:yyyy-MM-dd}"
                    );
                }
            }

            // Note: Legacy code has commented out future date check for modify operation
            return ContractValidationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error validating start date (modify) for vehicle: {VmfCode}",
                vmfCode
            );
            return ContractValidationResult.Failed($"Validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Legacy: NEW_DEV_VAL_Contracts (for end date on close)
    /// </summary>
    public async Task<ContractValidationResult> ValidateEndDateAsync(int vmfCode, DateTime endDate)
    {
        try
        {
            var contracts = await _contractRepository.GetContractsByVehicleAsync(vmfCode);
            var currentContract = contracts.Where(c => c.still_current == "Y").FirstOrDefault();

            if (currentContract == null)
            {
                return ContractValidationResult.Failed("No active contract found for this vehicle");
            }

            if (endDate < currentContract.start_date)
            {
                return ContractValidationResult.Failed(
                    $"The current contract's start date is {currentContract.start_date:yyyy-MM-dd}. "
                        + $"Please select an end date after the start date"
                );
            }

            if (endDate > DateTime.Now)
            {
                return ContractValidationResult.Failed(
                    "The contract's closing date cannot be in the future"
                );
            }

            return ContractValidationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating end date for vehicle: {VmfCode}", vmfCode);
            return ContractValidationResult.Failed($"Validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Legacy: NEW_DEV_VAL_Contracts (for start odometer)
    /// </summary>
    public async Task<ContractValidationResult> ValidateStartOdometerAsync(
        int vmfCode,
        int startOdometer
    )
    {
        try
        {
            var contracts = await _contractRepository.GetContractsByVehicleAsync(vmfCode);
            var previousContract = contracts
                .Where(c => c.end_odometer.HasValue && c.end_odometer > 0)
                .OrderByDescending(c => c.end_date)
                .FirstOrDefault();

            if (previousContract != null)
            {
                if (startOdometer < previousContract.end_odometer!.Value)
                {
                    return ContractValidationResult.Failed(
                        $"The previous contract's end odometer reading was {previousContract.end_odometer.Value}"
                    );
                }
            }

            return ContractValidationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating start odometer for vehicle: {VmfCode}", vmfCode);
            return ContractValidationResult.Failed($"Validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Legacy: CheckEndOdoClose validation
    /// </summary>
    public async Task<ContractValidationResult> ValidateEndOdometerCloseAsync(
        int vmfCode,
        int endOdometer
    )
    {
        try
        {
            var contracts = await _contractRepository.GetContractsByVehicleAsync(vmfCode);
            var currentContract = contracts.Where(c => c.still_current == "Y").FirstOrDefault();

            if (currentContract == null)
            {
                return ContractValidationResult.Failed("No active contract found for this vehicle");
            }

            if (endOdometer < currentContract.start_odometer)
            {
                return ContractValidationResult.Failed(
                    $"The contract's end odometer reading must be greater than the start odometer reading of {currentContract.start_odometer}"
                );
            }

            return ContractValidationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error validating end odometer (close) for vehicle: {VmfCode}",
                vmfCode
            );
            return ContractValidationResult.Failed($"Validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Legacy: CheckEndOdo validation (for add operation)
    /// </summary>
    public Task<ContractValidationResult> ValidateEndOdometerAddAsync(int endOdometer)
    {
        if (endOdometer > 0)
        {
            return Task.FromResult(
                ContractValidationResult.Failed(
                    "The contract's end odometer reading must be 0 when the contract is open"
                )
            );
        }

        return Task.FromResult(ContractValidationResult.Success());
    }

    /// <summary>
    /// Legacy: CheckStillCurrentAdd validation
    /// </summary>
    public Task<ContractValidationResult> ValidateStillCurrentAddAsync(string? stillCurrent)
    {
        if (stillCurrent != "Y")
        {
            return Task.FromResult(
                ContractValidationResult.Failed(
                    "The contract is current and the still current field must be set to 'Y'"
                )
            );
        }

        return Task.FromResult(ContractValidationResult.Success());
    }

    /// <summary>
    /// Legacy: CheckStillCurrentClose validation
    /// </summary>
    public Task<ContractValidationResult> ValidateStillCurrentCloseAsync(string? stillCurrent)
    {
        if (stillCurrent != "N")
        {
            return Task.FromResult(
                ContractValidationResult.Failed(
                    "The contract is closed and the still current field must be set to 'N'"
                )
            );
        }

        return Task.FromResult(ContractValidationResult.Success());
    }

    /// <summary>
    /// Legacy: CheckTargetReturnDate validation
    /// </summary>
    public Task<ContractValidationResult> ValidateTargetReturnDateAsync(DateTime targetReturnDate)
    {
        if (targetReturnDate < DateTime.Now)
        {
            return Task.FromResult(
                ContractValidationResult.Failed(
                    "The contract's Target Return Date has to be today or in the future"
                )
            );
        }

        return Task.FromResult(ContractValidationResult.Success());
    }

    #endregion

    #region Comprehensive Validation Methods

    /// <summary>
    /// Comprehensive validation for adding a new contract
    /// Legacy Ruleset: ValContractAdd
    /// </summary>
    public async Task<ContractValidationResult> ValidateContractAddAsync(Contract contract)
    {
        var result = new ContractValidationResult
        {
            ValidationRuleset = ContractValidationRulesets.Add,
        };

        try
        {
            _logger.LogInformation(
                "Validating contract add for vehicle: {VmfCode}",
                contract.vmf_code
            );

            // 1. Validate registration number exists
            var vehicle = await _vehicleRepository.GetByIdAsync(contract.vmf_code);
            if (vehicle == null)
            {
                result.AddError("Invalid vehicle code");
                return result;
            }

            var regValidation = await ValidateVehicleRegistrationNumberAsync(
                vehicle.registration_number!
            );
            if (!regValidation.IsValid)
                result.AddErrors(regValidation.Errors);

            // 2. Ensure the vehicle has an approved effective tariff before contract capture
            var tariffValidation = await ValidateVehicleTariffAsync(vehicle, contract.start_date);
            if (!tariffValidation.IsValid)
                result.AddErrors(tariffValidation.Errors);

            // 3. Check for duplicate contracts
            var duplicateValidation = await ValidateDuplicateContractAsync(contract.vmf_code);
            if (!duplicateValidation.IsValid)
                result.AddErrors(duplicateValidation.Errors);

            // 4. Validate site code
            var siteValidation = await ValidateSiteCodeAsync(contract.site_code);
            if (!siteValidation.IsValid)
                result.AddErrors(siteValidation.Errors);

            // 5. Validate start date
            var startDateValidation = await ValidateStartDateAsync(
                contract.vmf_code,
                contract.start_date
            );
            if (!startDateValidation.IsValid)
                result.AddErrors(startDateValidation.Errors);

            // 6. Validate start odometer
            var startOdoValidation = await ValidateStartOdometerAsync(
                contract.vmf_code,
                contract.start_odometer
            );
            if (!startOdoValidation.IsValid)
                result.AddErrors(startOdoValidation.Errors);

            // 7. Validate end odometer is zero
            var endOdoValidation = await ValidateEndOdometerAddAsync(contract.end_odometer ?? 0);
            if (!endOdoValidation.IsValid)
                result.AddErrors(endOdoValidation.Errors);

            // 8. Validate still_current is 'Y'
            var stillCurrentValidation = await ValidateStillCurrentAddAsync(contract.still_current);
            if (!stillCurrentValidation.IsValid)
                result.AddErrors(stillCurrentValidation.Errors);

            // 9. Validate target return date
            if (contract.target_return_date.HasValue)
            {
                var targetDateValidation = await ValidateTargetReturnDateAsync(
                    contract.target_return_date.Value
                );
                if (!targetDateValidation.IsValid)
                    result.AddErrors(targetDateValidation.Errors);
            }

            _logger.LogInformation(
                "Contract add validation completed: {IsValid}, Errors: {ErrorCount}",
                result.IsValid,
                result.Errors.Count
            );

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during contract add validation");
            result.AddError($"Validation error: {ex.Message}");
            return result;
        }
    }

    private async Task<ContractValidationResult> ValidateVehicleTariffAsync(
        Vehicle vehicle,
        DateTime? effectiveDate
    )
    {
        var model = await _modelRepository.GetByIdAsync(vehicle.model_code);
        if (model == null)
        {
            return ContractValidationResult.Failed(
                "This vehicle cannot be contracted because its model configuration is missing."
            );
        }

        var approvedTariff = await _tariffRepository.GetApprovedTariffForClassAsync(
            model.class_code,
            (effectiveDate ?? DateTime.Today).Date
        );

        return approvedTariff != null
            ? ContractValidationResult.Success()
            : ContractValidationResult.Failed(
                $"No approved tariff is captured for vehicle class {model.class_code}. Capture the tariff before opening or submitting this contract."
            );
    }

    /// <summary>
    /// Comprehensive validation for modifying a contract
    /// Legacy Ruleset: ValContractModify
    /// </summary>
    public async Task<ContractValidationResult> ValidateContractModifyAsync(Contract contract)
    {
        var result = new ContractValidationResult
        {
            ValidationRuleset = ContractValidationRulesets.Modify,
        };

        try
        {
            _logger.LogInformation(
                "Validating contract modify for contract: {ContractCode}",
                contract.contract_code
            );

            // Validate vehicle registration
            var vehicle = await _vehicleRepository.GetByIdAsync(contract.vmf_code);
            if (vehicle != null)
            {
                var regValidation = await ValidateVehicleRegistrationNumberAsync(
                    vehicle.registration_number!
                );
                if (!regValidation.IsValid)
                    result.AddErrors(regValidation.Errors);
            }

            // Validate site code
            var siteValidation = await ValidateSiteCodeAsync(contract.site_code);
            if (!siteValidation.IsValid)
                result.AddErrors(siteValidation.Errors);

            // Validate start date (modify version - less restrictive)
            var startDateValidation = await ValidateStartDateModifyAsync(
                contract.vmf_code,
                contract.start_date
            );
            if (!startDateValidation.IsValid)
                result.AddErrors(startDateValidation.Errors);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during contract modify validation");
            result.AddError($"Validation error: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Comprehensive validation for closing a contract
    /// Legacy Ruleset: ValContractClose
    /// </summary>
    public async Task<ContractValidationResult> ValidateContractCloseAsync(Contract contract)
    {
        var result = new ContractValidationResult
        {
            ValidationRuleset = ContractValidationRulesets.Close,
        };

        try
        {
            _logger.LogInformation(
                "Validating contract close for contract: {ContractCode}",
                contract.contract_code
            );

            // 1. Check for open trip authorities
            var tripAuthorityValidation = await ValidateNoOpenTripAuthoritiesAsync(
                contract.contract_code
            );
            if (!tripAuthorityValidation.IsValid)
                result.AddErrors(tripAuthorityValidation.Errors);

            // 2. Validate end date
            if (contract.end_date.HasValue)
            {
                var endDateValidation = await ValidateEndDateAsync(
                    contract.vmf_code,
                    contract.end_date.Value
                );
                if (!endDateValidation.IsValid)
                    result.AddErrors(endDateValidation.Errors);
            }
            else
            {
                result.AddError("End date is required when closing a contract");
            }

            // 3. Validate end odometer
            if (contract.end_odometer.HasValue)
            {
                var endOdoValidation = await ValidateEndOdometerCloseAsync(
                    contract.vmf_code,
                    contract.end_odometer.Value
                );
                if (!endOdoValidation.IsValid)
                    result.AddErrors(endOdoValidation.Errors);
            }
            else
            {
                result.AddError("End odometer is required when closing a contract");
            }

            // 4. Validate still_current is 'N'
            var stillCurrentValidation = await ValidateStillCurrentCloseAsync(
                contract.still_current
            );
            if (!stillCurrentValidation.IsValid)
                result.AddErrors(stillCurrentValidation.Errors);

            // 5. Validate vehicle registration
            var vehicle = await _vehicleRepository.GetByIdAsync(contract.vmf_code);
            if (vehicle != null)
            {
                var regValidation = await ValidateVehicleRegistrationNumberAsync(
                    vehicle.registration_number!
                );
                if (!regValidation.IsValid)
                    result.AddErrors(regValidation.Errors);
            }

            // 6. Validate site code
            var siteValidation = await ValidateSiteCodeAsync(contract.site_code);
            if (!siteValidation.IsValid)
                result.AddErrors(siteValidation.Errors);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during contract close validation");
            result.AddError($"Validation error: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Comprehensive validation for splitting a contract
    /// Legacy Ruleset: ValContractSplit
    /// </summary>
    public async Task<ContractValidationResult> ValidateContractSplitAsync(Contract contract)
    {
        var result = new ContractValidationResult
        {
            ValidationRuleset = ContractValidationRulesets.Split,
        };

        try
        {
            // Standard validations for split
            var vehicle = await _vehicleRepository.GetByIdAsync(contract.vmf_code);
            if (vehicle != null)
            {
                var regValidation = await ValidateVehicleRegistrationNumberAsync(
                    vehicle.registration_number!
                );
                if (!regValidation.IsValid)
                    result.AddErrors(regValidation.Errors);
            }

            var siteValidation = await ValidateSiteCodeAsync(contract.site_code);
            if (!siteValidation.IsValid)
                result.AddErrors(siteValidation.Errors);

            // Validate target return date
            if (contract.target_return_date.HasValue)
            {
                var targetDateValidation = await ValidateTargetReturnDateAsync(
                    contract.target_return_date.Value
                );
                if (!targetDateValidation.IsValid)
                    result.AddErrors(targetDateValidation.Errors);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during contract split validation");
            result.AddError($"Validation error: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Comprehensive validation for extending a contract
    /// Legacy Ruleset: ValContractExtend
    /// </summary>
    public async Task<ContractValidationResult> ValidateContractExtendAsync(Contract contract)
    {
        var result = new ContractValidationResult
        {
            ValidationRuleset = ContractValidationRulesets.Extend,
        };

        try
        {
            // Standard validations for extend
            var vehicle = await _vehicleRepository.GetByIdAsync(contract.vmf_code);
            if (vehicle != null)
            {
                var regValidation = await ValidateVehicleRegistrationNumberAsync(
                    vehicle.registration_number!
                );
                if (!regValidation.IsValid)
                    result.AddErrors(regValidation.Errors);
            }

            var siteValidation = await ValidateSiteCodeAsync(contract.site_code);
            if (!siteValidation.IsValid)
                result.AddErrors(siteValidation.Errors);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during contract extend validation");
            result.AddError($"Validation error: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Comprehensive validation for cancelling a contract
    /// Legacy Ruleset: ValContractCancel
    /// </summary>
    public async Task<ContractValidationResult> ValidateContractCancelAsync(Contract contract)
    {
        var result = new ContractValidationResult
        {
            ValidationRuleset = ContractValidationRulesets.Cancel,
        };

        try
        {
            // Standard validations for cancel
            var vehicle = await _vehicleRepository.GetByIdAsync(contract.vmf_code);
            if (vehicle != null)
            {
                var regValidation = await ValidateVehicleRegistrationNumberAsync(
                    vehicle.registration_number!
                );
                if (!regValidation.IsValid)
                    result.AddErrors(regValidation.Errors);
            }

            var siteValidation = await ValidateSiteCodeAsync(contract.site_code);
            if (!siteValidation.IsValid)
                result.AddErrors(siteValidation.Errors);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during contract cancel validation");
            result.AddError($"Validation error: {ex.Message}");
            return result;
        }
    }

    #endregion
}
