using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

[Table("accident")]
public class Accident
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("accident_code")]
    public int accident_code { get; set; }

    [Column("vmf_code")]
    public int vmf_code { get; set; }

    [Column("posting_month_code")]
    public short? posting_month_code { get; set; }

    [Column("description")]
    public string? description { get; set; }

    [Column("driver_name")]
    public string? driver_name { get; set; }

    [Column("driver_employ_number")]
    public string? driver_employ_number { get; set; }

    [Column("hq_reference")]
    public string? hq_reference { get; set; }

    [Column("gg_reference")]
    public string? gg_reference { get; set; }

    [Column("sa_reference")]
    public string? sa_reference { get; set; }

    [Column("occurence_date")]
    public DateTime? occurence_date { get; set; }

    [Column("occurence_time")]
    public DateTime? occurence_time { get; set; }

    [Column("reported_date")]
    public DateTime? reported_date { get; set; }

    [Column("claim_amount")]
    public decimal? claim_amount { get; set; }

    [Column("excess_amount")]
    public decimal? excess_amount { get; set; }

    // Legacy garage/call-centre fields. These are read and written by the
    // compatibility repository because they are absent from the expanded
    // EF model on some databases.
    [NotMapped] public decimal? Call_Refer { get; set; }
    [NotMapped] public string? captured_person { get; set; }
    [NotMapped] public string? fin_year { get; set; }
    [NotMapped] public string? garage { get; set; }
    [NotMapped] public string? driver_telno { get; set; }
    [NotMapped] public short? driver_site_code { get; set; }
    [NotMapped] public string? transoffic_name { get; set; }
    [NotMapped] public string? transoffic_tel { get; set; }
    [NotMapped] public decimal? accident_km { get; set; }
    [NotMapped] public short? acc_type_code { get; set; }
    [NotMapped] public string? Flag_gg_hq { get; set; }
    [NotMapped] public DateTime? Flag_gg_hq_date { get; set; }
    [NotMapped] public DateTime? file_close_date { get; set; }
    [NotMapped] public string? case_number { get; set; }
    [NotMapped] public string? reporting_authority { get; set; }
    [NotMapped] public decimal? cost_of_repair { get; set; }
    [NotMapped] public string? damage_description { get; set; }
    [NotMapped] public string? death { get; set; }
    [NotMapped] public string? injured { get; set; }
    [NotMapped] public string? third_party_regno { get; set; }
    [NotMapped] public string? third_party_owner { get; set; }
    [NotMapped] public string? third_party_tel { get; set; }
    [NotMapped] public decimal? third_party_claim { get; set; }
    [NotMapped] public string? SecondThirdPartyRegNo { get; set; }
    [NotMapped] public string? th_claim_receive { get; set; }
    [NotMapped] public decimal? claim_against_dept { get; set; }
    [NotMapped] public string? letterhead { get; set; }
    [NotMapped] public string? z181 { get; set; }
    [NotMapped] public string? part3 { get; set; }
    [NotMapped] public string? statement { get; set; }
    [NotMapped] public string? sketch { get; set; }
    [NotMapped] public string? iddoc { get; set; }
    [NotMapped] public string? drivelic { get; set; }
    [NotMapped] public string? docs_acc_relieve { get; set; }
    [NotMapped] public string? flag_case_num { get; set; }
    [NotMapped] public string? trip_author { get; set; }
    [NotMapped] public string? Flag_trip_author { get; set; }
    [NotMapped] public DateTime? Flag_trip_auth_date { get; set; }
    [NotMapped] public string? driver_fault { get; set; }
    [NotMapped] public string? attorney_insure { get; set; }
    [NotMapped] public string? insurance_claim { get; set; }
    [NotMapped] public DateTime? priv_dampay_date { get; set; }
    [NotMapped] public string? th_claim_accept_reject { get; set; }
    [NotMapped] public string? th_claim_reject_reason { get; set; }
    [NotMapped] public decimal? write_off_amount { get; set; }
    [NotMapped] public DateTime? write_off_date { get; set; }
    [NotMapped] public string? occurence_place { get; set; }
    [NotMapped] public string? Tow_need { get; set; }
    [NotMapped] public string? notes { get; set; }

    // Navigation properties
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
