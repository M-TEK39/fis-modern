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
    public int model_code { get; set; }
    [Required]
    public string? model_name { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Make is required.")]
    public int? make_code { get; set; }
    public string? make_name { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Unit of measure is required.")]
    public int? unit_of_measure_code { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "License type is required.")]
    public int? licence_code { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Class is required.")]
    public int? class_code { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Vehicle type is required.")]
    public int? type_code { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Fuel type is required.")]
    public int? fuel_type_code { get; set; }
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

public record ClassDto
{
    public int class_code { get; set; }
    [Required]
    public string? class_description { get; set; }
}

public record UnitOfMeasureDto
{
    public int unit_of_measure_code { get; set; }
    [Required]
    public string? unit_description { get; set; }
    public string? unit_abbreviation { get; set; }
    public string? unit_category { get; set; }
}

public record LicenseTypeDto
{
    public int licence_code { get; set; }
    [Required]
    public string? licence_description { get; set; }
    public string? licence_category { get; set; }
}
