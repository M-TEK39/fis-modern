using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Financial;

/// <summary>
/// Overhead_AverageTariffCalc Entity - EXACT legacy schema match
/// </summary>
[Table("Overhead_AverageTariffCalc", Schema = "fin")]
public class Overhead_AverageTariffCalc
{
    [Key]
    [Column("TariffParameterID")]
    public int TariffParameterID { get; set; }

    [Column("OverheadTypeId")]
    public byte OverheadTypeId { get; set; }

    [Column("AverageTariff")]
    public decimal AverageTariff { get; set; }

    [Column("CaptureDate")]
    public DateTime CaptureDate { get; set; }

    [Column("ModifiedDate")]
    public DateTime? ModifiedDate { get; set; }

    [Column("user_access_code")]
    public short? user_access_code { get; set; }

    [Column("user_access_name")]
    public string? user_access_name { get; set; }

    // Navigation properties
    [ForeignKey("OverheadTypeId")]
    public virtual OverheadType? OverheadType { get; set; }

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

    // Navigation properties for audit trail
    [ForeignKey("created_by_user_code")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("modified_by_user_code")]
    public virtual User? ModifiedByUser { get; set; }
}
