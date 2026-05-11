using System.Net.Http.Json;
using System.ComponentModel.DataAnnotations;

namespace FIS.Web.Services;

internal class ApiLocationResponse
{
    public int LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ContactPerson { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Province { get; set; }
    public string? Country { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsActive { get; set; }
}

internal class ApiLocationRequest
{
    public int LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ContactPerson { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Province { get; set; }
    public string? Country { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsActive { get; set; } = true;
}

public class LocationApiService
{
    private readonly HttpClient _httpClient;
    private readonly TokenService _tokenService;
    private readonly ILogger<LocationApiService> _logger;

    public LocationApiService(HttpClient httpClient, TokenService tokenService, ILogger<LocationApiService> logger)
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

    public async Task<List<LocationViewDto>> GetAllAsync()
    {
        try
        {
            AddAuthHeader();
            var result = await _httpClient.GetFromJsonAsync<List<ApiLocationResponse>>("api/Location");
            if (result == null) return new List<LocationViewDto>();

            return result.Select(MapToView).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching locations");
            return new List<LocationViewDto>();
        }
    }

    public async Task<LocationViewDto?> CreateAsync(LocationViewDto location)
    {
        try
        {
            AddAuthHeader();
            var response = await _httpClient.PostAsJsonAsync("api/Location", MapToRequest(location));
            response.EnsureSuccessStatusCode();
            var created = await response.Content.ReadFromJsonAsync<ApiLocationResponse>();
            return created == null ? null : MapToView(created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating location");
            throw;
        }
    }

    public async Task<LocationViewDto?> UpdateAsync(LocationViewDto location)
    {
        try
        {
            AddAuthHeader();
            var response = await _httpClient.PutAsJsonAsync($"api/Location/{location.LocationId}", MapToRequest(location));
            response.EnsureSuccessStatusCode();
            var updated = await response.Content.ReadFromJsonAsync<ApiLocationResponse>();
            return updated == null ? null : MapToView(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating location {LocationId}", location.LocationId);
            throw;
        }
    }

    public async Task DeleteAsync(int locationId)
    {
        try
        {
            AddAuthHeader();
            var response = await _httpClient.DeleteAsync($"api/Location/{locationId}");
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting location {LocationId}", locationId);
            throw;
        }
    }

    private static LocationViewDto MapToView(ApiLocationResponse src)
    {
        var addressParts = new[] { src.AddressLine1, src.AddressLine2, src.City, src.Province, src.PostalCode }
            .Where(p => !string.IsNullOrWhiteSpace(p));

        return new LocationViewDto
        {
            LocationId = src.LocationId,
            LocationName = src.LocationName,
            Address = string.Join(", ", addressParts),
            Latitude = src.Latitude,
            Longitude = src.Longitude
        };
    }

    private static ApiLocationRequest MapToRequest(LocationViewDto view)
    {
        // Use Address as AddressLine1 for now; extend mapping if more fields are added to UI.
        return new ApiLocationRequest
        {
            LocationId = view.LocationId,
            LocationName = view.LocationName ?? string.Empty,
            AddressLine1 = view.Address,
            Latitude = view.Latitude,
            Longitude = view.Longitude,
            IsActive = true,
            Country = "South Africa"
        };
    }
}

public class LocationViewDto
{
    public int LocationId { get; set; }
    [Required]
    public string? LocationName { get; set; }
    public string? Address { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}
