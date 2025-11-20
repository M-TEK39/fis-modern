namespace FIS.Web.Models;

public class AccidentDto
{
    public int accident_code { get; set; }
    public string accident_reference { get; set; } = "";
    public DateTime accident_date { get; set; }
    public int? vehicle_id { get; set; }  // For form binding
    public string vehicle_registration { get; set; } = "";
    public string vehicle_description { get; set; } = "";
    public int? driver_id { get; set; }  // For form binding
    public string accident_location { get; set; } = "";
    public string accident_description { get; set; } = "";  // Added
    public string severity { get; set; } = "";
    public decimal? cost_of_repair { get; set; }
    public decimal? third_party_claim { get; set; }
    public decimal? insurance_excess { get; set; }  // Added
    public string status { get; set; } = "";
    public string? description { get; set; }
    public string? driver_name { get; set; }
    public string? department { get; set; }
    public string? weather_conditions { get; set; }  // Changed from bool
    public string? police_case_number { get; set; }  // Renamed
    public string? insurance_claim_number { get; set; }
    public bool injuries_reported { get; set; }  // Added
    public bool third_party_involved { get; set; }  // Added
    public DateTime? date_reported { get; set; }
    public string? reported_by { get; set; }
    public string? notes { get; set; }
}