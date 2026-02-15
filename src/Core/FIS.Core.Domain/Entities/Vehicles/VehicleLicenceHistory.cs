using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// Preserves a point-in-time snapshot of a vehicle's licence record each time
/// a new licence is captured.  The vehicle_master fields are updated in place
/// (legacy behaviour) but the previous values are written here first so that
/// the full renewal history is never lost.
///
/// Table: vehicle_licence_history  (new — no legacy equivalent)
/// </summary>
[Table("vehicle_licence_history")]
public class VehicleLicenceHistory
{
    [Key]
    [Column("licence_history_id")]
    public int licence_history_id { get; set; }

    /// <summary>FK to vehicle_master</summary>
    [Required]
    [Column("vmf_code")]
    public int vmf_code { get; set; }

    // ── Snapshotted licence fields (mirrors vehicle_master columns) ──────

    [Column("licence_due_date")]
    public DateTime? licence_due_date { get; set; }

    [Column("lic_register_number")]
    [StringLength(50)]
    public string? lic_register_number { get; set; }

    [Column("lic_registration_doc")]
    [StringLength(100)]
    public string? lic_registration_doc { get; set; }

    [Column("licence_comments")]
    [StringLength(2000)]
    public string? licence_comments { get; set; }

    [Column("cof_last_done")]
    public DateTime? cof_last_done { get; set; }

    [Column("cof_required")]
    [StringLength(1)]
    public string? cof_required { get; set; }

    [Column("tare")]
    public int? tare { get; set; }

    [Column("Licence_receiver")]
    [StringLength(100)]
    public string? Licence_receiver { get; set; }

    [Column("Licence_receiver_id")]
    [StringLength(50)]
    public string? Licence_receiver_id { get; set; }

    [Column("Licence_receiver_tel")]
    [StringLength(30)]
    public string? Licence_receiver_tel { get; set; }

    [Column("Licence_receiver_site")]
    public short? Licence_receiver_site { get; set; }

    [Column("Licence_date_taken")]
    public DateTime? Licence_date_taken { get; set; }

    // ── Metadata ─────────────────────────────────────────────────────────

    /// <summary>
    /// When this snapshot was saved (i.e. the moment the old licence was superseded).
    /// </summary>
    [Column("captured_at")]
    public DateTime captured_at { get; set; }

    /// <summary>
    /// User who performed the new licence capture (triggering this snapshot).
    /// </summary>
    [Column("captured_by_user_code")]
    public int? captured_by_user_code { get; set; }

    /// <summary>
    /// Optional free-text note recorded at the time of the update.
    /// </summary>
    [Column("update_notes")]
    [StringLength(2000)]
    public string? update_notes { get; set; }

    // Navigation properties
    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }

    [ForeignKey("captured_by_user_code")]
    public virtual User? CapturedByUser { get; set; }
}
