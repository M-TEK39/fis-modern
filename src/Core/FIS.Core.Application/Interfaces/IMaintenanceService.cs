using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Maintenance Service interface for vehicle maintenance business operations
/// Handles maintenance scheduling, cost tracking, compliance, and service history
/// </summary>
public interface IMaintenanceService
{
    /// <summary>
    /// Create a new maintenance record with automatic next service calculation
    /// Calculates next service date and odometer based on service intervals
    /// </summary>
    Task<MaintenanceRecord> CreateMaintenanceRecordAsync(MaintenanceRecord maintenanceRecord);

    /// <summary>
    /// Update an existing maintenance record
    /// Recalculates next service dates if intervals changed
    /// </summary>
    Task UpdateMaintenanceRecordAsync(MaintenanceRecord maintenanceRecord);

    /// <summary>
    /// Get maintenance record by ID
    /// </summary>
    Task<MaintenanceRecord?> GetMaintenanceRecordByIdAsync(int maintenanceId);

    /// <summary>
    /// Get all maintenance records for a vehicle
    /// </summary>
    Task<IEnumerable<MaintenanceRecord>> GetMaintenanceHistoryAsync(int vmfCode);

    /// <summary>
    /// Calculate next service date based on last service and interval
    /// Legacy: Maintenance scheduling algorithm
    /// </summary>
    DateTime? CalculateNextServiceDate(DateTime lastServiceDate, int intervalDays);

    /// <summary>
    /// Calculate next service odometer based on last reading and interval
    /// Legacy: Odometer-based service scheduling
    /// </summary>
    int? CalculateNextServiceOdometer(int lastOdometerReading, int intervalKm);

    /// <summary>
    /// Check if a vehicle is due for maintenance
    /// Checks both date-based and odometer-based criteria
    /// </summary>
    Task<bool> IsMaintenanceDueAsync(int vmfCode, int currentOdometer);

    /// <summary>
    /// Get vehicles due for maintenance
    /// Returns vehicles where next_service_date is approaching or next_service_odometer is exceeded
    /// </summary>
    Task<IEnumerable<MaintenanceRecord>> GetDueMaintenanceAsync(int daysAhead = 30);

    /// <summary>
    /// Get overdue maintenance records
    /// Vehicles that have exceeded their service date or odometer
    /// </summary>
    Task<IEnumerable<MaintenanceRecord>> GetOverdueMaintenanceAsync();

    /// <summary>
    /// Calculate total maintenance cost for a vehicle
    /// Sums all maintenance costs from history
    /// </summary>
    Task<decimal> CalculateTotalMaintenanceCostAsync(int vmfCode);

    /// <summary>
    /// Calculate average maintenance cost for a vehicle
    /// </summary>
    Task<decimal> CalculateAverageMaintenanceCostAsync(int vmfCode);

    /// <summary>
    /// Calculate cost per kilometer for a vehicle
    /// Total maintenance cost divided by total kilometers serviced
    /// </summary>
    Task<decimal> CalculateCostPerKilometerAsync(int vmfCode);

    /// <summary>
    /// Get maintenance records by service type (SERVICE, REPAIR, INSPECTION, etc.)
    /// </summary>
    Task<IEnumerable<MaintenanceRecord>> GetMaintenanceByTypeAsync(string maintenanceType);

    /// <summary>
    /// Get maintenance records by date range
    /// </summary>
    Task<IEnumerable<MaintenanceRecord>> GetMaintenanceByDateRangeAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Check if roadworthy certificate is valid
    /// Validates roadworthy_expiry_date against current date
    /// </summary>
    bool IsRoadworthyCertificateValid(MaintenanceRecord maintenanceRecord);

    /// <summary>
    /// Check if warranty is still active
    /// Validates warranty_expiry_date against current date
    /// </summary>
    bool IsWarrantyActive(MaintenanceRecord maintenanceRecord);

    /// <summary>
    /// Get vehicles with expiring roadworthy certificates
    /// Returns maintenance records where roadworthy_expiry_date is within specified days
    /// </summary>
    Task<IEnumerable<MaintenanceRecord>> GetExpiringRoadworthyCertificatesAsync(int daysAhead = 30);

    /// <summary>
    /// Get vehicles with expiring warranties
    /// Returns maintenance records where warranty_expiry_date is within specified days
    /// </summary>
    Task<IEnumerable<MaintenanceRecord>> GetExpiringWarrantiesAsync(int daysAhead = 30);

    /// <summary>
    /// Get scheduled maintenance (future maintenance)
    /// Returns maintenance records with status = SCHEDULED
    /// </summary>
    Task<IEnumerable<MaintenanceRecord>> GetScheduledMaintenanceAsync();

    /// <summary>
    /// Get in-progress maintenance
    /// Returns maintenance records with status = IN_PROGRESS
    /// </summary>
    Task<IEnumerable<MaintenanceRecord>> GetInProgressMaintenanceAsync();

    /// <summary>
    /// Schedule maintenance for a vehicle
    /// Creates a new maintenance record with status = SCHEDULED
    /// </summary>
    Task<MaintenanceRecord> ScheduleMaintenanceAsync(
        int vmfCode,
        DateTime scheduledDate,
        string maintenanceType,
        string description,
        int? estimatedOdometer = null);

    /// <summary>
    /// Complete scheduled maintenance
    /// Updates status to COMPLETED and records actual costs and details
    /// </summary>
    Task CompleteMaintenanceAsync(
        int maintenanceId,
        DateTime actualDate,
        int actualOdometer,
        decimal totalCost,
        string? mechanicNotes = null);

    /// <summary>
    /// Cancel scheduled maintenance
    /// Updates status to CANCELLED
    /// </summary>
    Task CancelMaintenanceAsync(int maintenanceId, string? reason = null);

    /// <summary>
    /// Get last service record for a vehicle
    /// Most recent completed maintenance record
    /// </summary>
    Task<MaintenanceRecord?> GetLastServiceAsync(int vmfCode);

    /// <summary>
    /// Get maintenance statistics for a vehicle
    /// Total cost, average cost, service counts by type
    /// </summary>
    Task<MaintenanceStatistics> GetMaintenanceStatisticsAsync(int vmfCode);

    /// <summary>
    /// Get service history report for a vehicle
    /// Comprehensive maintenance history with costs and intervals
    /// </summary>
    Task<ServiceHistoryReport> GetServiceHistoryReportAsync(int vmfCode);

    /// <summary>
    /// Delete maintenance record
    /// Validates that record can be deleted (not linked to other systems)
    /// </summary>
    Task DeleteMaintenanceRecordAsync(int maintenanceId);
}

/// <summary>
/// Maintenance statistics model for reporting
/// </summary>
public class MaintenanceStatistics
{
    public int VehicleCode { get; set; }
    public int TotalRecords { get; set; }
    public decimal TotalCost { get; set; }
    public decimal AverageCost { get; set; }
    public decimal CostPerKilometer { get; set; }
    public DateTime? LastServiceDate { get; set; }
    public DateTime? NextServiceDate { get; set; }
    public int? NextServiceOdometer { get; set; }
    public Dictionary<string, int> ServiceCounts { get; set; } = new();
    public Dictionary<string, decimal> CostsByType { get; set; } = new();
    public bool MaintenanceDue { get; set; }
    public bool MaintenanceOverdue { get; set; }
    public bool RoadworthyExpiringSoon { get; set; }
    public bool WarrantyExpiringSoon { get; set; }
}

// Report models are defined in ReportModels.cs to avoid duplicates
