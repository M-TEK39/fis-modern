using System.Net.Http.Json;
using System.Text.Json;

namespace FIS.Web.Services;

public class LicenseMaintenanceApiService
{
    private readonly HttpClient _httpClient;
    private readonly TokenService _tokenService;
    private readonly ILogger<LicenseMaintenanceApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public LicenseMaintenanceApiService(HttpClient httpClient, TokenService tokenService, ILogger<LicenseMaintenanceApiService> logger)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
        _logger = logger;
    }

    private void AddAuthHeader()
    {
        if (string.IsNullOrWhiteSpace(_tokenService.Token)) return;
        _httpClient.DefaultRequestHeaders.Remove("Cookie");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"FIS_Access_Token={_tokenService.Token}");
    }

    public async Task<bool> SubmitPasswordAsync(string path, string password)
    {
        try
        {
            AddAuthHeader();
            var response = await _httpClient.PostAsJsonAsync(path, new { password });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting license password to {Path}", path);
            return false;
        }
    }

    public async Task<T?> SubmitAsync<T>(string path, object payload)
    {
        try
        {
            AddAuthHeader();
            var response = await _httpClient.PostAsJsonAsync(path, payload);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(_jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting license payload to {Path}", path);
            return default;
        }
    }
}
