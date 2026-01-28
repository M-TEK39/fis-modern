using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Data.Entities
{
    /// <summary>
    /// MaintenanceTrigger entity representing maintenance triggers from legacy 'maintenance_trigger' table.
    /// Maps maintenance trigger types for vehicle service scheduling (e.g., mileage-based, time-based).
    /// Used by Model entity for vehicle maintenance configuration.
    /// </summary>
    [Table("maintenance_trigger")]
    public class MaintenanceTrigger
    {
        /// <summary>
        /// Primary key - unique identifier for the maintenance trigger
        /// Auto-generated identity column in legacy database
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public short maint_trigger_code { get; set; }

        /// <summary>
        /// Description of the maintenance trigger (e.g., "Mileage Based", "Time Based", "Hours Based")
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string description { get; set; } = string.Empty;

        /// <summary>
        /// Trigger identifier code for the maintenance system
        /// </summary>
        [MaxLength(50)]
        public string? trigger_id { get; set; }

        // Note: Model entity has maint_trigger_code foreign key field but no navigation property
        // This is intentional for legacy schema compatibility

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
    }
}