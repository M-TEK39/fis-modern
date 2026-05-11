using System.Net.Http.Json;
using FIS.Web.Models;
using System.Text.Json.Serialization;

namespace FIS.Web.Services;

public class TariffApiService
{
    private readonly HttpClient _httpClient;
    private readonly TokenService _tokenService;
    private readonly ILogger<TariffApiService> _logger;

    public TariffApiService(HttpClient httpClient, TokenService tokenService, ILogger<TariffApiService> logger)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
        _logger = logger;
    }

    private void AddAuthHeader()
    {
        if (string.IsNullOrWhiteSpace(_tokenService.Token))
        {
            return;
        }

        _httpClient.DefaultRequestHeaders.Remove("Cookie");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"FIS_Access_Token={_tokenService.Token}");
    }

    public async Task<TariffResultDto?> GetContractTariffAsync(int contractCode, DateTime? checkDate = null, string tariffType = "Fixed")
    {
        try
        {
            AddAuthHeader();
            var dateParam = checkDate?.ToString("o");
            var url = $"api/Tariff/contract/{contractCode}";
            if (dateParam != null)
            {
                url += $"?checkDate={Uri.EscapeDataString(dateParam)}&tariffType={Uri.EscapeDataString(tariffType)}";
            }

            var result = await _httpClient.GetFromJsonAsync<TariffResultDto>(url);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching contract tariff {ContractCode}", contractCode);
            return null;
        }
    }

    public async Task<TariffResultDto?> CalculateAsync(TariffRequestDto request)
    {
        try
        {
            AddAuthHeader();
            var response = await _httpClient.PostAsJsonAsync("api/Tariff/calculate", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TariffResultDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating tariff");
            return null;
        }
    }

    public async Task<VehicleTariffPreviewResponseDto?> GetPreviewForVehicleAsync(int vmfCode)
    {
        try
        {
            AddAuthHeader();
            return await _httpClient.GetFromJsonAsync<VehicleTariffPreviewResponseDto>($"api/Tariff/preview-for-vehicle/{vmfCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tariff preview for vehicle {VmfCode}", vmfCode);
            return null;
        }
    }

    public async Task<TariffManagementListResult> GetTariffManagementAsync(TariffManagementFilter filter)
    {
        try
        {
            AddAuthHeader();
            var queryParts = new List<string>();
            if (filter.class_code.HasValue)
            {
                queryParts.Add($"class_code={filter.class_code.Value}");
            }
            if (filter.year_manufactured.HasValue)
            {
                queryParts.Add($"year_manufactured={filter.year_manufactured.Value}");
            }
            if (filter.tariff_approval_status.HasValue)
            {
                queryParts.Add($"tariff_approval_status={filter.tariff_approval_status.Value}");
            }
            if (filter.effective_on.HasValue)
            {
                queryParts.Add($"effective_on={Uri.EscapeDataString(filter.effective_on.Value.ToString("yyyy-MM-dd"))}");
            }

            var url = "api/tariff-management";
            if (queryParts.Count > 0)
            {
                url += "?" + string.Join("&", queryParts);
            }

            var response = await _httpClient.GetFromJsonAsync<TariffManagementListResponseDto>(url);
            return new TariffManagementListResult
            {
                total_count = response?.total_count ?? 0,
                tariffs = response?.tariffs ?? new List<TariffManagementItemDto>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tariff management report");
            return new TariffManagementListResult();
        }
    }
}

public record TariffRequestDto(int ContractCode);
public record TariffResultDto(decimal? Amount, string? Status, string? Notes);

public class VehicleTariffPreviewResponseDto
{
    [JsonPropertyName("vmf_code")]
    public int? VmfCode { get; set; }

    [JsonPropertyName("fleet_number")]
    public string? FleetNumber { get; set; }

    [JsonPropertyName("registration")]
    public string? Registration { get; set; }

    [JsonPropertyName("year_manufactured")]
    public int? YearManufactured { get; set; }

    [JsonPropertyName("model_code")]
    public short? ModelCode { get; set; }

    [JsonPropertyName("class_code")]
    public short? ClassCode { get; set; }

    [JsonPropertyName("tariff")]
    public VehicleTariffPreviewDto? Tariff { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}

public class VehicleTariffPreviewDto
{
    [JsonPropertyName("tariff_code")]
    public int? TariffCode { get; set; }

    [JsonPropertyName("class_code")]
    public short? ClassCode { get; set; }

    [JsonPropertyName("year_manufactured")]
    public short? YearManufactured { get; set; }

    [JsonPropertyName("monthly_fixed_amount")]
    public decimal? MonthlyFixedAmount { get; set; }

    [JsonPropertyName("monthly_odo_amount")]
    public decimal? MonthlyOdoAmount { get; set; }

    [JsonPropertyName("daily_fixed_amount")]
    public decimal? DailyFixedAmount { get; set; }

    [JsonPropertyName("hourly_fixed_amount")]
    public decimal? HourlyFixedAmount { get; set; }

    [JsonPropertyName("fuel_kilo_tariff")]
    public decimal? FuelKiloTariff { get; set; }

    [JsonPropertyName("effective_start_date")]
    public string? EffectiveStartDate { get; set; }

    [JsonPropertyName("effective_end_date")]
    public string? EffectiveEndDate { get; set; }
}

public class TariffManagementFilter
{
    public short? class_code { get; set; }
    public short? year_manufactured { get; set; }
    public short? tariff_approval_status { get; set; }
    public DateTime? effective_on { get; set; }
}

public class TariffManagementListResult
{
    public int total_count { get; set; }
    public List<TariffManagementItemDto> tariffs { get; set; } = new();
}

public class TariffManagementItemDto
{
    [JsonPropertyName("tariff_code")]
    public int tariff_code { get; set; }

    [JsonPropertyName("class_code")]
    public short class_code { get; set; }

    [JsonPropertyName("year_manufactured")]
    public short year_manufactured { get; set; }

    [JsonPropertyName("monthly_fixed_amount")]
    public decimal? monthly_fixed_amount { get; set; }

    [JsonPropertyName("monthly_odo_amount")]
    public decimal? monthly_odo_amount { get; set; }

    [JsonPropertyName("daily_fixed_amount")]
    public decimal? daily_fixed_amount { get; set; }

    [JsonPropertyName("hourly_fixed_amount")]
    public decimal? hourly_fixed_amount { get; set; }

    [JsonPropertyName("fuel_kilo_tariff")]
    public decimal? fuel_kilo_tariff { get; set; }

    [JsonPropertyName("effective_start_date")]
    public DateTime? effective_start_date { get; set; }

    [JsonPropertyName("effective_end_date")]
    public DateTime? effective_end_date { get; set; }

    [JsonPropertyName("tariff_approval_status")]
    public short tariff_approval_status { get; set; }

    [JsonPropertyName("approval_status_text")]
    public string? approval_status_text { get; set; }
}

file sealed class TariffManagementListResponseDto
{
    [JsonPropertyName("total_count")]
    public int total_count { get; set; }

    [JsonPropertyName("tariffs")]
    public List<TariffManagementItemDto>? tariffs { get; set; }
}
