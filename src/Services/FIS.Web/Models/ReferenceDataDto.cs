namespace FIS.Web.Models;

public record MakeDto
{
    public int make_code { get; set; }
    public string? make_name { get; set; }
}

public record ModelDto
{
    public int model_code { get; set; }
    public string? model_name { get; set; }
    public int? make_code { get; set; }
    public string? make_name { get; set; }
}

public record VehicleTypeDto
{
    public int type_code { get; set; }
    public string? type_name { get; set; }
}

public record FuelTypeDto
{
    public int fuel_type_code { get; set; }
    public string? fuel_type_name { get; set; }
}
