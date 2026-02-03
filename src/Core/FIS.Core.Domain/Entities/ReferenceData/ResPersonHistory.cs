using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.ReferenceData;

[Table("res_person_history")]
public class ResPersonHistory
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; } // Surrogate PK

    [Column("department_code")]
    public int? department_code { get; set; }

    [Column("department_number")]
    [StringLength(50)]
    public string? department_number { get; set; }

    [Column("site_code")]
    public int? site_code { get; set; }

    [Column("res_person")]
    [StringLength(255)]
    public string? res_person { get; set; }

    [Column("telephone")]
    [StringLength(50)]
    public string? telephone { get; set; }

    [Column("net_address")]
    [StringLength(255)]
    public string? net_address { get; set; }

    [Column("date_changed")]
    public DateTime date_changed { get; set; }

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
