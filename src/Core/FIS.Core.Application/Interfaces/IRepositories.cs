using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Drivers;
using FIS.Core.Domain.Entities.Financial;
using TypeEntity = FIS.Core.Domain.Entities.ReferenceData.VehicleType;

namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Repository interface for vehicle operations
/// </summary>
public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdAsync(int vmfCode);
    Task<Vehicle?> GetByFleetNumberAsync(string fleetNumber);
    Task<Vehicle?> GetByRegistrationNumberAsync(string registrationNumber);
    Task<IEnumerable<Vehicle>> GetActiveVehiclesAsync();
    Task<IEnumerable<Vehicle>> GetAvailableVehiclesAsync();
    Task<IEnumerable<Vehicle>> GetAllAsync();
    Task<IEnumerable<Vehicle>> SearchVehiclesAsync(string searchTerm);
    Task<Vehicle> CreateAsync(Vehicle vehicle, int currentUserId);
    Task UpdateAsync(Vehicle vehicle, int currentUserId);
    Task DeleteAsync(int vmfCode, int currentUserId);
}

/// <summary>
/// Repository interface for contract operations
/// </summary>
public interface IContractRepository
{
    Task<Contract?> GetByIdAsync(int contractCode);
    Task<IEnumerable<Contract>> GetActiveContractsAsync();
    Task<IEnumerable<Contract>> GetContractsByVehicleAsync(int vmfCode);
    Task<IEnumerable<Contract>> GetAllAsync();
    Task<Contract?> GetActiveContractByVehicleAsync(int vmfCode);
    Task<bool> HasActiveContractAsync(int vmfCode);
    Task<Contract> CreateAsync(Contract contract, int currentUserId);
    Task UpdateAsync(Contract contract, int currentUserId);
    Task DeleteAsync(int contractCode, int currentUserId);
    Task EndContractAsync(int contractCode, DateTime endDate, int? endOdometer = null, string? notes = null);
}

/// <summary>
/// Repository interface for user operations
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(int userAccessCode);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByTelephoneAsync(string telephone);
    Task<IEnumerable<User>> GetAllUsersAsync();
    Task<User> CreateAsync(User user, int currentUserId);
    Task UpdateAsync(User user, int currentUserId);
    Task DeleteAsync(int userAccessCode, int currentUserId);
}

/// <summary>
/// Repository interface for site operations
/// </summary>
public interface ISiteRepository
{
    Task<Site?> GetByIdAsync(int siteCode);
    Task<Site?> GetByNameAsync(string siteName);
    Task<IEnumerable<Site>> GetActiveSitesAsync();
    Task<IEnumerable<Site>> SearchSitesAsync(string searchTerm);
    Task<Site> CreateAsync(Site site, int currentUserId);
    Task UpdateAsync(Site site, int currentUserId);
    Task DeleteAsync(int siteCode, int currentUserId);
}

/// <summary>
/// Repository interface for department operations
/// </summary>
public interface IDepartmentRepository
{
    Task<Department?> GetByIdAsync(int departmentCode);
    Task<Department?> GetByNameAsync(string departmentName);
    Task<IEnumerable<Department>> GetActiveDepartmentsAsync();
    Task<IEnumerable<Department>> GetByCompanyAsync(int companyCode);
    Task<IEnumerable<Department>> SearchDepartmentsAsync(string searchTerm);
    Task<Department> CreateAsync(Department department, int currentUserId);
    Task UpdateAsync(Department department, int currentUserId);
    Task DeleteAsync(int departmentCode, int currentUserId);
}

/// <summary>
/// Repository interface for driver operations
/// </summary>
public interface IDriverRepository
{
    Task<Driver?> GetByIdAsync(string driverId);
    Task<Driver?> GetByLicenceNumberAsync(string licenceNumber);
    Task<IEnumerable<Driver>> GetActiveDriversAsync();
    Task<IEnumerable<Driver>> SearchDriversAsync(string searchTerm);
    Task<Driver> CreateAsync(Driver driver, int currentUserId);
    Task UpdateAsync(Driver driver, int currentUserId);
    Task DeleteAsync(string driverId, int currentUserId);
}

/// <summary>
/// Repository interface for trip operations
/// </summary>
public interface ITripRepository
{
    Task<Trip?> GetByIdAsync(int tripId);
    Task<IEnumerable<Trip>> GetAllAsync();
    Task<IEnumerable<Trip>> GetTripsByVehicleAsync(int vmfCode);
    Task<IEnumerable<Trip>> GetTripsByDriverAsync(string driverId);
    Task<IEnumerable<Trip>> GetTripsByContractAsync(int contractCode);
    Task<IEnumerable<Trip>> GetTripsByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<Trip> CreateAsync(Trip trip, int currentUserId);
    Task UpdateAsync(Trip trip, int currentUserId);
    Task DeleteAsync(int tripId, int currentUserId);
}

/// <summary>
/// Repository interface for fuel card operations
/// </summary>
public interface IFuelCardRepository
{
    Task<FuelCard?> GetByIdAsync(int fuelCardId);
    Task<FuelCard?> GetByCardNumberAsync(string cardNumber);
    Task<IEnumerable<FuelCard>> GetActiveFuelCardsAsync();
    Task<IEnumerable<FuelCard>> GetFuelCardsByVehicleAsync(int vmfCode);
    Task<FuelCard> CreateAsync(FuelCard fuelCard, int currentUserId);
    Task UpdateAsync(FuelCard fuelCard, int currentUserId);
    Task DeleteAsync(int fuelCardId, int currentUserId);
}

/// <summary>
/// Repository interface for make operations
/// </summary>
public interface IMakeRepository
{
    Task<Make?> GetByIdAsync(short makeCode);
    Task<Make?> GetByNameAsync(string makeName);
    Task<IEnumerable<Make>> GetAllMakesAsync();
    Task<IEnumerable<Make>> SearchMakesAsync(string searchTerm);
    Task<Make> CreateAsync(Make make, int currentUserId);
    Task<Make> UpdateAsync(Make make, int currentUserId);
    Task DeleteAsync(short makeCode, int currentUserId);
}

/// <summary>
/// Repository interface for model operations
/// </summary>
public interface IModelRepository
{
    Task<Model?> GetByIdAsync(short modelCode);
    Task<Model?> GetByNameAsync(string modelName);
    Task<IEnumerable<Model>> GetAllModelsAsync();
    Task<IEnumerable<Model>> GetModelsByMakeAsync(short makeCode);
    Task<IEnumerable<Model>> GetModelsByEngineTypeAsync(string engineType);
    Task<IEnumerable<Model>> SearchModelsAsync(string searchTerm);
    Task<Model> CreateAsync(Model model, int currentUserId);
    Task<Model> UpdateAsync(Model model, int currentUserId);
    Task DeleteAsync(short modelCode, int currentUserId);
}

/// <summary>
/// Repository interface for type operations
/// </summary>
public interface ITypeRepository
{
    Task<TypeEntity?> GetByIdAsync(short typeCode);
    Task<TypeEntity?> GetByNameAsync(string typeName);
    Task<IEnumerable<TypeEntity>> GetAllTypesAsync();
    Task<IEnumerable<TypeEntity>> SearchTypesAsync(string searchTerm);
    Task<TypeEntity> CreateAsync(TypeEntity type, int currentUserId);
    Task<TypeEntity> UpdateAsync(TypeEntity type, int currentUserId);
    Task DeleteAsync(short typeCode, int currentUserId);
}

/// <summary>
/// Repository interface for trip driver operations
/// </summary>
public interface ITripDriverRepository
{
    Task<TripDriver?> GetByIdAsync(int tripDriverCode);
    Task<TripDriver?> GetByNameAsync(string tripDriverName);
    Task<IEnumerable<TripDriver>> GetByTripAuthorityAsync(int tripAuthorityCode);
    Task<IEnumerable<TripDriver>> GetBySiteAsync(int siteCode);
    Task<IEnumerable<TripDriver>> GetPrimaryDriversAsync();
    Task<IEnumerable<TripDriver>> GetActiveDriversAsync();
    Task<IEnumerable<TripDriver>> GetByLicenseTypeAsync(int licenseTypeId);
    Task<TripDriver> CreateAsync(TripDriver tripDriver, int currentUserId);
    Task UpdateAsync(TripDriver tripDriver, int currentUserId);
    Task DeleteAsync(int tripDriverCode, int currentUserId);
    Task<IEnumerable<TripDriver>> SearchDriversAsync(string searchTerm);
}

/// <summary>
/// Repository interface for private hire operations
/// </summary>
public interface IPrivateHireRepository
{
    Task<PrivateHire?> GetByIdAsync(int privateHireCode);
    Task<IEnumerable<PrivateHire>> GetByVehicleAsync(int vmfCode);
    Task<IEnumerable<PrivateHire>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<PrivateHire>> GetActiveHiresAsync();
    Task<PrivateHire> CreateAsync(PrivateHire privateHire, int currentUserId);
    Task UpdateAsync(PrivateHire privateHire, int currentUserId);
    Task DeleteAsync(int privateHireCode, int currentUserId);
    Task<IEnumerable<PrivateHire>> SearchHiresAsync(string searchTerm);
}

/// <summary>
/// Repository interface for location operations
/// </summary>
public interface ILocationRepository
{
    Task<Location?> GetByIdAsync(int locationId);
    Task<Location?> GetByNameAsync(string locationName);
    Task<IEnumerable<Location>> GetAllLocationsAsync();
    Task<IEnumerable<Location>> GetByCountryAsync(string country);
    Task<IEnumerable<Location>> GetByProvinceAsync(string province);
    Task<Location> CreateAsync(Location location, int currentUserId);
    Task UpdateAsync(Location location, int currentUserId);
    Task DeleteAsync(int locationId, int currentUserId);
    Task<IEnumerable<Location>> SearchLocationsAsync(string searchTerm);
}

/// <summary>
/// Repository interface for maintenance record operations
/// </summary>
public interface IMaintenanceRecordRepository
{
    Task<MaintenanceRecord?> GetByIdAsync(int maintenanceId);
    Task<IEnumerable<MaintenanceRecord>> GetByVehicleAsync(int vmfCode);
    Task<IEnumerable<MaintenanceRecord>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<MaintenanceRecord>> GetByServiceTypeAsync(string serviceType);
    Task<IEnumerable<MaintenanceRecord>> GetAllAsync();
    Task<MaintenanceRecord> CreateAsync(MaintenanceRecord maintenanceRecord, int currentUserId);
    Task UpdateAsync(MaintenanceRecord maintenanceRecord, int currentUserId);
    Task DeleteAsync(int maintenanceId, int currentUserId);
    Task<IEnumerable<MaintenanceRecord>> SearchMaintenanceRecordsAsync(string searchTerm);
}

/// <summary>
/// Repository interface for journal detail operations (financial transactions)
/// </summary>
public interface IJournalDetailRepository
{
    Task<JournalDetail?> GetByIdAsync(int journalDetailId);
    Task<JournalDetail?> GetByCodeAsync(Guid journalDetailCode);
    Task<IEnumerable<JournalDetail>> GetByVehicleAsync(int vmfCode);
    Task<IEnumerable<JournalDetail>> GetBySiteAsync(short siteCode);
    Task<IEnumerable<JournalDetail>> GetByDepartmentAsync(int departmentCode);
    Task<IEnumerable<JournalDetail>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<JournalDetail>> GetByFinancialYearAsync(string financialYear);
    Task<IEnumerable<JournalDetail>> GetReversalsForJournalAsync(Guid journalDetailCode);
    Task<JournalDetail> CreateAsync(JournalDetail journalDetail, int currentUserId);
    Task UpdateAsync(JournalDetail journalDetail, int currentUserId);
    Task DeleteAsync(int journalDetailId, int currentUserId);
}