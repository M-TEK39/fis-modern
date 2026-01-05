using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Data.Entities
{
    /// <summary>
    /// FuelType entity representing fuel types from legacy 'fuel_type' table.
    /// Maps vehicle fuel classifications like petrol, diesel, electric, hybrid, etc.
    /// Used by Model entity for vehicle specifications.
    /// </summary>
    [Table("fuel_type")]
    public class FuelType
    {
        /// <summary>
        /// Primary key - unique identifier for the fuel type
        /// Auto-generated identity column in legacy database
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public short fuel_type_code { get; set; }

        /// <summary>
        /// Description of the fuel type (e.g., "Petrol", "Diesel", "Electric", "Hybrid")
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string fuel_description { get; set; } = string.Empty;

        // Note: Model entity has fuel_type_code foreign key field but no navigation property
        // This is intentional for legacy schema compatibility
    }
}