using FIS.Core.Application.Services.Validation;
using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Contract validation service interface
/// Implements all legacy validation rules from GGFleet.BLL.Contract
/// Maps legacy stored procedures to modern EF Core queries
/// </summary>
public interface IContractValidationService
{
    /// <summary>
    /// Validates if a contract exists for the given registration number
    /// Legacy SP: NEW_DEV_SEL_ContractByReg
    /// </summary>
    Task<ContractValidationResult> ValidateContractRegistrationNumberAsync(string registrationNumber);

    /// <summary>
    /// Validates if the registration number exists in the vehicle table
    /// Legacy SP: NEW_DEV_VAL_Registration
    /// </summary>
    Task<ContractValidationResult> ValidateVehicleRegistrationNumberAsync(string registrationNumber);

    /// <summary>
    /// Checks for duplicate open contracts for a vehicle
    /// Legacy SP: NEW_DEV_VAL_DuplicateContract
    /// </summary>
    Task<ContractValidationResult> ValidateDuplicateContractAsync(int vmfCode);

    /// <summary>
    /// Checks for open trip authorities before closing a contract
    /// Legacy SP: NEW_DEV_VAL_OpenTripAuthority
    /// </summary>
    Task<ContractValidationResult> ValidateNoOpenTripAuthoritiesAsync(int contractCode);

    /// <summary>
    /// Validates site code and department code
    /// Legacy SP: NEW_DEV_VAL_Site
    /// </summary>
    Task<ContractValidationResult> ValidateSiteCodeAsync(short siteCode);

    /// <summary>
    /// Validates contract start date against previous contracts
    /// Legacy SP: NEW_DEV_VAL_Contracts
    /// </summary>
    Task<ContractValidationResult> ValidateStartDateAsync(int vmfCode, DateTime startDate);

    /// <summary>
    /// Validates start date for modify operation (less restrictive)
    /// Legacy SP: NEW_DEV_VAL_Contracts
    /// </summary>
    Task<ContractValidationResult> ValidateStartDateModifyAsync(int vmfCode, DateTime startDate);

    /// <summary>
    /// Validates contract end date when closing
    /// Legacy SP: NEW_DEV_VAL_Contracts
    /// </summary>
    Task<ContractValidationResult> ValidateEndDateAsync(int vmfCode, DateTime endDate);

    /// <summary>
    /// Validates start odometer against previous contract's end odometer
    /// Legacy SP: NEW_DEV_VAL_Contracts
    /// </summary>
    Task<ContractValidationResult> ValidateStartOdometerAsync(int vmfCode, int startOdometer);

    /// <summary>
    /// Validates end odometer when closing contract
    /// Legacy SP: NEW_DEV_VAL_Contracts
    /// </summary>
    Task<ContractValidationResult> ValidateEndOdometerCloseAsync(int vmfCode, int endOdometer);

    /// <summary>
    /// Validates end odometer is zero when adding new contract
    /// </summary>
    Task<ContractValidationResult> ValidateEndOdometerAddAsync(int endOdometer);

    /// <summary>
    /// Validates still_current field is 'Y' when adding
    /// </summary>
    Task<ContractValidationResult> ValidateStillCurrentAddAsync(string? stillCurrent);

    /// <summary>
    /// Validates still_current field is 'N' when closing
    /// </summary>
    Task<ContractValidationResult> ValidateStillCurrentCloseAsync(string? stillCurrent);

    /// <summary>
    /// Validates target return date is today or in the future
    /// </summary>
    Task<ContractValidationResult> ValidateTargetReturnDateAsync(DateTime targetReturnDate);

    /// <summary>
    /// Comprehensive validation for adding a new contract
    /// Runs all applicable validation rules
    /// </summary>
    Task<ContractValidationResult> ValidateContractAddAsync(Contract contract);

    /// <summary>
    /// Comprehensive validation for modifying a contract
    /// Runs all applicable validation rules
    /// </summary>
    Task<ContractValidationResult> ValidateContractModifyAsync(Contract contract);

    /// <summary>
    /// Comprehensive validation for closing a contract
    /// Runs all applicable validation rules
    /// </summary>
    Task<ContractValidationResult> ValidateContractCloseAsync(Contract contract);

    /// <summary>
    /// Comprehensive validation for splitting a contract
    /// Runs all applicable validation rules
    /// </summary>
    Task<ContractValidationResult> ValidateContractSplitAsync(Contract contract);

    /// <summary>
    /// Comprehensive validation for extending a contract
    /// Runs all applicable validation rules
    /// </summary>
    Task<ContractValidationResult> ValidateContractExtendAsync(Contract contract);

    /// <summary>
    /// Comprehensive validation for cancelling a contract
    /// Runs all applicable validation rules
    /// </summary>
    Task<ContractValidationResult> ValidateContractCancelAsync(Contract contract);
}
