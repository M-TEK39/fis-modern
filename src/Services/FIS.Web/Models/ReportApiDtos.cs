namespace FIS.Web.Models;

public record NewInServiceReportDto
{
    public int total_count { get; set; }
    public NewInServiceFilters filters_applied { get; set; } = new();
    public string? assumption_note { get; set; }
    public List<NewInServiceVehicleDto> vehicles { get; set; } = new();
}

public record NewInServiceFilters
{
    public string? search { get; set; }
    public byte? vs_code { get; set; }
    public short? type_code { get; set; }
    public short? location_code { get; set; }
    public short? make_code { get; set; }
    public short? model_code { get; set; }
    public short? vehicle_status_code { get; set; }
}

public record NewInServiceVehicleDto
{
    public int vmf_code { get; set; }
    public string? fleet_number { get; set; }
    public string? registration_number { get; set; }
    public short vehicle_status_code { get; set; }
    public string? status_text { get; set; }
    public short? type_code { get; set; }
    public string? type_description { get; set; }
    public byte? vs_code { get; set; }
    public string? hired_from { get; set; }
    public short? model_code { get; set; }
    public string? make_description { get; set; }
    public string? model_description { get; set; }
    public short? location_code { get; set; }
    public string? site_name { get; set; }
    public string? chassis_number { get; set; }
    public string? engine_number_1 { get; set; }
    public short? year_manufactured { get; set; }
    public DateTime? take_on_date { get; set; }
    public string? invoice_number { get; set; }
    public DateTime? date_created { get; set; }
    public int? current_odo { get; set; }
    public ActiveRemarkDto? active_remark { get; set; }
}

public record ActiveRemarkDto
{
    public int remark_id { get; set; }
    public string? remark_category { get; set; }
    public string? remark_text { get; set; }
    public DateTime? date_created { get; set; }
}

public record ReportAuditTrailDto
{
    public List<AuditEntryDto> Entries { get; set; } = new();
    public int TotalCount { get; set; }
}

public record AuditEntryDto
{
    public int AuditId { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime AccessedDate { get; set; }
    public string Action { get; set; } = string.Empty;
}

public record RegistrationCertificatesReportDto
{
    public List<CertificateDto> Certificates { get; set; } = new();
    public int TotalCount { get; set; }
}

public record CertificateDto
{
    public int VmfCode { get; set; }
    public string FleetNumber { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public DateTime? PeriodFrom { get; set; }
    public DateTime? PeriodTo { get; set; }
    public DateTime? DateUploaded { get; set; }
    public string RegistrationCertificate { get; set; } = string.Empty;
    public DateTime? IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public record FmlMaintenanceHistoryReportDto
{
    public List<FmlMaintenanceHistoryRow> Records { get; set; } = new();
    public decimal? GrandTotal { get; set; }
    public int TotalCount { get; set; }
}

public record FmlMaintenanceHistoryRow
{
    public string? GgNumber { get; set; }
    public short? YearManufactured { get; set; }
    public string? ModelDescription { get; set; }
    public string? CurrentStatus { get; set; }
    public DateTime? CurrentStatusDate { get; set; }
    public string? HiredFrom { get; set; }
    public string? MaintenanceExpenseType { get; set; }
    public decimal? TotalCostOverDateRange { get; set; }
}

public record FmlContractsReportDto
{
    public List<FmlContractRow> Contracts { get; set; } = new();
    public int TotalCount { get; set; }
}

public record FmlContractRow
{
    public int? RowNumber { get; set; }
    public string? GgNumber { get; set; }
    public string? GpNumber { get; set; }
    public string? Model { get; set; }
    public short? YearModel { get; set; }
    public string? HiredFrom { get; set; }
    public string? HireType { get; set; }
    public string? StillCurrent { get; set; }
    public DateTime? ContractStartDate { get; set; }
    public DateTime? TargetReturnDate { get; set; }
    public string? ContractType { get; set; }
    public string? SiteName { get; set; }
    public decimal? FixedTariff { get; set; }
}

public record FmlVehiclesNoContractsReportDto
{
    public List<FmlVehicleNoContractRow> Vehicles { get; set; } = new();
    public int TotalCount { get; set; }
}

public record FmlVehicleNoContractRow
{
    public int? VehicleCounter { get; set; }
    public string? GgNumber { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? HiredFrom { get; set; }
    public string? VehicleStatus { get; set; }
    public string? Location { get; set; }
    public short? YearModel { get; set; }
    public string? ModelDescription { get; set; }
    public string? ClassDescription { get; set; }
    public decimal? PurchaseAmount { get; set; }
}

public record FmlOverUtilizedReportDto
{
    public List<FmlOverUtilizedRow> Vehicles { get; set; } = new();
    public int TotalCount { get; set; }
}

public record FmlOverUtilizedRow
{
    public int? VehicleCounter { get; set; }
    public string? GgNumber { get; set; }
    public string? GpNumber { get; set; }
    public string? HiredFrom { get; set; }
    public string? Month { get; set; }
    public decimal? MaxOdoMeter { get; set; }
    public decimal? MinOdoMeter { get; set; }
    public decimal? ActualKilos { get; set; }
    public decimal? AgreedKilos { get; set; }
    public decimal? ExcessKilos { get; set; }
    public decimal? AgreedOverallKilo { get; set; }
    public decimal? AgreedTerms { get; set; }
    public decimal? ActualTerm { get; set; }
    public decimal? TotalKilos { get; set; }
    public decimal? TotalExcessKilos { get; set; }
    public decimal? AverageMonthlyKilos { get; set; }
    public string? ProjectedEndMonth { get; set; }
    public DateTime? ProjectedEndDate { get; set; }
    public short? YearModel { get; set; }
    public string? ModelDescription { get; set; }
    public decimal? PurchaseAmount { get; set; }
}


public sealed record LegacyDynamicReportDto
{
    public string ReportKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? LegacyTarget { get; set; }
    public bool IsApproximate { get; set; }
    public string? ApproximationReason { get; set; }
    public List<LegacyDynamicReportColumnDto> Columns { get; set; } = new();
    public List<Dictionary<string, string?>> Rows { get; set; } = new();
    public int TotalCount { get; set; }
}

public sealed record LegacyDynamicReportColumnDto
{
    public string Key { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
}
