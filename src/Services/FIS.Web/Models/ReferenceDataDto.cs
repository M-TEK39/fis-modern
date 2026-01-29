using System.ComponentModel.DataAnnotations;

namespace FIS.Web.Models;

public record MakeDto
{
    public int make_code { get; set; }
    [Required]
    public string? make_name { get; set; }
    public DateTime? date_updated { get; set; }
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
    [Range(1, int.MaxValue, ErrorMessage = "License fee is required.")]
    public int? licence_fee_code { get; set; }
    public int? maint_trigger_code { get; set; }
    public string? engine_type { get; set; }
    public int? engine_capacity { get; set; }
    public int? rated_power { get; set; }
    public int? fuel_tank_capacity { get; set; }
    public decimal? target_consumption { get; set; }
    public int? target_tyre_life { get; set; }
    public int? service_interval { get; set; }
    public string? vemm_code { get; set; }
    public int? gvm { get; set; }
    public string? transmission { get; set; }
    public decimal? wesbank_kilos_per_litre { get; set; }
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
    public decimal? rate_per_litre { get; set; }
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

public record LicenseFeeDto
{
    public int licence_fee_code { get; set; }
    [Required]
    public string? licence_description { get; set; }
    public decimal? licence_fee { get; set; }
}

public record DriverLicenceDto
{
    public int licence_code { get; set; }
    [Required]
    public string? description { get; set; }
}

public record ExtraCodeDto
{
    public int extra_code { get; set; }
    [Required]
    public string? extra_description { get; set; }
}

public record LossTypeDto
{
    public int loss_code { get; set; }
    [Required]
    public string? loss_description { get; set; }
}

public record ProvinceDto
{
    public string? province_code { get; set; }
    public string? province_name { get; set; }
}
