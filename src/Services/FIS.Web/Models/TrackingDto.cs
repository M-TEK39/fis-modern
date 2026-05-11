namespace FIS.Web.Models;

public class TrackingDto
{
    public short track_code { get; set; }
    public int? vmf_code { get; set; }
    public string? track_num { get; set; }
    public string? gg_previous { get; set; }
    public string? gg_follow { get; set; }
    public DateTime? install_date { get; set; }
    public DateTime? remove_date { get; set; }
    public string? track_status { get; set; }
    public string? track_type { get; set; }
    public string? track_note { get; set; }
}

public class TrackingReportDto
{
    public string ReportType { get; set; } = string.Empty;
    public List<TrackingDto> Data { get; set; } = new();
}

public class TrackingOneVehicleReportRequestDto
{
    public int VmfCode { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class TrackingOneDeviceReportRequestDto
{
    public string DeviceId { get; set; } = string.Empty;
}

public class TrackingAllVehiclesReportRequestDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? TrackerType { get; set; }
}

public class TrackingAllDevicesReportRequestDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class TrackingInstallPeriodReportRequestDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class TrackingSitePeriodReportRequestDto
{
    public int SiteCode { get; set; }
    public bool AllSites { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class TrackingDeptPeriodReportRequestDto
{
    public int DepartmentCode { get; set; }
    public bool AllDepartments { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
