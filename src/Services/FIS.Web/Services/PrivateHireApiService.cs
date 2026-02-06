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
            var response = await _httpClient.GetAsync("api/privatehire");
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<List<PrivateHireVehicleResponseDto>>();
            return payload?.Select(MapVehicle).ToList() ?? new List<PrivateHireVehicleDto>();
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
            var response = await _httpClient.GetAsync($"api/privatehire/{vehicleId}");
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<PrivateHireVehicleResponseDto>();
            return payload == null ? null : MapVehicle(payload);
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
            var response = await _httpClient.PostAsJsonAsync("api/privatehire", ToApiVehicle(vehicle));
            response.EnsureSuccessStatusCode();

            var createdVehicle = await response.Content.ReadFromJsonAsync<PrivateHireVehicleResponseDto>();
            return createdVehicle == null
                ? throw new InvalidOperationException("Failed to create vehicle")
                : MapVehicle(createdVehicle);
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
            var response = await _httpClient.PutAsJsonAsync($"api/privatehire/{vehicleId}", ToApiVehicle(vehicle));
            response.EnsureSuccessStatusCode();

            var updatedVehicle = await response.Content.ReadFromJsonAsync<PrivateHireVehicleResponseDto>();
            return updatedVehicle == null
                ? throw new InvalidOperationException("Failed to update vehicle")
                : MapVehicle(updatedVehicle);
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
            var response = await _httpClient.DeleteAsync($"api/privatehire/{vehicleId}");
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
            var response = await _httpClient.GetAsync("api/privatehire/contractors");
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
            var response = await _httpClient.GetAsync($"api/privatehire/contractors/{contractorId}");
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
            var response = await _httpClient.PostAsJsonAsync("api/privatehire/contractors", contractor);
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
            var response = await _httpClient.PutAsJsonAsync($"api/privatehire/contractors/{contractorId}", contractor);
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
            var response = await _httpClient.DeleteAsync($"api/privatehire/contractors/{contractorId}");
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Unable to connect to the API service");
        }
    }

    private static PrivateHireVehicleDto MapVehicle(PrivateHireVehicleResponseDto source)
        => new()
        {
            vehicle_id = source.PHV_code ?? source.vehicle_id ?? 0,
            registration_number = source.registration_number ?? string.Empty,
            make_model = source.model_description ?? source.make_model ?? string.Empty,
            contractor_id = source.contractor_id ?? 0,
            contractor_name = source.contractor_name ?? string.Empty,
            department_code = source.department_code ?? 0,
            department_name = source.department_name ?? string.Empty,
            hire_start_date = source.date_hired ?? source.hire_start_date,
            hire_end_date = source.date_retired ?? source.hire_end_date,
            monthly_rate = source.monthly_rate,
            hire_status = source.hire_status ?? "Active",
            notes = source.notes ?? string.Empty
        };

    private static object ToApiVehicle(PrivateHireVehicleDto source)
        => new
        {
            PHV_code = source.vehicle_id,
            registration_number = source.registration_number,
            contractor_id = source.contractor_id == 0 ? (int?)null : source.contractor_id,
            department_code = source.department_code == 0 ? (int?)null : source.department_code,
            date_hired = source.hire_start_date,
            date_retired = source.hire_end_date,
            notes = source.notes
        };
}

internal sealed class PrivateHireVehicleResponseDto
{
    public int? PHV_code { get; set; }
    public int? vehicle_id { get; set; }
    public string? registration_number { get; set; }
    public string? model_description { get; set; }
    public string? make_model { get; set; }
    public int? contractor_id { get; set; }
    public string? contractor_name { get; set; }
    public int? department_code { get; set; }
    public string? department_name { get; set; }
    public DateTime? date_hired { get; set; }
    public DateTime? date_retired { get; set; }
    public DateTime? hire_start_date { get; set; }
    public DateTime? hire_end_date { get; set; }
    public decimal? monthly_rate { get; set; }
    public string? hire_status { get; set; }
    public string? notes { get; set; }
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
