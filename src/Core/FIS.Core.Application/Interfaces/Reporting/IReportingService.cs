using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Service for generating reports and PDF documents that mirror legacy reporting functionality
/// Maps to legacy report generation from GGFIS_v2.0/AReports and FISReports
/// </summary>
public interface IReportingService
{
    // Vehicle Reports
    Task<VehicleReport> GenerateVehicleReportAsync(int vmfCode);
    Task<List<VehicleReport>> GenerateVehicleReportsAsync(List<int> vmfCodes);
    Task<MasterFileReport> GenerateMasterFileReportAsync(int? vmfCode = null);
    Task<UniversalReport> GenerateUniversalReportAsync(UniversalReportRequest request);

    // Financial Reports
    Task<SummaryIncomeReport> GenerateSummaryIncomeReportAsync(int financialYear);
    Task<DetailedIncomeReport> GenerateDetailedIncomeReportAsync(int financialYear);
    Task<TariffListReport> GenerateTariffListReportAsync(int financialYear);
    Task<VehicleBillingHistoryReport> GenerateVehicleBillingHistoryAsync(
        int vmfCode,
        int financialYear
    );
    Task<KiloGapsReport> GenerateKiloGapsReportAsync(int financialYear);

    // Maintenance Reports
    Task<ServiceHistoryReport> GenerateServiceHistoryReportAsync(int vmfCode);
    Task<MaintenanceScheduleReport> GenerateMaintenanceScheduleReportAsync(
        DateTime startDate,
        DateTime endDate
    );
    Task<MaintenanceCostReport> GenerateMaintenanceCostReportAsync(
        int? vmfCode,
        DateTime startDate,
        DateTime endDate
    );

    // Trip Reports
    Task<TripSummaryReport> GenerateTripSummaryReportAsync(
        int? vmfCode,
        DateTime startDate,
        DateTime endDate
    );
    Task<TripDetailReport> GenerateTripDetailReportAsync(int tripId);
    Task<AuthorityReport> GenerateAuthorityReportAsync(int contractId);

    // Contract Reports
    Task<ContractSummaryReport> GenerateContractSummaryReportAsync(int? contractId = null);
    Task<ContractBillingReport> GenerateContractBillingReportAsync(
        int contractId,
        DateTime startDate,
        DateTime endDate
    );

    // PDF Generation
    Task<byte[]> GenerateVehicleReportPdfAsync(int vmfCode);
    Task<byte[]> GenerateCustomReportPdfAsync(
        string reportType,
        Dictionary<string, object> parameters
    );

    // Export Functions
    Task<byte[]> ExportToCsvAsync<T>(List<T> data, string filename);
    Task<byte[]> ExportToExcelAsync<T>(List<T> data, string filename);

    // Legacy Compatibility
    Task<List<ReportDefinition>> GetAvailableReportsAsync();
    Task<ReportDefinition> GetReportDefinitionAsync(string reportName);
}

// All report models are defined in ReportModels.cs to avoid duplicates
