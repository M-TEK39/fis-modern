using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

[Table("vehicle_assessment")]
public class VehicleAssessment
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("vehicle_assessment_code")]
    public int vehicle_assessment_code { get; set; }

    [Column("vmf_code")]
    public int vmf_code { get; set; }

    [Column("spare_wheel")]
    public bool spare_wheel { get; set; }

    [Column("jack")]
    public bool jack { get; set; }

    [Column("wheel_spanner")]
    public bool wheel_spanner { get; set; }

    [Column("wheel_lock_key")]
    public bool wheel_lock_key { get; set; }

    [Column("fuel_card")]
    public bool fuel_card { get; set; }

    [Column("license_disc")]
    public bool license_disc { get; set; }

    [Column("cof_disc")]
    public bool cof_disc { get; set; }

    [Column("fire_extinguisher")]
    public bool? fire_extinguisher { get; set; }

    // The first-aid field exists only on some expanded databases. The
    // procedure-backed compatibility repository reads it when available;
    // keeping it out of the static EF model prevents legacy projections from
    // failing when the column is absent.
    [NotMapped]
    public bool? first_aid_kit { get; set; }

    // Legacy vehicle_assessment stores this value as capture_date.
    [NotMapped]
    public DateTime? assessment_date { get; set; }

    // Legacy vehicle_assessment stores this value as assessment_notes.
    [NotMapped]
    public string? notes { get; set; }

    // Navigation properties
    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }

    // Global audit fields (AI_CODING_RULES.md - Section 4.5)
    [NotMapped]
    public DateTime date_created { get; set; }

    [NotMapped]
    public DateTime? date_updated { get; set; }

    [NotMapped]
    public int? created_by_user_code { get; set; }

    [NotMapped]
    public int? modified_by_user_code { get; set; }

    [NotMapped]
    public bool is_deleted { get; set; } = false;

    // Legacy assessment fields retained for procedure-backed writes. They are
    // intentionally not EF-mapped because the modern entity previously
    // omitted them even though the client schema and stored procedures use
    // them.
    [NotMapped]
    public bool radio { get; set; }

    [NotMapped]
    public bool gear_lock { get; set; }

    [NotMapped]
    public bool logbook { get; set; }

    [NotMapped]
    public string? logbook_start_number { get; set; }

    [NotMapped]
    public string? logbook_end_number { get; set; }

    [NotMapped]
    public bool smash_and_grab { get; set; }

    [NotMapped]
    public bool tracker { get; set; }

    [NotMapped]
    public short? sets_of_keys { get; set; }

    [NotMapped]
    public bool damages { get; set; }

    [Column("assessment_notes")]
    public string? assessment_notes { get; set; }

    [Column("capture_date")]
    public DateTime? capture_date { get; set; }

    [Column("modified_date")]
    public DateTime? modified_date { get; set; }

    [Column("user_access_code")]
    public short? user_access_code { get; set; }

    [Column("user_access_name")]
    public string? user_access_name { get; set; }

    // Navigation properties for audit trail
    [ForeignKey("created_by_user_code")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("modified_by_user_code")]
    public virtual User? ModifiedByUser { get; set; }
}
