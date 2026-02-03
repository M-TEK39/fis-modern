using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Locations;

[Table("Ambulance")]
public class Ambulance
{
    [Key]
    [Column("Ambulance_code")]
    public short Ambulance_code { get; set; }

    [Column("Amb_area")]
    [StringLength(255)]
    public string? Amb_area { get; set; }

    [Column("Amb_name")]
    [StringLength(255)]
    public string? Amb_name { get; set; }

    [Column("Amb_tel")]
    [StringLength(50)]
    public string? Amb_tel { get; set; }

    [Column("Amb_fax")]
    [StringLength(50)]
    public string? Amb_fax { get; set; }

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
