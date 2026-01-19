using System.ComponentModel.DataAnnotations;

namespace FIS.Web.Models;

public class ContractDto
{
    public int contract_id { get; set; }
    [Required]
    public string contract_number { get; set; } = "";
    [Required]
    public string vehicle_registration { get; set; } = "";
    public string vehicle_make { get; set; } = "";
    public string vehicle_model { get; set; } = "";
    public string department_name { get; set; } = "";
    [Range(1, int.MaxValue, ErrorMessage = "Department is required.")]
    public int department_code { get; set; }
    [Required]
    public string contractor_name { get; set; } = "";
    [Required]
    public DateTime start_date { get; set; } = DateTime.Today;
    public DateTime? end_date { get; set; }
    public decimal? monthly_cost { get; set; }
    [Required]
    public string status { get; set; } = "Active";
    public string? contract_notes { get; set; }

    // Navigation properties for the table component
    public VehicleDto? Vehicle { get; set; }
    public DepartmentDto? Department { get; set; }
}
