using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.System;

[Table("system_parameters")]
public class SystemParameter
{
    [Key]
    [Column("sys_parameters_code")]
    public short sys_parameters_code { get; set; }

    [Column("database_version")]
    [StringLength(50)]
    public string? database_version { get; set; }

    [Column("app_version")]
    [StringLength(50)]
    public string? app_version { get; set; }

    [Column("vat_percent")]
    public decimal vat_percent { get; set; }

    [Column("daily_weight")]
    public decimal daily_weight { get; set; }

    [Column("hourly_weight")]
    public decimal hourly_weight { get; set; }

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
