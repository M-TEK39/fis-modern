using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Drivers;

/// <summary>
/// Trip-driver assignment. The archived client table is trip_drivers and has
/// no modern audit columns. Persistence goes through TripDriverRepository
/// (procedure-first, then a guarded dynamic fallback) rather than this EF
/// mapping. site_driver_code is the insert-time lookup key required by
/// DEV_INS_TripDrivers; it is not a trip_drivers column.
/// </summary>
[Table("trip_driver")]
public class TripDriver
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("trip_driver_code")]
    public int trip_driver_code { get; set; }

    [Column("trip_driver_name")]
    public string? trip_driver_name { get; set; }

    [Column("trip_driver_id")]
    public string? trip_driver_id { get; set; }

    [Column("trip_authority_code")]
    public int trip_authority_code { get; set; }

    [Column("trip_driver_primary")]
    public bool trip_driver_primary { get; set; }

    [Column("site_code")]
    public int? site_code { get; set; }

    [Column("driver_licence_type_id")]
    public int? driver_licence_type_id { get; set; }

    [Column("driver_passportnumber")]
    public string? driver_passportnumber { get; set; }

    [Column("driver_persalnumber")]
    public string? driver_persalnumber { get; set; }

    [Column("driver_contractnumber")]
    public string? driver_contractnumber { get; set; }

    [Column("driver_licence_number")]
    public string? driver_licence_number { get; set; }

    [Column("driver_licence_issuedate")]
    public DateTime? driver_licence_issuedate { get; set; }

    [Column("driver_licence_lastVerifiedDate")]
    public DateTime? driver_licence_lastVerifiedDate { get; set; }

    [Column("driver_hasPDP")]
    public bool driver_hasPDP { get; set; }

    [Column("driver_PDP_ExpiryDate")]
    public DateTime? driver_PDP_ExpiryDate { get; set; }

    [Column("driver_licence_ExpiryDate")]
    public DateTime? driver_licence_ExpiryDate { get; set; }

    [Column("driver_active")]
    public bool driver_active { get; set; }

    /// <summary>
    /// Site-driver primary key used by DEV_INS_TripDrivers. Not stored on
    /// trip_drivers; the procedure copies identity and licence fields from
    /// site_drivers.
    /// </summary>
    [NotMapped]
    public int site_driver_code { get; set; }

    // Optional expanded audit fields. The original trip_drivers table does
    // not contain them; TripDriverRepository projects them only when present.
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
