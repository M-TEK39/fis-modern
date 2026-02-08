using FIS.Web.Models;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FIS.Web.Services;

internal class ApiContractResponse
{
    [JsonPropertyName("contractCode")]
    public int ContractCode { get; set; }

    [JsonPropertyName("vmfCode")]
    public int VmfCode { get; set; }

    [JsonPropertyName("siteCode")]
    public short SiteCode { get; set; }

    [JsonPropertyName("startDate")]
    public DateTime StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTime? EndDate { get; set; }

    [JsonPropertyName("stillCurrent")]
    public string? StillCurrent { get; set; }

    [JsonPropertyName("contractTypeCode")]
    public string? ContractTypeCode { get; set; }

    [JsonPropertyName("contractStatusCode")]
    public short? ContractStatusCode { get; set; }

    [JsonPropertyName("userCode")]
    public short? UserCode { get; set; }

    [JsonPropertyName("approverCode")]
    public int? ApproverCode { get; set; }

    [JsonPropertyName("createdByUserCode")]
    public int? CreatedByUserCode { get; set; }

    [JsonPropertyName("modifiedByUserCode")]
    public int? ModifiedByUserCode { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public class ContractApiService
{
    private readonly HttpClient _httpClient;
    private const int RecentContractWindow = 80;

    public ContractApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<FIS.Web.Models.ContractDto>> GetContractsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/Contracts/active");
            response.EnsureSuccessStatusCode();

            var apiContracts = await response.Content.ReadFromJsonAsync<List<ApiContractResponse>>();
            if (apiContracts == null)
            {
                return new List<FIS.Web.Models.ContractDto>();
            }

            return apiContracts.Select(MapToDto).ToList();
        }
        catch (HttpRequestException)
        {
            // Return empty list if API is not available
            return new List<FIS.Web.Models.ContractDto>();
        }
        catch (Exception)
        {
            // Return empty list for any other errors
            return new List<FIS.Web.Models.ContractDto>();
        }
    }

    public async Task<List<FIS.Web.Models.ContractDto>> GetRecentContractsWindowAsync()
    {
        var active = await GetContractsAsync();
        var results = new List<ContractDto>();
        if (active.Count == 0)
        {
            return results;
        }

        var maxId = active.Max(c => c.contract_id);
        var minId = Math.Max(1, maxId - RecentContractWindow);
        var upperId = maxId + 10;

        for (var id = minId; id <= upperId; id++)
        {
            var contract = await GetContractAsync(id);
            if (contract != null)
            {
                results.Add(contract);
            }
        }

        return results
            .GroupBy(c => c.contract_id)
            .Select(g => g.First())
            .ToList();
    }

    public async Task<ContractDto?> GetLatestContractForVehicleAsync(int vmfCode)
    {
        if (vmfCode <= 0)
        {
            return null;
        }

        var active = await GetContractsAsync();
        var directActive = active
            .Where(c => c.vmf_code == vmfCode)
            .OrderByDescending(c => c.contract_id)
            .FirstOrDefault();
        if (directActive != null)
        {
            return directActive;
        }

        var recent = await GetRecentContractsWindowAsync();
        return recent
            .Where(c => c.vmf_code == vmfCode)
            .OrderByDescending(c => c.contract_id)
            .FirstOrDefault();
    }

    public async Task<FIS.Web.Models.ContractDto?> GetContractAsync(int contractId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/Contracts/{contractId}");
            response.EnsureSuccessStatusCode();

            var apiContract = await response.Content.ReadFromJsonAsync<ApiContractResponse>();
            return apiContract == null ? null : MapToDto(apiContract);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<FIS.Web.Models.ContractDto> CreateContractAsync(ContractCreateDto contract)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/Contracts", contract);
            response.EnsureSuccessStatusCode();
            
            var createdContract = await response.Content.ReadFromJsonAsync<FIS.Web.Models.ContractDto>();
            return createdContract ?? throw new InvalidOperationException("Failed to create contract");
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Unable to connect to the API service");
        }
    }

    public async Task<FIS.Web.Models.ContractDto> UpdateContractAsync(int contractId, ContractUpdateDto contract)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/Contracts/{contractId}", contract);
            response.EnsureSuccessStatusCode();
            
            var updatedContract = await response.Content.ReadFromJsonAsync<FIS.Web.Models.ContractDto>();
            return updatedContract ?? throw new InvalidOperationException("Failed to update contract");
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Unable to connect to the API service");
        }
    }

    public async Task DeleteContractAsync(int contractId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/Contracts/{contractId}");
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Unable to connect to the API service");
        }
    }

    public async Task<bool> HireContractAsync(HireContractRequestDto request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/Contracts/hire", request);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task<List<ContractSummaryDto>> GetActiveContractsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/Contracts/active");
            response.EnsureSuccessStatusCode();

            var contracts = await response.Content.ReadFromJsonAsync<List<ContractSummaryDto>>();
            return contracts ?? new List<ContractSummaryDto>();
        }
        catch (HttpRequestException)
        {
            return new List<ContractSummaryDto>();
        }
        catch (Exception)
        {
            return new List<ContractSummaryDto>();
        }
    }

    public Task<FinanceApiResult> GetActionAsync(string endpoint)
        => SendAsync(HttpMethod.Get, endpoint, null);

    public Task<FinanceApiResult> PostActionAsync(string endpoint, object payload)
        => SendAsync(HttpMethod.Post, endpoint, payload);

    public Task<FinanceApiResult> PutActionAsync(string endpoint, object payload)
        => SendAsync(HttpMethod.Put, endpoint, payload);

    private async Task<FinanceApiResult> SendAsync(HttpMethod method, string endpoint, object? payload)
    {
        try
        {
            using var request = new HttpRequestMessage(method, endpoint);
            if (payload != null && method != HttpMethod.Get)
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
                var errorMessage = errorValue.GetString();
                if (!string.IsNullOrWhiteSpace(errorMessage))
                {
                    return errorMessage.Trim();
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
            // Ignore parse failures and keep the fallback status message.
        }

        return null;
    }

    private static ContractDto MapToDto(ApiContractResponse api)
    {
        var stillCurrent = (api.StillCurrent ?? string.Empty).Trim().ToUpperInvariant();
        var statusLabel = api.ContractStatusCode switch
        {
            1 => "Pending Approval",
            2 => "Approved",
            3 => stillCurrent == "Y" ? "Active" : "Closed",
            4 => "Declined For Correction",
            5 => "Declined",
            _ => stillCurrent == "Y" ? "Active" : stillCurrent == "N" ? "Closed" : "Unknown"
        };

        return new ContractDto
        {
            contract_id = api.ContractCode,
            vmf_code = api.VmfCode,
            site_code = api.SiteCode,
            contract_status_code = api.ContractStatusCode,
            still_current = stillCurrent,
            user_code = api.UserCode,
            approver_code = api.ApproverCode,
            created_by_user_code = api.CreatedByUserCode,
            modified_by_user_code = api.ModifiedByUserCode,
            contract_number = api.ContractCode.ToString(),
            vehicle_registration = api.VmfCode.ToString(),
            department_code = 0,
            contractor_name = api.ContractTypeCode ?? "",
            start_date = api.StartDate,
            end_date = api.EndDate,
            status = statusLabel,
            contract_notes = api.Notes
        };
    }
}

public class ContractCreateDto
{
    public string contract_number { get; set; } = "";
    public string vehicle_registration { get; set; } = "";
    public int department_code { get; set; }
    public string contractor_name { get; set; } = "";
    public DateTime start_date { get; set; }
    public DateTime? end_date { get; set; }
    public decimal? monthly_cost { get; set; }
    public string status { get; set; } = "Active";
    public string? contract_notes { get; set; }
}

public class ContractUpdateDto
{
    public string contract_number { get; set; } = "";
    public string vehicle_registration { get; set; } = "";
    public int department_code { get; set; }
    public string contractor_name { get; set; } = "";
    public DateTime start_date { get; set; }
    public DateTime? end_date { get; set; }
    public decimal? monthly_cost { get; set; }
    public string status { get; set; } = "Active";
    public string? contract_notes { get; set; }
}

public class HireContractRequestDto
{
    public int VmfCode { get; set; }
    public short SiteCode { get; set; }
    public int? StartOdometer { get; set; }
    public string? DriverId { get; set; }
    public string? Notes { get; set; }
    public DateTime? TargetReturnDate { get; set; }
}

public class ContractSummaryDto
{
    public int contract_code { get; set; }
    public int vmf_code { get; set; }
    public short site_code { get; set; }
    public string? still_current { get; set; }
}
