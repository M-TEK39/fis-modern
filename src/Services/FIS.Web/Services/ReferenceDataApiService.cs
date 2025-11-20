using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

public class ReferenceDataApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ReferenceDataApiService> _logger;

    public ReferenceDataApiService(HttpClient httpClient, ILogger<ReferenceDataApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    // Makes
    public async Task<List<MakeDto>> GetMakesAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<MakeDto>>("api/makes");
            return result ?? new List<MakeDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching makes");
            return new List<MakeDto>();
        }
    }

    public async Task<MakeDto?> GetMakeAsync(int makeCode)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<MakeDto>($"api/makes/{makeCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching make {MakeCode}", makeCode);
            return null;
        }
    }

    public async Task<bool> CreateMakeAsync(MakeDto make)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/makes", make);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating make");
            return false;
        }
    }

    public async Task<bool> UpdateMakeAsync(int makeCode, MakeDto make)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/makes/{makeCode}", make);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating make {MakeCode}", makeCode);
            return false;
        }
    }

    public async Task<bool> DeleteMakeAsync(int makeCode)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/makes/{makeCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting make {MakeCode}", makeCode);
            return false;
        }
    }

    // Models
    public async Task<List<ModelDto>> GetModelsAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<ModelDto>>("api/models");
            return result ?? new List<ModelDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching models");
            return new List<ModelDto>();
        }
    }

    public async Task<ModelDto?> GetModelAsync(int modelCode)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ModelDto>($"api/models/{modelCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching model {ModelCode}", modelCode);
            return null;
        }
    }

    public async Task<bool> CreateModelAsync(ModelDto model)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/models", model);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating model");
            return false;
        }
    }

    public async Task<bool> UpdateModelAsync(int modelCode, ModelDto model)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/models/{modelCode}", model);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating model {ModelCode}", modelCode);
            return false;
        }
    }

    public async Task<bool> DeleteModelAsync(int modelCode)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/models/{modelCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting model {ModelCode}", modelCode);
            return false;
        }
    }

    // Types
    public async Task<List<VehicleTypeDto>> GetTypesAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<VehicleTypeDto>>("api/types");
            return result ?? new List<VehicleTypeDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching types");
            return new List<VehicleTypeDto>();
        }
    }

    public async Task<VehicleTypeDto?> GetTypeAsync(int typeCode)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<VehicleTypeDto>($"api/types/{typeCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching type {TypeCode}", typeCode);
            return null;
        }
    }

    public async Task<bool> CreateTypeAsync(VehicleTypeDto type)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/types", type);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating type");
            return false;
        }
    }

    public async Task<bool> UpdateTypeAsync(int typeCode, VehicleTypeDto type)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/types/{typeCode}", type);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating type {TypeCode}", typeCode);
            return false;
        }
    }

    public async Task<bool> DeleteTypeAsync(int typeCode)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/types/{typeCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting type {TypeCode}", typeCode);
            return false;
        }
    }

    // Fuel Types
    public async Task<List<FuelTypeDto>> GetFuelTypesAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<FuelTypeDto>>("api/fueltypes");
            return result ?? new List<FuelTypeDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching fuel types");
            return new List<FuelTypeDto>();
        }
    }

    public async Task<FuelTypeDto?> GetFuelTypeAsync(int fuelTypeCode)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<FuelTypeDto>($"api/fueltypes/{fuelTypeCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching fuel type {FuelTypeCode}", fuelTypeCode);
            return null;
        }
    }

    public async Task<bool> CreateFuelTypeAsync(FuelTypeDto fuelType)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/fueltypes", fuelType);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating fuel type");
            return false;
        }
    }

    public async Task<bool> UpdateFuelTypeAsync(int fuelTypeCode, FuelTypeDto fuelType)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/fueltypes/{fuelTypeCode}", fuelType);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating fuel type {FuelTypeCode}", fuelTypeCode);
            return false;
        }
    }

    public async Task<bool> DeleteFuelTypeAsync(int fuelTypeCode)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/fueltypes/{fuelTypeCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting fuel type {FuelTypeCode}", fuelTypeCode);
            return false;
        }
    }
}
