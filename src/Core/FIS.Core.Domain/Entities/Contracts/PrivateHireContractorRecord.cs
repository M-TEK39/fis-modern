namespace FIS.Core.Domain.Entities.Contracts;

/// <summary>
/// Compatibility record for the legacy Contractors table and its expanded audit shape.
/// This is intentionally not an EF entity: the table is read and written through guarded
/// SQL because either the legacy or expanded column set may be present at runtime.
/// </summary>
public sealed class PrivateHireContractorRecord
{
    public short contractor_id { get; set; }
    public string? contractor_name { get; set; }
    public string? physical_address { get; set; }
    public string? postal_address { get; set; }
    public string? tel_number { get; set; }
    public string? fax_number { get; set; }
    public string? email_address { get; set; }
    public string? contact_person { get; set; }
    public short? active { get; set; }
    public string? type { get; set; }
    public bool? quotations { get; set; }
    public string? project_name { get; set; }
    public DateTime? project_begdat { get; set; }
    public DateTime? project_enddat { get; set; }

    public DateTime? date_created { get; set; }
    public DateTime? date_updated { get; set; }
    public int? created_by_user_code { get; set; }
    public int? modified_by_user_code { get; set; }
    public bool is_deleted { get; set; }
}
