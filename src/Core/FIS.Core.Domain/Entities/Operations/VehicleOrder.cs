using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

[Table("Vehicle_orders")]
public class VehicleOrder
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("order_id")]
    public int order_id { get; set; }

    [Column("make_code")]
    public short? make_code { get; set; }

    [Column("model_code")]
    public short model_code { get; set; }

    [Column("quantity")]
    public short quantity { get; set; }

    [Column("supplier_id")]
    public short supplier_id { get; set; }

    [StringLength(100)]
    [Column("order_number")]
    public string? order_number { get; set; }

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
