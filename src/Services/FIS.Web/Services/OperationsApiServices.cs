using System.Net.Http.Json;
using System.Text.Json;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace FIS.Web.Services;

/// <summary>
/// Lightweight base class for simple CRUD-style API calls with case-insensitive JSON handling.
/// Components can supply their own DTO types when calling these methods.
/// </summary>
public abstract class BaseApiService
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    protected BaseApiService(HttpClient httpClient, ILogger logger)
    {
        HttpClient = httpClient;
        Logger = logger;
    }

    protected HttpClient HttpClient { get; }
    protected ILogger Logger { get; }

    protected async Task<List<T>> GetListAsync<T>(string path)
    {
        try
        {
            var response = await HttpClient.GetAsync(path);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<T>>(_jsonOptions) ?? new();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to GET list from {Path}", path);
            return new List<T>();
        }
    }

    protected async Task<T?> GetAsync<T>(string path)
    {
        try
        {
            var response = await HttpClient.GetAsync(path);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(_jsonOptions);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to GET item from {Path}", path);
            return default;
        }
    }

    protected async Task<TResponse?> PostAsync<TPayload, TResponse>(string path, TPayload payload)
    {
        try
        {
            var response = await HttpClient.PostAsJsonAsync(path, payload);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to POST to {Path}", path);
            throw;
        }
    }

    protected async Task<TResponse?> PutAsync<TPayload, TResponse>(string path, TPayload payload)
    {
        try
        {
            var response = await HttpClient.PutAsJsonAsync(path, payload);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to PUT to {Path}", path);
            throw;
        }
    }

    protected async Task DeleteAsync(string path)
    {
        try
        {
            var response = await HttpClient.DeleteAsync(path);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to DELETE {Path}", path);
            throw;
        }
    }
}

// Operations/auxiliary services
public class CallCentreApiService(HttpClient httpClient, ILogger<CallCentreApiService> logger) : BaseApiService(httpClient, logger)
{
    private const string BasePath = "api/callcentre";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(short id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(short id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(short id) => DeleteAsync($"{BasePath}/{id}");
}

public class FineApiService(HttpClient httpClient, ILogger<FineApiService> logger) : BaseApiService(httpClient, logger)
{
    private const string BasePath = "api/fine";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class LogbookApiService(HttpClient httpClient, ILogger<LogbookApiService> logger) : BaseApiService(httpClient, logger)
{
    private const string BasePath = "api/logbook";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class LogsheetApiService(HttpClient httpClient, ILogger<LogsheetApiService> logger) : BaseApiService(httpClient, logger)
{
    private const string BasePath = "api/logsheet";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class MonitorApiService(HttpClient httpClient, ILogger<MonitorApiService> logger) : BaseApiService(httpClient, logger)
{
    private const string BasePath = "api/monitor";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class TrackingApiService(HttpClient httpClient, ILogger<TrackingApiService> logger) : BaseApiService(httpClient, logger)
{
    private const string BasePath = "api/tracking";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class TowingApiService(HttpClient httpClient, ILogger<TowingApiService> logger) : BaseApiService(httpClient, logger)
{
    private const string BasePath = "api/towing";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class AuctionApiService(HttpClient httpClient, ILogger<AuctionApiService> logger) : BaseApiService(httpClient, logger)
{
    private const string BasePath = "api/auction";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class ClearanceApiService(HttpClient httpClient, ILogger<ClearanceApiService> logger) : BaseApiService(httpClient, logger)
{
    private const string BasePath = "api/clearance";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class LossApiService(HttpClient httpClient, ILogger<LossApiService> logger) : BaseApiService(httpClient, logger)
{
    private const string BasePath = "api/loss";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class VehicleOrderApiService(HttpClient httpClient, ILogger<VehicleOrderApiService> logger) : BaseApiService(httpClient, logger)
{
    private const string BasePath = "api/vehicleorder";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class VehiclePhotoApiService(HttpClient httpClient, ILogger<VehiclePhotoApiService> logger) : BaseApiService(httpClient, logger)
{
    private const string BasePath = "api/vehiclephoto";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class WorkshopApiService(HttpClient httpClient, ILogger<WorkshopApiService> logger) : BaseApiService(httpClient, logger)
{
    private const string BasePath = "api/workshop";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class TaxiApiService(HttpClient httpClient, ILogger<TaxiApiService> logger) : BaseApiService(httpClient, logger)
{
    private const string BasePath = "api/taxi";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class AssetVerificationApiService(HttpClient httpClient, ILogger<AssetVerificationApiService> logger) : BaseApiService(httpClient, logger)
{
    private const string BasePath = "api/assetverification";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class VehicleAssessmentApiService(HttpClient httpClient, ILogger<VehicleAssessmentApiService> logger) : BaseApiService(httpClient, logger)
{
    private const string BasePath = "api/vehicleassessment";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class VehicleDamageApiService(HttpClient httpClient, ILogger<VehicleDamageApiService> logger) : BaseApiService(httpClient, logger)
{
    private const string BasePath = "api/vehicledamage";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class SupplierApiService(HttpClient httpClient, ILogger<SupplierApiService> logger) : BaseApiService(httpClient, logger)
{
    private const string BasePath = "api/supplier";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class BookingApiService(HttpClient httpClient, ILogger<BookingApiService> logger) : BaseApiService(httpClient, logger)
{
    private const string BasePath = "api/booking";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class LeaseContractTermsApiService(HttpClient httpClient, ILogger<LeaseContractTermsApiService> logger) : BaseApiService(httpClient, logger)
{
    private const string BasePath = "api/leasecontractterms";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class TrafficDeptApiService(HttpClient httpClient, ILogger<TrafficDeptApiService> logger) : BaseApiService(httpClient, logger)
{
    private const string BasePath = "api/trafficdept";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}
