using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

// Internal API response models for mapping
internal class ApiMakeResponse
{
    public short make_code { get; set; }
    public string make_description { get; set; } = string.Empty;
    public DateTime? date_updated { get; set; }
}

internal class ApiModelResponse
{
    public short model_code { get; set; }
    public short make_code { get; set; }
    public string model_description { get; set; } = string.Empty;
    public string? make_description { get; set; }
    public short unit_of_measure_code { get; set; }
    public short licence_code { get; set; }
    public short class_code { get; set; }
    public short fuel_type_code { get; set; }
    public short? type_code { get; set; }
    public short? maint_trigger_code { get; set; }
    public string? engine_type { get; set; }
    public short? engine_capacity { get; set; }
    public short? rated_power { get; set; }
    public short? fuel_tank_capacity { get; set; }
    public decimal? target_consumption { get; set; }
    public int? target_tyre_life { get; set; }
    public int? service_interval { get; set; }
    public string? vemm_code { get; set; }
    public short? licence_fee_code { get; set; }
    public int? gvm { get; set; }
    public string? transmission { get; set; }
    public decimal? wesbank_kilos_per_litre { get; set; }
}

internal class ApiVehicleTypeResponse
{
    public short type_code { get; set; }
    public string type_description { get; set; } = string.Empty;
}

internal class ApiFuelTypeResponse
{
    public short fuel_type_code { get; set; }
    public string fuel_description { get; set; } = string.Empty;
    public decimal? rate_per_litre { get; set; }
}

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
            // Define inline class for API response mapping
            var apiResponse = await _httpClient.GetFromJsonAsync<List<ApiMakeResponse>>("api/make");
            if (apiResponse == null) return new List<MakeDto>();
            
            return apiResponse.Select(make => new MakeDto
            {
                make_code = make.make_code,
                make_name = make.make_description,
                date_updated = make.date_updated
            }).ToList();
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
            var apiResponse = await _httpClient.GetFromJsonAsync<ApiMakeResponse>($"api/make/{makeCode}");
            if (apiResponse == null) return null;
            
            return new MakeDto
            {
                make_code = apiResponse.make_code,
                make_name = apiResponse.make_description,
                date_updated = apiResponse.date_updated
            };
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
            // Map MakeDto to CreateMakeDto for API
            var createMakeDto = new
            {
                make_description = make.make_name
            };
            
            var response = await _httpClient.PostAsJsonAsync("api/make", createMakeDto);
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
            // Map MakeDto to Make entity for API
            var makeEntity = new
            {
                make_code = (short)makeCode,
                make_description = make.make_name
            };
            
            var response = await _httpClient.PutAsJsonAsync($"api/make/{makeCode}", makeEntity);
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
            var response = await _httpClient.DeleteAsync($"api/make/{makeCode}");
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
            var apiResponse = await _httpClient.GetFromJsonAsync<List<ApiModelResponse>>("api/model");
            if (apiResponse == null) return new List<ModelDto>();
            
            return apiResponse.Select(model => new ModelDto
            {
                model_code = model.model_code,
                make_code = model.make_code,
                model_name = model.model_description,
                make_name = model.make_description,
                unit_of_measure_code = model.unit_of_measure_code,
                fuel_type_code = model.fuel_type_code,
                licence_code = model.licence_code,
                class_code = model.class_code,
                type_code = model.type_code,
                maint_trigger_code = model.maint_trigger_code,
                engine_type = model.engine_type,
                engine_capacity = model.engine_capacity,
                rated_power = model.rated_power,
                fuel_tank_capacity = model.fuel_tank_capacity,
                target_consumption = model.target_consumption,
                target_tyre_life = model.target_tyre_life,
                service_interval = model.service_interval,
                vemm_code = model.vemm_code,
                licence_fee_code = model.licence_fee_code,
                gvm = model.gvm,
                transmission = model.transmission,
                wesbank_kilos_per_litre = model.wesbank_kilos_per_litre
            }).ToList();
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
            var apiResponse = await _httpClient.GetFromJsonAsync<ApiModelResponse>($"api/model/{modelCode}");
            if (apiResponse == null) return null;
            
            return new ModelDto
            {
                model_code = apiResponse.model_code,
                make_code = apiResponse.make_code,
                model_name = apiResponse.model_description,
                make_name = apiResponse.make_description,
                unit_of_measure_code = apiResponse.unit_of_measure_code,
                fuel_type_code = apiResponse.fuel_type_code,
                licence_code = apiResponse.licence_code,
                class_code = apiResponse.class_code,
                type_code = apiResponse.type_code,
                maint_trigger_code = apiResponse.maint_trigger_code,
                engine_type = apiResponse.engine_type,
                engine_capacity = apiResponse.engine_capacity,
                rated_power = apiResponse.rated_power,
                fuel_tank_capacity = apiResponse.fuel_tank_capacity,
                target_consumption = apiResponse.target_consumption,
                target_tyre_life = apiResponse.target_tyre_life,
                service_interval = apiResponse.service_interval,
                vemm_code = apiResponse.vemm_code,
                licence_fee_code = apiResponse.licence_fee_code,
                gvm = apiResponse.gvm,
                transmission = apiResponse.transmission,
                wesbank_kilos_per_litre = apiResponse.wesbank_kilos_per_litre
            };
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
            // Map ModelDto to API expected format
            var createModelDto = new
            {
                make_code = model.make_code,
                unit_of_measure_code = model.unit_of_measure_code,
                type_code = model.type_code,
                fuel_type_code = model.fuel_type_code,
                licence_code = model.licence_code,
                class_code = model.class_code,
                model_description = model.model_name,
                maint_trigger_code = model.maint_trigger_code,
                engine_type = model.engine_type,
                engine_capacity = model.engine_capacity,
                rated_power = model.rated_power,
                fuel_tank_capacity = model.fuel_tank_capacity,
                target_consumption = model.target_consumption,
                target_tyre_life = model.target_tyre_life,
                service_interval = model.service_interval,
                vemm_code = model.vemm_code,
                licence_fee_code = model.licence_fee_code,
                gvm = model.gvm,
                transmission = model.transmission,
                wesbank_kilos_per_litre = model.wesbank_kilos_per_litre
            };
            
            var response = await _httpClient.PostAsJsonAsync("api/model", createModelDto);
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
            // Map ModelDto to Model entity for API
            var modelEntity = new
            {
                model_code = (short)modelCode,
                make_code = model.make_code,
                unit_of_measure_code = model.unit_of_measure_code,
                type_code = model.type_code,
                fuel_type_code = model.fuel_type_code,
                licence_code = model.licence_code,
                class_code = model.class_code,
                model_description = model.model_name,
                maint_trigger_code = model.maint_trigger_code,
                engine_type = model.engine_type,
                engine_capacity = model.engine_capacity,
                rated_power = model.rated_power,
                fuel_tank_capacity = model.fuel_tank_capacity,
                target_consumption = model.target_consumption,
                target_tyre_life = model.target_tyre_life,
                service_interval = model.service_interval,
                vemm_code = model.vemm_code,
                licence_fee_code = model.licence_fee_code,
                gvm = model.gvm,
                transmission = model.transmission,
                wesbank_kilos_per_litre = model.wesbank_kilos_per_litre
            };
            
            var response = await _httpClient.PutAsJsonAsync($"api/model/{modelCode}", modelEntity);
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
            var response = await _httpClient.DeleteAsync($"api/model/{modelCode}");
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
            var apiResponse = await _httpClient.GetFromJsonAsync<List<ApiVehicleTypeResponse>>("api/type");
            if (apiResponse == null) return new List<VehicleTypeDto>();
            
            return apiResponse.Select(type => new VehicleTypeDto
            {
                type_code = type.type_code,
                type_name = type.type_description
            }).ToList();
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
            var apiResponse = await _httpClient.GetFromJsonAsync<ApiVehicleTypeResponse>($"api/type/{typeCode}");
            if (apiResponse == null) return null;
            
            return new VehicleTypeDto
            {
                type_code = apiResponse.type_code,
                type_name = apiResponse.type_description
            };
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
            // Map VehicleTypeDto to API expected format
            var createTypeDto = new
            {
                type_description = type.type_name
            };
            
            var response = await _httpClient.PostAsJsonAsync("api/type", createTypeDto);
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
            // Map VehicleTypeDto to Type entity for API
            var typeEntity = new
            {
                type_code = (short)typeCode,
                type_description = type.type_name
            };
            
            var response = await _httpClient.PutAsJsonAsync($"api/type/{typeCode}", typeEntity);
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
            var response = await _httpClient.DeleteAsync($"api/type/{typeCode}");
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
            var apiResponse = await _httpClient.GetFromJsonAsync<List<ApiFuelTypeResponse>>("api/fueltype");
            if (apiResponse == null) return new List<FuelTypeDto>();
            
            return apiResponse.Select(fuelType => new FuelTypeDto
            {
                fuel_type_code = fuelType.fuel_type_code,
                fuel_type_name = fuelType.fuel_description,
                rate_per_litre = fuelType.rate_per_litre
            }).ToList();
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
            var apiResponse = await _httpClient.GetFromJsonAsync<ApiFuelTypeResponse>($"api/fueltype/{fuelTypeCode}");
            if (apiResponse == null) return null;
            
            return new FuelTypeDto
            {
                fuel_type_code = apiResponse.fuel_type_code,
                fuel_type_name = apiResponse.fuel_description,
                rate_per_litre = apiResponse.rate_per_litre
            };
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
            // Map FuelTypeDto to API expected format
            var createFuelTypeDto = new
            {
                fuel_description = fuelType.fuel_type_name,
                rate_per_litre = fuelType.rate_per_litre
            };
            
            var response = await _httpClient.PostAsJsonAsync("api/fueltype", createFuelTypeDto);
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
            // Map FuelTypeDto to FuelType entity for API
            var fuelTypeEntity = new
            {
                fuel_type_code = (short)fuelTypeCode,
                fuel_description = fuelType.fuel_type_name,
                rate_per_litre = fuelType.rate_per_litre
            };
            
            var response = await _httpClient.PutAsJsonAsync(
                $"api/fueltype/{fuelTypeCode}",
                fuelTypeEntity
            );
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
            var response = await _httpClient.DeleteAsync($"api/fueltype/{fuelTypeCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting fuel type {FuelTypeCode}", fuelTypeCode);
            return false;
        }
    }
}
