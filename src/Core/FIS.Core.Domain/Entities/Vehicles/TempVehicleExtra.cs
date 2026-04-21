using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Vehicles;

[Table("Temp_Vehicle_extras")]
public class TempVehicleExtra
{
    [Key]
    [Column("extras_code")]
    public int extras_code { get; set; } // Changed to int to match Database.cs

    [Column("temp_vmf_code")]
    public int temp_vmf_code { get; set; } // Corrected from vmf_code

    [Column("extra_code")]
    public short extra_code { get; set; }

    [Column("quantity")]
    public short quantity { get; set; }

    [Column("amount")]
    public decimal? amount { get; set; }

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
