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
        string? notes = null
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
    public int? StartOdometer { get; set; }
    public string? DriverId { get; set; }
    public string? DriverName { get; set; }
    public int? SiteDriverCode { get; set; }
    public short? UserCode { get; set; }
    public string? Authorisation { get; set; }
    public string? Notes { get; set; }
    public DateTime? TargetReturnDate { get; set; }

    /// <summary>
    /// The user who is capturing/creating this contract.
    /// Required for self-approval prevention — stored as created_by_user_code.
    /// </summary>
    public int? CreatedByUserId { get; set; }
}
