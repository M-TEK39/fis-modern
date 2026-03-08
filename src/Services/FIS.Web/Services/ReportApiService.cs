using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

public class ReportApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ReportApiService> _logger;

    public ReportApiService(HttpClient httpClient, ILogger<ReportApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
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
        int? vmfCode = null,
        int? departmentCode = null)
    {
        try
        {
            var queryParts = new List<string>();
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
}
