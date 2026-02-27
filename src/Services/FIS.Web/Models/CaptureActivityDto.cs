using System.Text.Json.Serialization;

namespace FIS.Web.Models;

public class CaptureActivityReportDto
{
    [JsonPropertyName("filters_applied")]
    public Dictionary<string, string> FiltersApplied { get; set; } = new();

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }

    [JsonPropertyName("summary")]
    public Dictionary<string, int> Summary { get; set; } = new();

    [JsonPropertyName("details")]
    public Dictionary<string, List<CaptureActivityDetailDto>> Details { get; set; } = new();
}

public class CaptureActivityDetailDto
{
    [JsonPropertyName("record_id")]
    public int RecordId { get; set; }

    [JsonPropertyName("vmf_code")]
    public int? VmfCode { get; set; }

    [JsonPropertyName("fleet_number")]
    public string? FleetNumber { get; set; }

    [JsonPropertyName("registration_number")]
    public string? RegistrationNumber { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("date_captured")]
    public DateTime? DateCaptured { get; set; }

    [JsonPropertyName("captured_by_user_code")]
    public int? CapturedByUserCode { get; set; }

    [JsonPropertyName("module")]
    public string? Module { get; set; }
}
