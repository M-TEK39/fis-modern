namespace FIS.Web.Models;

public class ContractDto
{
    public int contract_id { get; set; }
    public string contract_number { get; set; } = "";
    public string vehicle_registration { get; set; } = "";
    public string vehicle_make { get; set; } = "";
    public string vehicle_model { get; set; } = "";
    public string department_name { get; set; } = "";
    public int department_code { get; set; }
    public string contractor_name { get; set; } = "";
    public DateTime start_date { get; set; } = DateTime.Today;
    public DateTime? end_date { get; set; }
    public decimal? monthly_cost { get; set; }
    public string status { get; set; } = "Active";
    public string? contract_notes { get; set; }

    // Navigation properties for the table component
    public VehicleDto? Vehicle { get; set; }
    public DepartmentDto? Department { get; set; }
}