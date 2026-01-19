using System.ComponentModel.DataAnnotations;

namespace FIS.Web.Models;

public record MakeDto
{
    public int make_code { get; set; }
    [Required]
    public string? make_name { get; set; }
}

public record ModelDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Model code is required.")]
    public int model_code { get; set; }
    [Required]
    public string? model_name { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Make is required.")]
    public int? make_code { get; set; }
    public string? make_name { get; set; }
}

public record VehicleTypeDto
{
    public int type_code { get; set; }
    [Required]
    public string? type_name { get; set; }
}

public record FuelTypeDto
{
    public int fuel_type_code { get; set; }
    [Required]
    public string? fuel_type_name { get; set; }
}
