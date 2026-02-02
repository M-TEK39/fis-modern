namespace FIS.Web.Models;

public class LogsheetDto
{
    public int log_code { get; set; }
    public int vmf_code { get; set; }
    public double start_odo { get; set; }
    public double end_odo { get; set; }
    public DateTime month { get; set; }
    public short site_code { get; set; }
    public string? rek_num { get; set; }
    public int? days_used { get; set; }
    public int? bund_num { get; set; }
}
