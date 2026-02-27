using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Vehicles;

/// <summary>
/// Stores metadata for documents/scans attached to a vehicle.
/// The physical file lives on the filesystem under DocumentStorage:BasePath.
///
/// Document categories correspond to FIS modules so all documents for a vehicle
/// (accidents, licences, fines, job cards, etc.) can be retrieved under one vehicle file.
///
/// Camera/scan support: the upload endpoint accepts multipart/form-data.
/// On mobile/tablet the frontend uses input[capture="environment"] to open the camera.
/// </summary>
[Table("vehicle_documents")]
public class VehicleDocument
{
    [Key]
    [Column("document_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int document_id { get; set; }

    /// <summary>FK → vehicle_master.vmf_code</summary>
    [Required]
    [Column("vmf_code")]
    public int vmf_code { get; set; }

    /// <summary>
    /// Module category: Accident | Licence | Fine | Logbook | Maintenance |
    ///                  Contract | Registration | Insurance | RoadWorthy | Other
    /// </summary>
    [Required]
    [Column("document_category")]
    [StringLength(50)]
    public string document_category { get; set; } = "Other";

    /// <summary>Free-text description of what the document is.</summary>
    [Column("document_description")]
    [StringLength(500)]
    public string? document_description { get; set; }

    /// <summary>Original filename as uploaded by the user.</summary>
    [Required]
    [Column("original_file_name")]
    [StringLength(255)]
    public string original_file_name { get; set; } = string.Empty;

    /// <summary>Relative path under the DocumentStorage:BasePath directory.</summary>
    [Required]
    [Column("stored_file_path")]
    [StringLength(500)]
    public string stored_file_path { get; set; } = string.Empty;

    /// <summary>MIME type (e.g. image/jpeg, application/pdf).</summary>
    [Required]
    [Column("mime_type")]
    [StringLength(100)]
    public string mime_type { get; set; } = "application/octet-stream";

    /// <summary>File size in bytes.</summary>
    [Column("file_size_bytes")]
    public long file_size_bytes { get; set; }

    /// <summary>
    /// Optional: link to a specific record in another table.
    /// e.g. reference_type="Accident", reference_id=42 links to accident 42.
    /// Allows fetching all documents for a specific accident/fine/job card.
    /// </summary>
    [Column("reference_type")]
    [StringLength(50)]
    public string? reference_type { get; set; }

    [Column("reference_id")]
    public int? reference_id { get; set; }

    // Global audit fields
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

    // Navigation
    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }
}
