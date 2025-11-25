using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

public class UserApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserApiService> _logger;

    public UserApiService(HttpClient httpClient, ILogger<UserApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<UserDto>> GetAllAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<UserDto>>("api/User");
            return result ?? new List<UserDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching users");
            return new List<UserDto>();
        }
    }
}

public record UserDto(
    int Id,
    string? UserName,
    string? Email,
    string? Telephone
);
