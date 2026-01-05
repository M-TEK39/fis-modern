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
}
