using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.System;

[Table("tyda")]
public class Tyda
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; } // Surrogate PK

    [Column("xf_nom")]
    [StringLength(50)]
    public string? xf_nom { get; set; }

    [Column("xd_nom")]
    [StringLength(50)]
    public string? xd_nom { get; set; }

    [Column("xs_odo")]
    public int? xs_odo { get; set; }

    [Column("xe_odo")]
    public int? xe_odo { get; set; }

    [Column("xs_dat")]
    public DateTime? xs_dat { get; set; }

    [Column("xe_dat")]
    public DateTime? xe_dat { get; set; }

    [Column("xreknum")]
    [StringLength(50)]
    public string? xreknum { get; set; }

    [Column("xtrdat")]
    public DateTime? xtrdat { get; set; }

    [Column("xclas")]
    public int? xclas { get; set; }

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
