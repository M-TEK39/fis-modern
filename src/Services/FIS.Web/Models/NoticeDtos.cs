namespace FIS.Web.Models;

public record NoticeScheduleDto
{
    public int NoticeScheduleId { get; set; }
    public int NoticeId { get; set; }
    public string TitleField { get; set; } = "";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? CreatedDate { get; set; }
    public int? SortOrder { get; set; }
}

public record NoticeDetailDto
{
    public int NoticeId { get; set; }
    public DateTime? NoticeDate { get; set; }
    public string? NoticeFrom { get; set; }
    public string? NoticeTitle { get; set; }
    public string? NoticeBody { get; set; }
    public string? NoticePerson { get; set; }
    public string? NoticePersonTitle { get; set; }
}

public record NoticeScheduleSaveRequest
{
    public int NoticeScheduleId { get; set; }
    public int NoticeId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int SortOrder { get; set; }
    public string? CreatedBy { get; set; }
}

public record NoticeSaveRequest
{
    public int NoticeId { get; set; }
    public DateTime? NoticeDate { get; set; }
    public string? NoticeFrom { get; set; }
    public string? NoticeTitle { get; set; }
    public string? NoticeBody { get; set; }
    public string? NoticePerson { get; set; }
    public string? NoticePersonTitle { get; set; }
}
