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
    }
}