using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FIS.Web.Models;

namespace FIS.Web.Services;

public partial class JobCardApiService : BaseApiService
{
    private const string BasePath = "api/jobcards";
    private readonly HttpClient _httpClient;
    private readonly ILogger<JobCardApiService> _logger;

    public JobCardApiService(HttpClient httpClient, TokenService tokenService, ILogger<JobCardApiService> logger)
        : base(httpClient, tokenService, logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<List<JobCardDto>> GetAllAsync()
        => GetListAsync<JobCardDto>(BasePath);

    public Task<JobCardDto?> GetByIdAsync(int id)
        => GetAsync<JobCardDto>($"{BasePath}/{id}");

    public Task<List<JobCardDto>> GetByGGNumberAsync(string ggNumber)
        => GetListAsync<JobCardDto>($"{BasePath}/by-gg/{Uri.EscapeDataString(ggNumber)}");

    public Task<List<JobCardDto>> GetPriorityUnassignedAsync()
        => GetListAsync<JobCardDto>($"{BasePath}/priority/unassigned");

    public Task<JobCardDto?> CreateAsync(CreateJobCardRequest payload)
        => PostAsync<CreateJobCardRequest, JobCardDto>(BasePath, payload);

    public Task<JobCardDto?> UpdateAsync(int id, UpdateJobCardRequest payload)
        => PutAsync<UpdateJobCardRequest, JobCardDto>($"{BasePath}/{id}", payload);

    public Task<FinanceApiResult> AuthorizeAsync(int id, string? comment)
        => SendActionAsync(HttpMethod.Post, $"{BasePath}/{id}/authorize", new JobCardAuthorizationRequest
        {
            comment = comment
        });

    public Task<FinanceApiResult> DeclineAsync(int id, string reason)
        => SendActionAsync(HttpMethod.Post, $"{BasePath}/{id}/decline", new JobCardDeclineRequest
        {
            decline_reason = reason
        });

    public Task<FinanceApiResult> CancelAsync(int id, string? reason)
        => SendActionAsync(HttpMethod.Post, $"{BasePath}/{id}/cancel", new JobCardCancelRequest
        {
            cancel_reason = reason
        });

    public Task<FinanceApiResult> CloseAsync(int id, JobCardCloseRequest payload)
        => SendActionAsync(HttpMethod.Post, $"{BasePath}/{id}/close", payload);

    public Task<FinanceApiResult> UpdateCostsAsync(int id, JobCardCostRequest payload)
        => SendActionAsync(HttpMethod.Patch, $"{BasePath}/{id}/costs", payload);

    public async Task<JobCardRepairCostReportDto> GetRepairCostReportAsync(
        int? vmfCode = null,
        int? siteCode = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var queryParts = new List<string>();
        if (vmfCode.HasValue && vmfCode.Value > 0)
        {
            queryParts.Add($"vmfCode={vmfCode.Value}");
        }
        if (siteCode.HasValue && siteCode.Value > 0)
        {
            queryParts.Add($"siteCode={siteCode.Value}");
        }
        if (fromDate.HasValue)
        {
            queryParts.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        }
        if (toDate.HasValue)
        {
            queryParts.Add($"toDate={toDate.Value:yyyy-MM-dd}");
        }

        var endpoint = $"{BasePath}/repair-cost-report";
        if (queryParts.Count > 0)
        {
            endpoint = $"{endpoint}?{string.Join("&", queryParts)}";
        }

        var response = await _httpClient.GetAsync(endpoint);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return new JobCardRepairCostReportDto();
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            return ParseRepairCostReport(doc.RootElement);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse repair cost report response.");
            return new JobCardRepairCostReportDto();
        }
    }

    public async Task<FinanceApiResult> DeleteAsync(int id)
        => await SendActionAsync(HttpMethod.Delete, $"{BasePath}/{id}", null);

    private async Task<FinanceApiResult> SendActionAsync(HttpMethod method, string endpoint, object? payload)
    {
        try
        {
            using var request = new HttpRequestMessage(method, endpoint);
            if (payload != null && method != HttpMethod.Get && method != HttpMethod.Delete)
            {
                request.Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");
            }

            using var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            return new FinanceApiResult
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                Endpoint = endpoint,
                Message = response.IsSuccessStatusCode
                    ? "Request completed successfully."
                    : SanitizeUserMessage(ExtractApiErrorMessage(body)) ?? $"Request failed with status {(int)response.StatusCode} ({response.StatusCode}).",
                ResponseBody = body
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JobCard request failed.");
            return new FinanceApiResult
            {
                Success = false,
                Endpoint = endpoint,
                Message = SanitizeUserMessage(ex.Message) ?? "Request failed."
            };
        }
    }

    private static string? ExtractApiErrorMessage(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var errorValue))
            {
                var error = errorValue.GetString();
                if (!string.IsNullOrWhiteSpace(error))
                {
                    return error.Trim();
                }
            }

            if (root.TryGetProperty("message", out var messageValue))
            {
                var message = messageValue.GetString();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    return message.Trim();
                }
            }
        }
        catch
        {
            // Keep fallback status text.
        }

        return null;
    }

    private static string? SanitizeUserMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var trimmed = message.Trim();
        var endpointIndex = trimmed.IndexOf("Endpoint:", StringComparison.OrdinalIgnoreCase);
        if (endpointIndex >= 0)
        {
            trimmed = trimmed[..endpointIndex].Trim();
        }

        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}

public class CreateJobCardRequest
{
    public int vmf_code { get; set; }
    public short extra_code { get; set; }
    public string? jcs_comment { get; set; }
    public string? damages { get; set; }
    public string? priority { get; set; }
}

public class UpdateJobCardRequest
{
    public string? jcs_comment { get; set; }
    public string? damages { get; set; }
    public string? comments { get; set; }
    public int? assigned_to { get; set; }
    public DateTime? assigned_date { get; set; }
    public string? priority { get; set; }
}

public class JobCardAuthorizationRequest
{
    public string? comment { get; set; }
}

public class JobCardDeclineRequest
{
    public string decline_reason { get; set; } = string.Empty;
}

public class JobCardCancelRequest
{
    public string? cancel_reason { get; set; }
}

public class JobCardCloseRequest
{
    public string? close_notes { get; set; }
    public decimal? labour_cost { get; set; }
    public decimal? parts_cost { get; set; }
    public decimal? other_cost { get; set; }
    public string? invoice_number { get; set; }
    public DateTime? invoice_date { get; set; }
    public string? service_provider { get; set; }
}

public class JobCardCostRequest
{
    public decimal? labour_cost { get; set; }
    public decimal? parts_cost { get; set; }
    public decimal? other_cost { get; set; }
    public string? invoice_number { get; set; }
    public DateTime? invoice_date { get; set; }
    public string? service_provider { get; set; }
}

public class JobCardDto
{
    [JsonPropertyName("job_card_id")]
    public int JobCardId { get; set; }

    [JsonPropertyName("gg_number")]
    public string? GGNumber { get; set; }

    [JsonPropertyName("vmf_code")]
    public int VmfCode { get; set; }

    [JsonPropertyName("registration_number")]
    public string? RegistrationNumber { get; set; }

    [JsonPropertyName("extra_code")]
    public short ExtraCode { get; set; }

    [JsonPropertyName("extra_description")]
    public string? JobDescription { get; set; }

    [JsonPropertyName("priority")]
    public string? Priority { get; set; }

    [JsonPropertyName("status_code")]
    public int StatusCode { get; set; }

    [JsonPropertyName("status_text")]
    public string? Status { get; set; }

    [JsonPropertyName("assigned_to")]
    public int? AssignedTo { get; set; }

    [JsonPropertyName("assigned_to_name")]
    public string? AssignedToName { get; set; }

    [JsonPropertyName("assigned_date")]
    public DateTime? AssignedDate { get; set; }

    [JsonPropertyName("date_created")]
    public DateTime? DateCreated { get; set; }

    [JsonPropertyName("captured_by_user_code")]
    public int? CapturedByUserCode { get; set; }

    [JsonPropertyName("authorized_by_user_code")]
    public int? AuthorizedByUserCode { get; set; }

    [JsonPropertyName("comments")]
    public string? Comments { get; set; }

    [JsonPropertyName("jcs_comment")]
    public string? JcsComment { get; set; }

    [JsonPropertyName("damages")]
    public string? Damages { get; set; }

    [JsonPropertyName("authorizer")]
    public int? Authorizer { get; set; }

    [JsonPropertyName("authorizer_name")]
    public string? AuthorizerName { get; set; }

    [JsonPropertyName("reviewed")]
    public string? ReviewedFlag { get; set; }

    [JsonPropertyName("labour_cost")]
    public decimal? LabourCost { get; set; }

    [JsonPropertyName("parts_cost")]
    public decimal? PartsCost { get; set; }

    [JsonPropertyName("other_cost")]
    public decimal? OtherCost { get; set; }

    [JsonPropertyName("total_cost")]
    public decimal? TotalCost { get; set; }

    [JsonPropertyName("invoice_number")]
    public string? InvoiceNumber { get; set; }

    [JsonPropertyName("invoice_date")]
    public DateTime? InvoiceDate { get; set; }

    [JsonPropertyName("service_provider")]
    public string? ServiceProvider { get; set; }

    public string? CapturedBy => CapturedByUserCode?.ToString();
    [JsonIgnore]
    public bool? Reviewed => ReviewedFlag?.Equals("Y", StringComparison.OrdinalIgnoreCase);
}

public class JobCardRepairCostReportDto
{
    public List<JobCardRepairCostLineDto> Items { get; set; } = new();
    public decimal GrandTotal { get; set; }
    public decimal TotalLabour { get; set; }
    public decimal TotalParts { get; set; }
    public decimal TotalOther { get; set; }
}

public class JobCardRepairCostLineDto
{
    public int JobCardId { get; set; }
    public int? VmfCode { get; set; }
    public int? SiteCode { get; set; }
    public string? GGNumber { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? ServiceProvider { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime? InvoiceDate { get; set; }
    public decimal LabourCost { get; set; }
    public decimal PartsCost { get; set; }
    public decimal OtherCost { get; set; }
    public decimal TotalCost { get; set; }
    public DateTime? DateClosed { get; set; }
}

public partial class JobCardApiService
{
    private static JobCardRepairCostReportDto ParseRepairCostReport(JsonElement root)
    {
        var result = new JobCardRepairCostReportDto();
        var itemsElement = TryGetProperty(root, "items")
                           ?? TryGetProperty(root, "lineItems")
                           ?? TryGetProperty(root, "records")
                           ?? TryGetProperty(root, "data");

        if (itemsElement.HasValue && itemsElement.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in itemsElement.Value.EnumerateArray())
            {
                result.Items.Add(new JobCardRepairCostLineDto
                {
                    JobCardId = ReadInt(row, "job_card_id", "jobCardId"),
                    VmfCode = ReadNullableInt(row, "vmf_code", "vmfCode"),
                    SiteCode = ReadNullableInt(row, "site_code", "siteCode"),
                    GGNumber = ReadString(row, "gg_number", "ggNumber"),
                    RegistrationNumber = ReadString(row, "registration_number", "registrationNumber"),
                    ServiceProvider = ReadString(row, "service_provider", "serviceProvider"),
                    InvoiceNumber = ReadString(row, "invoice_number", "invoiceNumber"),
                    InvoiceDate = ReadNullableDate(row, "invoice_date", "invoiceDate"),
                    LabourCost = ReadDecimal(row, "labour_cost", "labourCost"),
                    PartsCost = ReadDecimal(row, "parts_cost", "partsCost"),
                    OtherCost = ReadDecimal(row, "other_cost", "otherCost"),
                    TotalCost = ReadDecimal(row, "total_cost", "totalCost"),
                    DateClosed = ReadNullableDate(row, "date_closed", "dateClosed")
                });
            }
        }

        result.GrandTotal = ReadDecimal(root, "grand_total", "grandTotal");
        result.TotalLabour = ReadDecimal(root, "total_labour", "totalLabour");
        result.TotalParts = ReadDecimal(root, "total_parts", "totalParts");
        result.TotalOther = ReadDecimal(root, "total_other", "totalOther");
        return result;
    }

    private static JsonElement? TryGetProperty(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) ? value : null;

    private static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var property))
            {
                var value = property.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }
        return null;
    }

    private static int ReadInt(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var property) && property.TryGetInt32(out var value))
            {
                return value;
            }
        }
        return 0;
    }

    private static int? ReadNullableInt(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var property) && property.TryGetInt32(out var value))
            {
                return value;
            }
        }
        return null;
    }

    private static decimal ReadDecimal(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var decimalValue))
            {
                return decimalValue;
            }

            if (property.ValueKind == JsonValueKind.String
                && decimal.TryParse(property.GetString(), out decimalValue))
            {
                return decimalValue;
            }
        }
        return 0m;
    }

    private static DateTime? ReadNullableDate(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.String
                && DateTime.TryParse(property.GetString(), out var dateValue))
            {
                return dateValue;
            }
        }
        return null;
    }
}
