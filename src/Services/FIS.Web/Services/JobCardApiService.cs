using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FIS.Web.Models;

namespace FIS.Web.Services;

public class JobCardApiService : BaseApiService
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

    public Task<FinanceApiResult> CloseAsync(int id, string? notes)
        => SendActionAsync(HttpMethod.Post, $"{BasePath}/{id}/close", new JobCardCloseRequest
        {
            close_notes = notes
        });

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
                    : ExtractApiErrorMessage(body) ?? $"Request failed with status {(int)response.StatusCode} ({response.StatusCode}).",
                ResponseBody = body
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JobCard request failed for {Endpoint}", endpoint);
            return new FinanceApiResult
            {
                Success = false,
                Endpoint = endpoint,
                Message = ex.Message
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

    public string? CapturedBy => CapturedByUserCode?.ToString();
    public bool? Reviewed => ReviewedFlag?.Equals("Y", StringComparison.OrdinalIgnoreCase);
}
