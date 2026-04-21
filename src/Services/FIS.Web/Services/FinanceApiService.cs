using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
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

    public async Task<List<FinanceOptionDto>> GetFinanceReportPostingMonthsAsync(string filterBy = "Department")
    {
        try
        {
            var encodedFilterBy = Uri.EscapeDataString(filterBy);
            using var response = await _httpClient.GetAsync($"api/finance/reports/posting-months?filterBy={encodedFilterBy}");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Finance posting-month lookup returned status {StatusCode}; using batch-date fallback.",
                    (int)response.StatusCode);
                return await GetBatchDatesAsync();
            }

            var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

            if (payload.ValueKind == JsonValueKind.Object
                && payload.TryGetProperty("months", out var monthsElement)
                && monthsElement.ValueKind == JsonValueKind.Array)
            {
                var options = ParseLookupOptions(monthsElement);
                if (options.Count > 0)
                {
                    return options;
                }
            }

            return await GetBatchDatesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Finance posting-month lookup failed; using batch-date fallback.");
            return await GetBatchDatesAsync();
        }
    }

    public Task<List<FinanceOptionDto>> GetSegmentTypesAsync()
        => GetLookupAsync("api/finance/reference/segment-types");

    public Task<List<FinanceOptionDto>> GetTariffYearsAsync()
        => GetLookupAsync("api/finance/tariff-parameters/years", BuildFinancialYearFallback);

    public async Task<FinanceTariffParametersDto?> GetTariffParametersAsync(string year)
    {
        try
        {
            var payload = await _httpClient.GetFromJsonAsync<JsonElement>($"api/finance/tariff-parameters/{Uri.EscapeDataString(year)}");
            return ParseTariffParameters(payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading tariff parameters for year {Year}", year);
            return null;
        }
    }

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

    public Task<FinanceApiResult> DeleteActionAsync(string endpoint)
        => SendAsync(HttpMethod.Delete, endpoint, null);

    public async Task<FinanceFileResult> GetFileAsync(string endpoint, string defaultFilename)
    {
        try
        {
            using var response = await _httpClient.GetAsync(endpoint);
            var fileBytes = await response.Content.ReadAsByteArrayAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new FinanceFileResult
                {
                    Success = false,
                    Message = $"Request failed with status {(int)response.StatusCode} ({response.StatusCode})."
                };
            }

            return new FinanceFileResult
            {
                Success = true,
                Message = "File generated successfully.",
                Content = fileBytes,
                ContentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
                Filename = ResolveFilename(response.Content.Headers.ContentDisposition, defaultFilename)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Finance file download failed.");
            return new FinanceFileResult
            {
                Success = false,
                Message = SanitizeUserMessage(ex.Message) ?? "Request failed."
            };
        }
    }

    public async Task<FinanceFileResult> PostFileAsync(string endpoint, object payload, string defaultFilename)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            using var response = await _httpClient.SendAsync(request);
            var fileBytes = await response.Content.ReadAsByteArrayAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new FinanceFileResult
                {
                    Success = false,
                    Message = $"Request failed with status {(int)response.StatusCode} ({response.StatusCode})."
                };
            }

            return new FinanceFileResult
            {
                Success = true,
                Message = "File generated successfully.",
                Content = fileBytes,
                ContentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
                Filename = ResolveFilename(response.Content.Headers.ContentDisposition, defaultFilename)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Finance file request failed.");
            return new FinanceFileResult
            {
                Success = false,
                Message = SanitizeUserMessage(ex.Message) ?? "Request failed."
            };
        }
    }

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
            _logger.LogError(ex, "Finance upload failed.");
            return new FinanceApiResult
            {
                Success = false,
                Endpoint = endpoint,
                Message = SanitizeUserMessage(ex.Message) ?? "Request failed."
            };
        }
    }

    private async Task<List<FinanceOptionDto>> GetLookupAsync(
        string endpoint,
        Func<List<FinanceOptionDto>>? fallbackFactory = null)
    {
        try
        {
            var payload = await _httpClient.GetFromJsonAsync<JsonElement>(endpoint);
            var options = ParseLookupOptions(payload);

            if (options.Count > 0)
            {
                return options;
            }

            return fallbackFactory?.Invoke() ?? new List<FinanceOptionDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Finance lookup request failed.");
            return fallbackFactory?.Invoke() ?? new List<FinanceOptionDto>();
        }
    }

    private static List<FinanceOptionDto> ParseLookupOptions(JsonElement payload)
    {
        var options = new List<FinanceOptionDto>();

        if (payload.ValueKind != JsonValueKind.Array)
        {
            return options;
        }

        foreach (var item in payload.EnumerateArray())
        {
            switch (item.ValueKind)
            {
                case JsonValueKind.String:
                {
                    var text = item.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        options.Add(new FinanceOptionDto { Value = text, Label = text });
                    }
                    break;
                }
                case JsonValueKind.Number:
                {
                    var raw = item.GetRawText();
                    options.Add(new FinanceOptionDto { Value = raw, Label = raw });
                    break;
                }
                case JsonValueKind.Object:
                {
                    var value = GetCaseInsensitiveProperty(item, "value")
                        ?? GetCaseInsensitiveProperty(item, "code")
                        ?? GetCaseInsensitiveProperty(item, "id");

                    var label = GetCaseInsensitiveProperty(item, "label")
                        ?? GetCaseInsensitiveProperty(item, "name")
                        ?? GetCaseInsensitiveProperty(item, "description")
                        ?? value;

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        options.Add(new FinanceOptionDto
                        {
                            Value = value,
                            Label = string.IsNullOrWhiteSpace(label) ? value : label
                        });
                    }
                    break;
                }
            }
        }

        return options;
    }

    private static string? GetCaseInsensitiveProperty(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        }

        return null;
    }

    private static FinanceTariffParametersDto ParseTariffParameters(JsonElement payload)
    {
        var model = new FinanceTariffParametersDto
        {
            Year = ToInt(GetCaseInsensitiveProperty(payload, "year")),
            IsApproved = ToBool(GetCaseInsensitiveProperty(payload, "is_approved")),
            ApprovedBy = GetCaseInsensitiveProperty(payload, "approved_by") ?? string.Empty,
            EffectiveDate = GetCaseInsensitiveProperty(payload, "effective_date")
        };

        if (TryGetArrayProperty(payload, "parameters", out var parametersArray))
        {
            foreach (var item in parametersArray.EnumerateArray())
            {
                model.Parameters.Add(new FinanceTariffParameterValueDto
                {
                    ParameterName = GetCaseInsensitiveProperty(item, "parameterName")
                        ?? GetCaseInsensitiveProperty(item, "parameter_name")
                        ?? string.Empty,
                    Value = ToDecimalNullable(GetCaseInsensitiveProperty(item, "value")),
                    Unit = GetCaseInsensitiveProperty(item, "unit") ?? string.Empty
                });
            }
        }

        if (TryGetArrayProperty(payload, "fixedTariffs", out var fixedTariffsArray))
        {
            model.FixedTariffs = ParseTariffRates(fixedTariffsArray);
        }

        if (TryGetArrayProperty(payload, "kiloTariffs", out var kiloTariffsArray))
        {
            model.KiloTariffs = ParseTariffRates(kiloTariffsArray);
        }

        if (TryGetArrayProperty(payload, "maintenanceValues", out var maintenanceArray))
        {
            foreach (var item in maintenanceArray.EnumerateArray())
            {
                model.MaintenanceValues.Add(new FinanceMaintenanceValueDto
                {
                    ClassCode = ToShortNullable(GetCaseInsensitiveProperty(item, "classCode")
                        ?? GetCaseInsensitiveProperty(item, "class_code")),
                    ClassDescription = GetCaseInsensitiveProperty(item, "classDescription")
                        ?? GetCaseInsensitiveProperty(item, "class_description")
                        ?? string.Empty,
                    MonthsAge = ToIntNullable(GetCaseInsensitiveProperty(item, "monthsAge")
                        ?? GetCaseInsensitiveProperty(item, "months_age")),
                    KilometerAge = ToIntNullable(GetCaseInsensitiveProperty(item, "kilometerAge")
                        ?? GetCaseInsensitiveProperty(item, "kilometer_age")),
                    Amount = ToDecimalNullable(GetCaseInsensitiveProperty(item, "amount")),
                    RandPerKilometer = ToDecimalNullable(GetCaseInsensitiveProperty(item, "randPerKilometer")
                        ?? GetCaseInsensitiveProperty(item, "rand_per_kilometer"))
                });
            }
        }

        return model;
    }

    private static List<FinanceTariffRateDto> ParseTariffRates(JsonElement array)
    {
        var rows = new List<FinanceTariffRateDto>();
        foreach (var item in array.EnumerateArray())
        {
            rows.Add(new FinanceTariffRateDto
            {
                ClassCode = ToShortNullable(GetCaseInsensitiveProperty(item, "classCode")
                    ?? GetCaseInsensitiveProperty(item, "class_code")),
                ClassDescription = GetCaseInsensitiveProperty(item, "classDescription")
                    ?? GetCaseInsensitiveProperty(item, "class_description")
                    ?? string.Empty,
                Amount = ToDecimalNullable(GetCaseInsensitiveProperty(item, "amount")),
                Unit = GetCaseInsensitiveProperty(item, "unit") ?? string.Empty,
                EffectiveDate = GetCaseInsensitiveProperty(item, "effectiveDate")
                    ?? GetCaseInsensitiveProperty(item, "effective_date")
            });
        }

        return rows;
    }

    private static bool TryGetArrayProperty(JsonElement element, string propertyName, out JsonElement array)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                array = property.Value;
                return true;
            }
        }

        array = default;
        return false;
    }

    private static int ToInt(string? value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    private static int? ToIntNullable(string? value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static short? ToShortNullable(string? value)
        => short.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static decimal? ToDecimalNullable(string? value)
        => decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static bool ToBool(string? value)
        => bool.TryParse(value, out var parsed) && parsed;

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
            _logger.LogError(ex, "Finance request failed.");
            return new FinanceApiResult
            {
                Success = false,
                Endpoint = endpoint,
                Message = SanitizeUserMessage(ex.Message) ?? "Request failed."
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

    private static string? SanitizeUserMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var trimmed = message.Trim();
        var endpointIndex = trimmed.IndexOf("Endpoint:", StringComparison.OrdinalIgnoreCase);
        if (endpointIndex >= 0)
        {
            trimmed = trimmed[..endpointIndex].Trim();
        }

        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string ResolveFilename(ContentDispositionHeaderValue? disposition, string fallback)
    {
        var candidate = disposition?.FileNameStar ?? disposition?.FileName;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return fallback;
        }

        return candidate.Trim('"');
    }
}

public class FinanceFileResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/octet-stream";
    public string Filename { get; set; } = "download.bin";
}
