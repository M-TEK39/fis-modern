namespace FIS.Core.Application.Services.Validation;

/// <summary>
/// Represents the result of a contract validation operation
/// Supports both single and multiple validation errors
/// </summary>
public class ContractValidationResult
{
    public bool IsValid { get; private set; }
    public List<string> Errors { get; private set; }
    public List<string> Warnings { get; private set; }
    public string? ValidationRuleset { get; set; }

    public ContractValidationResult()
    {
        IsValid = true;
        Errors = new List<string>();
        Warnings = new List<string>();
    }

    public static ContractValidationResult Success()
    {
        return new ContractValidationResult { IsValid = true };
    }

    public static ContractValidationResult Failed(string error)
    {
        return new ContractValidationResult
        {
            IsValid = false,
            Errors = new List<string> { error }
        };
    }

    public static ContractValidationResult Failed(List<string> errors)
    {
        return new ContractValidationResult
        {
            IsValid = false,
            Errors = errors
        };
    }

    public void AddError(string error)
    {
        IsValid = false;
        Errors.Add(error);
    }

    public void AddWarning(string warning)
    {
        Warnings.Add(warning);
    }

    public void AddErrors(IEnumerable<string> errors)
    {
        foreach (var error in errors)
        {
            AddError(error);
        }
    }

    public string GetErrorMessage()
    {
        return string.Join("; ", Errors);
    }

    public string GetWarningMessage()
    {
        return string.Join("; ", Warnings);
    }

    public override string ToString()
    {
        if (IsValid)
            return "Validation passed";

        return $"Validation failed: {GetErrorMessage()}";
    }
}

/// <summary>
/// Validation rulesets matching legacy system validation points
/// </summary>
public static class ContractValidationRulesets
{
    public const string RegistrationNo = "ValContractRegistrationNo";
    public const string VehicleRegistrationNo = "ValVehicleRegistrationNo";
    public const string VehicleDuplicateContract = "ValVehicleDuplicateContract";

    public const string PreAdd = "ValContractPreAdd";
    public const string Add = "ValContractAdd";

    public const string PreModify = "ValContractPreModify";
    public const string Modify = "ValContractModify";

    public const string PreAllocation = "ValContractPreAllocation";
    public const string Allocation = "ValContractAllocation";

    public const string PreBackDate = "ValContractPreBackDate";
    public const string BackDate = "ValContractBackDate";

    public const string PreCancel = "ValContractPreCancel";
    public const string Cancel = "ValContractCancel";

    public const string PreClose = "ValContractPreClose";
    public const string Close = "ValContractClose";

    public const string PreExtend = "ValContractPreExtend";
    public const string Extend = "ValContractExtend";

    public const string PreMoveVehicle = "ValContractPreMoveVehicle";
    public const string MoveVehicle = "ValContractMoveVehicle";

    public const string PreSplit = "ValContractPreSplit";
    public const string Split = "ValContractSplit";
}
