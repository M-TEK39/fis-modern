using System.Net.Http.Json;

namespace FIS.Web.Services;

public class UserProfileApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserProfileApiService> _logger;

    public UserProfileApiService(HttpClient httpClient, ILogger<UserProfileApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<UserProfileDto?> GetByUserAccessCodeAsync(int userAccessCode)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<UserProfileDto>($"api/userprofile/{userAccessCode}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load profile for user access code {UserAccessCode}", userAccessCode);
            return null;
        }
    }

    public async Task<UserProfileDto?> GetByFirstNameAsync(string firstName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            return null;
        }

        try
        {
            var encoded = Uri.EscapeDataString(firstName.Trim());
            return await _httpClient.GetFromJsonAsync<UserProfileDto>($"api/userprofile/by-name/{encoded}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load profile for first name {FirstName}", firstName);
            return null;
        }
    }
}

public class UserProfileDto
{
    public short UserAccessCode { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Telephone { get; set; }
    public short? SiteCode { get; set; }
    public byte? PositionCode { get; set; }
    public int? PersalNumber { get; set; }
    public int? ContractNumber { get; set; }
    public int? SaIdNumber { get; set; }
    public int? PassportNumber { get; set; }
    public int? CellphoneNumber { get; set; }
    public int? FaxNumber { get; set; }
    public string? UserStatus { get; set; }
    public long AccessLevel { get; set; }
    public bool UserActive { get; set; }
    public DateTime? LastLogOn { get; set; }
}
