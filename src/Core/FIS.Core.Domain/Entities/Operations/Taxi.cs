using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// Taxi Entity - Taxi/contracted vehicle request management
/// Maps to legacy 'Taxis' table with exact field names for compatibility
/// </summary>
[Table("Taxis")]
public class Taxi
{
    /// <summary>
    /// Primary key - Request ID
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("request_id")]
    public int request_id { get; set; }

    /// <summary>
    /// Requisition number
    /// </summary>
    [StringLength(50)]
    [Column("rek_num")]
    public string? rek_num { get; set; }

    /// <summary>
    /// Contractor ID
    /// </summary>
    [Column("contractor_id")]
    public short? contractor_id { get; set; }

    /// <summary>
    /// Vehicle code (as string in legacy system)
    /// </summary>
    [StringLength(50)]
    [Column("vmf_code")]
    public string? vmf_code { get; set; }

    /// <summary>
    /// Department code
    /// </summary>
    [Column("department_code")]
    public short? department_code { get; set; }

    /// <summary>
    /// Site code
    /// </summary>
    [Column("site_code")]
    public short site_code { get; set; }

    /// <summary>
    /// Date taxi is required
    /// </summary>
    [Column("date_required")]
    public DateTime date_required { get; set; }

    /// <summary>
    /// Time taxi is required
    /// </summary>
    [Column("time_required")]
    public DateTime time_required { get; set; }

    /// <summary>
    /// Vehicle type code
    /// </summary>
    [Column("vehicle_type_code")]
    public short? vehicle_type_code { get; set; }

    /// <summary>
    /// Official/passenger name
    /// </summary>
    [StringLength(100)]
    [Column("official")]
    public string? official { get; set; }

    /// <summary>
    /// Official rank/position
    /// </summary>
    [StringLength(50)]
    [Column("rank")]
    public string? rank { get; set; }

    /// <summary>
    /// Destination address line 1
    /// </summary>
    [StringLength(200)]
    [Column("address_1")]
    public string? address_1 { get; set; }

    /// <summary>
    /// Destination address line 2
    /// </summary>
    [StringLength(200)]
    [Column("address_2")]
    public string? address_2 { get; set; }

    /// <summary>
    /// Destination address line 3
    /// </summary>
    [StringLength(200)]
    [Column("address_3")]
    public string? address_3 { get; set; }

    /// <summary>
    /// Flight information (if applicable)
    /// </summary>
    [StringLength(100)]
    [Column("flight")]
    public string? flight { get; set; }

    // Navigation properties
    /// <summary>
    /// Associated department
    /// </summary>
    [ForeignKey("department_code")]
    public virtual Department? Department { get; set; }

    /// <summary>
    /// Associated site
    /// </summary>
    [ForeignKey("site_code")]
    public virtual Site? Site { get; set; }
}
