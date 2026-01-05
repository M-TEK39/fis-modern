namespace FIS.Web.Models;

public record VehicleDto
{
    // Primary Key
    public int vmf_code { get; set; }
    
    // Core Vehicle Info - Required
    public short model_code { get; set; }
    public string? model_name { get; set; }
    public short type_code { get; set; }
    public string? type_name { get; set; }
    public short vehicle_status_code { get; set; }
    public string? status_description { get; set; }
    public short location_code { get; set; }
    public string? location_description { get; set; }
    
    // Identification
    public string? fleet_number { get; set; }
    public string? registration_number { get; set; }
    public string? asset_number { get; set; }
    public string? engine_number_1 { get; set; }
    public string? chassis_number { get; set; }
    
    // Dates & Odometer - Required
    public DateTime? take_on_date { get; set; }
    public int? take_on_odo { get; set; }
    public int? current_odo { get; set; }
    public int? odo_adjustment { get; set; }
    public DateTime? odo_update_date { get; set; }
    
    // Specifications
    public int? tare { get; set; }
    public int? gvm { get; set; }
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
}

public record PagedResult<T>
{
    public List<T> Data { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

