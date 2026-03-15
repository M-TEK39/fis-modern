using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Contracts;

[Table("Contractors")]
public class Contractor
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("contractor_id")]
    public short contractor_id { get; set; }

    [Column("contractor_name")]
    [StringLength(255)]
    public string? contractor_name { get; set; }

    [Column("physical_address")]
    [StringLength(500)]
    public string? physical_address { get; set; }

    [Column("postal_address")]
    [StringLength(500)]
    public string? postal_address { get; set; }

    [Column("tel_number")]
    [StringLength(50)]
    public string? tel_number { get; set; }

    [Column("fax_number")]
    [StringLength(50)]
    public string? fax_number { get; set; }

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
