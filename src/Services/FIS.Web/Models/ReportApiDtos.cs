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
    public string RegistrationNumber { get; set; } = string.Empty;
    public DateTime? IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string Status { get; set; } = string.Empty;
}
