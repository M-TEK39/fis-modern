using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace FIS.Web.Services;

public class SiteApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SiteApiService> _logger;

    public SiteApiService(HttpClient httpClient, ILogger<SiteApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<SiteResponseDto>> GetAllAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<SiteResponseDto>>("api/site");
            return result ?? new List<SiteResponseDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching sites");
            return new List<SiteResponseDto>();
        }
    }

    public async Task<SiteResponseDto?> GetByIdAsync(short siteCode)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<SiteResponseDto>($"api/site/{siteCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching site {SiteCode}", siteCode);
            return null;
        }
    }

    public async Task<SiteResponseDto?> CreateAsync(SiteRequestDto site)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/site", site);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SiteResponseDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating site");
            throw;
        }
    }

    public async Task<SiteResponseDto?> UpdateAsync(short siteCode, SiteRequestDto site)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/site/{siteCode}", site);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SiteResponseDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating site {SiteCode}", siteCode);
            throw;
        }
    }

    public async Task DeleteAsync(short siteCode)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/site/{siteCode}");
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting site {SiteCode}", siteCode);
            throw;
        }
    }
}

public class SiteRequestDto
{
    public short? DepartmentCode { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ResponsiblePerson { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? Address3 { get; set; }
    public string? PostalCode { get; set; }
    public string? ProvinceCode { get; set; }
    public string? Telephone { get; set; }
    public string? Fax { get; set; }
    public string? NetAddress { get; set; }
    public string? DepartmentNumber { get; set; }
    public string? MapReference { get; set; }
    public string? MapDescription { get; set; }
    public string? CellNumber { get; set; }
    public bool SiteActive { get; set; } = true;
    public string? Telephone2 { get; set; }
    public string? Fax1 { get; set; }
    public byte? FinancialSystemCode { get; set; }
    public bool? FinancialSystemActive { get; set; }
}

public class SiteResponseDto
{
    public short SiteCode { get; set; }
    public short? DepartmentCode { get; set; }
    public string? Description { get; set; }
    public string? ResponsiblePerson { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? Address3 { get; set; }
    public string? PostalCode { get; set; }
    public string? ProvinceCode { get; set; }
    public string? Telephone { get; set; }
    public string? Fax { get; set; }
    public string? NetAddress { get; set; }
    public string? DepartmentNumber { get; set; }
    public string? MapReference { get; set; }
    public string? MapDescription { get; set; }
    public string? CellNumber { get; set; }
    public bool SiteActive { get; set; }
    public string? Telephone2 { get; set; }
    public string? Fax1 { get; set; }
    public byte? FinancialSystemCode { get; set; }
    public bool? FinancialSystemActive { get; set; }
    public DateTime DateCreated { get; set; }
}
