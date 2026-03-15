using System.Net.Http.Json;
using System.Text.Json;
using System.Net.Http;
using FIS.Web.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

namespace FIS.Web.Services;

/// <summary>
/// Lightweight base class for simple CRUD-style API calls with case-insensitive JSON handling.
/// Automatically adds JWT token to requests.
/// </summary>
public abstract class BaseApiService
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    protected BaseApiService(HttpClient httpClient, TokenService tokenService, ILogger logger)
    {
        HttpClient = httpClient;
        TokenService = tokenService;
        Logger = logger;
    }

    protected HttpClient HttpClient { get; }
    protected TokenService TokenService { get; }
    protected ILogger Logger { get; }

    /// <summary>
    /// Adds JWT token to request if available
    /// </summary>
    private void AddAuthorizationHeader()
    {
        if (TokenService.IsTokenValid && !string.IsNullOrEmpty(TokenService.Token))
        {
            HttpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", TokenService.Token);
        }
    }

    protected async Task<List<T>> GetListAsync<T>(string path)
    {
        try
        {
            AddAuthorizationHeader();
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
            AddAuthorizationHeader();
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
            AddAuthorizationHeader();
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
            AddAuthorizationHeader();
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
public class CallCentreApiService(HttpClient httpClient, TokenService tokenService, ILogger<CallCentreApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/callcentre";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(short id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(short id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(short id) => DeleteAsync($"{BasePath}/{id}");
}

public class FineApiService(HttpClient httpClient, TokenService tokenService, ILogger<FineApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/fine";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class RegistrationApiService(HttpClient httpClient, TokenService tokenService, ILogger<RegistrationApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/registration";

    public async Task<RegistrationSearchResponseDto> SearchAsync(string query)
        => await GetAsync<RegistrationSearchResponseDto>($"{BasePath}/search?q={Uri.EscapeDataString(query)}")
            ?? new RegistrationSearchResponseDto();

    public Task<RegistrationHistoryResponseDto?> GetVehicleHistoryAsync(int vmfCode)
        => GetAsync<RegistrationHistoryResponseDto>($"{BasePath}/vehicle/{vmfCode}");
}

public class LogbookApiService(HttpClient httpClient, TokenService tokenService, ILogger<LogbookApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/logbook";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class LogsheetApiService(HttpClient httpClient, TokenService tokenService, ILogger<LogsheetApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/logsheet";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class MonitorApiService(HttpClient httpClient, TokenService tokenService, ILogger<MonitorApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/monitor";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class TrackingApiService(HttpClient httpClient, TokenService tokenService, ILogger<TrackingApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/tracking";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class TowingApiService(HttpClient httpClient, TokenService tokenService, ILogger<TowingApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/towing";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class AuctionApiService(HttpClient httpClient, TokenService tokenService, ILogger<AuctionApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/auction";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<List<T>> GetByVehicleAsync<T>(int vmfCode) => GetListAsync<T>($"{BasePath}/vehicle/{vmfCode}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class LossApiService(HttpClient httpClient, TokenService tokenService, ILogger<LossApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/loss";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<List<T>> GetByVehicleAsync<T>(string vehicleIdentifier) =>
        GetListAsync<T>($"{BasePath}/vehicle/{Uri.EscapeDataString(vehicleIdentifier)}");
    public Task<List<T>> GetByVmfCodeAsync<T>(int vmfCode) => GetListAsync<T>($"{BasePath}/vmf/{vmfCode}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class LossReportApiService(HttpClient httpClient, TokenService tokenService, ILogger<LossReportApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/report/losses";

    public async Task<List<T>> GetVehicleAsync<T>(object payload) =>
        await PostAsync<object, List<T>>($"{BasePath}/vehicle", payload) ?? new List<T>();
    public async Task<List<T>> GetAllAsync<T>(object payload) =>
        await PostAsync<object, List<T>>($"{BasePath}/all", payload) ?? new List<T>();
    public async Task<List<T>> GetNoReportAsync<T>(object payload) =>
        await PostAsync<object, List<T>>($"{BasePath}/no-report", payload) ?? new List<T>();
    public async Task<List<T>> GetWithReportAsync<T>(object payload) =>
        await PostAsync<object, List<T>>($"{BasePath}/with-report", payload) ?? new List<T>();
    public async Task<List<T>> GetDeptPeriodAsync<T>(object payload) =>
        await PostAsync<object, List<T>>($"{BasePath}/dept-period", payload) ?? new List<T>();
}

public class LicenseReportApiService(HttpClient httpClient, TokenService tokenService, ILogger<LicenseReportApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/report/licences";

    public async Task<List<T>> GetByIdentifierAsync<T>(string mode, object payload) =>
        await PostAsync<object, List<T>>($"{BasePath}/{mode}", payload) ?? new List<T>();
    public async Task<List<T>> GetAllAsync<T>(object payload) =>
        await PostAsync<object, List<T>>($"{BasePath}/all", payload) ?? new List<T>();
    public async Task<List<T>> GetDeptPeriodAsync<T>(object payload) =>
        await PostAsync<object, List<T>>($"{BasePath}/dept-period", payload) ?? new List<T>();
    public async Task<List<T>> GetExpireDateAsync<T>(object payload) =>
        await PostAsync<object, List<T>>($"{BasePath}/expire-date", payload) ?? new List<T>();
    public async Task<List<T>> GetMonthFeesAsync<T>(object payload) =>
        await PostAsync<object, List<T>>($"{BasePath}/month-fees", payload) ?? new List<T>();
    public async Task<List<T>> GetOldExpireAsync<T>(object payload) =>
        await PostAsync<object, List<T>>($"{BasePath}/old-expire", payload) ?? new List<T>();
    public async Task<List<T>> GetSapAsync<T>(object payload) =>
        await PostAsync<object, List<T>>($"{BasePath}/sap", payload) ?? new List<T>();
    public async Task<List<T>> GetCofAsync<T>(object payload) =>
        await PostAsync<object, List<T>>($"{BasePath}/cof", payload) ?? new List<T>();
    public async Task<List<T>> GetModelFeesAsync<T>(object payload) =>
        await PostAsync<object, List<T>>($"{BasePath}/model-fees", payload) ?? new List<T>();
    public async Task<List<T>> GetGgModelFeesAsync<T>(object payload) =>
        await PostAsync<object, List<T>>($"{BasePath}/gg-model-fees", payload) ?? new List<T>();
    public async Task<List<T>> GetWorkgroupAsync<T>(object payload) =>
        await PostAsync<object, List<T>>($"{BasePath}/workgroup", payload) ?? new List<T>();
    public async Task<List<T>> GetWorkgroupLatestAsync<T>(object payload) =>
        await PostAsync<object, List<T>>($"{BasePath}/workgroup-latest", payload) ?? new List<T>();
    public async Task<List<T>> GetGgmtReceivedAsync<T>(object payload) =>
        await PostAsync<object, List<T>>($"{BasePath}/ggmt-received", payload) ?? new List<T>();
    public async Task<List<T>> GetSiteAsync<T>(object payload) =>
        await PostAsync<object, List<T>>($"{BasePath}/site", payload) ?? new List<T>();
}

public class VehicleOrderApiService(HttpClient httpClient, TokenService tokenService, ILogger<VehicleOrderApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/vehicleorder";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class VehiclePhotoApiService(HttpClient httpClient, TokenService tokenService, ILogger<VehiclePhotoApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/vehiclephoto";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<List<T>> GetByVehicleAsync<T>(int vmfCode) => GetListAsync<T>($"{BasePath}/vehicle/{vmfCode}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
    public string GetFileUrl(int id) => new Uri(HttpClient.BaseAddress!, $"{BasePath}/{id}/file").ToString();

    public async Task<T?> UploadAsync<T>(
        int vmfCode,
        Stream fileStream,
        string fileName,
        string contentType,
        string? description,
        int? orientation)
    {
        try
        {
            if (TokenService.IsTokenValid && !string.IsNullOrEmpty(TokenService.Token))
            {
                HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenService.Token);
            }

            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(vmfCode.ToString()), "vmfCode");
            if (!string.IsNullOrWhiteSpace(description))
            {
                form.Add(new StringContent(description), "description");
            }
            if (orientation.HasValue && orientation.Value > 0)
            {
                form.Add(new StringContent(orientation.Value.ToString()), "orientation");
            }

            using var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType);
            form.Add(streamContent, "file", string.IsNullOrWhiteSpace(fileName) ? "image.jpg" : fileName);

            var response = await HttpClient.PostAsync($"{BasePath}/upload", form);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to upload vehicle photo for vmfCode {VmfCode}", vmfCode);
            throw;
        }
    }
}

public class WorkshopApiService(HttpClient httpClient, TokenService tokenService, ILogger<WorkshopApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/workshop";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class TaxiApiService(HttpClient httpClient, TokenService tokenService, ILogger<TaxiApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/taxi";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> GetByRequisitionAsync<T>(string rekNum) => GetAsync<T>($"{BasePath}/lookup/{Uri.EscapeDataString(rekNum)}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class TaxiLogApiService(HttpClient httpClient, TokenService tokenService, ILogger<TaxiLogApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/taxilog";

    public Task<T?> GetReferencesAsync<T>() => GetAsync<T>($"{BasePath}/references");
    public Task<T?> LookupAsync<T>(string rekNum, string mode) => GetAsync<T>($"{BasePath}/lookup/{Uri.EscapeDataString(rekNum)}?mode={Uri.EscapeDataString(mode)}");
    public Task<T?> CreateAsync<TPayload, T>(TPayload payload) => PostAsync<TPayload, T>(BasePath, payload);
    public Task<T?> UpdateAsync<TPayload, T>(int logId, TPayload payload) => PutAsync<TPayload, T>($"{BasePath}/{logId}", payload);
}

public class AssetVerificationApiService(HttpClient httpClient, TokenService tokenService, ILogger<AssetVerificationApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/assetverification";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class VehicleAssessmentApiService(HttpClient httpClient, TokenService tokenService, ILogger<VehicleAssessmentApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/vehicleassessment";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class VehicleDamageApiService(HttpClient httpClient, TokenService tokenService, ILogger<VehicleDamageApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/vehicledamage";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class SupplierApiService(HttpClient httpClient, TokenService tokenService, ILogger<SupplierApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/supplier";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class BookingApiService(HttpClient httpClient, TokenService tokenService, ILogger<BookingApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/booking";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class LeaseContractTermsApiService(HttpClient httpClient, TokenService tokenService, ILogger<LeaseContractTermsApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/leasecontractterms";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}

public class TrafficDeptApiService(HttpClient httpClient, TokenService tokenService, ILogger<TrafficDeptApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/trafficdept";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}
