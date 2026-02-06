using FIS.Web.Models;
using System.Text;
using System.Text.Json;

namespace FIS.Web.Services;

internal class ApiContractResponse
{
    public int contract_code { get; set; }
    public int vmf_code { get; set; }
    public short site_code { get; set; }
    public DateTime start_date { get; set; }
    public DateTime? end_date { get; set; }
    public string? still_current { get; set; }
    public string? contract_type { get; set; }
    public string? Notes { get; set; }
}

public class ContractApiService
{
    private readonly HttpClient _httpClient;

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
                    : $"Request failed with status {(int)response.StatusCode} ({response.StatusCode}).",
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

    private static ContractDto MapToDto(ApiContractResponse api)
    {
        return new ContractDto
        {
            contract_id = api.contract_code,
            vmf_code = api.vmf_code,
            site_code = api.site_code,
            contract_number = api.contract_code.ToString(),
            vehicle_registration = api.vmf_code.ToString(),
            department_code = 0,
            contractor_name = api.contract_type ?? "",
            start_date = api.start_date,
            end_date = api.end_date,
            status = api.still_current == "Y" ? "Active" : api.still_current == "N" ? "Closed" : "Unknown",
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
