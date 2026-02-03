using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Auth;

[Table("UserCompany", Schema = "Configuration")]
public class UserCompany
{
    [Key]
    [Column("UserCompanyID")]
    public int UserCompanyID { get; set; }

    [Column("Code")]
    [StringLength(50)]
    public string? Code { get; set; }

    [Column("Name")]
    [StringLength(255)]
    public string? Name { get; set; }

    [Column("ReportLogo")]
    public byte[]? ReportLogo { get; set; }

    [Column("ReportLogoMimeType")]
    [StringLength(50)]
    public string? ReportLogoMimeType { get; set; }

    [Column("ReportLogoFilename")]
    [StringLength(255)]
    public string? ReportLogoFilename { get; set; }

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
