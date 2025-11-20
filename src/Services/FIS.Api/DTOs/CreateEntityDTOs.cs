using System.ComponentModel.DataAnnotations;

namespace FIS.Api.DTOs
{
    /// <summary>
    /// DTO for creating a new Make entity
    /// Excludes auto-generated ID fields
    /// </summary>
    public class CreateMakeDto
    {
        /// <summary>
        /// Make description/name (Toyota, Ford, BMW, etc.)
        /// </summary>
        [Required]
        [StringLength(100)]
        public string make_description { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for creating a new Type entity
    /// Excludes auto-generated ID fields
    /// </summary>
    public class CreateTypeDto
    {
        /// <summary>
        /// Description of the vehicle type (e.g., "Car", "Van", "Truck", "Motorcycle")
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string type_description { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for creating a new FuelType entity
    /// Excludes auto-generated ID fields
    /// </summary>
    public class CreateFuelTypeDto
    {
        /// <summary>
        /// Description of the fuel type (e.g., "Petrol", "Diesel", "Electric", "Hybrid")
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string fuel_description { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for creating a new MaintenanceTrigger entity
    /// Excludes auto-generated ID fields
    /// </summary>
    public class CreateMaintenanceTriggerDto
    {
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
    }
}