using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

[Table("Asset_Verification")]
public class AssetVerification
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("asset_verification_code")]
    public int asset_verification_code { get; set; }

    [Column("province")]
    public string? province { get; set; }

    [Column("department_name")]
    public string? department_name { get; set; }

    [Column("site_code")]
    public short? site_code { get; set; }

    [Column("site_name")]
    public string? site_name { get; set; }

    [Column("responsible_manager")]
    public string? responsible_manager { get; set; }

    [Column("tel_no")]
    public string? tel_no { get; set; }

    [Column("fax_no")]
    public string? fax_no { get; set; }

    [Column("vehicle_reg_no")]
    public string? vehicle_reg_no { get; set; }

    [Column("vmf_code")]
    public int? vmf_code { get; set; }

    [Column("verification_date")]
    public DateTime? verification_date { get; set; }

    [Column("verified_by")]
    public int? verified_by { get; set; }

    [Column("verification_status")]
    public string? verification_status { get; set; }

    [Column("notes")]
    public string? notes { get; set; }

    // Navigation properties
    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }

    [ForeignKey("site_code")]
    public virtual Site? Site { get; set; }

    [ForeignKey("verified_by")]
    public virtual User? VerifiedByUser { get; set; }

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

    // Navigation properties for audit trail
    [ForeignKey("created_by_user_code")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("modified_by_user_code")]
    public virtual User? ModifiedByUser { get; set; }
}
