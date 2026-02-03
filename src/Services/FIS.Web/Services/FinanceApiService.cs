using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Globalization;
using FIS.Web.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace FIS.Web.Services;

internal class FinanceDepartmentResponse
{
    public short DepartmentCode { get; set; }
    public string? Description { get; set; }
}

internal class FinanceSiteResponse
{
    public short SiteCode { get; set; }
    public short? DepartmentCode { get; set; }
    public string? Description { get; set; }
}

internal class FinanceProvinceResponse
{
    public string? province_code { get; set; }
    public string? province_name { get; set; }
}

internal class FinanceVehicleResponse
{
    public int vmf_code { get; set; }
    public string? registration_number { get; set; }
    public string? fleet_number { get; set; }
}

internal class FinanceLookupResponse
{
    public string? Value { get; set; }
    public string? Label { get; set; }
}

public class FinanceApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FinanceApiService> _logger;

    public FinanceApiService(HttpClient httpClient, ILogger<FinanceApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<FinanceOptionDto>> GetDepartmentsAsync()
    {
        try
        {
            var items = await _httpClient.GetFromJsonAsync<List<FinanceDepartmentResponse>>("api/department");
            return items?.Select(x => new FinanceOptionDto
            {
                Value = x.DepartmentCode.ToString(),
                Label = string.IsNullOrWhiteSpace(x.Description)
                    ? x.DepartmentCode.ToString()
                    : $"{x.DepartmentCode} - {x.Description}"
            }).ToList() ?? new List<FinanceOptionDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading finance departments");
            return new List<FinanceOptionDto>();
        }
    }

    public async Task<List<FinanceOptionDto>> GetSitesAsync(string? departmentCode = null)
    {
        try
        {
            var items = await _httpClient.GetFromJsonAsync<List<FinanceSiteResponse>>("api/site") ?? new List<FinanceSiteResponse>();
            if (!string.IsNullOrWhiteSpace(departmentCode) && short.TryParse(departmentCode, out var deptCode))
            {
                items = items.Where(x => x.DepartmentCode == deptCode).ToList();
            }

            return items.Select(x => new FinanceOptionDto
            {
                Value = x.SiteCode.ToString(),
                Label = string.IsNullOrWhiteSpace(x.Description)
                    ? x.SiteCode.ToString()
                    : $"{x.SiteCode} - {x.Description}"
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading finance sites");
            return new List<FinanceOptionDto>();
        }
    }

    public async Task<List<FinanceOptionDto>> GetProvincesAsync()
    {
        try
        {
            var items = await _httpClient.GetFromJsonAsync<List<FinanceProvinceResponse>>("api/province");
            return items?.Select(x => new FinanceOptionDto
            {
                Value = x.province_code ?? string.Empty,
                Label = string.IsNullOrWhiteSpace(x.province_name)
                    ? (x.province_code ?? string.Empty)
                    : $"{x.province_code} - {x.province_name}"
            }).Where(x => !string.IsNullOrWhiteSpace(x.Value)).ToList() ?? new List<FinanceOptionDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading finance provinces");
            return new List<FinanceOptionDto>();
        }
    }

    public async Task<List<FinanceOptionDto>> SearchVehiclesAsync(string? searchTerm)
    {
        try
        {
            var query = Uri.EscapeDataString(searchTerm ?? string.Empty);
            var items = await _httpClient.GetFromJsonAsync<List<FinanceVehicleResponse>>($"api/vehicles/search?searchTerm={query}");
            return items?.Select(x => new FinanceOptionDto
            {
                Value = x.vmf_code.ToString(),
                Label = $"{x.registration_number} ({x.fleet_number})"
            }).ToList() ?? new List<FinanceOptionDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching finance vehicles");
            return new List<FinanceOptionDto>();
        }
    }

    public Task<List<FinanceOptionDto>> GetFinancialYearsAsync()
        => GetLookupAsync("api/finance/reference/financial-years", BuildFinancialYearFallback);

    public Task<List<FinanceOptionDto>> GetBatchDatesAsync()
        => GetLookupAsync("api/finance/reference/batch-dates", BuildBatchDateFallback);

    public Task<List<FinanceOptionDto>> GetSegmentTypesAsync()
        => GetLookupAsync("api/finance/reference/segment-types");

    public Task<List<FinanceOptionDto>> GetTariffYearsAsync()
        => GetLookupAsync("api/finance/tariff-parameters/years", BuildFinancialYearFallback);

    public async Task<FinanceBatchStatusDto?> GetBatchStatusAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<FinanceBatchStatusDto>("api/finance/batch/status");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting finance batch status");
            return null;
        }
    }

    public Task<FinanceApiResult> GetActionAsync(string endpoint)
        => SendAsync(HttpMethod.Get, endpoint, null);

    public Task<FinanceApiResult> PostActionAsync(string endpoint, object payload)
        => SendAsync(HttpMethod.Post, endpoint, payload);

    public Task<FinanceApiResult> PutActionAsync(string endpoint, object payload)
        => SendAsync(HttpMethod.Put, endpoint, payload);

    public async Task<FinanceApiResult> UploadFileAsync(
        string endpoint,
        IBrowserFile file,
        Dictionary<string, string>? metadata = null)
    {
        using var content = new MultipartFormDataContent();

        var streamContent = new StreamContent(file.OpenReadStream(20 * 1024 * 1024));
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
        content.Add(streamContent, "file", file.Name);

        if (metadata != null)
        {
            foreach (var pair in metadata)
            {
                content.Add(new StringContent(pair.Value ?? string.Empty), pair.Key);
            }
        }

        try
        {
            using var response = await _httpClient.PostAsync(endpoint, content);
            var body = await response.Content.ReadAsStringAsync();

            return new FinanceApiResult
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                Endpoint = endpoint,
                Message = response.IsSuccessStatusCode
                    ? "Request completed successfully."
                    : $"Request failed with status {(int)response.StatusCode} ({response.StatusCode}).",
                ResponseBody = body
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Finance upload failed for {Endpoint}", endpoint);
            return new FinanceApiResult
            {
                Success = false,
                Endpoint = endpoint,
                Message = ex.Message
            };
        }
    }

    private async Task<List<FinanceOptionDto>> GetLookupAsync(
        string endpoint,
        Func<List<FinanceOptionDto>>? fallbackFactory = null)
    {
        try
        {
            var items = await _httpClient.GetFromJsonAsync<List<FinanceLookupResponse>>(endpoint);
            return items?.Select(x => new FinanceOptionDto
            {
                Value = x.Value ?? string.Empty,
                Label = x.Label ?? x.Value ?? string.Empty
            }).Where(x => !string.IsNullOrWhiteSpace(x.Value)).ToList() ?? new List<FinanceOptionDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lookup failed for {Endpoint}", endpoint);
            return fallbackFactory?.Invoke() ?? new List<FinanceOptionDto>();
        }
    }

    private static List<FinanceOptionDto> BuildFinancialYearFallback()
    {
        var currentYear = DateTime.UtcNow.Year;
        var options = new List<FinanceOptionDto>();

        for (var year = currentYear - 5; year <= currentYear + 1; year++)
        {
            options.Add(new FinanceOptionDto
            {
                Value = year.ToString(CultureInfo.InvariantCulture),
                Label = year.ToString(CultureInfo.InvariantCulture)
            });
        }

        return options;
    }

    private static List<FinanceOptionDto> BuildBatchDateFallback()
    {
        var currentMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var options = new List<FinanceOptionDto>();

        for (var offset = 0; offset < 12; offset++)
        {
            var month = currentMonth.AddMonths(-offset);
            options.Add(new FinanceOptionDto
            {
                Value = month.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                Label = month.ToString("MMM yyyy", CultureInfo.InvariantCulture)
            });
        }

        return options;
    }

    private async Task<FinanceApiResult> SendAsync(HttpMethod method, string endpoint, object? payload)
    {
        try
        {
            using var request = new HttpRequestMessage(method, endpoint);
            if (payload != null)
            {
                var json = JsonSerializer.Serialize(payload);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            using var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            return new FinanceApiResult
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                Endpoint = endpoint,
                Message = BuildMessage(response.StatusCode),
                ResponseBody = body
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Finance request failed for {Endpoint}", endpoint);
            return new FinanceApiResult
            {
                Success = false,
                Endpoint = endpoint,
                Message = ex.Message
            };
        }
    }

    private static string BuildMessage(HttpStatusCode statusCode)
    {
        if ((int)statusCode is >= 200 and < 300)
        {
            return "Request completed successfully.";
        }

        return $"Request failed with status {(int)statusCode} ({statusCode}).";
    }
}
