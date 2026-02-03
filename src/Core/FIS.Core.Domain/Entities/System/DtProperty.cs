using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.System;

[Table("dtproperties")]
public class DtProperty
{
    [Key]
    [Column("id")]
    public int id { get; set; }

    [Column("objectid")]
    public int? objectid { get; set; }

    [Column("property")]
    [StringLength(64)]
    public string? property { get; set; }

    [Column("value")]
    [StringLength(255)]
    public string? value { get; set; }

    [Column("lvalue")]
    public byte[]? lvalue { get; set; }

    [Column("version")]
    public int version { get; set; }

    [Column("uvalue")]
    [StringLength(255)]
    public string? uvalue { get; set; }

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
