using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.System;

[Table("error_log")]
public class ErrorLog
{
    [Key]
    [Column("error_log_code")]
    public int error_log_code { get; set; }

    [Column("description")]
    [StringLength(2000)]
    public string? description { get; set; }

    [Column("data")]
    public string? data { get; set; }

    [Column("log_date")]
    public DateTime log_date { get; set; }

    [Column("log_time")]
    public DateTime log_time { get; set; }

    [Column("log_read")]
    [StringLength(1)]
    public string? log_read { get; set; }

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
