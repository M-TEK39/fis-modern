using System.Net.Http.Json;

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

    public async Task<List<ApiUserDto>> GetAllAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<ApiUserDto>>("api/User");
            return result ?? new List<ApiUserDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching users");
            return new List<ApiUserDto>();
        }
    }

    public async Task<ApiUserDto?> CreateAsync(string? email, string? telephone)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/User", new CreateUserRequest
            {
                Email = email,
                TelephoneNumber = telephone
            });
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ApiUserDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            throw;
        }
    }

    public async Task<ApiUserDto?> UpdateAsync(int userAccessCode, string? email, string? telephone)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/User/{userAccessCode}", new UpdateUserRequest
            {
                Email = email,
                TelephoneNumber = telephone
            });
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ApiUserDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserAccessCode}", userAccessCode);
            throw;
        }
    }

    public async Task DeleteAsync(int userAccessCode)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/User/{userAccessCode}");
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserAccessCode}", userAccessCode);
            throw;
        }
    }
}

public record ApiUserDto
{
    public int UserAccessCode { get; set; }
    public string? TelephoneNumber { get; set; }
    public string? Email { get; set; }
}

public record CreateUserRequest
{
    public string? TelephoneNumber { get; set; }
    public string? Email { get; set; }
}

public record UpdateUserRequest : CreateUserRequest;
