using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Vehicles;

[Table("VehicleKilos")]
public class VehicleKilo
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; } // Added missing PK

    [Column("vmf_code")]
    public int? vmf_code { get; set; }

    [Column("registration_number")]
    [StringLength(50)]
    public string? registration_number { get; set; }

    [Column("fleet_number")]
    [StringLength(50)]
    public string? fleet_number { get; set; }

    [Column("start_odo")]
    public double? start_odo { get; set; }

    [Column("end_odo")]
    public double? end_odo { get; set; }

    [Column("contract_code")]
    public int? contract_code { get; set; }

    // Global audit fields
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

    // Navigation properties
    [ForeignKey("created_by_user_code")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("modified_by_user_code")]
    public virtual User? ModifiedByUser { get; set; }

    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }

    [ForeignKey("contract_code")]
    public virtual Contract? Contract { get; set; }
}
