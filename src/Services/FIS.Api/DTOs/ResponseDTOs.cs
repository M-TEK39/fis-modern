namespace FIS.Api.DTOs
{
    /// <summary>
    /// DTO for Make responses - excludes navigation properties to prevent circular references
    /// </summary>
    public class MakeResponseDto
    {
        public short make_code { get; set; }
        public string make_description { get; set; } = string.Empty;
        // Audit fields
        public DateTime date_created { get; set; }
        public DateTime? date_updated { get; set; }
        public int? created_by_user_code { get; set; }
        public int? modified_by_user_code { get; set; }
        public bool is_deleted { get; set; }
    }

    /// <summary>
    /// DTO for Model responses - excludes navigation properties to prevent circular references
    /// Includes all Model entity fields for complete data transfer
    /// </summary>
    public class ModelResponseDto
    {
        public short model_code { get; set; }
        public string model_description { get; set; } = string.Empty;
        public short make_code { get; set; }
        public string make_description { get; set; } = string.Empty;

        // Additional Model fields
        public short unit_of_measure_code { get; set; }
        public short fuel_type_code { get; set; }
        public short licence_code { get; set; }
        public short? maint_trigger_code { get; set; }
        public short class_code { get; set; }
        public string? engine_type { get; set; }
        public short? engine_capacity { get; set; }
        public short? rated_power { get; set; }
        public short? fuel_tank_capacity { get; set; }
        public decimal? target_consumption { get; set; }
        public int? target_tyre_life { get; set; }
        public int? service_interval { get; set; }
        public string? vemm_code { get; set; }
        public short? licence_fee_code { get; set; }
        public int? gvm { get; set; }
        public string? transmission { get; set; }
        public decimal? wesbank_kilos_per_litre { get; set; }
        // Audit fields
        public DateTime date_created { get; set; }
        public DateTime? date_updated { get; set; }
        public int? created_by_user_code { get; set; }
        public int? modified_by_user_code { get; set; }
        public bool is_deleted { get; set; }
    }

    /// <summary>
    /// DTO for Type responses - excludes navigation properties to prevent circular references
    /// </summary>
    public class TypeResponseDto
    {
        public short type_code { get; set; }
        public string type_description { get; set; } = string.Empty;
        // Audit fields
        public DateTime date_created { get; set; }
        public DateTime? date_updated { get; set; }
        public int? created_by_user_code { get; set; }
        public int? modified_by_user_code { get; set; }
        public bool is_deleted { get; set; }
    }

    /// <summary>
    /// DTO for Class responses - excludes navigation properties and audit fields
    /// </summary>
    public class ClassResponseDto
    {
        public short class_code { get; set; }
        public string? description { get; set; }
        // Audit fields
        public DateTime date_created { get; set; }
        public DateTime? date_updated { get; set; }
        public int? created_by_user_code { get; set; }
        public int? modified_by_user_code { get; set; }
        public bool is_deleted { get; set; }
    }

    /// <summary>
    /// DTO for FuelType responses - excludes navigation properties to prevent circular references
    /// </summary>
    public class FuelTypeResponseDto
    {
        public short fuel_type_code { get; set; }
        public string fuel_description { get; set; } = string.Empty;

        /// <summary>
        /// Current rate per litre from fuel_tariff table (null if no active tariff)
        /// </summary>
        public decimal? rate_per_litre { get; set; }

        // Audit fields
        public DateTime date_created { get; set; }
        public DateTime? date_updated { get; set; }
        public int? created_by_user_code { get; set; }
        public int? modified_by_user_code { get; set; }
        public bool is_deleted { get; set; }
    }

    /// <summary>
    /// DTO for Department responses - excludes navigation properties to prevent circular references
    /// </summary>
    public class DepartmentResponseDto
    {
        public short department_code { get; set; }
        public string department_description { get; set; } = string.Empty;
        public string res_person { get; set; } = string.Empty;
        // Audit fields
        public DateTime date_created { get; set; }
        public DateTime? date_updated { get; set; }
        public int? created_by_user_code { get; set; }
        public int? modified_by_user_code { get; set; }
        public bool is_deleted { get; set; }
    }

    /// <summary>
    /// DTO for job card responses - excludes navigation properties to prevent circular references
    /// </summary>
    public class JobCardResponseDto
    {
        public int job_card_id { get; set; }
        public int vmf_code { get; set; }
        public string? gg_number { get; set; } // From Vehicle navigation
        public string? registration_number { get; set; } // From Vehicle navigation
        public short extra_code { get; set; }
        public string? extra_description { get; set; } // From ExtraCodeRef navigation
        public int status_code { get; set; }
        public string? status_text { get; set; } // Friendly status name
        public string? priority { get; set; }
        public int? assigned_to { get; set; }
        public string? assigned_to_name { get; set; } // From AssignedToUser navigation
        public DateTime? assigned_date { get; set; }
        public string? jcs_comment { get; set; }
        public string? damages { get; set; }
        public string? comments { get; set; }
        public int? authorizer { get; set; }
        public string? authorizer_name { get; set; } // From AuthorizerUser navigation
        public string? reviewed { get; set; }

        // Required for frontend self-approval enforcement
        public int? captured_by_user_code { get; set; }
        public int? authorized_by_user_code { get; set; }

        // Audit fields
        public DateTime date_created { get; set; }
        public DateTime? date_updated { get; set; }
        public int? created_by_user_code { get; set; }
        public int? modified_by_user_code { get; set; }
    }
}