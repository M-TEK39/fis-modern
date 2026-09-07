namespace FIS.Core.Application.Interfaces;

public interface IFmlReportRepository
{
    Task<FmlMaintenanceHistoryReport> GetMaintenanceHistoryAsync(
        DateTime? startDate,
        DateTime? endDate,
        int? financialYear,
        string? vehicleNumber);

    Task<FmlContractsReport> GetContractsExpiringAsync();

    Task<FmlContractsReport> GetExpiredOpenContractsAsync();

    Task<FmlVehiclesNoContractsReport> GetVehiclesNoContractsAsync();

    Task<FmlOverUtilizedReport> GetOverUtilizedAsync(DateTime? startDate, DateTime? endDate);
}

public sealed record FmlMaintenanceHistoryReport(
    IReadOnlyList<FmlMaintenanceHistoryRecord> Records,
    decimal GrandTotal)
{
    public int TotalCount => Records.Count;
}

public sealed record FmlMaintenanceHistoryRecord(
    string? GgNumber,
    short? YearManufactured,
    string? ModelDescription,
    string? CurrentStatus,
    DateTime? CurrentStatusDate,
    string? HiredFrom,
    string? MaintenanceExpenseType,
    decimal? TotalCostOverDateRange);

public sealed record FmlContractsReport(IReadOnlyList<FmlContractRecord> Contracts)
{
    public int TotalCount => Contracts.Count;
}

public sealed record FmlContractRecord(
    int? RowNumber,
    string? GgNumber,
    string? GpNumber,
    string? Model,
    short? YearModel,
    string? HiredFrom,
    string? HireType,
    string? StillCurrent,
    DateTime? ContractStartDate,
    DateTime? TargetReturnDate,
    string? ContractType,
    string? SiteName,
    decimal? FixedTariff);

public sealed record FmlVehiclesNoContractsReport(IReadOnlyList<FmlVehicleNoContractRecord> Vehicles)
{
    public int TotalCount => Vehicles.Count;
}

public sealed record FmlVehicleNoContractRecord(
    int? VehicleCounter,
    string? GgNumber,
    string? RegistrationNumber,
    string? HiredFrom,
    string? VehicleStatus,
    string? Location,
    short? YearModel,
    string? ModelDescription,
    string? ClassDescription,
    decimal? PurchaseAmount);

public sealed record FmlOverUtilizedReport(IReadOnlyList<FmlOverUtilizedRecord> Vehicles)
{
    public int TotalCount => Vehicles.Count;
}

public sealed record FmlOverUtilizedRecord(
    int? VehicleCounter,
    string? GgNumber,
    string? GpNumber,
    string? HiredFrom,
    string? Month,
    decimal? MaxOdoMeter,
    decimal? MinOdoMeter,
    decimal? ActualKilos,
    decimal? AgreedKilos,
    decimal? ExcessKilos,
    decimal? AgreedOverallKilo,
    decimal? AgreedTerms,
    decimal? ActualTerm,
    decimal? TotalKilos,
    decimal? TotalExcessKilos,
    decimal? AverageMonthlyKilos,
    string? ProjectedEndMonth,
    DateTime? ProjectedEndDate,
    short? YearModel,
    string? ModelDescription,
    decimal? PurchaseAmount);
