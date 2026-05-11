namespace FIS.Web.Models;

public class MonitorDto
{
    public short monitor_code { get; set; }
    public int? vmf_code { get; set; }
    public DateTime? Capture_dat { get; set; }
    public short? User_access_code { get; set; }
    public string? Inquiry_type { get; set; }
    public string? Inquiry_Desc { get; set; }
    public string? Driver_name { get; set; }
    public string? Driver_persalno { get; set; }
    public short? Driver_Site { get; set; }
}

public class MonitorStatsRequestDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class MonitorReportDto
{
    public string ReportType { get; set; } = string.Empty;
    public List<MonitorStatisticItemDto> Data { get; set; } = new();
}

public class MonitorStatisticItemDto
{
    public string InquiryType { get; set; } = string.Empty;
    public int Count { get; set; }
}
