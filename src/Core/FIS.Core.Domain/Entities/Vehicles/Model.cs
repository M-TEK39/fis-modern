using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// Model Entity - Vehicle model definitions with specifications
/// Maps to legacy 'model' table with exact field names for compatibility
/// Contains detailed vehicle specifications and configuration
/// </summary>
[Table("model")]
public class Model
{
    /// <summary>
    /// Primary key - Model code (model identifier)
    /// Auto-generated identity column in legacy database
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("model_code")]
    public short model_code { get; set; }

    /// <summary>
    /// Foreign key to Make table
    /// </summary>
    [Column("make_code")]
    public short make_code { get; set; }

    /// <summary>
    /// Unit of measure code for specifications
    /// </summary>
    [Column("unit_of_measure_code")]
    public short unit_of_measure_code { get; set; }

    /// <summary>
    /// Foreign key to fuel_type table
    /// </summary>
    [Column("fuel_type_code")]
    public short fuel_type_code { get; set; }

    /// <summary>
    /// License type code required for this model
    /// </summary>
    [Column("licence_code")]
    public short licence_code { get; set; }

    /// <summary>
    /// Optional maintenance trigger code for service scheduling
    /// </summary>
    [Column("maint_trigger_code")]
    public short? maint_trigger_code { get; set; }

    /// <summary>
    /// Vehicle class code
    /// </summary>
    [Column("class_code")]
    public short class_code { get; set; }

    /// <summary>
    /// Vehicle type code (NEW: User-approved schema extension)
    /// Links to type table for vehicle categorization
    /// </summary>
    [Column("type_code")]
    public short? type_code { get; set; }

    /// <summary>
    /// Model description/name (Camry, F-150, etc.)
    /// </summary>
    [Required]
    [StringLength(100)]
    [Column("model_description")]
    public string model_description { get; set; } = string.Empty;

    /// <summary>
    /// Engine type description
    /// </summary>
    [StringLength(50)]
    [Column("engine_type")]
    public string? engine_type { get; set; }

    /// <summary>
    /// Engine capacity in CC
    /// </summary>
    [Column("engine_capacity")]
    public short? engine_capacity { get; set; }

    /// <summary>
    /// Rated power in kW
    /// </summary>
    [Column("rated_power")]
    public short? rated_power { get; set; }

    /// <summary>
    /// Fuel tank capacity in liters
    /// </summary>
    [Column("fuel_tank_capacity")]
    public short? fuel_tank_capacity { get; set; }

    /// <summary>
    /// Target fuel consumption (L/100km)
    /// </summary>
    [Column("target_consumption")]
    public decimal? target_consumption { get; set; }

    /// <summary>
    /// Target tyre life in kilometers
    /// </summary>
    [Column("target_tyre_life")]
    public int? target_tyre_life { get; set; }

    /// <summary>
    /// Service interval in kilometers
    /// </summary>
    [Column("service_interval")]
    public int? service_interval { get; set; }

    /// <summary>
    /// VEMM code for vehicle registration
    /// </summary>
    [StringLength(20)]
    [Column("vemm_code")]
    public string? vemm_code { get; set; }

    /// <summary>
    /// License fee code for registration costs
    /// </summary>
    [Column("licence_fee_code")]
    public short? licence_fee_code { get; set; }

    /// <summary>
    /// Gross Vehicle Mass (GVM) in kg
    /// </summary>
    [Column("gvm")]
    public int? gvm { get; set; }

    /// <summary>
    /// Transmission type (Manual, Automatic, etc.)
    /// </summary>
    [StringLength(20)]
    [Column("transmission")]
    public string? transmission { get; set; }

    /// <summary>
    /// Wesbank fuel efficiency (kilos per liter)
    /// </summary>
    [Column("wesbank_kilos_per_litre")]
    public decimal? wesbank_kilos_per_litre { get; set; }

    // Navigation properties
    /// <summary>
    /// Associated make/manufacturer
    /// </summary>
    [ForeignKey("make_code")]
    public virtual Make? Make { get; set; }

    /// <summary>
    /// Vehicles using this model
    /// </summary>
    public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();

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

    // Computed properties for modern API compatibility
    public short ModelCode => model_code;
    public string ModelDescription => model_description;
    public string Name => model_description;
    public string FullName =>
        Make != null ? $"{Make.make_description} {model_description}" : model_description;
}
