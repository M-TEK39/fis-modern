using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Operations;

[Table("third_party_projects")]
public class ThirdPartyProject
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("project_id")]
    public int project_id { get; set; }

    [Column("department_code")]
    public short? department_code { get; set; }

    [Column("site_code")]
    public short? site_code { get; set; }

    [Column("description")]
    [StringLength(255)]
    public string? description { get; set; }

    [Column("start_date")]
    public DateTime? start_date { get; set; }

    [Column("end_date")]
    public DateTime? end_date { get; set; }

    [Column("responsible_person")]
    [StringLength(100)]
    public string? responsible_person { get; set; }

    [Column("rp_physical_address")]
    [StringLength(255)]
    public string? rp_physical_address { get; set; }

    [Column("rp_postal_address")]
    [StringLength(255)]
    public string? rp_postal_address { get; set; }

    [Column("rp_tel")]
    [StringLength(50)]
    public string? rp_tel { get; set; }

    [Column("rp_fax")]
    [StringLength(50)]
    public string? rp_fax { get; set; }

    [Column("rp_email")]
    [StringLength(100)]
    public string? rp_email { get; set; }

    [Column("rp_cell")]
    [StringLength(50)]
    public string? rp_cell { get; set; }

    [Column("notes")]
    [StringLength(500)]
    public string? notes { get; set; }

    [Column("order_reference")]
    [StringLength(50)]
    public string? order_reference { get; set; }

    [Column("class_configuration")]
    [StringLength(255)]
    public string? class_configuration { get; set; }

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

    [ForeignKey("department_code")]
    public virtual Department? Department { get; set; }

    [ForeignKey("site_code")]
    public virtual Site? Site { get; set; }
}
