using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.System;

[Table("Request_Change")]
public class RequestChange
{
    [Key]
    [Column("request_code")]
    public short request_code { get; set; }

    [Column("request_date")]
    public DateTime? request_date { get; set; }

    [Column("request_name")]
    [StringLength(255)]
    public string? request_name { get; set; }

    [Column("captured_by_userid")]
    public int? captured_by_userid { get; set; }

    [Column("approved_date")]
    public DateTime? approved_date { get; set; }

    [Column("approved_name")]
    [StringLength(255)]
    public string? approved_name { get; set; }

    [Column("change_description")]
    public string? change_description { get; set; }

    [Column("sub_system_affected")]
    [StringLength(255)]
    public string? sub_system_affected { get; set; }

    [Column("tech_description")]
    public string? tech_description { get; set; }

    [Column("tech_component_impact")]
    public string? tech_component_impact { get; set; }

    [Column("operational_impact")]
    public string? operational_impact { get; set; }

    [Column("training_impact")]
    public string? training_impact { get; set; }

    [Column("completion_date")]
    public DateTime? completion_date { get; set; }

    [Column("approve_or_not")]
    [StringLength(10)]
    public string? approve_or_not { get; set; }

    [Column("request_comment")]
    public string? request_comment { get; set; }

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
