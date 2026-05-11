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
    private const string AccessCookieName = "FIS_Access_Token";
    private const string RefreshCookieName = "FIS_Refresh_Token";
    private static readonly TimeSpan RefreshFallbackLifetime = TimeSpan.FromHours(8);

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

            string? accessToken = loginResponse?.Token;
            DateTime? accessExpiresAt = loginResponse?.ExpiresAt;
            string? refreshToken = null;
            DateTime? refreshExpiresAt = null;

            // Extract cookies from API response and forward to browser
            var apiUri = new Uri(apiUrl);
            var cookies = handler.CookieContainer.GetCookies(apiUri);

            _logger.LogInformation("Received {Count} cookies from API", cookies.Count);

            foreach (Cookie cookie in cookies)
            {
                _logger.LogInformation("Cookie received from API: {Name} = {Value}", cookie.Name, cookie.Value?.Substring(0, Math.Min(20, cookie.Value?.Length ?? 0)));

                if ((cookie.Name == AccessCookieName || cookie.Name == RefreshCookieName) && !string.IsNullOrEmpty(cookie.Value))
                {
                    var cookieExpiresAt = GetCookieExpiryUtc(cookie);
                    var cookieOptions = new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = Request.IsHttps,
                        SameSite = SameSiteMode.Lax,
                        Expires = cookieExpiresAt ?? DateTimeOffset.UtcNow.Add(RefreshFallbackLifetime),
                        Path = "/",
                        Domain = null
                    };

                    Response.Cookies.Append(cookie.Name, cookie.Value, cookieOptions);

                    if (cookie.Name == AccessCookieName)
                    {
                        accessToken = cookie.Value;
                        accessExpiresAt = cookieExpiresAt;
                    }
                    else if (cookie.Name == RefreshCookieName)
                    {
                        refreshToken = cookie.Value;
                        refreshExpiresAt = cookieExpiresAt;
                    }

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
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = loginResponse.ExpiresAt,
                    Path = "/",
                    Domain = null
                };

                Response.Cookies.Append(AccessCookieName, loginResponse.Token, cookieOptions);
                accessToken = loginResponse.Token;
                accessExpiresAt = loginResponse.ExpiresAt;

                _logger.LogInformation("✅ Manually set access cookie from response token, Expires: {Expires}", loginResponse.ExpiresAt);
            }

            var sessionKey = HttpContext.Session.Id;
            if (!string.IsNullOrWhiteSpace(sessionKey) && !string.IsNullOrWhiteSpace(accessToken) && accessExpiresAt.HasValue)
            {
                _sessionTokenCache.SetTokens(sessionKey, accessToken, accessExpiresAt.Value, refreshToken, refreshExpiresAt);
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
    /// Refresh auth cookies through the API and keep server-side session token cache aligned.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        try
        {
            var sessionKey = HttpContext.Session.Id;
            if (!_sessionTokenCache.TryGetRefreshToken(sessionKey, out var refreshCookie) &&
                (!Request.Cookies.TryGetValue(RefreshCookieName, out refreshCookie) ||
                 string.IsNullOrWhiteSpace(refreshCookie)))
            {
                return Unauthorized(new { error = "Refresh token cookie is missing." });
            }

            var apiUrl = $"{_apiBaseUrl}/api/auth/refresh";
            var apiUri = new Uri(apiUrl);
            var handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = new CookieContainer(),
                AllowAutoRedirect = false
            };
            handler.CookieContainer.Add(apiUri, new Cookie(RefreshCookieName, refreshCookie) { Path = "/" });

            using var client = new HttpClient(handler);
            var response = await client.PostAsync(apiUrl, null);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("API refresh failed: {StatusCode}, {Content}", response.StatusCode, errorContent);
                return StatusCode((int)response.StatusCode, errorContent);
            }

            var loginResponse = await response.Content.ReadFromJsonAsync<LegacyLoginResponse>();
            if (loginResponse is null || string.IsNullOrWhiteSpace(loginResponse.Token))
            {
                return StatusCode(502, new { error = "API refresh response was empty." });
            }

            string? accessToken = loginResponse.Token;
            DateTime? accessExpiresAt = loginResponse.ExpiresAt;
            string? refreshToken = null;
            DateTime? refreshExpiresAt = null;

            foreach (Cookie cookie in handler.CookieContainer.GetCookies(apiUri))
            {
                if ((cookie.Name != AccessCookieName && cookie.Name != RefreshCookieName) || string.IsNullOrEmpty(cookie.Value))
                {
                    continue;
                }

                var cookieExpiresAt = GetCookieExpiryUtc(cookie);
                Response.Cookies.Append(
                    cookie.Name,
                    cookie.Value,
                    new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = Request.IsHttps,
                        SameSite = SameSiteMode.Lax,
                        Expires = cookieExpiresAt ?? DateTimeOffset.UtcNow.Add(RefreshFallbackLifetime),
                        Path = "/",
                        Domain = null
                    });

                if (cookie.Name == AccessCookieName)
                {
                    accessToken = cookie.Value;
                    accessExpiresAt = cookieExpiresAt;
                }
                else if (cookie.Name == RefreshCookieName)
                {
                    refreshToken = cookie.Value;
                    refreshExpiresAt = cookieExpiresAt;
                }
            }

            if (!string.IsNullOrWhiteSpace(sessionKey) && !string.IsNullOrWhiteSpace(accessToken) && accessExpiresAt.HasValue)
            {
                _sessionTokenCache.SetTokens(sessionKey, accessToken, accessExpiresAt.Value, refreshToken, refreshExpiresAt);
            }

            return Ok(loginResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying refresh request to API");
            return StatusCode(500, new { error = "Internal server error during token refresh" });
        }
    }

    /// <summary>
    /// Logout and clear auth cookies
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var sessionKey = HttpContext.Session.Id;
        try
        {
            var apiUrl = $"{_apiBaseUrl}/api/auth/logout";
            var apiUri = new Uri(apiUrl);
            var handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = new CookieContainer(),
                AllowAutoRedirect = false
            };

            if (!_sessionTokenCache.TryGetAccessToken(sessionKey, out var accessCookie) &&
                (!Request.Cookies.TryGetValue(AccessCookieName, out accessCookie) || string.IsNullOrWhiteSpace(accessCookie)))
            {
                accessCookie = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(accessCookie))
            {
                handler.CookieContainer.Add(apiUri, new Cookie(AccessCookieName, accessCookie) { Path = "/" });
            }

            if (!_sessionTokenCache.TryGetRefreshToken(sessionKey, out var refreshCookie) &&
                (!Request.Cookies.TryGetValue(RefreshCookieName, out refreshCookie) || string.IsNullOrWhiteSpace(refreshCookie)))
            {
                refreshCookie = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(refreshCookie))
            {
                handler.CookieContainer.Add(apiUri, new Cookie(RefreshCookieName, refreshCookie) { Path = "/" });
            }

            using var client = new HttpClient(handler);
            await client.PostAsync(apiUrl, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call API logout endpoint");
        }

        Response.Cookies.Delete(AccessCookieName);
        Response.Cookies.Delete(RefreshCookieName);

        if (!string.IsNullOrWhiteSpace(sessionKey))
        {
            _sessionTokenCache.RemoveTokens(sessionKey);
        }

        _logger.LogInformation("User logged out - auth cookies cleared");

        return Ok(new { message = "Logged out successfully" });
    }

    private static DateTime? GetCookieExpiryUtc(Cookie cookie)
    {
        return cookie.Expires == DateTime.MinValue
            ? null
            : cookie.Expires.ToUniversalTime();
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
    public bool PasswordExpired { get; set; }
    public int PasswordExpiresIn { get; set; }
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
