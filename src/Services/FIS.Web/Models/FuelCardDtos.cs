using System.Text.Json.Serialization;

namespace FIS.Web.Models;

public sealed record IssueFuelCardRequest
{
    [JsonPropertyName("vmfCode")]
    public int VmfCode { get; set; }

    [JsonPropertyName("receiverName")]
    public string ReceiverName { get; set; } = string.Empty;

    [JsonPropertyName("receiverTelephone")]
    public string ReceiverTelephone { get; set; } = string.Empty;

    [JsonPropertyName("siteCode")]
    public int SiteCode { get; set; }
}

public sealed record FuelCardCreateRequest
{
    public int? vmf_code { get; set; }
    public short? Counter { get; set; }
    public string? card_number { get; set; }
    public string? PAN_number { get; set; }
    public string? ExpReason { get; set; }
    public DateTime? Status_date { get; set; }
    public DateTime? PetTaken { get; set; }
    public DateTime? PetExpire { get; set; }
    public string? LinkGGNum { get; set; }
}

public sealed record PrivateHireFuelCardCreateRequest
{
    public string RegistrationNumber { get; set; } = string.Empty;
    public short? Counter { get; set; }
    public string? CardNumber { get; set; }
    public string? PanNumber { get; set; }
}

public sealed record ReturnFuelCardRequest
{
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

public sealed record FuelCardDto
{
    public int FuelCardCode { get; set; }
    public string CardNumber { get; set; } = string.Empty;
    public int VmfCode { get; set; }
    public string ReceiverName { get; set; } = string.Empty;
    public DateTime IssuedDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public short SiteCode { get; set; }
}

public sealed record FuelCardActivityDto
{
    public string CardNumber { get; set; } = string.Empty;
    public int VmfCode { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Receiver { get; set; } = string.Empty;
}

public sealed record FuelCardAllocationReportDto
{
    public int TotalCards { get; set; }
    public int ActiveCards { get; set; }
    public int ReturnedCards { get; set; }
    public int ExpiringCards { get; set; }
    public Dictionary<string, int> StatusBreakdown { get; set; } = new();
    public Dictionary<string, int> CardsByGarage { get; set; } = new();
    public List<FuelCardActivityDto> RecentActivity { get; set; } = new();
}

public record ApiResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Error { get; set; }
    public object? Data { get; set; }
}

public record FuelCardIssueResponse : ApiResponse
{
    public FuelCardDto? FuelCard { get; set; }
    public object? LegacyCompatibility { get; set; }
}

public record FuelCardReportResponse : ApiResponse
{
    public FuelCardAllocationReportDto? Report { get; set; }
    public object? LegacyDataSources { get; set; }
    public DateTime GeneratedAt { get; set; }
}
