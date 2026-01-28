using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Data.Entities
{
    /// <summary>
    /// Type entity representing vehicle types/classifications from legacy 'type' table.
    /// Maps vehicle classification types like car, van, truck, motorcycle, etc.
    /// </summary>
    [Table("type")]
    public class Type
    {
        /// <summary>
        /// Primary key - unique identifier for the vehicle type
        /// Auto-generated identity column in legacy database
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public short type_code { get; set; }

        /// <summary>
        /// Description of the vehicle type (e.g., "Car", "Van", "Truck", "Motorcycle")
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string type_description { get; set; } = string.Empty;

        // Note: Vehicle entity has type_code foreign key field but no navigation property
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