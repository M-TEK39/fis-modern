using FIS.Web.Models;
using System.ComponentModel.DataAnnotations;

namespace FIS.Web.Services;

public class PrivateHireApiService
{
    private readonly HttpClient _httpClient;

    public PrivateHireApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // Vehicle methods
    public async Task<List<PrivateHireVehicleDto>> GetVehiclesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/private-hire/vehicles");
            response.EnsureSuccessStatusCode();
            
            var vehicles = await response.Content.ReadFromJsonAsync<List<PrivateHireVehicleDto>>();
            return vehicles ?? new List<PrivateHireVehicleDto>();
        }
        catch (HttpRequestException)
        {
            return new List<PrivateHireVehicleDto>();
        }
        catch (Exception)
        {
            return new List<PrivateHireVehicleDto>();
        }
    }

    public async Task<PrivateHireVehicleDto?> GetVehicleAsync(int vehicleId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/private-hire/vehicles/{vehicleId}");
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadFromJsonAsync<PrivateHireVehicleDto>();
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

    public async Task<PrivateHireVehicleDto> CreateVehicleAsync(PrivateHireVehicleDto vehicle)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/private-hire/vehicles", vehicle);
            response.EnsureSuccessStatusCode();
            
            var createdVehicle = await response.Content.ReadFromJsonAsync<PrivateHireVehicleDto>();
            return createdVehicle ?? throw new InvalidOperationException("Failed to create vehicle");
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Unable to connect to the API service");
        }
    }

    public async Task<PrivateHireVehicleDto> UpdateVehicleAsync(int vehicleId, PrivateHireVehicleDto vehicle)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/private-hire/vehicles/{vehicleId}", vehicle);
            response.EnsureSuccessStatusCode();
            
            var updatedVehicle = await response.Content.ReadFromJsonAsync<PrivateHireVehicleDto>();
            return updatedVehicle ?? throw new InvalidOperationException("Failed to update vehicle");
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Unable to connect to the API service");
        }
    }

    public async Task DeleteVehicleAsync(int vehicleId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/private-hire/vehicles/{vehicleId}");
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Unable to connect to the API service");
        }
    }

    // Contractor methods
    public async Task<List<PrivateHireContractorDto>> GetContractorsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/private-hire/contractors");
            response.EnsureSuccessStatusCode();
            
            var contractors = await response.Content.ReadFromJsonAsync<List<PrivateHireContractorDto>>();
            return contractors ?? new List<PrivateHireContractorDto>();
        }
        catch (HttpRequestException)
        {
            return new List<PrivateHireContractorDto>();
        }
        catch (Exception)
        {
            return new List<PrivateHireContractorDto>();
        }
    }

    public async Task<PrivateHireContractorDto?> GetContractorAsync(int contractorId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/private-hire/contractors/{contractorId}");
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadFromJsonAsync<PrivateHireContractorDto>();
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

    public async Task<PrivateHireContractorDto> CreateContractorAsync(PrivateHireContractorDto contractor)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/private-hire/contractors", contractor);
            response.EnsureSuccessStatusCode();
            
            var createdContractor = await response.Content.ReadFromJsonAsync<PrivateHireContractorDto>();
            return createdContractor ?? throw new InvalidOperationException("Failed to create contractor");
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Unable to connect to the API service");
        }
    }

    public async Task<PrivateHireContractorDto> UpdateContractorAsync(int contractorId, PrivateHireContractorDto contractor)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/private-hire/contractors/{contractorId}", contractor);
            response.EnsureSuccessStatusCode();
            
            var updatedContractor = await response.Content.ReadFromJsonAsync<PrivateHireContractorDto>();
            return updatedContractor ?? throw new InvalidOperationException("Failed to update contractor");
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Unable to connect to the API service");
        }
    }

    public async Task DeleteContractorAsync(int contractorId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/private-hire/contractors/{contractorId}");
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Unable to connect to the API service");
        }
    }
}

// DTOs for Private Hire operations
public class PrivateHireVehicleDto
{
    public int vehicle_id { get; set; }
    [Required]
    public string registration_number { get; set; } = "";
    [Required]
    public string make_model { get; set; } = "";
    [Range(1, int.MaxValue, ErrorMessage = "Contractor is required.")]
    public int contractor_id { get; set; }
    public string contractor_name { get; set; } = "";
    [Range(1, int.MaxValue, ErrorMessage = "Department is required.")]
    public int department_code { get; set; }
    public string department_name { get; set; } = "";
    [Required]
    public DateTime? hire_start_date { get; set; }
    public DateTime? hire_end_date { get; set; }
    public decimal? monthly_rate { get; set; }
    public string hire_status { get; set; } = "Active";
    public string notes { get; set; } = "";
}

public class PrivateHireContractorDto
{
    public int contractor_id { get; set; }
    [Required]
    public string company_name { get; set; } = "";
    [Required]
    public string contact_person { get; set; } = "";
    [Required]
    [Phone]
    public string phone { get; set; } = "";
    [EmailAddress]
    public string email { get; set; } = "";
    public string business_registration { get; set; } = "";
    public string address { get; set; } = "";
    public string status { get; set; } = "Active";
}
