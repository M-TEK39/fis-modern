namespace FIS.Web.Models;

public class VehiclePhotoDto
{
    public int VehiclePhotoInfoCode { get; set; }
    public int VehicleMasterCode { get; set; }
    public string? FileUrl { get; set; }
    public int? Orientation { get; set; }
    public string? Description { get; set; }
}
