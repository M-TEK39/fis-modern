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
}
