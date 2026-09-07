using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

[Table("workshop")]
public class Workshop
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("ww_code")]
    public short ww_code { get; set; }

    [Column("vmf_code")]
    public int? vmf_code { get; set; }

    [Column("receive_time")]
    public TimeSpan? receive_time { get; set; }

    [Column("receive_date")]
    public DateTime? receive_date { get; set; }

    [Column("complete_time")]
    public TimeSpan? complete_time { get; set; }

    // The client schema keeps the workshop date and time separately. These
    // legacy fields are intentionally exposed by the API, but are not mapped
    // by EF because the expanded schema does not contain all of them.
    [NotMapped]
    public DateTime? complete_date { get; set; }

    [NotMapped]
    public TimeSpan? fetch_time { get; set; }

    [NotMapped]
    public DateTime? fetch_date { get; set; }

    [NotMapped]
    public string? driver_name { get; set; }

    [NotMapped]
    public string? contact_name { get; set; }

    [NotMapped]
    public string? contact_tel { get; set; }

    [NotMapped]
    public string? ww_remarks { get; set; }

    [NotMapped]
    public string? ww_reason { get; set; }

    [NotMapped]
    public string? accid_mech { get; set; }

    [NotMapped]
    public short? ww_site { get; set; }

    [NotMapped]
    public string? contact_fax { get; set; }

    [NotMapped]
    public string? contact_email { get; set; }

    [NotMapped]
    public decimal? call_refer { get; set; }

    [NotMapped]
    public decimal? ww_km { get; set; }

    [NotMapped]
    public decimal? recover_cost { get; set; }

    [NotMapped]
    public string? rem_other_repair { get; set; }

    [NotMapped]
    public int? tow_comp { get; set; }

    [NotMapped]
    public decimal? tow_amount { get; set; }

    [NotMapped]
    public string? spare_wheel { get; set; }

    [NotMapped]
    public string? jack { get; set; }

    [NotMapped]
    public string? wheel_spanner { get; set; }

    [NotMapped]
    public string? radio { get; set; }

    [NotMapped]
    public string? two_way { get; set; }

    [NotMapped]
    public string? gear_lock { get; set; }

    [NotMapped]
    public string? keys { get; set; }

    [NotMapped]
    public string? fuel { get; set; }

    [NotMapped]
    public string? inter_exter { get; set; }

    [NotMapped]
    public int? merch_code { get; set; }

    [NotMapped]
    public DateTime? date_to_merch { get; set; }

    [NotMapped]
    public decimal? km_to_merch { get; set; }

    [NotMapped]
    public DateTime? date_from_merch { get; set; }

    [NotMapped]
    public decimal? km_from_merch { get; set; }

    [NotMapped]
    public decimal? cost_repair { get; set; }

    [NotMapped]
    public decimal? points { get; set; }

    [NotMapped]
    public string? fa_auth_num { get; set; }

    [NotMapped]
    public string? test_name { get; set; }

    [NotMapped]
    public string? inform_admin { get; set; }

    [NotMapped]
    public DateTime? date_from_ww { get; set; }

    [NotMapped]
    public TimeSpan? time_from_ww { get; set; }

    [NotMapped]
    public string? fetch_name { get; set; }

    [NotMapped]
    public string? job_close { get; set; }

    [NotMapped]
    public string? garage { get; set; }

    [NotMapped]
    public string? blue_light { get; set; }

    [NotMapped]
    public short? monitor_refer { get; set; }

    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }

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
