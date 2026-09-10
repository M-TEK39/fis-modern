using FIS.Core.Application.DTOs;
using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Service interface for vehicle operations
/// </summary>
public interface IVehicleService
{
    Task<VehicleDto?> GetVehicleAsync(int vmfCode);
    Task<VehicleDto?> GetVehicleByFleetNumberAsync(string fleetNumber);
    Task<IEnumerable<VehicleDto>> GetAvailableVehiclesAsync();
    Task<IEnumerable<VehicleDto>> SearchVehiclesAsync(string searchTerm);
    Task<VehicleDto> CreateVehicleAsync(CreateVehicleDto dto);
    Task<VehicleDto> UpdateVehicleAsync(int vmfCode, UpdateVehicleDto dto);
    Task DeleteVehicleAsync(int vmfCode);
    Task<bool> IsVehicleAvailableAsync(int vmfCode);
}

/// <summary>
/// Service interface for duplicate prevention operations
/// </summary>
public interface IDuplicatePreventionService
{
    Task<bool> CanCreateContractAsync(int vmfCode, DateTime startDate);
    Task<IEnumerable<Contract>> FindConflictingContractsAsync(
        int vmfCode,
        DateTime startDate,
        DateTime? endDate = null
    );
    Task<bool> HasActiveContractAsync(int vmfCode);
    Task<Contract?> GetActiveContractAsync(int vmfCode);
}
