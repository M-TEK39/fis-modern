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
