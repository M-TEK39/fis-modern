using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.ReferenceData;

[Table("company")]
public class Company
{
    [Key]
    [Column("company_code")]
    public short company_code { get; set; }

    [Column("description")]
    [StringLength(255)]
    public string? description { get; set; }

    [Column("Address_1")]
    [StringLength(255)]
    public string? Address_1 { get; set; }

    [Column("Address_2")]
    [StringLength(255)]
    public string? Address_2 { get; set; }

    [Column("Address_3")]
    [StringLength(255)]
    public string? Address_3 { get; set; }

    [Column("Postal_code")]
    [StringLength(50)]
    public string? Postal_code { get; set; }

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
