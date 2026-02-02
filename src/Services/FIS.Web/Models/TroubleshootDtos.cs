namespace FIS.Web.Models;

public record TroubleshootUserDto
{
    public int UserAccessCode { get; set; }
    public string Name { get; set; } = "";
    public string? SiteDescription { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}

public record TroubleshootSiteUserDto
{
    public int UserAccessCode { get; set; }
    public string Name { get; set; } = "";
    public string? SiteDescription { get; set; }
    public short? SiteCode { get; set; }
}

public record TroubleshootLogSearchRequest
{
    public int UserAccessCode { get; set; }
}

public record TroubleshootLogEntryDto
{
    public int Id { get; set; }
    public string? VehicleIdentifier { get; set; }
    public string? ProblemDescription { get; set; }
    public string? Status { get; set; }
    public DateTime? LoggedDate { get; set; }
    public string? LoggedBy { get; set; }
}

public record TroubleshootReportFilter
{
    public string? ProblemKeyword { get; set; }
    public int? UserAccessCode { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public bool OpenInExcel { get; set; }
}

public record OdometerCorrectionSearchRequest
{
    public string SearchMode { get; set; } = "GG";
    public string SearchValue { get; set; } = "";
}

public record OdometerCorrectionResultDto
{
    public string? VehicleIdentifier { get; set; }
    public string? TripAuthorityNumber { get; set; }
    public int? CurrentOdometer { get; set; }
    public int? LastOdometer { get; set; }
}

public record RemoveTripsRequest
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public record ApproverRankDto
{
    public int Id { get; set; }
    public string? RankName { get; set; }
    public string? Description { get; set; }
}

public record VehicleMasterEditRequest
{
    public string? VehicleIdentifier { get; set; }
}

public record UpdateRecoveredGgRequest
{
    public string? VehicleIdentifier { get; set; }
    public string? Notes { get; set; }
}
