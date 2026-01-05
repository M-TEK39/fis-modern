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

    [Column("first_aid_kit")]
    public bool? first_aid_kit { get; set; }

    [Column("assessment_date")]
    public DateTime? assessment_date { get; set; }

    [Column("notes")]
    public string? notes { get; set; }

    // Navigation properties
    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }
}
