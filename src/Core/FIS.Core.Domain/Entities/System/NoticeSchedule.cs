using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.System;

/// <summary>
/// NoticeSchedule Entity - Scheduling and display management for notices
/// Maps to legacy 'NoticeSchedule' table with exact field names for compatibility
/// </summary>
[Table("NoticeSchedule")]
public class NoticeSchedule
{
    /// <summary>
    /// Primary key - Notice Schedule ID
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("notice_schedule_id")]
    public int notice_schedule_id { get; set; }

    /// <summary>
    /// Foreign key to Notice table
    /// </summary>
    [Column("notice_id")]
    public int notice_id { get; set; }

    /// <summary>
    /// Title field for display
    /// </summary>
    [StringLength(500)]
    [Column("title_field")]
    public string? title_field { get; set; }

    /// <summary>
    /// Start date for notice display
    /// </summary>
    [Column("start_date")]
    public DateTime? start_date { get; set; }

    /// <summary>
    /// End date for notice display
    /// </summary>
    [Column("end_date")]
    public DateTime? end_date { get; set; }

    /// <summary>
    /// Sort order for display
    /// </summary>
    [Column("sort_order")]
    public int? sort_order { get; set; }

    // Navigation properties
    /// <summary>
    /// Associated notice
    /// </summary>
    [ForeignKey("notice_id")]
    public virtual Notice? Notice { get; set; }

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
