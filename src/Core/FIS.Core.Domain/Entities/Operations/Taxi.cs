using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// Taxi Entity - Taxi/contracted vehicle request management
/// Maps to legacy 'Taxis' table with exact field names for compatibility
/// </summary>
[Table("Taxis")]
public class Taxi
{
    /// <summary>
    /// Primary key - Request ID
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("request_id")]
    public int request_id { get; set; }

    /// <summary>
    /// Requisition number
    /// </summary>
    [StringLength(50)]
    [Column("rek_num")]
    public string? rek_num { get; set; }

    /// <summary>
    /// Contractor ID
    /// </summary>
    [Column("contractor_id")]
    public short? contractor_id { get; set; }

    /// <summary>
    /// Vehicle code (as string in legacy system)
    /// </summary>
    [StringLength(50)]
    [Column("vmf_code")]
    public string? vmf_code { get; set; }

    /// <summary>
    /// Department code
    /// </summary>
    [Column("department_code")]
    public short? department_code { get; set; }

    /// <summary>
    /// Site code
    /// </summary>
    [Column("site_code")]
    public short site_code { get; set; }

    /// <summary>
    /// Date taxi is required
    /// </summary>
    [Column("date_required")]
    public DateTime date_required { get; set; }

    /// <summary>
    /// Time taxi is required
    /// </summary>
    [Column("time_required")]
    public DateTime time_required { get; set; }

    /// <summary>
    /// Vehicle type code
    /// </summary>
    [Column("vehicle_type_code")]
    public short? vehicle_type_code { get; set; }

    /// <summary>
    /// Official/passenger name
    /// </summary>
    [StringLength(100)]
    [Column("official")]
    public string? official { get; set; }

    /// <summary>
    /// Official rank/position
    /// </summary>
    [StringLength(50)]
    [Column("rank")]
    public string? rank { get; set; }

    [Column("confirmed")]
    public short? confirmed { get; set; }

    [Column("sub_contractor_id")]
    public short? sub_contractor_id { get; set; }

    [StringLength(255)]
    [Column("cancelled")]
    public string? cancelled { get; set; }

    [StringLength(100)]
    [Column("driver")]
    public string? driver { get; set; }

    [StringLength(50)]
    [Column("reg_num")]
    public string? reg_num { get; set; }

    [Column("parent_taxi_code")]
    public int? parent_taxi_code { get; set; }

    /// <summary>
    /// Destination address line 1
    /// </summary>
    [StringLength(200)]
    [Column("address_1")]
    public string? address_1 { get; set; }

    /// <summary>
    /// Destination address line 2
    /// </summary>
    [StringLength(200)]
    [Column("address_2")]
    public string? address_2 { get; set; }

    /// <summary>
    /// Destination address line 3
    /// </summary>
    [StringLength(200)]
    [Column("address_3")]
    public string? address_3 { get; set; }

    /// <summary>
    /// Flight information (if applicable)
    /// </summary>
    [StringLength(100)]
    [Column("flight")]
    public string? flight { get; set; }

    // Legacy request fields. These are intentionally kept out of the EF model because
    // the expanded schema does not contain them. TaxiRepository reads/writes them via
    // guarded compatibility SQL when the client schema provides them.
    [NotMapped]
    public string? instructions { get; set; }

    [NotMapped]
    public string? destination_1 { get; set; }

    [NotMapped]
    public string? destination_2 { get; set; }

    [NotMapped]
    public string? destination_3 { get; set; }

    [NotMapped]
    public short? user_access_code { get; set; }

    [NotMapped]
    public DateTime? request_date { get; set; }

    [NotMapped]
    public string? resp_code { get; set; }

    [NotMapped]
    public string? object_code { get; set; }

    [NotMapped]
    public string? fms_code { get; set; }

    [NotMapped]
    public DateTime? date_required_2 { get; set; }

    [NotMapped]
    public DateTime? time_required_2 { get; set; }

    [NotMapped]
    public string? address_12 { get; set; }

    [NotMapped]
    public string? address_22 { get; set; }

    [NotMapped]
    public string? address_32 { get; set; }

    [NotMapped]
    public string? destination_12 { get; set; }

    [NotMapped]
    public string? destination_22 { get; set; }

    [NotMapped]
    public string? destination_32 { get; set; }

    [NotMapped]
    public string? trans_man_name { get; set; }

    [NotMapped]
    public DateTime? trans_man_date { get; set; }

    [NotMapped]
    public string? trans_man_rank { get; set; }

    [NotMapped]
    public string? trans_man_tel { get; set; }

    [NotMapped]
    public string? booking_by { get; set; }

    [NotMapped]
    public DateTime? arrival_time { get; set; }

    [NotMapped]
    public string? project { get; set; }

    [NotMapped]
    public bool? driver_available { get; set; }

    [NotMapped]
    public string? persal { get; set; }

    [NotMapped]
    public bool? JIA_pickup { get; set; }

    [NotMapped]
    public string? official_tel_num { get; set; }

    [NotMapped]
    public string? fund_code { get; set; }

    // Navigation properties
    /// <summary>
    /// Associated department
    /// </summary>
    [ForeignKey("department_code")]
    public virtual Department? Department { get; set; }

    /// <summary>
    /// Associated site
    /// </summary>
    [ForeignKey("site_code")]
    public virtual Site? Site { get; set; }

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
}
