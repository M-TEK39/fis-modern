namespace FIS.Web.Models;

public class NotifyListDto
{
    public int Notify_list_code { get; set; }
    public string? Notify_list_desc { get; set; }
    public string? Notify_email1 { get; set; }
    public DateTime date_created { get; set; }
    public DateTime? date_updated { get; set; }
}

public class NotifyListCreateUpdateDto
{
    public string? Notify_list_desc { get; set; }
    public string? Notify_email1 { get; set; }
}
