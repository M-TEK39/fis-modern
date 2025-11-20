using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// FuelCard Entity - EXACT legacy schema match for Fuel_card table
/// Uses exact field names from Database.cs - NO modernization
/// </summary>
[Table("Fuel_card")]
public class FuelCard
{
    [Key]
    [Column("Fuel_card_code")]
    public int Fuel_card_code { get; set; }

    [Column("vmf_code")]
    public int? vmf_code { get; set; }

    [Column("Counter")]
    public short? Counter { get; set; }

    [Column("card_number")]
    [StringLength(15)]
    public string? card_number { get; set; }

    [Column("PAN_number")]
    [StringLength(15)]
    public string? PAN_number { get; set; }

    [Column("PetReceiver")]
    [StringLength(20)]
    public string? PetReceiver { get; set; }

    [Column("PetRecTel")]
    [StringLength(16)]
    public string? PetRecTel { get; set; }

    [Column("PetTaken")]
    public DateTime? PetTaken { get; set; }

    [Column("PetExpire")]
    public DateTime? PetExpire { get; set; }

    [Column("ExpReason")]
    [StringLength(20)]
    public string? ExpReason { get; set; }

    [Column("PetComment")]
    [StringLength(80)]
    public string? PetComment { get; set; }

    [Column("LinkGGNum")]
    [StringLength(10)]
    public string? LinkGGNum { get; set; }

    [Column("Status_date")]
    public DateTime? Status_date { get; set; }

    [Column("PetRecId")]
    [StringLength(13)]
    public string? PetRecId { get; set; }

    [Column("PetRecFax")]
    [StringLength(15)]
    public string? PetRecFax { get; set; }

    [Column("Bank_cnt")]
    [StringLength(2)]
    public string? Bank_cnt { get; set; }

    [Column("Inciddat")]
    public DateTime? Inciddat { get; set; }

    [Column("Petrecsite")]
    public short? Petrecsite { get; set; }

    [Column("Petprint")]
    [StringLength(1)]
    public string? Petprint { get; set; }

    [Column("Garage")]
    [StringLength(1)]
    public string? Garage { get; set; }

    // Navigation properties with proper foreign key mappings
    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }

    [ForeignKey("Petrecsite")]
    public virtual Site? Site { get; set; }
}
