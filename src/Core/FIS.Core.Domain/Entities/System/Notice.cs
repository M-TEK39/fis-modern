using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.System;

/// <summary>
/// Notice Entity - System notice/announcement management
/// Maps to legacy 'Notices' table with exact field names for compatibility
/// </summary>
[Table("Notices")]
public class Notice
{
    /// <summary>
    /// Primary key - Notice ID
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("notice_id")]
    public int notice_id { get; set; }

    /// <summary>
    /// Date of the notice
    /// </summary>
    [Column("notice_date")]
    public DateTime? notice_date { get; set; }

    /// <summary>
    /// Department or person the notice is from
    /// </summary>
    [StringLength(200)]
    [Column("notice_from")]
    public string? notice_from { get; set; }

    /// <summary>
    /// Notice title/subject
    /// </summary>
    [StringLength(500)]
    [Column("notice_title")]
    public string? notice_title { get; set; }

    /// <summary>
    /// Notice body/content
    /// </summary>
    [Column("notice_body")]
    public string? notice_body { get; set; }

    /// <summary>
    /// Person responsible for the notice
    /// </summary>
    [StringLength(200)]
    [Column("notice_person")]
    public string? notice_person { get; set; }

    /// <summary>
    /// Title of the person responsible
    /// </summary>
    [StringLength(200)]
    [Column("notice_person_title")]
    public string? notice_person_title { get; set; }

    // Navigation properties
    /// <summary>
    /// Notice schedules associated with this notice
    /// </summary>
    public virtual ICollection<NoticeSchedule> NoticeSchedules { get; set; } =
        new List<NoticeSchedule>();

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
