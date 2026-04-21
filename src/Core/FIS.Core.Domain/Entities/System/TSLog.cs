using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.System;

[Table("TS_Log")]
public class TSLog
{
    [Key]
    [Column("ErrorID")]
    public short ErrorID { get; set; }

    [Column("TSDate")]
    public DateTime? TSDate { get; set; }

    [Column("TSTime")]
    public DateTime? TSTime { get; set; }

    [Column("user_access_code")]
    public short? user_access_code { get; set; }

    [Column("SiteCode")]
    public short? SiteCode { get; set; }

    [Column("ErrorCode")]
    [StringLength(50)]
    public string? ErrorCode { get; set; }

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
