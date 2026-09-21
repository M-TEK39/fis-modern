using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Financial;

/// <summary>
/// Overhead costs for modern tariff system (fin schema).
/// Defines overhead cost categories and amounts.
/// Maps to legacy fin.Overhead table.
/// </summary>
[Table("Overhead", Schema = "fin")]
public class Overhead
{
    [Key]
    [Column("OverheadId")]
    public int OverheadId { get; set; }

    [Column("OverheadDescription")]
    public string OverheadDescription { get; set; } = string.Empty;

    [Column("OverheadAmount")]
    public decimal OverheadAmount { get; set; }

    [Column("OverheadTypeId")]
    public byte OverheadTypeId { get; set; }

    [Column("OverheadNote")]
    public string? OverheadNote { get; set; }

    [Column("TariffParameterID")]
    public int TariffParameterID { get; set; }

    [Column("TariffParameterYear")]
    public int? TariffParameterYear { get; set; }

    [Column("CaptureDate")]
    public DateTime CaptureDate { get; set; }

    [Column("ModifiedDate")]
    public DateTime? ModifiedDate { get; set; }

    [Column("user_access_code")]
    public short? user_access_code { get; set; }

    [Column("user_access_name")]
    public string? user_access_name { get; set; }

    // Navigation properties
    public virtual TariffParameter? TariffParameter { get; set; }

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
