using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

internal class ApiDriverResponse
{
    public int SiteDriverCode { get; set; }
    public int SiteCode { get; set; }
    public int DriverLicenceTypeId { get; set; }
    public string? DriverSurname { get; set; }
    public string? DriverFirstname { get; set; }
    public string? DriverSAId { get; set; }
    public string? DriverPassportNumber { get; set; }
    public string? DriverPersonalNumber { get; set; }
    public string? DriverContractNumber { get; set; }
    public string? DriverLicenceNumber { get; set; }
    public DateTime DriverLicenceIssueDate { get; set; }
    public DateTime DriverLicenceLastVerifiedDate { get; set; }
    public bool DriverHasPDP { get; set; }
    public DateTime? DriverPDPExpiryDate { get; set; }
    public DateTime? DriverLicenceExpiryDate { get; set; }
    public bool DriverActive { get; set; }
}

internal class ApiDriverRequest
{
    public int SiteCode { get; set; }
    public int DriverLicenceTypeId { get; set; }
    public string DriverSurname { get; set; } = string.Empty;
    public string DriverFirstname { get; set; } = string.Empty;
    public string? DriverSAId { get; set; }
    public string? DriverPassportNumber { get; set; }
    public string? DriverPersonalNumber { get; set; }
    public string? DriverContractNumber { get; set; }
    public string? DriverLicenceNumber { get; set; }
    public DateTime DriverLicenceIssueDate { get; set; }
    public DateTime DriverLicenceLastVerifiedDate { get; set; }
    public bool DriverHasPDP { get; set; }
    public DateTime? DriverPDPExpiryDate { get; set; }
    public DateTime? DriverLicenceExpiryDate { get; set; }
    public bool DriverActive { get; set; } = true;
}

public class DriverApiService
{
    private readonly HttpClient _httpClient;
    private readonly TokenService _tokenService;
    private readonly ILogger<DriverApiService> _logger;

    public DriverApiService(HttpClient httpClient, TokenService tokenService, ILogger<DriverApiService> logger)
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

    public async Task<List<DriverDto>> GetDriversAsync()
    {
        try
        {
            AddAuthHeader();
            var result = await _httpClient.GetFromJsonAsync<List<ApiDriverResponse>>("api/Driver");
            if (result == null) return new List<DriverDto>();

            return result.Select(MapToDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching drivers");
            return new List<DriverDto>();
        }
    }

    public async Task<DriverDto?> GetDriverAsync(int driverCode)
    {
        try
        {
            AddAuthHeader();
            var result = await _httpClient.GetFromJsonAsync<ApiDriverResponse>($"api/Driver/{driverCode}");
            return result == null ? null : MapToDto(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching driver {DriverCode}", driverCode);
            return null;
        }
    }

    public async Task<bool> CreateDriverAsync(DriverDto driver)
    {
        try
        {
            AddAuthHeader();
            var response = await _httpClient.PostAsJsonAsync("api/Driver", MapToRequest(driver));
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating driver");
            return false;
        }
    }

    public async Task<bool> UpdateDriverAsync(int driverCode, DriverDto driver)
    {
        try
        {
            AddAuthHeader();
            var response = await _httpClient.PutAsJsonAsync($"api/Driver/{driverCode}", MapToRequest(driver));
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating driver {DriverCode}", driverCode);
            return false;
        }
    }

    public async Task<bool> DeleteDriverAsync(int driverCode)
    {
        try
        {
            AddAuthHeader();
            var response = await _httpClient.DeleteAsync($"api/Driver/{driverCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting driver {DriverCode}", driverCode);
            return false;
        }
    }

    private static DriverDto MapToDto(ApiDriverResponse apiDriver)
    {
        return new DriverDto
        {
            site_driver_code = apiDriver.SiteDriverCode,
            site_code = apiDriver.SiteCode,
            driver_licence_type_id = apiDriver.DriverLicenceTypeId,
            driver_surname = apiDriver.DriverSurname,
            driver_firstname = apiDriver.DriverFirstname,
            driver_SA_id = apiDriver.DriverSAId,
            driver_passportnumber = apiDriver.DriverPassportNumber,
            driver_persalnumber = apiDriver.DriverPersonalNumber,
            driver_contractnumber = apiDriver.DriverContractNumber,
            driver_licence_number = apiDriver.DriverLicenceNumber,
            driver_licence_issuedate = apiDriver.DriverLicenceIssueDate,
            driver_licence_lastVerifiedDate = apiDriver.DriverLicenceLastVerifiedDate,
            driver_hasPDP = apiDriver.DriverHasPDP,
            driver_PDP_ExpiryDate = apiDriver.DriverPDPExpiryDate,
            driver_licence_ExpiryDate = apiDriver.DriverLicenceExpiryDate,
            driver_active = apiDriver.DriverActive
        };
    }

    private static ApiDriverRequest MapToRequest(DriverDto driver)
    {
        return new ApiDriverRequest
        {
            SiteCode = driver.site_code,
            DriverLicenceTypeId = driver.driver_licence_type_id,
            DriverSurname = driver.driver_surname ?? string.Empty,
            DriverFirstname = driver.driver_firstname ?? string.Empty,
            DriverSAId = driver.driver_SA_id,
            DriverPassportNumber = driver.driver_passportnumber,
            DriverPersonalNumber = driver.driver_persalnumber,
            DriverContractNumber = driver.driver_contractnumber,
            DriverLicenceNumber = driver.driver_licence_number,
            DriverLicenceIssueDate = driver.driver_licence_issuedate ?? DateTime.MinValue,
            DriverLicenceLastVerifiedDate = driver.driver_licence_lastVerifiedDate ?? DateTime.MinValue,
            DriverHasPDP = driver.driver_hasPDP,
            DriverPDPExpiryDate = driver.driver_PDP_ExpiryDate,
            DriverLicenceExpiryDate = driver.driver_licence_ExpiryDate,
            DriverActive = driver.driver_active
        };
    }
}
