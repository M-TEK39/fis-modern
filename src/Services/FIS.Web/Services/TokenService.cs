using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.IdentityModel.Tokens.Jwt;

namespace FIS.Web.Services;

/// <summary>
/// Service to manage JWT token storage in Blazor Server.
/// Scoped per circuit (browser session). Uses HttpContext.Items as a relay so that
/// AuthorizationHeaderHandler (transient, resolved via root scope) can read the
/// correct circuit's token via IHttpContextAccessor's AsyncLocal-backed HttpContext.
/// </summary>
public class TokenService
{
    private readonly ProtectedSessionStorage _sessionStorage;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<TokenService> _logger;

    private const string TOKEN_KEY = "FIS_JWT_Token";
    private const string EXPIRY_KEY = "FIS_JWT_Expiry";
    private const string ITEMS_KEY = "FIS_JWT_Token";
    private const string COOKIE_KEY = "FIS_JWT_Token";

    // Instance fields — isolated per circuit via AddScoped registration.
    private string? _currentToken;
    private DateTime _currentExpiresAt;
    private bool _hydratedFromSession;
    private readonly object _lock = new();

    public TokenService(
        ProtectedSessionStorage sessionStorage,
        IHttpContextAccessor httpContextAccessor,
        ILogger<TokenService> logger)
    {
        _sessionStorage = sessionStorage;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public string? Token
    {
        get
        {
            lock (_lock)
            {
                return _currentToken;
            }
        }
    }

    public bool IsTokenValid
    {
        get
        {
            lock (_lock)
            {
                return !string.IsNullOrEmpty(_currentToken) && _currentExpiresAt > DateTime.UtcNow;
            }
        }
    }

    public async Task InitializeAsync()
    {
        lock (_lock)
        {
            if (_hydratedFromSession)
            {
                return;
            }
        }

        try
        {
            // First hydrate from HttpOnly cookie so prerender/full reload can still authenticate
            // before browser session storage is available.
            var cookieToken = _httpContextAccessor.HttpContext?.Request.Cookies[COOKIE_KEY];
            if (!string.IsNullOrWhiteSpace(cookieToken))
            {
                if (TryReadExpiryFromJwt(cookieToken, out var cookieExpiry) && cookieExpiry > DateTime.UtcNow)
                {
                    lock (_lock)
                    {
                        _currentToken = cookieToken;
                        _currentExpiresAt = cookieExpiry;
                    }

                    SetHttpContextItem(cookieToken);
                    _logger.LogInformation("✅ Token hydrated from auth cookie, expires: {Expiry}", cookieExpiry);
                }
                else
                {
                    _logger.LogWarning("⚠️ Auth cookie token is missing/expired.");
                }
            }

            var tokenResult = await _sessionStorage.GetAsync<string>(TOKEN_KEY);
            var expiryResult = await _sessionStorage.GetAsync<DateTime>(EXPIRY_KEY);

            if (tokenResult.Success && expiryResult.Success)
            {
                lock (_lock)
                {
                    _currentToken = tokenResult.Value;
                    _currentExpiresAt = expiryResult.Value;
                }

                if (IsTokenValid)
                {
                    // Relay into HttpContext.Items so AuthorizationHeaderHandler can read it.
                    SetHttpContextItem(tokenResult.Value);
                    _logger.LogInformation("✅ Token loaded from session storage, expires: {Expiry}", expiryResult.Value);
                }
                else
                {
                    _logger.LogWarning("⚠️ Token in session storage is expired");
                    await ClearTokenAsync();
                }
            }

            lock (_lock)
            {
                _hydratedFromSession = true;
            }
        }
        catch (InvalidOperationException ex) when (IsPrerenderInteropException(ex))
        {
            _logger.LogDebug("Token storage unavailable during prerender. Initialization deferred.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load token from session storage");
        }
    }

    public async Task SetTokenAsync(string token, DateTime expiresAt)
    {
        try
        {
            lock (_lock)
            {
                _currentToken = token;
                _currentExpiresAt = expiresAt;
                _hydratedFromSession = true;
            }

            // Relay into HttpContext.Items so AuthorizationHeaderHandler can read it.
            SetHttpContextItem(token);

            await _sessionStorage.SetAsync(TOKEN_KEY, token);
            await _sessionStorage.SetAsync(EXPIRY_KEY, expiresAt);

            _logger.LogInformation("✅ Token stored, expires: {Expiry}", expiresAt);
        }
        catch (InvalidOperationException ex) when (IsPrerenderInteropException(ex))
        {
            _logger.LogDebug("Token saved in memory only during prerender. Browser storage write deferred.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store token");
        }
    }

    public async Task ClearTokenAsync()
    {
        try
        {
            lock (_lock)
            {
                _currentToken = null;
                _currentExpiresAt = DateTime.MinValue;
                _hydratedFromSession = true;
            }

            // Remove from HttpContext.Items relay.
            _httpContextAccessor.HttpContext?.Items.Remove(ITEMS_KEY);

            await _sessionStorage.DeleteAsync(TOKEN_KEY);
            await _sessionStorage.DeleteAsync(EXPIRY_KEY);

            _logger.LogInformation("Token cleared");
        }
        catch (InvalidOperationException ex) when (IsPrerenderInteropException(ex))
        {
            _logger.LogDebug("Token cleared in memory during prerender. Browser storage clear deferred.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear token");
        }
    }

    private void SetHttpContextItem(string? token)
    {
        if (_httpContextAccessor.HttpContext is { } ctx)
            ctx.Items[ITEMS_KEY] = token;
    }

    private static bool IsPrerenderInteropException(InvalidOperationException ex)
    {
        return ex.Message.Contains("statically rendered", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("prerender", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("JavaScript interop calls cannot be issued", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadExpiryFromJwt(string token, out DateTime expiryUtc)
    {
        expiryUtc = DateTime.MinValue;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            var expClaim = jwt.Claims.FirstOrDefault(c => c.Type == "exp")?.Value;
            if (!long.TryParse(expClaim, out var epoch))
            {
                return false;
            }

            expiryUtc = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Synchronous methods for backward compatibility
    public void SetToken(string token, DateTime expiresAt)
    {
        lock (_lock)
        {
            _currentToken = token;
            _currentExpiresAt = expiresAt;
        }
        SetHttpContextItem(token);
    }

    public void ClearToken()
    {
        lock (_lock)
        {
            _currentToken = null;
            _currentExpiresAt = DateTime.MinValue;
        }
        _httpContextAccessor.HttpContext?.Items.Remove(ITEMS_KEY);
    }
}
