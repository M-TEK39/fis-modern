using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Vehicles;

[Table("extras")]
public class Extra
{
    [Key]
    [Column("extras_code")]
    public short extras_code { get; set; }

    [Column("vmf_code")]
    public int vmf_code { get; set; }

    [Column("extra_code")]
    public short extra_code { get; set; }

    [Column("quantity")]
    public short quantity { get; set; }

    [Column("amount")]
    public decimal? amount { get; set; }

    [Column("serial_number")]
    [StringLength(50)]
    public string? serial_number { get; set; }

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
}
