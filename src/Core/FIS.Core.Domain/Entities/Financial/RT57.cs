using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Financial;

[Table("RT57", Schema = "fin")]
public class RT57
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; } // Added missing PK

    [Column("ITEM NUMBER")]
    [StringLength(50)]
    public string? ITEM_NUMBER { get; set; }

    [Column("ITEM DESCRIPTION")]
    [StringLength(255)]
    public string? ITEM_DESCRIPTION { get; set; }

    [Column("BRAND")]
    [StringLength(100)]
    public string? BRAND { get; set; }

    [Column("PRICE")]
    public decimal? PRICE { get; set; }

    [Column("CLASS")]
    public int? CLASS { get; set; }

    [Column("Category")]
    public byte? Category { get; set; }

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
