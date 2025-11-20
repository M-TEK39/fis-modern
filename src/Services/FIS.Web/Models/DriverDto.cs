namespace FIS.Web.Models;

public record DriverDto
{
    public int site_driver_code { get; set; }
    public int site_code { get; set; }
    public string? site_name { get; set; }
    public int driver_licence_type_id { get; set; }
    public string? licence_type_name { get; set; }
    public string? driver_surname { get; set; }
    public string? driver_firstname { get; set; }
    public string? driver_SA_id { get; set; }
    public string? driver_passportnumber { get; set; }
    public string? driver_persalnumber { get; set; }
    public string? driver_contractnumber { get; set; }
    public string? driver_licence_number { get; set; }
    public DateTime? driver_licence_issuedate { get; set; }
    public DateTime? driver_licence_lastVerifiedDate { get; set; }
    public bool driver_hasPDP { get; set; }
    public DateTime? driver_PDP_ExpiryDate { get; set; }
    public DateTime? driver_licence_ExpiryDate { get; set; }
    public bool driver_active { get; set; }
}
