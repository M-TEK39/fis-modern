using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Net;

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

    public AuthProxyController(IConfiguration configuration, ILogger<AuthProxyController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Proxy login request to API and capture JWT cookie
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LegacyLoginRequest request)
    {
        try
        {
            // Create HttpClient with cookie container to capture Set-Cookie headers
            var handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = new CookieContainer(),
                AllowAutoRedirect = false
            };

            using var client = new HttpClient(handler);
            var apiUrl = "http://localhost:5010/api/auth/login";
            var loginIdentifier = request.FirstName;
            if (string.IsNullOrWhiteSpace(loginIdentifier))
            {
                loginIdentifier = request.Username;
            }

            // Forward login request to API
            var response = await client.PostAsJsonAsync(apiUrl, new
            {
                firstName = loginIdentifier,
                username = loginIdentifier,
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

            // Extract cookies from API response and forward to browser
            var apiUri = new Uri(apiUrl);
            var cookies = handler.CookieContainer.GetCookies(apiUri);

            _logger.LogInformation("Received {Count} cookies from API", cookies.Count);

            foreach (Cookie cookie in cookies)
            {
                _logger.LogInformation("Cookie received from API: {Name} = {Value}", cookie.Name, cookie.Value?.Substring(0, Math.Min(20, cookie.Value?.Length ?? 0)));

                if (cookie.Name == "FIS_JWT_Token" && !string.IsNullOrEmpty(cookie.Value))
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

                    _logger.LogInformation("✅ Forwarded JWT cookie '{CookieName}' to browser, Expires: {Expires}",
                        cookie.Name, cookieOptions.Expires);
                }
            }

            // If no cookie was captured, log the token from response for debugging
            if (cookies.Count == 0 && loginResponse != null && !string.IsNullOrEmpty(loginResponse.Token))
            {
                _logger.LogWarning("⚠️ No cookies captured from API response, but login succeeded. Manually setting cookie from token.");

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

                Response.Cookies.Append("FIS_JWT_Token", loginResponse.Token, cookieOptions);

                _logger.LogInformation("✅ Manually set JWT cookie from response token, Expires: {Expires}", loginResponse.ExpiresAt);
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
    /// Logout and clear JWT cookie
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        try
        {
            // Call API logout endpoint
            using var client = new HttpClient();
            await client.PostAsync("http://localhost:5010/api/auth/logout", null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call API logout endpoint");
        }

        // Clear JWT cookie from browser
        Response.Cookies.Delete("FIS_JWT_Token");

        _logger.LogInformation("User logged out - JWT cookie cleared");

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
