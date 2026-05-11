using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

public class ReportApiService
{
    private readonly HttpClient _httpClient;
    private readonly TokenService _tokenService;
    private readonly ILogger<ReportApiService> _logger;

    public ReportApiService(HttpClient httpClient, TokenService tokenService, ILogger<ReportApiService> logger)
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

    public async Task<NewInServiceReportDto> GetNewInServiceReportAsync(
        string? search = null,
        byte? vs_code = null,
        short? type_code = null,
        short? location_code = null,
        short? make_code = null,
        short? model_code = null,
        short? vehicle_status_code = null)
    {
        try
        {
            AddAuthHeader();
            var queryParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(search))
            {
                queryParts.Add($"search={Uri.EscapeDataString(search)}");
            }

            if (vs_code.HasValue) queryParts.Add($"vs_code={vs_code.Value}");
            if (type_code.HasValue) queryParts.Add($"type_code={type_code.Value}");
            if (location_code.HasValue) queryParts.Add($"location_code={location_code.Value}");
            if (make_code.HasValue) queryParts.Add($"make_code={make_code.Value}");
            if (model_code.HasValue) queryParts.Add($"model_code={model_code.Value}");
            if (vehicle_status_code.HasValue) queryParts.Add($"vehicle_status_code={vehicle_status_code.Value}");

            var path = "api/report/new-in-service";
            if (queryParts.Count > 0)
            {
                path += "?" + string.Join("&", queryParts);
            }

            var result = await _httpClient.GetFromJsonAsync<NewInServiceReportDto>(path);
            return result ?? new NewInServiceReportDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading new/in-service report.");
            return new NewInServiceReportDto();
        }
    }

    public async Task<RegistrationCertificatesReportDto> GetRegistrationCertificatesAsync(
        string? mode = null,
        string? search = null,
        int? vmfCode = null,
        int? departmentCode = null)
    {
        try
        {
            AddAuthHeader();
            var queryParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(mode)) queryParts.Add($"mode={Uri.EscapeDataString(mode)}");
            if (!string.IsNullOrWhiteSpace(search)) queryParts.Add($"search={Uri.EscapeDataString(search)}");
            if (vmfCode.HasValue) queryParts.Add($"vmfCode={vmfCode.Value}");
            if (departmentCode.HasValue) queryParts.Add($"departmentCode={departmentCode.Value}");

            var path = "api/report/registration-certificates";
            if (queryParts.Count > 0)
            {
                path += "?" + string.Join("&", queryParts);
            }

            var result = await _httpClient.GetFromJsonAsync<RegistrationCertificatesReportDto>(path);
            return result ?? new RegistrationCertificatesReportDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading registration certificates report.");
            return new RegistrationCertificatesReportDto();
        }
    }

    public async Task<ReportAuditTrailDto> GetReportAuditTrailAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? userId = null)
    {
        try
        {
            AddAuthHeader();
            var queryParts = new List<string>();
            if (startDate.HasValue) queryParts.Add($"startDate={Uri.EscapeDataString(startDate.Value.ToString("o"))}");
            if (endDate.HasValue) queryParts.Add($"endDate={Uri.EscapeDataString(endDate.Value.ToString("o"))}");
            if (!string.IsNullOrWhiteSpace(userId)) queryParts.Add($"userId={Uri.EscapeDataString(userId)}");

            var path = "api/report/audit-trail";
            if (queryParts.Count > 0)
            {
                path += "?" + string.Join("&", queryParts);
            }

            var result = await _httpClient.GetFromJsonAsync<ReportAuditTrailDto>(path);
            return result ?? new ReportAuditTrailDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading report audit trail.");
            return new ReportAuditTrailDto();
        }
    }

    public async Task<ReportHelpDto> GetHelpAsync()
    {
        try
        {
            AddAuthHeader();
            var result = await _httpClient.GetFromJsonAsync<ReportHelpDto>("api/report/help");
            return result ?? new ReportHelpDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading report help.");
            return new ReportHelpDto();
        }
    }

    public async Task<FmlMaintenanceHistoryReportDto> GetFmlMaintenanceHistoryAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? finYear = null,
        string? ggNum = null,
        string? mode = null,
        string? search = null)
    {
        try
        {
            AddAuthHeader();
            var path = "api/report/fml/maintenance-history" + BuildFmlQuery(startDate, endDate, finYear, ggNum, mode, search);
            var result = await _httpClient.GetFromJsonAsync<FmlMaintenanceHistoryReportDto>(path);
            return result ?? new FmlMaintenanceHistoryReportDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading FML maintenance history report.");
            return new FmlMaintenanceHistoryReportDto();
        }
    }

    public async Task<FmlContractsReportDto> GetFmlContractsExpiringAsync()
    {
        try
        {
            AddAuthHeader();
            var result = await _httpClient.GetFromJsonAsync<FmlContractsReportDto>("api/report/fml/contracts-expiring");
            return result ?? new FmlContractsReportDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading FML contracts expiring report.");
            return new FmlContractsReportDto();
        }
    }

    public async Task<FmlContractsReportDto> GetFmlExpiredOpenContractsAsync()
    {
        try
        {
            AddAuthHeader();
            var result = await _httpClient.GetFromJsonAsync<FmlContractsReportDto>("api/report/fml/expired-open");
            return result ?? new FmlContractsReportDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading FML expired contracts report.");
            return new FmlContractsReportDto();
        }
    }

    public async Task<FmlVehiclesNoContractsReportDto> GetFmlVehiclesNoContractsAsync()
    {
        try
        {
            AddAuthHeader();
            var result = await _httpClient.GetFromJsonAsync<FmlVehiclesNoContractsReportDto>("api/report/fml/vehicles-no-contracts");
            return result ?? new FmlVehiclesNoContractsReportDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading FML vehicles with no contracts report.");
            return new FmlVehiclesNoContractsReportDto();
        }
    }

    public async Task<FmlOverUtilizedReportDto> GetFmlOverUtilizedAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            AddAuthHeader();
            var path = "api/report/fml/over-utilized" + BuildFmlQuery(startDate, endDate, null, null, null, null);
            var result = await _httpClient.GetFromJsonAsync<FmlOverUtilizedReportDto>(path);
            return result ?? new FmlOverUtilizedReportDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading FML over-utilized report.");
            return new FmlOverUtilizedReportDto();
        }
    }

    public async Task<LegacyDynamicReportDto> GetLegacyDynamicReportAsync(
        string reportKey,
        IReadOnlyDictionary<string, string?> filters,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportKey);

        var queryParts = filters
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value) && !string.Equals(pair.Key, "view", StringComparison.OrdinalIgnoreCase))
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}")
            .ToList();

        var path = $"api/report/dynamic/{Uri.EscapeDataString(reportKey)}";
        if (queryParts.Count > 0)
        {
            path += "?" + string.Join("&", queryParts);
        }

        try
        {
            AddAuthHeader();
            var result = await _httpClient.GetFromJsonAsync<LegacyDynamicReportDto>(path, cancellationToken);
            return result ?? new LegacyDynamicReportDto { ReportKey = reportKey };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dynamic legacy report {ReportKey}.", reportKey);
            throw;
        }
    }

    private static string BuildFmlQuery(DateTime? startDate, DateTime? endDate, string? finYear, string? ggNum, string? mode, string? search)
    {
        var queryParts = new List<string>();
        if (startDate.HasValue)
        {
            queryParts.Add($"startDate={Uri.EscapeDataString(startDate.Value.ToString("yyyy-MM-dd"))}");
        }

        if (endDate.HasValue)
        {
            queryParts.Add($"endDate={Uri.EscapeDataString(endDate.Value.ToString("yyyy-MM-dd"))}");
        }

        if (!string.IsNullOrWhiteSpace(finYear))
        {
            queryParts.Add($"finYear={Uri.EscapeDataString(finYear)}");
        }

        if (!string.IsNullOrWhiteSpace(ggNum))
        {
            queryParts.Add($"ggNum={Uri.EscapeDataString(ggNum)}");
        }

        if (!string.IsNullOrWhiteSpace(mode))
        {
            queryParts.Add($"mode={Uri.EscapeDataString(mode)}");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            queryParts.Add($"search={Uri.EscapeDataString(search)}");
        }

        return queryParts.Count > 0 ? "?" + string.Join("&", queryParts) : string.Empty;
    }
}
