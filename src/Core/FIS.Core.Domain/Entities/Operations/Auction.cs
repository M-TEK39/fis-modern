using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// Auction Entity - Vehicle auction management
/// Maps to legacy 'auction' table with exact field names for compatibility
/// </summary>
[Table("auction")]
public class Auction
{
    /// <summary>
    /// Primary key - Auction code
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("auction_code")]
    public short auction_code { get; set; }

    /// <summary>
    /// Foreign key to vehicle table
    /// </summary>
    [Column("vmf_code")]
    public int vmf_code { get; set; }

    /// <summary>
    /// Auction reference number
    /// </summary>
    [StringLength(50)]
    [Column("auction_number")]
    public string? auction_number { get; set; }

    /// <summary>
    /// Auction camp/location
    /// </summary>
    [StringLength(100)]
    [Column("camp")]
    public string? camp { get; set; }

    /// <summary>
    /// Lot number
    /// </summary>
    [Column("lot")]
    public decimal? lot { get; set; }

    /// <summary>
    /// Auction garage code
    /// </summary>
    [Column("auction_garage")]
    public short? auction_garage { get; set; }

    /// <summary>
    /// Authorization number
    /// </summary>
    [StringLength(50)]
    [Column("auth_number")]
    public string? auth_number { get; set; }

    /// <summary>
    /// Authorization date
    /// </summary>
    [Column("auth_date")]
    public DateTime? auth_date { get; set; }

    /// <summary>
    /// Vehicle odometer reading at auction
    /// </summary>
    [Column("auction_km")]
    public decimal? auction_km { get; set; }

    /// <summary>
    /// Garage owner details
    /// </summary>
    [StringLength(200)]
    [Column("garage_owner")]
    public string? garage_owner { get; set; }

    /// <summary>
    /// Reason vehicle was sold
    /// </summary>
    [StringLength(500)]
    [Column("reason_sold")]
    public string? reason_sold { get; set; }

    /// <summary>
    /// Estimated auction amount
    /// </summary>
    [Column("estimate_amount")]
    public decimal? estimate_amount { get; set; }

    /// <summary>
    /// Reserve amount (minimum bid)
    /// </summary>
    [Column("reserve_amount")]
    public decimal? reserve_amount { get; set; }

    /// <summary>
    /// Sold identifier/status
    /// </summary>
    [StringLength(50)]
    [Column("sold_id")]
    public string? sold_id { get; set; }

    /// <summary>
    /// Auction remarks/notes
    /// </summary>
    [Column("remark")]
    public string? remark { get; set; }

    // Navigation properties
    /// <summary>
    /// Associated vehicle
    /// </summary>
    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }
}
