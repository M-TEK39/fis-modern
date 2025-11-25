using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// Fine Entity - Traffic fine/penalty management
/// Maps to legacy 'Fines' table with exact field names for compatibility
/// </summary>
[Table("Fines")]
public class Fine
{
    /// <summary>
    /// Primary key - Fine code
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("Fine_code")]
    public int Fine_code { get; set; }

    /// <summary>
    /// Foreign key to vehicle table (nullable)
    /// </summary>
    [Column("vmf_code")]
    public int? vmf_code { get; set; }

    /// <summary>
    /// Date of the offence
    /// </summary>
    [Column("Offence_date")]
    public DateTime? Offence_date { get; set; }

    /// <summary>
    /// Offence reference number
    /// </summary>
    [StringLength(100)]
    [Column("Offence_reference")]
    public string? Offence_reference { get; set; }

    /// <summary>
    /// Authority/person who issued the fine
    /// </summary>
    [StringLength(100)]
    [Column("Offence_issuer")]
    public string? Offence_issuer { get; set; }

    /// <summary>
    /// Fine amount
    /// </summary>
    [Column("Fine_amount")]
    public decimal? Fine_amount { get; set; }

    /// <summary>
    /// Court appearance date
    /// </summary>
    [Column("Appear_date")]
    public DateTime? Appear_date { get; set; }

    /// <summary>
    /// Date fine was received by fleet management
    /// </summary>
    [Column("Receive_gg_date")]
    public DateTime? Receive_gg_date { get; set; }

    /// <summary>
    /// Date department was notified
    /// </summary>
    [Column("Notify_dept_date")]
    public DateTime? Notify_dept_date { get; set; }

    /// <summary>
    /// Site code where vehicle is assigned
    /// </summary>
    [Column("Site_code")]
    public short? Site_code { get; set; }

    /// <summary>
    /// Name/description of the offence
    /// </summary>
    [StringLength(200)]
    [Column("Offence_name")]
    public string? Offence_name { get; set; }

    /// <summary>
    /// Date fine was paid
    /// </summary>
    [Column("Fine_pay_date")]
    public DateTime? Fine_pay_date { get; set; }

    /// <summary>
    /// Date fine was withdrawn
    /// </summary>
    [Column("Withdraw_date")]
    public DateTime? Withdraw_date { get; set; }

    /// <summary>
    /// Payment due date
    /// </summary>
    [Column("Pay_due_date")]
    public DateTime? Pay_due_date { get; set; }

    /// <summary>
    /// Date issuer was notified
    /// </summary>
    [Column("Issuer_notify_date")]
    public DateTime? Issuer_notify_date { get; set; }

    // Navigation properties
    /// <summary>
    /// Associated vehicle
    /// </summary>
    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }

    /// <summary>
    /// Associated site
    /// </summary>
    [ForeignKey("Site_code")]
    public virtual Site? Site { get; set; }
}
