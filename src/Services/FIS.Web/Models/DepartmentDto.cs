using System.ComponentModel.DataAnnotations;

namespace FIS.Web.Models;

public record DepartmentDto
{
    public int department_code { get; set; }
    [Required]
    public string? department_description { get; set; }
    public string? contact_person { get; set; }
    public string? telephone { get; set; }
    public string? telephone2 { get; set; }
    public string? fax { get; set; }
    public string? fax2 { get; set; }
    public string? email { get; set; }
    public string? physical_address { get; set; }
    public string? postal_address { get; set; }
    public string? physical_address_line2 { get; set; }
    public string? physical_address_line3 { get; set; }
    public string? postal_code { get; set; }
    public string? net_address { get; set; }
    public string? department_number { get; set; }
    public string? cell_number { get; set; }
    public bool dept_active { get; set; } = true;
    public string? notes { get; set; }
    public string? department_abbr { get; set; }
    public string? bas_installation_code { get; set; }
    public string? clo_email { get; set; }
    public byte? financial_system_code { get; set; }
    public bool? financial_system_active { get; set; }
    public DateTime? financial_system_activate_date { get; set; }
    public int? default_site { get; set; }
    public bool? export_is_active { get; set; }
    public DateTime? date_last_exported { get; set; }
    public int? service_kilometres { get; set; }
    public int? service_years { get; set; }
    public decimal? overhead_percentage { get; set; }
    public int? user_access_code { get; set; }
    public string? comments { get; set; }
}
