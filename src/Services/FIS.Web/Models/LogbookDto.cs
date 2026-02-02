namespace FIS.Web.Models;

public class LogbookDto
{
    public short logbookcode { get; set; }
    public int? vmf_code { get; set; }
    public string? begin_num { get; set; }
    public string? end_num { get; set; }
    public DateTime? handout_date { get; set; }
    public short? site_code { get; set; }
    public string? lb_receiver_name { get; set; }
    public string? lb_tel_num { get; set; }
    public string? lb_comment { get; set; }
}
