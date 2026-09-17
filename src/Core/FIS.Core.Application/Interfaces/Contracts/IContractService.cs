using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Business service interface for contract/hire workflows
/// </summary>
public interface IContractService
{
    /// <summary>
    /// Create a hire contract for a vehicle
    /// </summary>
    Task<Contract?> HireVehicleAsync(HireContractRequest request);

    /// <summary>
    /// End the active contract for a vehicle (by vmfCode)
    /// </summary>
    Task<bool> EndContractByVmfCodeAsync(
        int vmfCode,
        int? endOdometer = null,
        string? notes = null,
        int currentUserId = 0
    );

    /// <summary>
    /// Get currently active contracts
    /// </summary>
    Task<IEnumerable<Contract>> GetActiveContractsAsync();
}

public class HireContractRequest
{
    public int VmfCode { get; set; }
    public short SiteCode { get; set; }
    /// <summary>
    /// The calendar-selected contract start date. Legacy capture defaults this
    /// to the current day, but permits the operator to choose a valid date;
    /// backdating uses <see cref="BackdatingStartDate"/> and takes precedence.
    /// </summary>
    public DateTime? StartDate { get; set; }
    public int? StartOdometer { get; set; }
    public string? DriverId { get; set; }
    public string? DriverName { get; set; }
    public int? SiteDriverCode { get; set; }
    public short? UserCode { get; set; }
    public string? Authorisation { get; set; }
    public string? Notes { get; set; }
    public DateTime? TargetReturnDate { get; set; }

    /// <summary>
    /// Optional legacy backdating request. When supplied, the compatibility
    /// repository passes it to the extended approval procedure if that
    /// deployed procedure signature exposes the archived fields.
    /// </summary>
    public DateTime? BackdatingStartDate { get; set; }
    public DateTime? BackdatingRequestedDate { get; set; }

    /// <summary>
    /// The legacy configured-tariff workflow supplies the billable contract
    /// type (A/B/C/F/L). The API resolves it from the source database before
    /// capture. An explicit type is accepted for an expanded database or for
    /// a legacy restore where the mapping object is absent; no modern-only
    /// contract type is invented.
    /// </summary>
    public string? ContractType { get; set; }

    /// <summary>
    /// The user who is capturing/creating this contract.
    /// Required for self-approval prevention — stored as created_by_user_code.
    /// </summary>
    public int? CreatedByUserId { get; set; }
}
