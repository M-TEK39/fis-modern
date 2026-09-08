using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Vehicles;

[Table("class")]
public class Class
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("class_code")]
    public short class_code { get; set; }

    [Column("description")]
    [StringLength(60)]
    public string? description { get; set; }

    // Legacy class fields. These remain part of the compatibility contract even
    // when the expanded schema does not contain the columns.
    [Column("class_number")]
    [StringLength(30)]
    public string? class_number { get; set; }

    [Column("bank_number")]
    [StringLength(30)]
    public string? bank_number { get; set; }

    [Column("months_life")]
    public short? months_life { get; set; }

    [Column("depreciation_percent")]
    public decimal? depreciation_percent { get; set; }

    [Column("odometer_life")]
    public decimal? odometer_life { get; set; }

    [Column("appreciate_percent")]
    public short? appreciate_percent { get; set; }

    [Column("replacement_cost")]
    public decimal? replacement_cost { get; set; }

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

    // Navigation properties
    [ForeignKey("created_by_user_code")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("modified_by_user_code")]
    public virtual User? ModifiedByUser { get; set; }
}
