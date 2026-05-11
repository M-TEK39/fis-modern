using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

public class CaptureActivityApiService
{
    private readonly HttpClient _httpClient;
    private readonly TokenService _tokenService;
    private readonly ILogger<CaptureActivityApiService> _logger;

    public CaptureActivityApiService(HttpClient httpClient, TokenService tokenService, ILogger<CaptureActivityApiService> logger)
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

    public async Task<CaptureActivityReportDto> GetReportAsync(
        DateTime dateFrom,
        DateTime? dateTo = null,
        string? module = null,
        short? siteCode = null,
        int? vmfCode = null,
        int? capturedBy = null)
    {
        try
        {
            AddAuthHeader();
            var queryParts = new List<string>
            {
                $"date_from={Uri.EscapeDataString(dateFrom.ToString("yyyy-MM-dd"))}"
            };

            if (dateTo.HasValue)
            {
                queryParts.Add($"date_to={Uri.EscapeDataString(dateTo.Value.ToString("yyyy-MM-dd"))}");
            }

            if (!string.IsNullOrWhiteSpace(module) && !module.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                queryParts.Add($"module={Uri.EscapeDataString(module)}");
            }

            if (siteCode.HasValue)
            {
                queryParts.Add($"site_code={siteCode.Value}");
            }

            if (vmfCode.HasValue)
            {
                queryParts.Add($"vmf_code={vmfCode.Value}");
            }

            if (capturedBy.HasValue)
            {
                queryParts.Add($"captured_by={capturedBy.Value}");
            }

            var path = "api/report/capture-activity";
            if (queryParts.Count > 0)
            {
                path += "?" + string.Join("&", queryParts);
            }

            var result = await _httpClient.GetFromJsonAsync<CaptureActivityReportDto>(path);
            return result ?? new CaptureActivityReportDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading capture activity report.");
            return new CaptureActivityReportDto();
        }
    }
}
