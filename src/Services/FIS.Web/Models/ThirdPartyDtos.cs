using System.ComponentModel.DataAnnotations;

namespace FIS.Web.Models;

public sealed record ThirdPartySupplierDto
{
    public int supplier_id { get; set; }
    public string? name { get; set; }
    public string? address { get; set; }
    public string? postal_address { get; set; }
    public string? tel { get; set; }
    public string? fax { get; set; }
    public string? cell { get; set; }
    public string? email { get; set; }
    public string? contact_person { get; set; }
    public string? notes { get; set; }
    public string? service_code { get; set; }
    public bool? active { get; set; }
}

public sealed record ThirdPartySupplierCreateDto
{
    [Required]
    public string? name { get; set; }
    public string? address { get; set; }
    public string? postal_address { get; set; }
    public string? tel { get; set; }
    public string? fax { get; set; }
    public string? cell { get; set; }
    public string? email { get; set; }
    public string? contact_person { get; set; }
    public string? notes { get; set; }
    public string? service_code { get; set; }
    public bool? active { get; set; }
}

public sealed record ThirdPartyProjectDto
{
    public int project_id { get; set; }
    public short? department_code { get; set; }
    public short? site_code { get; set; }
    public string? description { get; set; }
    public DateTime? start_date { get; set; }
    public DateTime? end_date { get; set; }
    public string? responsible_person { get; set; }
    public string? rp_physical_address { get; set; }
    public string? rp_postal_address { get; set; }
    public string? rp_tel { get; set; }
    public string? rp_fax { get; set; }
    public string? rp_email { get; set; }
    public string? rp_cell { get; set; }
    public string? notes { get; set; }
    public string? order_reference { get; set; }
    public string? class_configuration { get; set; }
}

public sealed record ThirdPartyProjectCreateDto
{
    [Required]
    public short? department_code { get; set; }
    public short? site_code { get; set; }
    [Required]
    public string? description { get; set; }
    public DateTime? start_date { get; set; }
    public DateTime? end_date { get; set; }
    public string? responsible_person { get; set; }
    public string? rp_physical_address { get; set; }
    public string? rp_postal_address { get; set; }
    public string? rp_tel { get; set; }
    public string? rp_fax { get; set; }
    public string? rp_email { get; set; }
    public string? rp_cell { get; set; }
    public string? notes { get; set; }
    public string? order_reference { get; set; }
    public string? class_configuration { get; set; }
}

public sealed record ThirdPartyAllocationDto
{
    public int allocation_id { get; set; }
    public int project_id { get; set; }
    public int? supplier_id { get; set; }
    public int? vehicle_id { get; set; }
    public int? class_id { get; set; }
    public int? quantity { get; set; }
}

public sealed record ThirdPartyAllocationCreateDto
{
    [Required]
    public int project_id { get; set; }
    public int? supplier_id { get; set; }
    public int? vehicle_id { get; set; }
    public int? class_id { get; set; }
    public int? quantity { get; set; }
}

public sealed record ThirdPartyVehicleDto
{
    public int vehicle_id { get; set; }
    public string? registration_number { get; set; }
    public string? model_description { get; set; }
    public string? model_year { get; set; }
    public string? chassis_number { get; set; }
}

public sealed record ThirdPartyClassRequirementDto
{
    public int class_id { get; set; }
    public string? class_name { get; set; }
    public int? required_count { get; set; }
}
