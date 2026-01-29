namespace FIS.Web.Models;

public class VehicleLookupDto
{
    public int VmfCode { get; set; }
    public string? GGNumber { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? MakeAndModel { get; set; }
    public short? YearManufactured { get; set; }
    public string? Colour { get; set; }
    public string? HireType { get; set; }
    public string? Status { get; set; }
    public string? HiredFrom { get; set; }
    public DateTime? StatusDate { get; set; }
}
