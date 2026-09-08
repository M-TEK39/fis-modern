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
        public short? type_code { get; set; }
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
        public string? class_number { get; set; }
        public string? bank_number { get; set; }
        public short? months_life { get; set; }
        public decimal? depreciation_percent { get; set; }
        public decimal? odometer_life { get; set; }
        public short? appreciate_percent { get; set; }
        public decimal? replacement_cost { get; set; }

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

        // Repair costs (populated once job card is closed with cost data)
        public decimal? labour_cost { get; set; }
        public decimal? parts_cost { get; set; }
        public decimal? other_cost { get; set; }
        public decimal? total_cost { get; set; }
        public string? invoice_number { get; set; }
        public DateTime? invoice_date { get; set; }
        public string? service_provider { get; set; }

        // Audit fields
        public DateTime date_created { get; set; }
        public DateTime? date_updated { get; set; }
        public int? created_by_user_code { get; set; }
        public int? modified_by_user_code { get; set; }
    }

    /// <summary>
    /// DTO for a single vehicle licence history entry
    /// </summary>
    public class VehicleLicenceHistoryDto
    {
        public int licence_history_id { get; set; }
        public int vmf_code { get; set; }
        public string? fleet_number { get; set; }
        public string? registration_number { get; set; }

        // Snapshotted licence values at time of supersession
        public DateTime? licence_due_date { get; set; }
        public string? lic_register_number { get; set; }
        public string? lic_registration_doc { get; set; }
        public string? licence_comments { get; set; }
        public DateTime? cof_last_done { get; set; }
        public string? cof_required { get; set; }
        public int? tare { get; set; }
        public string? Licence_receiver { get; set; }
        public string? Licence_receiver_id { get; set; }
        public string? Licence_receiver_tel { get; set; }
        public short? Licence_receiver_site { get; set; }
        public DateTime? Licence_date_taken { get; set; }

        // Metadata
        public DateTime captured_at { get; set; }
        public int? captured_by_user_code { get; set; }
        public string? captured_by_user_email { get; set; }
        public string? update_notes { get; set; }
    }

    /// <summary>
    /// DTO for vehicle remark responses
    /// </summary>
    public class VehicleRemarkResponseDto
    {
        public int remark_id { get; set; }
        public int vmf_code { get; set; }
        public string? fleet_number { get; set; }
        public string? registration_number { get; set; }
        public string remark_category { get; set; } = string.Empty;
        public string remark_text { get; set; } = string.Empty;
        public bool is_resolved { get; set; }
        public DateTime? resolved_date { get; set; }
        public string? resolved_by_user_email { get; set; }
        public string? resolution_notes { get; set; }
        public DateTime date_created { get; set; }
        public DateTime? date_updated { get; set; }
        public int? created_by_user_code { get; set; }
        public string? created_by_user_email { get; set; }
    }
}
