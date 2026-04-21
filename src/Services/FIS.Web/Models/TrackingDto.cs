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
