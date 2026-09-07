namespace FIS.Core.Domain.Entities.Operations;

/// <summary>
/// Compatibility projection for a scanned licence certificate. A certificate
/// can come from the expanded vehicle_documents table or the original
/// scan_docs table; Source and DocumentKey keep those records distinguishable.
/// </summary>
public sealed class LicenseCertificateDocument
{
    public string Source { get; set; } = string.Empty;
    public string DocumentKey { get; set; } = string.Empty;
    public int vmf_code { get; set; }
    public string? image { get; set; }
    public string? document_description { get; set; }
    public string? original_file_name { get; set; }
    public string? stored_file_path { get; set; }
    public string mime_type { get; set; } = "application/octet-stream";
    public long file_size_bytes { get; set; }
    public DateTime? period_begin { get; set; }
    public DateTime? period_end { get; set; }
    public DateTime? date_created { get; set; }
    public DateTime? date_updated { get; set; }
}
