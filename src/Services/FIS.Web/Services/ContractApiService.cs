using FIS.Web.Models;

namespace FIS.Web.Services;

public class ContractApiService
{
    private readonly HttpClient _httpClient;

    public ContractApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<ContractDto>> GetContractsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/Contracts");
            response.EnsureSuccessStatusCode();
            
            var contracts = await response.Content.ReadFromJsonAsync<List<ContractDto>>();
            return contracts ?? new List<ContractDto>();
        }
        catch (HttpRequestException)
        {
            // Return empty list if API is not available
            return new List<ContractDto>();
        }
        catch (Exception)
        {
            // Return empty list for any other errors
            return new List<ContractDto>();
        }
    }

    public async Task<ContractDto?> GetContractAsync(int contractId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/Contracts/{contractId}");
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadFromJsonAsync<ContractDto>();
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

    public async Task<ContractDto> CreateContractAsync(ContractCreateDto contract)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/Contracts", contract);
            response.EnsureSuccessStatusCode();
            
            var createdContract = await response.Content.ReadFromJsonAsync<ContractDto>();
            return createdContract ?? throw new InvalidOperationException("Failed to create contract");
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Unable to connect to the API service");
        }
    }

    public async Task<ContractDto> UpdateContractAsync(int contractId, ContractUpdateDto contract)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/Contracts/{contractId}", contract);
            response.EnsureSuccessStatusCode();
            
            var updatedContract = await response.Content.ReadFromJsonAsync<ContractDto>();
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
}

// DTOs for Contract operations
public class ContractDto
{
    public int contract_id { get; set; }
    public string contract_number { get; set; } = "";
    public string vehicle_registration { get; set; } = "";
    public int department_code { get; set; }
    public string department_name { get; set; } = "";
    public string contractor_name { get; set; } = "";
    public DateTime start_date { get; set; }
    public DateTime? end_date { get; set; }
    public decimal? monthly_cost { get; set; }
    public string status { get; set; } = "Active";
    public string contract_notes { get; set; } = "";
    public string vehicle_make { get; set; } = "";
    public string vehicle_model { get; set; } = "";
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
    public string contract_notes { get; set; } = "";
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
    public string contract_notes { get; set; } = "";
}
