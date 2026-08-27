namespace FIS.Web.Models;

public class ReportHelpDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<HelpSectionDto> Sections { get; set; } = new();
}

public class HelpSectionDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
