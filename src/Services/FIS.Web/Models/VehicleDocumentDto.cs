using System.Text.Json.Serialization;

namespace FIS.Web.Models;

public class VehicleDocumentDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("document_id")]
    public int DocumentId { get; set; }

    [JsonPropertyName("vmf_code")]
    public int VmfCode { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("file_name")]
    public string? FileName { get; set; }

    [JsonPropertyName("content_type")]
    public string? ContentType { get; set; }

    [JsonPropertyName("file_size_bytes")]
    public long? FileSizeBytes { get; set; }

    [JsonPropertyName("reference_type")]
    public string? ReferenceType { get; set; }

    [JsonPropertyName("reference_id")]
    public int? ReferenceId { get; set; }

    [JsonPropertyName("date_created")]
    public DateTime? DateCreated { get; set; }
}

public class VehicleDocumentDownloadResult
{
    public string FileName { get; set; } = "document";
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Content { get; set; } = Array.Empty<byte>();
}
