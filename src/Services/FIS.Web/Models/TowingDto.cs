namespace FIS.Web.Models;

public class TowingDto
{
    public short Towing_code { get; set; }
    public int vmf_code { get; set; }
    public decimal? Call_refer { get; set; }
    public DateTime? Tow_request_date { get; set; }
    public DateTime? Tow_request_time { get; set; }
    public string? Tow_location_start { get; set; }
    public string? Vehicle_problem { get; set; }
    public string? Keys { get; set; }
    public short? Site_code { get; set; }
}

public class TowingReportDto
{
    public string ReportType { get; set; } = string.Empty;
    public List<TowingDto> Data { get; set; } = new();
}

public class TowingRequestReportRequestDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class TowingFirmDateReportRequestDto
{
    public string FirmName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
