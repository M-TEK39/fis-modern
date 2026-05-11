using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Application.Services;

/// <summary>
/// Maintenance Service implementation
/// Handles vehicle maintenance scheduling, cost tracking, compliance, and service history
/// </summary>
public class MaintenanceService : IMaintenanceService
{
    private readonly IMaintenanceRecordRepository _maintenanceRecordRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<MaintenanceService> _logger;

    public MaintenanceService(
        IMaintenanceRecordRepository maintenanceRecordRepository,
        IVehicleRepository vehicleRepository,
        ICurrentUserContext currentUserContext,
        ILogger<MaintenanceService> logger)
    {
        _maintenanceRecordRepository = maintenanceRecordRepository ?? throw new ArgumentNullException(nameof(maintenanceRecordRepository));
        _vehicleRepository = vehicleRepository ?? throw new ArgumentNullException(nameof(vehicleRepository));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Create a new maintenance record with automatic next service calculation
    /// </summary>
    public async Task<MaintenanceRecord> CreateMaintenanceRecordAsync(MaintenanceRecord maintenanceRecord)
    {
        try
        {
            _logger.LogInformation("Creating maintenance record for vehicle: {VmfCode}, Type: {MaintenanceType}",
                maintenanceRecord.VmfCode, maintenanceRecord.MaintenanceType);

            // Set creation timestamp
            maintenanceRecord.CreatedDate = DateTime.UtcNow;

            // Calculate next service dates if intervals are provided
            if (maintenanceRecord.ServiceIntervalDays.HasValue && maintenanceRecord.ServiceIntervalDays.Value > 0)
            {
                maintenanceRecord.NextServiceDate = CalculateNextServiceDate(
                    maintenanceRecord.MaintenanceDate,
                    maintenanceRecord.ServiceIntervalDays.Value);
            }

            if (maintenanceRecord.ServiceIntervalKm.HasValue && maintenanceRecord.ServiceIntervalKm.Value > 0)
            {
                maintenanceRecord.NextServiceOdometer = CalculateNextServiceOdometer(
                    maintenanceRecord.OdometerReading,
                    maintenanceRecord.ServiceIntervalKm.Value);
            }

            // Set default status if not provided
            if (string.IsNullOrEmpty(maintenanceRecord.Status))
            {
                maintenanceRecord.Status = "COMPLETED";
            }

            var created = await _maintenanceRecordRepository.CreateAsync(maintenanceRecord, _currentUserContext.GetCurrentUserIdOrDefault());

            _logger.LogInformation("Maintenance record created: ID {MaintenanceId}, Next service: {NextServiceDate} / {NextServiceOdometer}km",
                created.MaintenanceId, created.NextServiceDate, created.NextServiceOdometer);

            return created;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating maintenance record for vehicle: {VmfCode}", maintenanceRecord.VmfCode);
            throw;
        }
    }

    /// <summary>
    /// Update an existing maintenance record
    /// </summary>
    public async Task UpdateMaintenanceRecordAsync(MaintenanceRecord maintenanceRecord)
    {
        try
        {
            _logger.LogInformation("Updating maintenance record: {MaintenanceId}", maintenanceRecord.MaintenanceId);

            // Set modification timestamp
            maintenanceRecord.ModifiedDate = DateTime.UtcNow;

            // Recalculate next service dates if intervals changed
            if (maintenanceRecord.ServiceIntervalDays.HasValue && maintenanceRecord.ServiceIntervalDays.Value > 0)
            {
                maintenanceRecord.NextServiceDate = CalculateNextServiceDate(
                    maintenanceRecord.MaintenanceDate,
                    maintenanceRecord.ServiceIntervalDays.Value);
            }

            if (maintenanceRecord.ServiceIntervalKm.HasValue && maintenanceRecord.ServiceIntervalKm.Value > 0)
            {
                maintenanceRecord.NextServiceOdometer = CalculateNextServiceOdometer(
                    maintenanceRecord.OdometerReading,
                    maintenanceRecord.ServiceIntervalKm.Value);
            }

            await _maintenanceRecordRepository.UpdateAsync(maintenanceRecord, _currentUserContext.GetCurrentUserIdOrDefault());

            _logger.LogInformation("Maintenance record updated: {MaintenanceId}", maintenanceRecord.MaintenanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating maintenance record: {MaintenanceId}", maintenanceRecord.MaintenanceId);
            throw;
        }
    }

    /// <summary>
    /// Get maintenance record by ID
    /// </summary>
    public async Task<MaintenanceRecord?> GetMaintenanceRecordByIdAsync(int maintenanceId)
    {
        return await _maintenanceRecordRepository.GetByIdAsync(maintenanceId);
    }

    /// <summary>
    /// Get all maintenance records for a vehicle
    /// </summary>
    public async Task<IEnumerable<MaintenanceRecord>> GetMaintenanceHistoryAsync(int vmfCode)
    {
        return await _maintenanceRecordRepository.GetByVehicleAsync(vmfCode);
    }

    /// <summary>
    /// Calculate next service date based on last service and interval
    /// </summary>
    public DateTime? CalculateNextServiceDate(DateTime lastServiceDate, int intervalDays)
    {
        if (intervalDays <= 0)
        {
            return null;
        }

        return lastServiceDate.AddDays(intervalDays);
    }

    /// <summary>
    /// Calculate next service odometer based on last reading and interval
    /// </summary>
    public int? CalculateNextServiceOdometer(int lastOdometerReading, int intervalKm)
    {
        if (intervalKm <= 0)
        {
            return null;
        }

        return lastOdometerReading + intervalKm;
    }

    /// <summary>
    /// Check if a vehicle is due for maintenance
    /// Checks both date-based and odometer-based criteria
    /// </summary>
    public async Task<bool> IsMaintenanceDueAsync(int vmfCode, int currentOdometer)
    {
        try
        {
            var lastService = await GetLastServiceAsync(vmfCode);
            if (lastService == null)
            {
                // No service history - consider due for initial service
                return true;
            }

            // Check date-based criteria
            if (lastService.NextServiceDate.HasValue)
            {
                if (DateTime.Now >= lastService.NextServiceDate.Value)
                {
                    _logger.LogInformation("Vehicle {VmfCode} due for service by date: {NextServiceDate}",
                        vmfCode, lastService.NextServiceDate.Value);
                    return true;
                }
            }

            // Check odometer-based criteria
            if (lastService.NextServiceOdometer.HasValue)
            {
                if (currentOdometer >= lastService.NextServiceOdometer.Value)
                {
                    _logger.LogInformation("Vehicle {VmfCode} due for service by odometer: {CurrentOdometer}/{NextServiceOdometer}km",
                        vmfCode, currentOdometer, lastService.NextServiceOdometer.Value);
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if maintenance is due for vehicle: {VmfCode}", vmfCode);
            throw;
        }
    }

    /// <summary>
    /// Get vehicles due for maintenance
    /// </summary>
    public async Task<IEnumerable<MaintenanceRecord>> GetDueMaintenanceAsync(int daysAhead = 30)
    {
        try
        {
            _logger.LogInformation("Getting maintenance due within {DaysAhead} days", daysAhead);

            var cutoffDate = DateTime.Now.AddDays(daysAhead);

            // Get all completed maintenance records with next service dates
            var allRecords = await _maintenanceRecordRepository.GetByServiceTypeAsync("SERVICE");

            var dueRecords = allRecords.Where(mr =>
                mr.Status == "COMPLETED" &&
                (mr.NextServiceDate.HasValue && mr.NextServiceDate.Value <= cutoffDate)
            ).ToList();

            _logger.LogInformation("Found {Count} vehicles due for maintenance", dueRecords.Count);

            return dueRecords;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting due maintenance");
            throw;
        }
    }

    /// <summary>
    /// Get overdue maintenance records
    /// </summary>
    public async Task<IEnumerable<MaintenanceRecord>> GetOverdueMaintenanceAsync()
    {
        try
        {
            _logger.LogInformation("Getting overdue maintenance");

            var allRecords = await _maintenanceRecordRepository.GetByServiceTypeAsync("SERVICE");

            var overdueRecords = allRecords.Where(mr =>
                mr.Status == "COMPLETED" &&
                (mr.NextServiceDate.HasValue && mr.NextServiceDate.Value < DateTime.Now)
            ).ToList();

            _logger.LogInformation("Found {Count} vehicles with overdue maintenance", overdueRecords.Count);

            return overdueRecords;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting overdue maintenance");
            throw;
        }
    }

    /// <summary>
    /// Calculate total maintenance cost for a vehicle
    /// </summary>
    public async Task<decimal> CalculateTotalMaintenanceCostAsync(int vmfCode)
    {
        try
        {
            var records = await _maintenanceRecordRepository.GetByVehicleAsync(vmfCode);
            var totalCost = records.Sum(mr => mr.TotalCost);

            _logger.LogInformation("Total maintenance cost for vehicle {VmfCode}: {TotalCost:C}",
                vmfCode, totalCost);

            return totalCost;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating total maintenance cost for vehicle: {VmfCode}", vmfCode);
            throw;
        }
    }

    /// <summary>
    /// Calculate average maintenance cost for a vehicle
    /// </summary>
    public async Task<decimal> CalculateAverageMaintenanceCostAsync(int vmfCode)
    {
        try
        {
            var records = await _maintenanceRecordRepository.GetByVehicleAsync(vmfCode);
            var recordsList = records.ToList();

            if (!recordsList.Any())
            {
                return 0;
            }

            var averageCost = recordsList.Average(mr => mr.TotalCost);

            _logger.LogInformation("Average maintenance cost for vehicle {VmfCode}: {AverageCost:C}",
                vmfCode, averageCost);

            return averageCost;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating average maintenance cost for vehicle: {VmfCode}", vmfCode);
            throw;
        }
    }

    /// <summary>
    /// Calculate cost per kilometer for a vehicle
    /// </summary>
    public async Task<decimal> CalculateCostPerKilometerAsync(int vmfCode)
    {
        try
        {
            var records = await _maintenanceRecordRepository.GetByVehicleAsync(vmfCode);
            var recordsList = records.OrderBy(mr => mr.OdometerReading).ToList();

            if (recordsList.Count < 2)
            {
                return 0; // Need at least 2 records to calculate
            }

            var totalCost = recordsList.Sum(mr => mr.TotalCost);
            var firstOdometer = recordsList.First().OdometerReading;
            var lastOdometer = recordsList.Last().OdometerReading;
            var totalKm = lastOdometer - firstOdometer;

            if (totalKm <= 0)
            {
                return 0;
            }

            var costPerKm = totalCost / totalKm;

            _logger.LogInformation("Cost per km for vehicle {VmfCode}: {CostPerKm:C} ({TotalCost:C} / {TotalKm}km)",
                vmfCode, costPerKm, totalCost, totalKm);

            return costPerKm;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating cost per kilometer for vehicle: {VmfCode}", vmfCode);
            throw;
        }
    }

    /// <summary>
    /// Get maintenance records by service type
    /// </summary>
    public async Task<IEnumerable<MaintenanceRecord>> GetMaintenanceByTypeAsync(string maintenanceType)
    {
        return await _maintenanceRecordRepository.GetByServiceTypeAsync(maintenanceType);
    }

    /// <summary>
    /// Get maintenance records by date range
    /// </summary>
    public async Task<IEnumerable<MaintenanceRecord>> GetMaintenanceByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _maintenanceRecordRepository.GetByDateRangeAsync(startDate, endDate);
    }

    /// <summary>
    /// Check if roadworthy certificate is valid
    /// </summary>
    public bool IsRoadworthyCertificateValid(MaintenanceRecord maintenanceRecord)
    {
        if (!maintenanceRecord.RoadworthyExpiryDate.HasValue)
        {
            return false;
        }

        return maintenanceRecord.RoadworthyExpiryDate.Value > DateTime.Now;
    }

    /// <summary>
    /// Check if warranty is still active
    /// </summary>
    public bool IsWarrantyActive(MaintenanceRecord maintenanceRecord)
    {
        if (!maintenanceRecord.WarrantyExpiryDate.HasValue)
        {
            return false;
        }

        return maintenanceRecord.WarrantyExpiryDate.Value > DateTime.Now;
    }

    /// <summary>
    /// Get vehicles with expiring roadworthy certificates
    /// </summary>
    public async Task<IEnumerable<MaintenanceRecord>> GetExpiringRoadworthyCertificatesAsync(int daysAhead = 30)
    {
        try
        {
            _logger.LogInformation("Getting expiring roadworthy certificates within {DaysAhead} days", daysAhead);

            var cutoffDate = DateTime.Now.AddDays(daysAhead);
            var startDate = DateTime.Now.AddYears(-5); // Look back 5 years
            var endDate = DateTime.Now;

            var allRecords = await _maintenanceRecordRepository.GetByDateRangeAsync(startDate, endDate);

            var expiringRecords = allRecords.Where(mr =>
                mr.RoadworthyExpiryDate.HasValue &&
                mr.RoadworthyExpiryDate.Value <= cutoffDate &&
                mr.RoadworthyExpiryDate.Value > DateTime.Now
            ).ToList();

            _logger.LogInformation("Found {Count} vehicles with expiring roadworthy certificates", expiringRecords.Count);

            return expiringRecords;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expiring roadworthy certificates");
            throw;
        }
    }

    /// <summary>
    /// Get vehicles with expiring warranties
    /// </summary>
    public async Task<IEnumerable<MaintenanceRecord>> GetExpiringWarrantiesAsync(int daysAhead = 30)
    {
        try
        {
            _logger.LogInformation("Getting expiring warranties within {DaysAhead} days", daysAhead);

            var cutoffDate = DateTime.Now.AddDays(daysAhead);
            var startDate = DateTime.Now.AddYears(-5); // Look back 5 years
            var endDate = DateTime.Now;

            var allRecords = await _maintenanceRecordRepository.GetByDateRangeAsync(startDate, endDate);

            var expiringRecords = allRecords.Where(mr =>
                mr.WarrantyExpiryDate.HasValue &&
                mr.WarrantyExpiryDate.Value <= cutoffDate &&
                mr.WarrantyExpiryDate.Value > DateTime.Now &&
                mr.WarrantyWorkFlag == "Y"
            ).ToList();

            _logger.LogInformation("Found {Count} vehicles with expiring warranties", expiringRecords.Count);

            return expiringRecords;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expiring warranties");
            throw;
        }
    }

    /// <summary>
    /// Get scheduled maintenance (future maintenance)
    /// </summary>
    public async Task<IEnumerable<MaintenanceRecord>> GetScheduledMaintenanceAsync()
    {
        try
        {
            var allRecords = await _maintenanceRecordRepository.GetByServiceTypeAsync("SERVICE");
            return allRecords.Where(mr => mr.Status == "SCHEDULED");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scheduled maintenance");
            throw;
        }
    }

    /// <summary>
    /// Get in-progress maintenance
    /// </summary>
    public async Task<IEnumerable<MaintenanceRecord>> GetInProgressMaintenanceAsync()
    {
        try
        {
            var allRecords = await _maintenanceRecordRepository.GetByServiceTypeAsync("SERVICE");
            return allRecords.Where(mr => mr.Status == "IN_PROGRESS");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting in-progress maintenance");
            throw;
        }
    }

    /// <summary>
    /// Schedule maintenance for a vehicle
    /// </summary>
    public async Task<MaintenanceRecord> ScheduleMaintenanceAsync(
        int vmfCode,
        DateTime scheduledDate,
        string maintenanceType,
        string description,
        int? estimatedOdometer = null)
    {
        try
        {
            _logger.LogInformation("Scheduling maintenance for vehicle {VmfCode} on {ScheduledDate}",
                vmfCode, scheduledDate);

            var maintenanceRecord = new MaintenanceRecord
            {
                VmfCode = vmfCode,
                ScheduledDate = scheduledDate,
                MaintenanceDate = scheduledDate, // Scheduled date
                MaintenanceType = maintenanceType,
                Description = description,
                OdometerReading = estimatedOdometer ?? 0,
                Status = "SCHEDULED",
                CreatedDate = DateTime.UtcNow,
                StillCurrentFlag = "Y"
            };

            var created = await _maintenanceRecordRepository.CreateAsync(maintenanceRecord, _currentUserContext.GetCurrentUserIdOrDefault());

            _logger.LogInformation("Maintenance scheduled: ID {MaintenanceId}", created.MaintenanceId);

            return created;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling maintenance for vehicle {VmfCode}", vmfCode);
            throw;
        }
    }

    /// <summary>
    /// Complete scheduled maintenance
    /// </summary>
    public async Task CompleteMaintenanceAsync(
        int maintenanceId,
        DateTime actualDate,
        int actualOdometer,
        decimal totalCost,
        string? mechanicNotes = null)
    {
        try
        {
            _logger.LogInformation("Completing maintenance {MaintenanceId}", maintenanceId);

            var maintenanceRecord = await _maintenanceRecordRepository.GetByIdAsync(maintenanceId);
            if (maintenanceRecord == null)
            {
                throw new InvalidOperationException($"Maintenance record {maintenanceId} not found");
            }

            maintenanceRecord.MaintenanceDate = actualDate;
            maintenanceRecord.OdometerReading = actualOdometer;
            maintenanceRecord.TotalCost = totalCost;
            maintenanceRecord.MechanicNotes = mechanicNotes;
            maintenanceRecord.Status = "COMPLETED";
            maintenanceRecord.ModifiedDate = DateTime.UtcNow;

            // Calculate next service if intervals are set
            if (maintenanceRecord.ServiceIntervalDays.HasValue)
            {
                maintenanceRecord.NextServiceDate = CalculateNextServiceDate(
                    actualDate, maintenanceRecord.ServiceIntervalDays.Value);
            }

            if (maintenanceRecord.ServiceIntervalKm.HasValue)
            {
                maintenanceRecord.NextServiceOdometer = CalculateNextServiceOdometer(
                    actualOdometer, maintenanceRecord.ServiceIntervalKm.Value);
            }

            await _maintenanceRecordRepository.UpdateAsync(maintenanceRecord, _currentUserContext.GetCurrentUserIdOrDefault());

            _logger.LogInformation("Maintenance {MaintenanceId} completed successfully", maintenanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing maintenance {MaintenanceId}", maintenanceId);
            throw;
        }
    }

    /// <summary>
    /// Cancel scheduled maintenance
    /// </summary>
    public async Task CancelMaintenanceAsync(int maintenanceId, string? reason = null)
    {
        try
        {
            _logger.LogInformation("Cancelling maintenance {MaintenanceId}, Reason: {Reason}",
                maintenanceId, reason ?? "Not specified");

            var maintenanceRecord = await _maintenanceRecordRepository.GetByIdAsync(maintenanceId);
            if (maintenanceRecord == null)
            {
                throw new InvalidOperationException($"Maintenance record {maintenanceId} not found");
            }

            maintenanceRecord.Status = "CANCELLED";
            maintenanceRecord.ModifiedDate = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(reason))
            {
                maintenanceRecord.MechanicNotes = $"CANCELLED: {reason}";
            }

            await _maintenanceRecordRepository.UpdateAsync(maintenanceRecord, _currentUserContext.GetCurrentUserIdOrDefault());

            _logger.LogInformation("Maintenance {MaintenanceId} cancelled", maintenanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling maintenance {MaintenanceId}", maintenanceId);
            throw;
        }
    }

    /// <summary>
    /// Get last service record for a vehicle
    /// </summary>
    public async Task<MaintenanceRecord?> GetLastServiceAsync(int vmfCode)
    {
        try
        {
            var records = await _maintenanceRecordRepository.GetByVehicleAsync(vmfCode);
            return records
                .Where(mr => mr.Status == "COMPLETED")
                .OrderByDescending(mr => mr.MaintenanceDate)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting last service for vehicle {VmfCode}", vmfCode);
            throw;
        }
    }

    /// <summary>
    /// Get maintenance statistics for a vehicle
    /// </summary>
    public async Task<MaintenanceStatistics> GetMaintenanceStatisticsAsync(int vmfCode)
    {
        try
        {
            _logger.LogInformation("Generating maintenance statistics for vehicle {VmfCode}", vmfCode);

            var records = await _maintenanceRecordRepository.GetByVehicleAsync(vmfCode);
            var recordsList = records.ToList();
            var lastService = await GetLastServiceAsync(vmfCode);

            // Get current odometer from last maintenance record
            var currentOdometer = lastService?.OdometerReading ?? 0;

            var stats = new MaintenanceStatistics
            {
                VehicleCode = vmfCode,
                TotalRecords = recordsList.Count,
                TotalCost = recordsList.Sum(mr => mr.TotalCost),
                AverageCost = recordsList.Any() ? recordsList.Average(mr => mr.TotalCost) : 0,
                CostPerKilometer = await CalculateCostPerKilometerAsync(vmfCode),
                LastServiceDate = lastService?.MaintenanceDate,
                NextServiceDate = lastService?.NextServiceDate,
                NextServiceOdometer = lastService?.NextServiceOdometer,
                ServiceCounts = recordsList
                    .GroupBy(mr => mr.MaintenanceType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                CostsByType = recordsList
                    .GroupBy(mr => mr.MaintenanceType)
                    .ToDictionary(g => g.Key, g => g.Sum(mr => mr.TotalCost)),
                MaintenanceDue = await IsMaintenanceDueAsync(vmfCode, currentOdometer),
                MaintenanceOverdue = lastService?.NextServiceDate.HasValue == true &&
                                    lastService.NextServiceDate.Value < DateTime.Now,
                RoadworthyExpiringSoon = lastService?.RoadworthyExpiryDate.HasValue == true &&
                                        lastService.RoadworthyExpiryDate.Value <= DateTime.Now.AddDays(30),
                WarrantyExpiringSoon = lastService?.WarrantyExpiryDate.HasValue == true &&
                                      lastService.WarrantyExpiryDate.Value <= DateTime.Now.AddDays(30)
            };

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating maintenance statistics for vehicle {VmfCode}", vmfCode);
            throw;
        }
    }

    /// <summary>
    /// Get service history report for a vehicle
    /// </summary>
    public async Task<ServiceHistoryReport> GetServiceHistoryReportAsync(int vmfCode)
    {
        try
        {
            _logger.LogInformation("Generating service history report for vehicle {VmfCode}", vmfCode);

            var records = await _maintenanceRecordRepository.GetByVehicleAsync(vmfCode);
            var recordsList = records.OrderBy(mr => mr.MaintenanceDate).ToList();

            var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode);

            var report = new ServiceHistoryReport
            {
                VehicleCode = vmfCode,
                RegistrationNumber = vehicle?.registration_number,
                MaintenanceRecords = recordsList,
                TotalCost = recordsList.Sum(mr => mr.TotalCost),
                AverageCostPerService = recordsList.Any() ? recordsList.Average(mr => mr.TotalCost) : 0,
                TotalServices = recordsList.Count,
                FirstServiceDate = recordsList.FirstOrDefault()?.MaintenanceDate,
                LastServiceDate = recordsList.LastOrDefault()?.MaintenanceDate
            };

            // Calculate average intervals
            if (recordsList.Count >= 2)
            {
                var lastRecord = recordsList.Last();
                report.ServiceIntervalDays = lastRecord.ServiceIntervalDays ?? 0;
                report.ServiceIntervalKm = lastRecord.ServiceIntervalKm ?? 0;
            }

            // Find most common service provider
            var providerGroups = recordsList
                .Where(mr => !string.IsNullOrEmpty(mr.ServiceProvider))
                .GroupBy(mr => mr.ServiceProvider)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            report.PreferredServiceProvider = providerGroups?.Key;

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating service history report for vehicle {VmfCode}", vmfCode);
            throw;
        }
    }

    /// <summary>
    /// Delete maintenance record
    /// </summary>
    public async Task DeleteMaintenanceRecordAsync(int maintenanceId)
    {
        try
        {
            _logger.LogInformation("Deleting maintenance record: {MaintenanceId}", maintenanceId);

            var maintenanceRecord = await _maintenanceRecordRepository.GetByIdAsync(maintenanceId);
            if (maintenanceRecord == null)
            {
                throw new InvalidOperationException($"Maintenance record {maintenanceId} not found");
            }

            // Could add validation here to prevent deletion of certain records
            // For example, don't delete if it's linked to warranty claims, etc.

            await _maintenanceRecordRepository.DeleteAsync(maintenanceId, _currentUserContext.GetCurrentUserIdOrDefault());

            _logger.LogInformation("Maintenance record deleted: {MaintenanceId}", maintenanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting maintenance record: {MaintenanceId}", maintenanceId);
            throw;
        }
    }
}
