using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Operations;

[Table("third_party_allocations")]
public class ThirdPartyAllocation
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("allocation_id")]
    public int allocation_id { get; set; }

    [Column("project_id")]
    public int project_id { get; set; }

    [Column("supplier_id")]
    public short? supplier_id { get; set; }

    [Column("vehicle_id")]
    public int? vehicle_id { get; set; }

    [Column("class_id")]
    public int? class_id { get; set; }

    [Column("quantity")]
    public int? quantity { get; set; }

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

    [ForeignKey("project_id")]
    public virtual ThirdPartyProject? Project { get; set; }

    [ForeignKey("supplier_id")]
    public virtual Supplier? Supplier { get; set; }

    [ForeignKey("vehicle_id")]
    public virtual Vehicle? Vehicle { get; set; }
}
