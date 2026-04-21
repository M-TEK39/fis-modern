using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.WorkshopEntities;

[Table("wwmerchant")]
public class Merchant
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("wwmerch_code")]
    public int wwmerch_code { get; set; }

    [Column("wwmerch_name")]
    [StringLength(255)]
    public string? wwmerch_name { get; set; }

    [Column("wwmerch_tel")]
    [StringLength(50)]
    public string? wwmerch_tel { get; set; }

    [Column("wwmerch_fax")]
    [StringLength(50)]
    public string? wwmerch_fax { get; set; }

    [Column("wwmerch_email")]
    [StringLength(255)]
    public string? wwmerch_email { get; set; }

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
