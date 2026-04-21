using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.System;

[Table("db_ddl_log")]
public class DbDdlLog
{
    [Key]
    [Column("db_ddl_log_code")]
    public int db_ddl_log_code { get; set; }

    [Column("post_time")]
    public DateTime post_time { get; set; }

    [Column("database_user")]
    [StringLength(255)]
    public string? database_user { get; set; }

    [Column("event")]
    [StringLength(255)]
    public string? @event { get; set; }

    [Column("schema")]
    [StringLength(255)]
    public string? schema { get; set; }

    [Column("object")]
    [StringLength(255)]
    public string? @object { get; set; }

    [Column("tsql")]
    public string? tsql { get; set; }

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
