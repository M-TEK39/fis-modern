using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// MaintenanceRecord Entity - Legacy compatibility for maintenance_records table
/// Maps to legacy snake_case structure with relationships to vehicles and service providers
/// </summary>
[Table("maintenance_records")]
public class MaintenanceRecord
{
    [Key]
    [Column("maintenance_id")]
    public int MaintenanceId { get; set; }

    [Required]
    [Column("vmf_code")]
    public int VmfCode { get; set; }

    [Required]
    [Column("maintenance_date")]
    public DateTime MaintenanceDate { get; set; }

    [Required]
    [StringLength(20)]
    [Column("maintenance_type")]
    public string MaintenanceType { get; set; } = string.Empty; // SERVICE, REPAIR, INSPECTION, etc.

    [Required]
    [Column("odometer_reading")]
    public int OdometerReading { get; set; }

    // Service details
    [StringLength(100)]
    [Column("service_provider")]
    public string? ServiceProvider { get; set; }

    [StringLength(50)]
    [Column("work_order_number")]
    public string? WorkOrderNumber { get; set; }

    [StringLength(50)]
    [Column("invoice_number")]
    public string? InvoiceNumber { get; set; }

    [Column("total_cost")]
    public decimal TotalCost { get; set; } = 0;

    [Column("labour_cost")]
    public decimal LabourCost { get; set; } = 0;

    [Column("parts_cost")]
    public decimal PartsCost { get; set; } = 0;

    // Service description
    [Required]
    [StringLength(2000)]
    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [StringLength(1000)]
    [Column("parts_used")]
    public string? PartsUsed { get; set; }

    // Scheduling
    [Column("scheduled_date")]
    public DateTime? ScheduledDate { get; set; }

    [Column("next_service_date")]
    public DateTime? NextServiceDate { get; set; }

    [Column("next_service_odometer")]
    public int? NextServiceOdometer { get; set; }

    // Service intervals
    [Column("service_interval_km")]
    public int? ServiceIntervalKm { get; set; }

    [Column("service_interval_days")]
    public int? ServiceIntervalDays { get; set; }

    // Status tracking
    [Required]
    [StringLength(20)]
    [Column("status")]
    public string Status { get; set; } = "COMPLETED"; // SCHEDULED, IN_PROGRESS, COMPLETED, CANCELLED

    [StringLength(1)]
    [Column("still_current")]
    public string StillCurrentFlag { get; set; } = "Y";

    [NotMapped]
    public bool StillCurrent => StillCurrentFlag == "Y";

    [StringLength(1)]
    [Column("warranty_work")]
    public string? WarrantyWorkFlag { get; set; } = "N";

    [NotMapped]
    public bool WarrantyWork => WarrantyWorkFlag == "Y";

    [Column("warranty_expiry_date")]
    public DateTime? WarrantyExpiryDate { get; set; }

    // Authorization and approval
    [StringLength(100)]
    [Column("authorized_by")]
    public string? AuthorizedBy { get; set; }

    [Column("authorization_date")]
    public DateTime? AuthorizationDate { get; set; }

    [StringLength(20)]
    [Column("authorization_code")]
    public string? AuthorizationCode { get; set; }

    // Vehicle condition
    [StringLength(1)]
    [Column("vehicle_roadworthy")]
    public string? VehicleRoadworthyFlag { get; set; } = "Y";

    [NotMapped]
    public bool VehicleRoadworthy => VehicleRoadworthyFlag == "Y";

    [Column("roadworthy_certificate_date")]
    public DateTime? RoadworthyCertificateDate { get; set; }

    [Column("roadworthy_expiry_date")]
    public DateTime? RoadworthyExpiryDate { get; set; }

    // Additional notes
    [StringLength(2000)]
    [Column("mechanic_notes")]
    public string? MechanicNotes { get; set; }

    [StringLength(2000)]
    [Column("inspection_notes")]
    public string? InspectionNotes { get; set; }

    // Audit fields
    [Column("created_date")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Column("modified_date")]
    public DateTime? ModifiedDate { get; set; }

    [Column("user_access_code")]
    public int? UserAccessCode { get; set; }

    // Navigation properties
    [ForeignKey("VmfCode")]
    public virtual Vehicle? Vehicle { get; set; }

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
}