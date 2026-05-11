using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Net;
using FIS.Web.Services;

namespace FIS.Web.Controllers;

/// <summary>
/// Authentication proxy controller
/// Forwards login/logout requests to API and manages cookies properly
/// </summary>
[ApiController]
[Route("[controller]")]
public class AuthProxyController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthProxyController> _logger;
    private readonly AuthSessionTokenCache _sessionTokenCache;
    private readonly string _apiBaseUrl;

    public AuthProxyController(
        IConfiguration configuration,
        ILogger<AuthProxyController> logger,
        AuthSessionTokenCache sessionTokenCache)
    {
        _configuration = configuration;
        _logger = logger;
        _sessionTokenCache = sessionTokenCache;
        _apiBaseUrl = (_configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5010").TrimEnd('/');
    }

    /// <summary>
    /// Proxy login request to API and capture HttpOnly access/refresh cookies
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LegacyLoginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { error = "Password is required." });
            }

            // Create HttpClient with cookie container to capture Set-Cookie headers
            var handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = new CookieContainer(),
                AllowAutoRedirect = false
            };

            using var client = new HttpClient(handler);
            var apiUrl = $"{_apiBaseUrl}/api/auth/login";
            var loginIdentifier = request.FirstName;
            if (string.IsNullOrWhiteSpace(loginIdentifier))
            {
                loginIdentifier = request.Username;
            }
            loginIdentifier = loginIdentifier?.Trim();

            if (string.IsNullOrWhiteSpace(loginIdentifier))
            {
                return BadRequest(new { error = "First name is required." });
            }

            // Enforce real credential validation before token issuance.
            var validateResponse = await client.PostAsJsonAsync(
                $"{_apiBaseUrl}/api/userprofile/validate",
                new
                {
                    firstName = loginIdentifier,
                    password = request.Password
                });

            if (!validateResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Credential validation endpoint failed with status {StatusCode}", validateResponse.StatusCode);
                return StatusCode((int)validateResponse.StatusCode, new { error = "Credential validation failed." });
            }

            var validation = await validateResponse.Content.ReadFromJsonAsync<CredentialValidationResponse>();
            if (validation?.IsValid != true || !validation.UserAccessCode.HasValue)
            {
                _logger.LogInformation("Invalid login attempt for FirstName {FirstName}", loginIdentifier);
                return Unauthorized(new { error = "Invalid credentials." });
            }

            // Forward login request to API
            var response = await client.PostAsJsonAsync(apiUrl, new
            {
                firstName = loginIdentifier,
                username = validation.UserAccessCode.Value.ToString(),
                password = request.Password
            });

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("API login failed: {StatusCode}, {Content}", response.StatusCode, errorContent);
                return StatusCode((int)response.StatusCode, errorContent);
            }

            // Read response
            var loginResponse = await response.Content.ReadFromJsonAsync<LegacyLoginResponse>();
            HttpContext.Session.SetString("auth.bootstrap", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());

            // Extract cookies from API response and forward to browser
            var apiUri = new Uri(apiUrl);
            var cookies = handler.CookieContainer.GetCookies(apiUri);

            _logger.LogInformation("Received {Count} cookies from API", cookies.Count);

            foreach (Cookie cookie in cookies)
            {
                _logger.LogInformation("Cookie received from API: {Name} = {Value}", cookie.Name, cookie.Value?.Substring(0, Math.Min(20, cookie.Value?.Length ?? 0)));

                if ((cookie.Name == "FIS_Access_Token" || cookie.Name == "FIS_Refresh_Token") && !string.IsNullOrEmpty(cookie.Value))
                {
                    var cookieOptions = new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = false,  // DEV: Allow HTTP
                        SameSite = SameSiteMode.Lax,
                        Expires = cookie.Expires != DateTime.MinValue ? cookie.Expires : DateTimeOffset.UtcNow.AddHours(8),
                        Path = "/",
                        Domain = null
                    };

                    Response.Cookies.Append(cookie.Name, cookie.Value, cookieOptions);

                    _logger.LogInformation("✅ Forwarded auth cookie '{CookieName}' to browser, Expires: {Expires}",
                        cookie.Name, cookieOptions.Expires);
                }
            }

            // If no cookie was captured, set access cookie from response token fallback.
            if (cookies.Count == 0 && loginResponse != null && !string.IsNullOrEmpty(loginResponse.Token))
            {
                _logger.LogWarning("⚠️ No cookies captured from API response, but login succeeded. Manually setting access cookie from response token.");

                // Manually create cookie from token in JSON response
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = false,
                    SameSite = SameSiteMode.Lax,
                    Expires = loginResponse.ExpiresAt,
                    Path = "/",
                    Domain = null
                };

                Response.Cookies.Append("FIS_Access_Token", loginResponse.Token, cookieOptions);

                _logger.LogInformation("✅ Manually set access cookie from response token, Expires: {Expires}", loginResponse.ExpiresAt);
            }

            var sessionKey = HttpContext.Session.Id;
            if (!string.IsNullOrWhiteSpace(sessionKey) && loginResponse is not null && !string.IsNullOrWhiteSpace(loginResponse.Token))
            {
                _sessionTokenCache.SetAccessToken(sessionKey, loginResponse.Token);
            }

            return Ok(loginResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying login request to API");
            return StatusCode(500, new { error = "Internal server error during login" });
        }
    }

    /// <summary>
    /// Logout and clear auth cookies
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        try
        {
            // Call API logout endpoint
            using var client = new HttpClient();
            await client.PostAsync($"{_apiBaseUrl}/api/auth/logout", null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call API logout endpoint");
        }

        Response.Cookies.Delete("FIS_Access_Token");
        Response.Cookies.Delete("FIS_Refresh_Token");

        var sessionKey = HttpContext.Session.Id;
        if (!string.IsNullOrWhiteSpace(sessionKey))
        {
            _sessionTokenCache.RemoveAccessToken(sessionKey);
        }

        _logger.LogInformation("User logged out - auth cookies cleared");

        return Ok(new { message = "Logged out successfully" });
    }

}

/// <summary>
/// Login request model
/// </summary>
public class LegacyLoginRequest
{
    public string FirstName { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

/// <summary>
/// Login response model
/// </summary>
public class LegacyLoginResponse
{
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public int UserAccessCode { get; set; }
    public string? Email { get; set; }
    public string? Message { get; set; }
}

public class CredentialValidationResponse
{
    public bool IsValid { get; set; }
    public short? UserAccessCode { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public short? SiteCode { get; set; }
}
