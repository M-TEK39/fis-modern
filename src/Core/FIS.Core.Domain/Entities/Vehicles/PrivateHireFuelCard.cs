using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Vehicles;

[Table("PrivHireFuel_card")]
public class PrivateHireFuelCard
{
    [Key]
    [Column("PHFuel_card_code")]
    public int PHFuel_card_code { get; set; }

    [Column("phv_code")]
    public int phv_code { get; set; }

    [Column("Counter")]
    public short? Counter { get; set; }

    [Column("card_number")]
    [StringLength(50)]
    public string? card_number { get; set; }

    [Column("PAN_number")]
    [StringLength(50)]
    public string? PAN_number { get; set; }

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
}
