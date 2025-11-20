using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Application.Services;

/// <summary>
/// Business service for Vehicle operations with validation and business rules
/// Implements complex business logic that goes beyond basic CRUD operations
/// </summary>
public class VehicleService
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IContractRepository _contractRepository;
    private readonly ILogger<VehicleService> _logger;

    public VehicleService(
        IVehicleRepository vehicleRepository,
        IContractRepository contractRepository,
        ILogger<VehicleService> logger)
    {
        _vehicleRepository = vehicleRepository ?? throw new ArgumentNullException(nameof(vehicleRepository));
        _contractRepository = contractRepository ?? throw new ArgumentNullException(nameof(contractRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get all vehicles with enhanced business information
    /// </summary>
    public async Task<IEnumerable<VehicleBusinessInfo>> GetVehiclesWithBusinessInfoAsync()
    {
        var vehicles = await _vehicleRepository.GetActiveVehiclesAsync();
        var result = new List<VehicleBusinessInfo>();

        foreach (var vehicle in vehicles)
        {
            var hasActiveContract = await _contractRepository.HasActiveContractAsync(vehicle.vmf_code);
            var activeContract = hasActiveContract 
                ? await _contractRepository.GetActiveContractByVehicleAsync(vehicle.vmf_code)
                : null;

            result.Add(new VehicleBusinessInfo
            {
                Vehicle = vehicle,
                IsCurrentlyHired = hasActiveContract,
                ActiveContract = activeContract,
                AvailabilityStatus = hasActiveContract ? "Hired" : "Available",
                CurrentMileage = vehicle.current_odo,
                DaysSinceLastService = vehicle.service_last_done.HasValue 
                    ? (DateTime.Now - vehicle.service_last_done.Value).Days 
                    : null
            });
        }

        return result;
    }

    /// <summary>
    /// Search vehicles with advanced filtering and business logic
    /// </summary>
    public async Task<IEnumerable<VehicleBusinessInfo>> SearchVehiclesAsync(VehicleSearchCriteria criteria)
    {
        var vehicles = await _vehicleRepository.SearchVehiclesAsync(criteria.SearchTerm ?? string.Empty);
        var result = new List<VehicleBusinessInfo>();

        foreach (var vehicle in vehicles)
        {
            var businessInfo = await CreateVehicleBusinessInfoAsync(vehicle);
            
            // Apply business filters
            if (criteria.AvailableOnly && businessInfo.IsCurrentlyHired)
                continue;
                
            if (criteria.MinMileage.HasValue && vehicle.current_odo < criteria.MinMileage.Value)
                continue;
                
            if (criteria.MaxMileage.HasValue && vehicle.current_odo > criteria.MaxMileage.Value)
                continue;

            result.Add(businessInfo);
        }

        return result;
    }

    /// <summary>
    /// Create a new vehicle with business validation
    /// </summary>
    public async Task<VehicleCreationResult> CreateVehicleAsync(VehicleCreationRequest request)
    {
        // Business validation
        var validationResult = await ValidateVehicleCreationAsync(request);
        if (!validationResult.IsValid)
        {
            return VehicleCreationResult.Failed(validationResult.Errors);
        }

        // Create vehicle entity
        var vehicle = new Vehicle
        {
            model_code = request.ModelCode,
            type_code = request.TypeCode,
            vehicle_status_code = request.VehicleStatusCode,
            location_code = request.LocationCode,
            fleet_number = request.FleetNumber,
            registration_number = request.RegistrationNumber,
            take_on_date = request.TakeOnDate ?? DateTime.Now,
            take_on_odo = request.TakeOnOdometer,
            current_odo = request.TakeOnOdometer,
            chassis_number = request.ChassisNumber,
            engine_number_1 = request.EngineNumber,
            year_manufactured = request.YearManufactured,
            purchase_date = request.PurchaseDate,
            purchase_amount = request.PurchaseAmount,
            book_value = request.BookValue ?? request.PurchaseAmount
        };

        try
        {
            var createdVehicle = await _vehicleRepository.CreateAsync(vehicle);
            _logger.LogInformation("Vehicle created successfully: VMF {VmfCode}, Fleet {FleetNumber}", 
                createdVehicle.vmf_code, createdVehicle.fleet_number);
            
            return VehicleCreationResult.Success(createdVehicle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create vehicle: Fleet {FleetNumber}, Registration {Registration}", 
                request.FleetNumber, request.RegistrationNumber);
            return VehicleCreationResult.Failed($"Database error: {ex.Message}");
        }
    }

    /// <summary>
    /// Update vehicle odometer with business rules
    /// </summary>
    public async Task<VehicleUpdateResult> UpdateOdometerAsync(int vmfCode, int newOdometer, string? notes = null)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode);
        if (vehicle == null)
        {
            return VehicleUpdateResult.NotFound();
        }

        // Business rule: New odometer must be higher than current
        if (newOdometer < vehicle.current_odo)
        {
            return VehicleUpdateResult.Failed("New odometer reading cannot be lower than current reading");
        }

        // Business rule: Check for unrealistic jumps (more than 10,000km)
        var odometerIncrease = newOdometer - vehicle.current_odo;
        if (odometerIncrease > 10000)
        {
            _logger.LogWarning("Large odometer increase detected: VMF {VmfCode}, increase {Increase}km", 
                vmfCode, odometerIncrease);
        }

        // Update vehicle
        vehicle.current_odo = newOdometer;
        vehicle.odo_update_date = DateTime.Now;
        
        try
        {
            await _vehicleRepository.UpdateAsync(vehicle);
            _logger.LogInformation("Odometer updated: VMF {VmfCode}, new reading {NewOdometer}", 
                vmfCode, newOdometer);
            
            return VehicleUpdateResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update odometer for VMF {VmfCode}", vmfCode);
            return VehicleUpdateResult.Failed($"Database error: {ex.Message}");
        }
    }

    /// <summary>
    /// Get vehicles that need service based on business rules
    /// </summary>
    public async Task<IEnumerable<VehicleServiceAlert>> GetVehiclesNeedingServiceAsync()
    {
        var vehicles = await _vehicleRepository.GetActiveVehiclesAsync();
        var alerts = new List<VehicleServiceAlert>();

        foreach (var vehicle in vehicles)
        {
            var alert = CheckVehicleServiceRequirements(vehicle);
            if (alert != null)
            {
                alerts.Add(alert);
            }
        }

        return alerts.OrderByDescending(a => a.Priority);
    }

    // Private helper methods

    private async Task<VehicleBusinessInfo> CreateVehicleBusinessInfoAsync(Vehicle vehicle)
    {
        var hasActiveContract = await _contractRepository.HasActiveContractAsync(vehicle.vmf_code);
        var activeContract = hasActiveContract 
            ? await _contractRepository.GetActiveContractByVehicleAsync(vehicle.vmf_code)
            : null;

        return new VehicleBusinessInfo
        {
            Vehicle = vehicle,
            IsCurrentlyHired = hasActiveContract,
            ActiveContract = activeContract,
            AvailabilityStatus = hasActiveContract ? "Hired" : "Available",
            CurrentMileage = vehicle.current_odo,
            DaysSinceLastService = vehicle.service_last_done.HasValue 
                ? (DateTime.Now - vehicle.service_last_done.Value).Days 
                : null
        };
    }

    private async Task<ValidationResult> ValidateVehicleCreationAsync(VehicleCreationRequest request)
    {
        var errors = new List<string>();

        // Required field validation
        if (string.IsNullOrWhiteSpace(request.FleetNumber))
            errors.Add("Fleet number is required");

        if (string.IsNullOrWhiteSpace(request.RegistrationNumber))
            errors.Add("Registration number is required");

        // Business rule validation
        if (!string.IsNullOrWhiteSpace(request.FleetNumber))
        {
            var existingFleet = await _vehicleRepository.GetByFleetNumberAsync(request.FleetNumber);
            if (existingFleet != null)
                errors.Add($"Fleet number {request.FleetNumber} already exists");
        }

        if (!string.IsNullOrWhiteSpace(request.RegistrationNumber))
        {
            var existingReg = await _vehicleRepository.GetByRegistrationNumberAsync(request.RegistrationNumber);
            if (existingReg != null)
                errors.Add($"Registration number {request.RegistrationNumber} already exists");
        }

        // Odometer validation
        if (request.TakeOnOdometer < 0)
            errors.Add("Take-on odometer cannot be negative");

        return new ValidationResult(errors.Count == 0, errors);
    }

    private VehicleServiceAlert? CheckVehicleServiceRequirements(Vehicle vehicle)
    {
        // Service interval business rules (example: service every 10,000km or 6 months)
        var kmSinceService = vehicle.service_last_odo.HasValue 
            ? vehicle.current_odo - vehicle.service_last_odo.Value 
            : vehicle.current_odo;

        var daysSinceService = vehicle.service_last_done.HasValue 
            ? (DateTime.Now - vehicle.service_last_done.Value).Days 
            : (DateTime.Now - vehicle.take_on_date).Days;

        if (kmSinceService > 10000)
        {
            return new VehicleServiceAlert
            {
                VmfCode = vehicle.vmf_code,
                FleetNumber = vehicle.fleet_number,
                AlertType = "Service Due - Mileage",
                Message = $"Service due: {kmSinceService}km since last service",
                Priority = kmSinceService > 15000 ? 3 : 2
            };
        }

        if (daysSinceService > 180) // 6 months
        {
            return new VehicleServiceAlert
            {
                VmfCode = vehicle.vmf_code,
                FleetNumber = vehicle.fleet_number,
                AlertType = "Service Due - Time",
                Message = $"Service due: {daysSinceService} days since last service",
                Priority = daysSinceService > 270 ? 3 : 2 // 9 months = high priority
            };
        }

        return null;
    }
}

// Supporting classes
public class VehicleBusinessInfo
{
    public Vehicle Vehicle { get; set; } = null!;
    public bool IsCurrentlyHired { get; set; }
    public Contract? ActiveContract { get; set; }
    public string AvailabilityStatus { get; set; } = string.Empty;
    public int CurrentMileage { get; set; }
    public int? DaysSinceLastService { get; set; }
}

public class VehicleSearchCriteria
{
    public string? SearchTerm { get; set; }
    public bool AvailableOnly { get; set; }
    public int? MinMileage { get; set; }
    public int? MaxMileage { get; set; }
}

public class VehicleCreationRequest
{
    public short ModelCode { get; set; }
    public short TypeCode { get; set; }
    public short VehicleStatusCode { get; set; }
    public short LocationCode { get; set; }
    public string FleetNumber { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public DateTime? TakeOnDate { get; set; }
    public int TakeOnOdometer { get; set; }
    public string? ChassisNumber { get; set; }
    public string? EngineNumber { get; set; }
    public short? YearManufactured { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchaseAmount { get; set; }
    public decimal? BookValue { get; set; }
}

public class VehicleCreationResult
{
    public bool IsSuccess { get; set; }
    public Vehicle? Vehicle { get; set; }
    public List<string> Errors { get; set; } = new();

    public static VehicleCreationResult Success(Vehicle vehicle) => 
        new() { IsSuccess = true, Vehicle = vehicle };

    public static VehicleCreationResult Failed(string error) => 
        new() { IsSuccess = false, Errors = new List<string> { error } };

    public static VehicleCreationResult Failed(List<string> errors) => 
        new() { IsSuccess = false, Errors = errors };
}

public class VehicleUpdateResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }

    public static VehicleUpdateResult Success() => new() { IsSuccess = true };
    public static VehicleUpdateResult Failed(string error) => new() { IsSuccess = false, ErrorMessage = error };
    public static VehicleUpdateResult NotFound() => new() { IsSuccess = false, ErrorMessage = "Vehicle not found" };
}

public class VehicleServiceAlert
{
    public int VmfCode { get; set; }
    public string? FleetNumber { get; set; }
    public string AlertType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Priority { get; set; } // 1=Low, 2=Medium, 3=High
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; }

    public ValidationResult(bool isValid, List<string> errors)
    {
        IsValid = isValid;
        Errors = errors;
    }
}