namespace FIS.Web.Models;

public record DepartmentDto
{
    public int department_code { get; set; }
    public string? department_description { get; set; }
    public string? contact_person { get; set; }
    public string? telephone { get; set; }
    public string? fax { get; set; }
    public string? email { get; set; }
    public string? physical_address { get; set; }
    public string? postal_address { get; set; }
}
