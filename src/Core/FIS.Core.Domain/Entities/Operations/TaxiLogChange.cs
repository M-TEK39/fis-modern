using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Operations;

[Table("Taxi_Log_changes")]
public class TaxiLogChange
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; } // Surrogate PK

    [Column("ID")]
    public short LegacyId { get; set; }

    [Column("rek_num")]
    [StringLength(50)]
    public string? rek_num { get; set; }

    [Column("days")]
    public short? days { get; set; }

    [Column("hours")]
    public double? hours { get; set; }

    [Column("km")]
    public decimal? km { get; set; }

    [Column("date_changed")]
    public DateTime? date_changed { get; set; }

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
