using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces
{
    /// <summary>
    /// Repository interface for Clearance entity operations
    /// Provides contract for CRUD operations on vehicle clearances
    /// </summary>
    public interface IClearanceRepository
    {
        Task<Clearance?> GetByIdAsync(int clearanceCode);
        Task<IEnumerable<Clearance>> GetAllAsync();
        Task<IEnumerable<Clearance>> GetByVehicleAsync(int vmfCode);
        Task<ClearanceLookupResult?> LookupVehicleAsync(string fleetOrReg);
        Task<Clearance> CreateAsync(Clearance clearance, int currentUserId);
        Task<Clearance> UpdateAsync(Clearance clearance, int currentUserId);
        Task DeleteAsync(int clearanceCode, int currentUserId);
    }

    /// <summary>
    /// Result of vehicle lookup for clearance operations
    /// </summary>
    public class ClearanceLookupResult
    {
        public int vmf_code { get; set; }
        public string? fleet_number { get; set; }
        public string? registration_number { get; set; }
    }
}
