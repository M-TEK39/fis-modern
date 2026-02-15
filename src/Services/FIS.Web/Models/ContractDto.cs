using System.ComponentModel.DataAnnotations;

namespace FIS.Web.Models;

public class ContractDto
{
    public int contract_id { get; set; }
    public int? vmf_code { get; set; }
    public short? site_code { get; set; }
    public short? contract_status_code { get; set; }
    public string? still_current { get; set; }
    public short? user_code { get; set; }
    public int? approver_code { get; set; }
    public int? created_by_user_code { get; set; }
    public int? modified_by_user_code { get; set; }
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
    public int? start_odometer { get; set; }
    public string? driver_id { get; set; }
    public decimal? monthly_cost { get; set; }
    public DateTime? target_return_date { get; set; }
    public string? fleet_number { get; set; }
    public string? site_name { get; set; }
    public string? driver_name { get; set; }
    [Required]
    public string status { get; set; } = "Active";
    public string? contract_notes { get; set; }

    // Navigation properties for the table component
    public VehicleDto? Vehicle { get; set; }
    public DepartmentDto? Department { get; set; }
}
