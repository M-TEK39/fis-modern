using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

[Table("Suppliers")]
public class Supplier
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("supplier_id")]
    public short supplier_id { get; set; }

    [Column("name")]
    public string? name { get; set; }

    [Column("address")]
    public string? address { get; set; }

    [Column("tel")]
    public string? tel { get; set; }

    [Column("fax")]
    public string? fax { get; set; }

    [Column("contact_person")]
    public string? contact_person { get; set; }

    [Column("email")]
    public string? email { get; set; }

    [Column("supplier_type")]
    public string? supplier_type { get; set; }

    [Column("active")]
    public bool? active { get; set; }

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
