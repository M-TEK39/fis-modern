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
    /// DTO for creating a new Model entity
    /// Excludes auto-generated model_code
    /// </summary>
    public class CreateModelDto
    {
        [Required]
        public short make_code { get; set; }

        [Required]
        public short unit_of_measure_code { get; set; }

        [Required]
        public short fuel_type_code { get; set; }

        [Required]
        public short licence_code { get; set; }

        public short? maint_trigger_code { get; set; }

        [Required]
        public short class_code { get; set; }

        public short? type_code { get; set; }

        [Required]
        [StringLength(100)]
        public string model_description { get; set; } = string.Empty;

        [StringLength(50)]
        public string? engine_type { get; set; }

        public short? engine_capacity { get; set; }

        public short? rated_power { get; set; }

        public short? fuel_tank_capacity { get; set; }

        public decimal? target_consumption { get; set; }

        public int? target_tyre_life { get; set; }

        public int? service_interval { get; set; }

        [StringLength(20)]
        public string? vemm_code { get; set; }

        public short? licence_fee_code { get; set; }

        public int? gvm { get; set; }

        [StringLength(20)]
        public string? transmission { get; set; }

        public decimal? wesbank_kilos_per_litre { get; set; }
    }

    /// <summary>
    /// DTO for updating an existing Model entity
    /// Includes model_code for identification
    /// </summary>
    public class UpdateModelDto
    {
        [Required]
        public short model_code { get; set; }

        [Required]
        public short make_code { get; set; }

        [Required]
        public short unit_of_measure_code { get; set; }

        [Required]
        public short fuel_type_code { get; set; }

        [Required]
        public short licence_code { get; set; }

        public short? maint_trigger_code { get; set; }

        [Required]
        public short class_code { get; set; }

        public short? type_code { get; set; }

        [Required]
        [StringLength(100)]
        public string model_description { get; set; } = string.Empty;

        [StringLength(50)]
        public string? engine_type { get; set; }

        public short? engine_capacity { get; set; }

        public short? rated_power { get; set; }

        public short? fuel_tank_capacity { get; set; }

        public decimal? target_consumption { get; set; }

        public int? target_tyre_life { get; set; }

        public int? service_interval { get; set; }

        [StringLength(20)]
        public string? vemm_code { get; set; }

        public short? licence_fee_code { get; set; }

        public int? gvm { get; set; }

        [StringLength(20)]
        public string? transmission { get; set; }

        public decimal? wesbank_kilos_per_litre { get; set; }
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
    /// DTO for creating a new Class entity
    /// Excludes auto-generated ID and audit fields
    /// </summary>
    public class CreateClassDto
    {
        /// <summary>
        /// Description of the vehicle class (e.g., "Sedan", "SUV", "Truck")
        /// </summary>
        [MaxLength(255)]
        public string? description { get; set; }
    }

    /// <summary>
    /// DTO for updating an existing Class entity
    /// Excludes audit fields (auto-populated by repository)
    /// </summary>
    public class UpdateClassDto
    {
        /// <summary>
        /// Description of the vehicle class
        /// </summary>
        [MaxLength(255)]
        public string? description { get; set; }
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

        /// <summary>
        /// Initial rate per litre (optional - creates fuel_tariff record if provided)
        /// </summary>
        public decimal? rate_per_litre { get; set; }

        /// <summary>
        /// Optional notes about the initial rate
        /// </summary>
        [MaxLength(1000)]
        public string? rate_notes { get; set; }
    }

    /// <summary>
    /// DTO for updating an existing FuelType entity
    /// </summary>
    public class UpdateFuelTypeDto
    {
        /// <summary>
        /// Description of the fuel type (e.g., "Petrol", "Diesel", "Electric", "Hybrid")
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string fuel_description { get; set; } = string.Empty;

        /// <summary>
        /// New rate per litre (optional - creates new fuel_tariff record if provided and different from current)
        /// </summary>
        public decimal? rate_per_litre { get; set; }

        /// <summary>
        /// Optional notes about the rate change
        /// </summary>
        [MaxLength(1000)]
        public string? rate_notes { get; set; }
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

    /// <summary>
    /// DTO for creating a new job card
    /// </summary>
    public class CreateJobCardDto
    {
        [Required]
        public int vmf_code { get; set; }

        [Required]
        public short extra_code { get; set; }

        [MaxLength(2000)]
        public string? jcs_comment { get; set; }

        [MaxLength(2000)]
        public string? damages { get; set; }

        [MaxLength(1)]
        public string? priority { get; set; } // 'H' or 'N'
    }

    /// <summary>
    /// DTO for updating an existing job card
    /// </summary>
    public class UpdateJobCardDto
    {
        [MaxLength(2000)]
        public string? jcs_comment { get; set; }

        [MaxLength(2000)]
        public string? damages { get; set; }

        [MaxLength(2000)]
        public string? comments { get; set; }

        public int? assigned_to { get; set; }

        public DateTime? assigned_date { get; set; }

        [MaxLength(1)]
        public string? priority { get; set; }
    }

    /// <summary>
    /// DTO for authorizing a job card
    /// </summary>
    public class JobCardAuthorizationDto
    {
        [MaxLength(2000)]
        public string? comment { get; set; }
    }

    /// <summary>
    /// DTO for declining a job card
    /// </summary>
    public class JobCardDeclineDto
    {
        [Required]
        [MaxLength(2000)]
        public string decline_reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for canceling a job card
    /// </summary>
    public class JobCardCancelDto
    {
        [MaxLength(2000)]
        public string? cancel_reason { get; set; }
    }

    /// <summary>
    /// DTO for closing a job card
    /// </summary>
    public class JobCardCloseDto
    {
        [MaxLength(2000)]
        public string? close_notes { get; set; }
    }
}