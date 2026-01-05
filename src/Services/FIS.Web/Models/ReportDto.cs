namespace FIS.Web.Models;

public record ReportDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime GeneratedDate { get; set; }
    public string FileSize { get; set; } = "";
    public string? FilePath { get; set; }
    public string? ReportType { get; set; }
    public string? Description { get; set; }
}