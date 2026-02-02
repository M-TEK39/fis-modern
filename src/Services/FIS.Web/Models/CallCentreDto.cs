namespace FIS.Web.Models;

public class CallCentreDto
{
    public short Call_centre_code { get; set; }
    public int? vmf_code { get; set; }
    public DateTime? Call_time { get; set; }
    public DateTime? Call_date { get; set; }
    public string? Capture_name { get; set; }
    public short? User_access_code { get; set; }
    public string? Caller_name { get; set; }
    public string? Driver_name { get; set; }
    public string? Driver_persalno { get; set; }
    public string? Driver_Licno { get; set; }
    public string? GG_number { get; set; }
    public string? Driver_base_station { get; set; }
    public short? Driver_Site { get; set; }
    public string? Driver_tel { get; set; }
    public string? Driver_cell { get; set; }
}
