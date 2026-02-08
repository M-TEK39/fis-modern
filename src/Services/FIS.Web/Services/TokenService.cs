using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace FIS.Web.Services;

/// <summary>
/// Service to manage JWT token storage in Blazor Server
/// Uses a simple static holder since HttpClientFactory creates separate scopes
/// </summary>
public class TokenService
{
    private readonly ProtectedSessionStorage _sessionStorage;
    private readonly ILogger<TokenService> _logger;

    private const string TOKEN_KEY = "FIS_JWT_Token";
    private const string EXPIRY_KEY = "FIS_JWT_Expiry";

    // Simple static holder - works because we're single-user in development
    // For multi-user production, you'd use distributed cache with user-specific keys
    private static string? _currentToken;
    private static DateTime _currentExpiresAt;
    private static readonly object _lock = new();

    public TokenService(ProtectedSessionStorage sessionStorage, ILogger<TokenService> logger)
    {
        _sessionStorage = sessionStorage;
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
        try
        {
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
                    _logger.LogInformation("✅ Token loaded from session storage, expires: {Expiry}", expiryResult.Value);
                }
                else
                {
                    _logger.LogWarning("⚠️ Token in session storage is expired");
                    await ClearTokenAsync();
                }
            }
        }
        catch (InvalidOperationException ex) when (IsPrerenderInteropException(ex))
        {
            // ProtectedSessionStorage uses JS interop and is unavailable during prerender.
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
            // Store in static holder (shared across ALL service instances)
            lock (_lock)
            {
                _currentToken = token;
                _currentExpiresAt = expiresAt;
            }

            // Also store in session storage for persistence across page refreshes
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
            // Clear static holder
            lock (_lock)
            {
                _currentToken = null;
                _currentExpiresAt = DateTime.MinValue;
            }

            // Clear from session storage
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

    private static bool IsPrerenderInteropException(InvalidOperationException ex)
    {
        return ex.Message.Contains("statically rendered", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("prerender", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("JavaScript interop calls cannot be issued", StringComparison.OrdinalIgnoreCase);
    }

    // Synchronous methods for backward compatibility
    public void SetToken(string token, DateTime expiresAt)
    {
        lock (_lock)
        {
            _currentToken = token;
            _currentExpiresAt = expiresAt;
        }
    }

    public void ClearToken()
    {
        lock (_lock)
        {
            _currentToken = null;
            _currentExpiresAt = DateTime.MinValue;
        }
    }
}
