using System.ComponentModel.DataAnnotations;

namespace FIS.Web.Models;

public record VehicleDto
{
    // Primary Key
    public int vmf_code { get; set; }
    
    // Core Vehicle Info - Required
    [Range(1, short.MaxValue, ErrorMessage = "Model code is required.")]
    public short model_code { get; set; }
    public string? model_name { get; set; }
    [Range(1, short.MaxValue, ErrorMessage = "Type code is required.")]
    public short type_code { get; set; }
    public string? type_name { get; set; }
    [Range(1, short.MaxValue, ErrorMessage = "Vehicle status code is required.")]
    public short vehicle_status_code { get; set; }
    public string? status_description { get; set; }
    [Range(1, short.MaxValue, ErrorMessage = "Location code is required.")]
    public short location_code { get; set; }
    public string? location_description { get; set; }
    
    // Identification
    [Required]
    public string? fleet_number { get; set; }
    [Required]
    public string? registration_number { get; set; }
    public string? previos_gg_number { get; set; }
    public string? followup_gg_number { get; set; }
    public string? asset_number { get; set; }
    [Required]
    public string? engine_number_1 { get; set; }
    [Required]
    public string? chassis_number { get; set; }
    public string? lic_register_number { get; set; }
    
    // Dates & Odometer - Required
    [Required]
    public DateTime? take_on_date { get; set; }
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Take on odometer is required.")]
    public int? take_on_odo { get; set; }
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Current odometer is required.")]
    public int? current_odo { get; set; }
    public int? odo_adjustment { get; set; }
    public DateTime? odo_update_date { get; set; }
    public DateTime? date_First_Regist { get; set; }
    public DateTime? vehicle_status_date { get; set; }
    
    // Specifications
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Tare is required.")]
    public int? tare { get; set; }
    public int? gvm { get; set; }
    [Required]
    [Range(1, short.MaxValue, ErrorMessage = "Year manufactured is required.")]
    public short? year_manufactured { get; set; }
    public string? optional_extras { get; set; }
    public string? colour { get; set; }
    public string? transmission { get; set; }
    public string? tow_hitch { get; set; }
    public string? canopy { get; set; }
    
    // Licensing & Cards
    public DateTime? licence_due_date { get; set; }
    public string? fuel_card_number { get; set; }
    public DateTime? fuel_card_date { get; set; }
    public string? maint_card_number { get; set; }
    public DateTime? maint_card_exdate { get; set; }
    public string? operator_card_number { get; set; }
    
    // Fuel
    public int? additional_fuel_tank { get; set; }
    public decimal? average_consumption { get; set; }
    
    // Financial
    public DateTime? purchase_date { get; set; }
    public decimal? purchase_amount { get; set; }
    public string? purchased_from { get; set; }
    public string? invoice_number { get; set; }
    public decimal? book_value { get; set; }
    public DateTime? book_value_date { get; set; }
    public string? sold_to { get; set; }
    public DateTime? sold_date { get; set; }
    public decimal? sold_amount { get; set; }
    public decimal? monthly_overhead { get; set; }
    
    // Maintenance & COF
    public DateTime? service_last_done { get; set; }
    public int? service_last_odo { get; set; }
    public DateTime? cof_last_done { get; set; }
    public string? cof_required { get; set; }
    public string? cof_number { get; set; }
    public decimal? Cof_amount { get; set; }
    public string? Licence_receiver { get; set; }

    // Audit fields (read-only in UI)
    public DateTime? date_created { get; set; }
    public DateTime? date_updated { get; set; }
    public DateTime? captured_date { get; set; }
}

public record PagedResult<T>
{
    public List<T> Data { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
