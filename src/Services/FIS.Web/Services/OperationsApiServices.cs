using System.Net.Http.Json;
using System.Text.Json;
using System.Net.Http;
using System.Net;
using FIS.Web.Models;
using Microsoft.Extensions.Logging;

namespace FIS.Web.Services;

/// <summary>
/// Lightweight base class for simple CRUD-style API calls with case-insensitive JSON handling.
/// Uses session access token from TokenService.
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
    /// Adds session access token as cookie header for API auth.
    /// </summary>
    private async Task AddAuthorizationHeaderAsync()
    {
        var token = await TokenService.GetTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        HttpClient.DefaultRequestHeaders.Remove("Cookie");
        HttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"FIS_Access_Token={token}");
    }

    private async Task<HttpResponseMessage> SendWithAuthRetryAsync(Func<Task<HttpResponseMessage>> send)
    {
        await AddAuthorizationHeaderAsync();
        var response = await send();
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        if (!await TokenService.RefreshAccessTokenAsync())
        {
            return response;
        }

        response.Dispose();
        await AddAuthorizationHeaderAsync();
        return await send();
    }

    protected async Task<List<T>> GetListAsync<T>(string path)
    {
        try
        {
            var response = await SendWithAuthRetryAsync(() => HttpClient.GetAsync(path));
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
            var response = await SendWithAuthRetryAsync(() => HttpClient.GetAsync(path));
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
            var response = await SendWithAuthRetryAsync(() => HttpClient.PostAsJsonAsync(path, payload));
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
            var response = await SendWithAuthRetryAsync(() => HttpClient.PutAsJsonAsync(path, payload));
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
            var response = await SendWithAuthRetryAsync(() => HttpClient.DeleteAsync(path));
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
    public async Task<CallCentreNotificationsDto> GetNotificationsAsync()
        => await GetAsync<CallCentreNotificationsDto>($"{BasePath}/notifications") ?? new CallCentreNotificationsDto();
    public Task<T?> GetByIdAsync<T>(short id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(short id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(short id) => DeleteAsync($"{BasePath}/{id}");
}

public class NotifyListApiService(HttpClient httpClient, TokenService tokenService, ILogger<NotifyListApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/notifylist";

    public Task<List<T>> GetAllAsync<T>(string? search = null)
    {
        var path = string.IsNullOrWhiteSpace(search)
            ? BasePath
            : $"{BasePath}?search={Uri.EscapeDataString(search.Trim())}";
        return GetListAsync<T>(path);
    }

    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<T?> CreateAsync<TPayload, T>(TPayload payload) => PostAsync<TPayload, T>(BasePath, payload);
    public Task<T?> UpdateAsync<TPayload, T>(int id, TPayload payload) => PutAsync<TPayload, T>($"{BasePath}/{id}", payload);
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

public class AuctionApiService(HttpClient httpClient, TokenService tokenService, ILogger<AuctionApiService> logger) : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/auction";

    public Task<List<T>> GetAllAsync<T>() => GetListAsync<T>(BasePath);
    public Task<T?> GetByIdAsync<T>(int id) => GetAsync<T>($"{BasePath}/{id}");
    public Task<List<T>> GetByVehicleAsync<T>(int vmfCode) => GetListAsync<T>($"{BasePath}/vehicle/{vmfCode}");
    public Task<T?> CreateAsync<T>(T payload) => PostAsync<T, T>(BasePath, payload);
    public Task<T?> UpdateAsync<T>(int id, T payload) => PutAsync<T, T>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
    public async Task<AuctionReportDto> GetOneVehicleReportAsync(AuctionOneVehicleReportRequestDto request)
        => await PostAsync<AuctionOneVehicleReportRequestDto, AuctionReportDto>($"{BasePath}/reports/one-vehicle", request)
           ?? new AuctionReportDto();
    public async Task<AuctionReportDto> GetAllVehiclesReportAsync(AuctionAllVehiclesReportRequestDto request)
        => await PostAsync<AuctionAllVehiclesReportRequestDto, AuctionReportDto>($"{BasePath}/reports/all-vehicles", request)
           ?? new AuctionReportDto();
    public async Task<AuctionReportDto> GetSaleToNameReportAsync(AuctionSaleToNameReportRequestDto request)
        => await PostAsync<AuctionSaleToNameReportRequestDto, AuctionReportDto>($"{BasePath}/reports/sale-to-name", request)
           ?? new AuctionReportDto();
    public async Task<AuctionReportDto> GetAuctionGgReportAsync(AuctionGgReportRequestDto request)
        => await PostAsync<AuctionGgReportRequestDto, AuctionReportDto>($"{BasePath}/reports/auction-gg", request)
           ?? new AuctionReportDto();
    public async Task<AuctionReportDto> GetAuctionLotReportAsync(AuctionLotReportRequestDto request)
        => await PostAsync<AuctionLotReportRequestDto, AuctionReportDto>($"{BasePath}/reports/auction-lot", request)
           ?? new AuctionReportDto();
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
