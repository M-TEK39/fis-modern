namespace FIS.Web.Models;

public class CallCentreDto
{
    public short Call_centre_code { get; set; }
    public int? vmf_code { get; set; }
    public DateTime? Call_time { get; set; }
    public DateTime? Call_date { get; set; }
    public string? Incident_type { get; set; }
    public string? Incident_Desc { get; set; }
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
    public string? Driver_fax { get; set; }
    public string? Driver_email { get; set; }
    public DateTime? Incident_date { get; set; }
    public DateTime? Incident_time { get; set; }
    public string? Caller_tel { get; set; }
    public string? TrOfficer_name { get; set; }
    public string? TrOfficer_tel { get; set; }
    public short? TrOfficer_Site { get; set; }
    public string? Incident_town { get; set; }
    public string? Incident_street { get; set; }
    public short? Counter { get; set; }
    public string? Caller_fax { get; set; }
    public string? TrOfficer_fax { get; set; }
    public string? Caller_email { get; set; }
    public string? TrOfficer_email { get; set; }
    public string? Inform_CRO { get; set; }
    public string? CRO_Remarks { get; set; }
    public string? Incident_Remarks { get; set; }
    public int? Notify_list_code { get; set; }
    public string? call_closed { get; set; }
    public DateTime date_created { get; set; }
    public DateTime? date_updated { get; set; }
}
