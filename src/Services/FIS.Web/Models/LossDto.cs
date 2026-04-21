namespace FIS.Web.Models;

public record LossDto
{
    public int loss_code { get; set; }
    public string? loss_reference { get; set; }
    public string? vehicle_identifier { get; set; }
    public int? vmf_code { get; set; }
    public DateTime? loss_date { get; set; }
    public int? loss_type_code { get; set; }
    public string? loss_type_description { get; set; }
    public string? loss_status { get; set; }
    public decimal? loss_amount { get; set; }
    public string? case_number { get; set; }
    public string? place_of_loss { get; set; }
    public string? dept_contact { get; set; }
    public string? sapd_station { get; set; }
    public string? inspector { get; set; }
    public string? driver_name { get; set; }
    public string? call_reference { get; set; }
    public string? hq_reference { get; set; }
    public string? remarks { get; set; }
    public string? dept_claim { get; set; }
    public short? site_code { get; set; }
}
