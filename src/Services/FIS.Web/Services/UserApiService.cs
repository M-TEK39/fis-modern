using System.Net.Http.Json;
using FIS.Web.Models;
using System.Linq;

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

    public async Task<List<UserSummaryDto>> GetAllSummariesAsync()
    {
        try
        {
            var profiles = await _httpClient.GetFromJsonAsync<List<UserProfileDto>>("api/userprofile")
                ?? new List<UserProfileDto>();

            return profiles.Select(MapToSummary).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching users");
            return new List<UserSummaryDto>();
        }
    }

    public async Task<ApiUserDto?> CreateAsync(string? email, string? telephone)
    {
        try
        {
            var firstName = ResolveFirstName(email);
            var response = await _httpClient.PostAsJsonAsync("api/userprofile", new CreateUserProfileRequest
            {
                FirstName = firstName,
                LastName = "User",
                Email = email,
                Telephone = telephone,
                Password = "Temp#1234",
                AccessLevel = 1
            });
            response.EnsureSuccessStatusCode();
            var created = await response.Content.ReadFromJsonAsync<UserProfileDto>();
            return created is null ? null : MapToApiUser(created);
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
            var response = await _httpClient.PutAsJsonAsync($"api/userprofile/{userAccessCode}", new UpdateUserProfileRequest
            {
                Email = email,
                Telephone = telephone
            });
            response.EnsureSuccessStatusCode();
            var refreshed = await _httpClient.GetFromJsonAsync<UserProfileDto>($"api/userprofile/{userAccessCode}");
            return refreshed is null ? null : MapToApiUser(refreshed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserAccessCode}", userAccessCode);
            throw;
        }
    }

    public async Task<UserProfileDto?> GetProfileAsync(int userAccessCode)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<UserProfileDto>($"api/userprofile/{userAccessCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user profile {UserAccessCode}", userAccessCode);
            return null;
        }
    }

    public async Task<UserProfileDto?> UpdateProfileAsync(int userAccessCode, UpdateUserProfileRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/userprofile/{userAccessCode}", request);
            response.EnsureSuccessStatusCode();
            return await _httpClient.GetFromJsonAsync<UserProfileDto>($"api/userprofile/{userAccessCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user profile {UserAccessCode}", userAccessCode);
            throw;
        }
    }

    public async Task DeleteAsync(int userAccessCode)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/userprofile/{userAccessCode}");
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserAccessCode}", userAccessCode);
            throw;
        }
    }

    private static UserSummaryDto MapToSummary(UserProfileDto profile)
    {
        return new UserSummaryDto
        {
            UserAccessCode = profile.UserAccessCode,
            UserName = profile.FirstName,
            Email = profile.Email,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            Telephone = profile.Telephone,
            LastLoginDate = profile.LastLogOn?.ToString("yyyy-MM-dd HH:mm")
        };
    }

    private static ApiUserDto MapToApiUser(UserProfileDto profile)
    {
        return new ApiUserDto
        {
            UserAccessCode = profile.UserAccessCode,
            TelephoneNumber = profile.Telephone,
            Email = profile.Email
        };
    }

    private static string ResolveFirstName(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "User";
        }

        var prefix = email.Split('@', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(prefix) ? "User" : prefix.Trim();
    }
}

public record ApiUserDto
{
    public int UserAccessCode { get; set; }
    public string? TelephoneNumber { get; set; }
    public string? Email { get; set; }
}

public record CreateUserProfileRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    public long? AccessLevel { get; set; }
}

public record UpdateUserProfileRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    public short? SiteCode { get; set; }
    public byte? PositionCode { get; set; }
    public int? PersalNumber { get; set; }
    public int? ContractNumber { get; set; }
    public int? SaIdNumber { get; set; }
    public int? PassportNumber { get; set; }
    public int? CellphoneNumber { get; set; }
    public int? FaxNumber { get; set; }
}
