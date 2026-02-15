using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Operations;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// Vehicle Entity - EXACT legacy schema match for vehicle_master table
/// Uses exact field names from Database.cs - NO modernization
/// </summary>
[Table("vehicle_master")]
public class Vehicle
{
    [Key]
    [Column("vmf_code")]
    public int vmf_code { get; set; }

    [Column("model_code")]
    public short model_code { get; set; }

    [Column("type_code")]
    public short type_code { get; set; }

    [Column("vehicle_status_code")]
    public short vehicle_status_code { get; set; }

    [Column("location_code")]
    public short location_code { get; set; }

    [Column("fleet_number")]
    public string? fleet_number { get; set; }

    [Column("registration_number")]
    public string? registration_number { get; set; }

    [Column("take_on_date")]
    public DateTime take_on_date { get; set; }

    [Column("take_on_odo")]
    public int take_on_odo { get; set; }

    [Column("current_odo")]
    public int current_odo { get; set; }

    [Column("odo_adjustment")]
    public int? odo_adjustment { get; set; }

    [Column("derived_odo")]
    public string? derived_odo { get; set; }

    [Column("odo_update_date")]
    public DateTime? odo_update_date { get; set; }

    [Column("engine_number_1")]
    public string? engine_number_1 { get; set; }

    [Column("chassis_number")]
    public string? chassis_number { get; set; }

    [Column("tare")]
    public int? tare { get; set; }

    [Column("gvm")]
    public int? gvm { get; set; }

    [Column("year_manufactured")]
    public short? year_manufactured { get; set; }

    [Column("optional_extras")]
    public string? optional_extras { get; set; }

    [Column("licence_due_date")]
    public DateTime? licence_due_date { get; set; }

    [Column("additional_fuel_tank")]
    public int? additional_fuel_tank { get; set; }

    [Column("average_consumption")]
    public decimal? average_consumption { get; set; }

    [Column("fuel_card_number")]
    public string? fuel_card_number { get; set; }

    [Column("fuel_card_date")]
    public DateTime? fuel_card_date { get; set; }

    [Column("purchase_date")]
    public DateTime? purchase_date { get; set; }

    [Column("purchase_amount")]
    public decimal? purchase_amount { get; set; }

    [Column("book_value")]
    public decimal? book_value { get; set; }

    [Column("book_value_date")]
    public DateTime? book_value_date { get; set; }

    [Column("maint_card_number")]
    public string? maint_card_number { get; set; }

    [Column("maint_card_exdate")]
    public DateTime? maint_card_exdate { get; set; }

    [Column("purchased_from")]
    public string? purchased_from { get; set; }

    [Column("sold_to")]
    public string? sold_to { get; set; }

    [Column("sold_date")]
    public DateTime? sold_date { get; set; }

    [Column("sold_amount")]
    public decimal? sold_amount { get; set; }

    [Column("service_last_done")]
    public DateTime? service_last_done { get; set; }

    [Column("service_last_odo")]
    public int? service_last_odo { get; set; }

    [Column("cof_last_done")]
    public DateTime? cof_last_done { get; set; }

    [Column("cof_required")]
    public string? cof_required { get; set; }

    [Column("cof_number")]
    public string? cof_number { get; set; }

    [Column("operator_card_number")]
    public string? operator_card_number { get; set; }

    [Column("monthly_overhead")]
    public decimal? monthly_overhead { get; set; }

    [Column("colour")]
    public string? colour { get; set; }

    [Column("tow_hitch")]
    public string? tow_hitch { get; set; }

    [Column("canopy")]
    public string? canopy { get; set; }

    [Column("Cof_amount")]
    public decimal? Cof_amount { get; set; }

    [Column("Licence_receiver")]
    public string? Licence_receiver { get; set; }

    [Column("Licence_receiver_id")]
    public string? Licence_receiver_id { get; set; }

    [Column("Licence_receiver_tel")]
    public string? Licence_receiver_tel { get; set; }

    [Column("Licence_receiver_site")]
    public short? Licence_receiver_site { get; set; }

    [Column("Licence_date_taken")]
    public DateTime? Licence_date_taken { get; set; }

    [Column("highest_km")]
    public decimal? highest_km { get; set; }

    [Column("fuel_ltd")]
    public decimal? fuel_ltd { get; set; }

    [Column("fuel_ytd")]
    public decimal? fuel_ytd { get; set; }

    [Column("fuel_3month_average")]
    public decimal? fuel_3month_average { get; set; }

    [Column("oil_ltd")]
    public decimal? oil_ltd { get; set; }

    [Column("oil_ytd")]
    public decimal? oil_ytd { get; set; }

    [Column("oil_3month_average")]
    public decimal? oil_3month_average { get; set; }

    [Column("maint_ltd")]
    public decimal? maint_ltd { get; set; }

    [Column("maint_ytd")]
    public decimal? maint_ytd { get; set; }

    [Column("maint_3month_average")]
    public decimal? maint_3month_average { get; set; }

    [Column("repairs_ltd")]
    public decimal? repairs_ltd { get; set; }

    [Column("repairs_ytd")]
    public decimal? repairs_ytd { get; set; }

    [Column("repairs_3month_average")]
    public decimal? repairs_3month_average { get; set; }

    [Column("tyres_ltd")]
    public decimal? tyres_ltd { get; set; }

    [Column("tyres_ytd")]
    public decimal? tyres_ytd { get; set; }

    [Column("tyres_3month_average")]
    public decimal? tyres_3month_average { get; set; }

    [Column("accident_ltd")]
    public decimal? accident_ltd { get; set; }

    [Column("accident_ytd")]
    public decimal? accident_ytd { get; set; }

    [Column("accident_3month_average")]
    public decimal? accident_3month_average { get; set; }

    [Column("toll_ltd")]
    public decimal? toll_ltd { get; set; }

    [Column("toll_ytd")]
    public decimal? toll_ytd { get; set; }

    [Column("toll_3month_average")]
    public decimal? toll_3month_average { get; set; }

    [Column("other_ltd")]
    public decimal? other_ltd { get; set; }

    [Column("other_ytd")]
    public decimal? other_ytd { get; set; }

    [Column("other_3month_average")]
    public decimal? other_3month_average { get; set; }

    [Column("km_ltd")]
    public int? km_ltd { get; set; }

    [Column("km_ytd")]
    public int? km_ytd { get; set; }

    [Column("km_3month_average")]
    public int? km_3month_average { get; set; }

    [Column("lic_register_number")]
    public string? lic_register_number { get; set; }

    [Column("lic_registration_doc")]
    public string? lic_registration_doc { get; set; }

    [Column("licence_comments")]
    public string? licence_comments { get; set; }

    [Column("default_site")]
    public short? default_site { get; set; }

    [Column("previos_gg_number")]
    public string? previos_gg_number { get; set; }

    [Column("followup_gg_number")]
    public string? followup_gg_number { get; set; }

    [Column("vehicle_status_date")]
    public DateTime? vehicle_status_date { get; set; }

    [Column("renumbered_to")]
    public string? renumbered_to { get; set; }

    [Column("barcode")]
    public string? barcode { get; set; }

    [Column("user_access_code")]
    public short? user_access_code { get; set; }

    [Column("captured_date")]
    public DateTime? captured_date { get; set; }

    [Column("reserved")]
    public short? reserved { get; set; }

    [Column("LPG")]
    public bool? LPG { get; set; }

    [Column("extended_service")]
    public string? extended_service { get; set; }

    [Column("destroyed_date")]
    public DateTime? destroyed_date { get; set; }

    [Column("destroyed_amount")]
    public decimal? destroyed_amount { get; set; }

    [Column("destroyed_receipt")]
    public string? destroyed_receipt { get; set; }

    [Column("previos_gg_number_2")]
    public string? previos_gg_number_2 { get; set; }

    [Column("date_First_Regist")]
    public DateTime? date_First_Regist { get; set; }

    [Column("vs_code")]
    public byte? vs_code { get; set; }

    [Column("invoice_number")]
    public string? invoice_number { get; set; }

    // IFMS / NATIS acquisition identifiers (Fleet Acquisition capture)
    [Column("ifms_vehicle_register_number")]
    [StringLength(50)]
    public string? ifms_vehicle_register_number { get; set; }

    [Column("natis_model_number")]
    [StringLength(50)]
    public string? natis_model_number { get; set; }

    [Column("RelieveVehicle")]
    public bool? RelieveVehicle { get; set; }

    [Column("initial_site_code")]
    public short? initial_site_code { get; set; }

    [Column("veh_site_code")]
    public short? veh_site_code { get; set; }

    [Column("temp_vmf_code")]
    public int? temp_vmf_code { get; set; }

    [Column("supplier_id")]
    public short? supplier_id { get; set; }

    // Global audit fields (AI_CODING_RULES.md - Section 4.5)
    [Column("date_created")]
    public DateTime date_created { get; set; }

    [Column("date_updated")]
    public DateTime? date_updated { get; set; }

    [Column("created_by_user_code")]
    public int? created_by_user_code { get; set; }

    [Column("modified_by_user_code")]
    public int? modified_by_user_code { get; set; }

    [Column("is_deleted")]
    public bool is_deleted { get; set; } = false;

    // Navigation properties for audit trail
    [ForeignKey("created_by_user_code")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("modified_by_user_code")]
    public virtual User? ModifiedByUser { get; set; }

    // Business entity navigation properties
    [ForeignKey("model_code")]
    public virtual Model? Model { get; set; }

    [ForeignKey("supplier_id")]
    public virtual Supplier? Supplier { get; set; }
}
