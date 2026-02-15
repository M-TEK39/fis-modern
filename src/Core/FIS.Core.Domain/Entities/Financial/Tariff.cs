using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Financial;

/// <summary>
/// Legacy tariff table (pre-2009 tariff system).
/// Contains tariff rates by vehicle class and year manufactured.
/// Maps to legacy dbo.tariff table.
/// </summary>
[Table("tariff")]
public class Tariff
{
    [Key]
    [Column("tariff_code")]
    public int tariff_code { get; set; }

    [Column("class_code")]
    public short class_code { get; set; }

    [Column("year_manufactured")]
    public short? year_manufactured { get; set; }

    // Fixed rate components
    [Column("monthly_fixed_amount")]
    public decimal monthly_fixed_amount { get; set; }

    [Column("monthly_odo_amount")]
    public decimal monthly_odo_amount { get; set; }

    [Column("daily_fixed_amount")]
    public decimal? daily_fixed_amount { get; set; }

    [Column("hourly_fixed_amount")]
    public decimal? hourly_fixed_amount { get; set; }

    // Effective date range
    [Column("effective_start_date")]
    public DateTime effective_start_date { get; set; }

    [Column("effective_end_date")]
    public DateTime? effective_end_date { get; set; }

    // Provision percentages
    [Column("replacement_percent")]
    public short? replacement_percent { get; set; }

    [Column("loss_percent")]
    public short? loss_percent { get; set; }

    [Column("profit_percent")]
    public short? profit_percent { get; set; }

    [Column("overhead_percent")]
    public short? overhead_percent { get; set; }

    [Column("accident_percent")]
    public short? accident_percent { get; set; }

    // Fuel component
    [Column("fuel_kilo_tariff")]
    public decimal? fuel_kilo_tariff { get; set; }

    // Approval workflow
    // Status: 0=Draft, 1=PendingApproval, 2=Approved, 3=Rejected
    // Tariffs >R100,000/month require approval before becoming effective.
    // Self-approval is blocked: the capturer cannot be the approver.
    [Column("tariff_approval_status")]
    public short tariff_approval_status { get; set; } = 0; // Default: Draft

    [Column("approver_code")]
    public int? approver_code { get; set; }

    [Column("approval_date")]
    public DateTime? approval_date { get; set; }

    [Column("rejection_reason")]
    [StringLength(500)]
    public string? rejection_reason { get; set; }

    // Audit fields
    [Column("date_created")]
    public DateTime? date_created { get; set; }

    [Column("created_by")]
    public int? created_by { get; set; }

    [Column("date_modified")]
    public DateTime? date_modified { get; set; }

    [Column("modified_by")]
    public int? modified_by { get; set; }

    // Global audit fields (AI_CODING_RULES.md - Section 4.5)
    

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

    [ForeignKey("approver_code")]
    public virtual User? ApproverUser { get; set; }
}
