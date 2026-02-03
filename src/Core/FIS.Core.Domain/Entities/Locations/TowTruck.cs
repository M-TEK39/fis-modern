using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Locations;

[Table("Tow_Truck")]
public class TowTruck
{
    [Key]
    [Column("Tow_code")]
    public short Tow_code { get; set; }

    [Column("Tow_area")]
    [StringLength(255)]
    public string? Tow_area { get; set; }

    [Column("Tow_name")]
    [StringLength(255)]
    public string? Tow_name { get; set; }

    [Column("Tow_tel")]
    [StringLength(50)]
    public string? Tow_tel { get; set; }

    [Column("Tow_fax")]
    [StringLength(50)]
    public string? Tow_fax { get; set; }

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
