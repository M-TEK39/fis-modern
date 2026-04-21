namespace FIS.Web.Models;

public class AssetVerificationRecord
{
    public int asset_verification_code { get; set; }
    public string? province { get; set; }
    public string? department_name { get; set; }
    public short? site_code { get; set; }
    public string? site_name { get; set; }
    public string? responsible_manager { get; set; }
    public string? tel_no { get; set; }
    public string? fax_no { get; set; }
    public string? vehicle_reg_no { get; set; }
    public int? vmf_code { get; set; }
    public DateTime? verification_date { get; set; }
    public int? verified_by { get; set; }
    public string? verification_status { get; set; }
    public string? notes { get; set; }

    // Legacy asset verification fields (may be added to API)
    public string? vehicle_make { get; set; }
    public string? vehicle_model { get; set; }
    public string? vehicle_colour { get; set; }
    public string? mobitrack_fitted { get; set; }
    public string? petrol_card { get; set; }
    public string? lamination { get; set; }
    public string? tyre_bands { get; set; }
    public string? barcode { get; set; }
    public string? logbook { get; set; }
    public string? gearlock { get; set; }
    public string? radio { get; set; }
    public string? car_keys { get; set; }
    public DateTime? licence_expiry_date { get; set; }
    public string? barcode_number { get; set; }
    public string? vehicle_engine_num { get; set; }
    public string? vehicle_chassis_num { get; set; }
    public int? current_km { get; set; }
    public DateTime? date_last_verified { get; set; }
    public string? comments { get; set; }
}
