namespace FIS.Web.Services;

/// <summary>
/// Circuit-scoped auth session state. Does not persist tokens in browser storage.
/// </summary>
public class TokenService
{
    private readonly object _lock = new();
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthSessionTokenCache _sessionTokenCache;
    private readonly ILogger<TokenService> _logger;
    private const string AccessCookie = "FIS_Access_Token";

    private string? _accessToken;
    private DateTime _accessExpiresAtUtc;
    private int _userAccessCode;
    private string? _email;

    public TokenService(
        IHttpContextAccessor httpContextAccessor,
        AuthSessionTokenCache sessionTokenCache,
        ILogger<TokenService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _sessionTokenCache = sessionTokenCache;
        _logger = logger;
    }

    public string? Token
    {
        get
        {
            lock (_lock)
            {
                if (string.IsNullOrWhiteSpace(_accessToken) || _accessExpiresAtUtc <= DateTime.UtcNow)
                {
                    TryBootstrapTokenUnsafe();
                }
                return _accessToken;
            }
        }
    }

    public bool IsTokenValid
    {
        get
        {
            lock (_lock)
            {
                return !string.IsNullOrWhiteSpace(_accessToken) && _accessExpiresAtUtc > DateTime.UtcNow;
            }
        }
    }

    public int UserAccessCode
    {
        get
        {
            lock (_lock)
            {
                return _userAccessCode;
            }
        }
    }

    public string? Email
    {
        get
        {
            lock (_lock)
            {
                return _email;
            }
        }
    }

    public Task InitializeAsync()
    {
        if (IsTokenValid)
        {
            return Task.CompletedTask;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return Task.CompletedTask;
        }

        if (httpContext.Request.Cookies.TryGetValue(AccessCookie, out var cookieToken) &&
            !string.IsNullOrWhiteSpace(cookieToken))
        {
            lock (_lock)
            {
                _accessToken = cookieToken;
                _accessExpiresAtUtc = DateTime.UtcNow.AddHours(8);
            }

            var sessionKey = httpContext.Session?.Id;
            if (!string.IsNullOrWhiteSpace(sessionKey))
            {
                _sessionTokenCache.SetAccessToken(sessionKey, cookieToken);
            }

            return Task.CompletedTask;
        }

        var key = httpContext.Session?.Id;
        if (!string.IsNullOrWhiteSpace(key) && _sessionTokenCache.TryGetAccessToken(key, out var cachedToken))
        {
            lock (_lock)
            {
                _accessToken = cachedToken;
                _accessExpiresAtUtc = DateTime.UtcNow.AddHours(8);
            }
            return Task.CompletedTask;
        }

        _logger.LogDebug("TokenService could not bootstrap token from request cookie or session cache.");
        return Task.CompletedTask;
    }

    private void TryBootstrapTokenUnsafe()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return;
        }

        if (httpContext.Request.Cookies.TryGetValue(AccessCookie, out var cookieToken) &&
            !string.IsNullOrWhiteSpace(cookieToken))
        {
            _accessToken = cookieToken;
            _accessExpiresAtUtc = DateTime.UtcNow.AddHours(8);

            var sessionKey = httpContext.Session?.Id;
            if (!string.IsNullOrWhiteSpace(sessionKey))
            {
                _sessionTokenCache.SetAccessToken(sessionKey, cookieToken);
            }

            return;
        }

        var key = httpContext.Session?.Id;
        if (!string.IsNullOrWhiteSpace(key) && _sessionTokenCache.TryGetAccessToken(key, out var cachedToken))
        {
            _accessToken = cachedToken;
            _accessExpiresAtUtc = DateTime.UtcNow.AddHours(8);
        }
    }

    public Task SetTokenAsync(string token, DateTime expiresAt)
    {
        lock (_lock)
        {
            _accessToken = token;
            _accessExpiresAtUtc = expiresAt.ToUniversalTime();
        }

        return Task.CompletedTask;
    }

    public Task SetUserContextAsync(int userAccessCode, string? email)
    {
        lock (_lock)
        {
            _userAccessCode = userAccessCode;
            _email = email;
        }

        return Task.CompletedTask;
    }

    public Task ClearTokenAsync()
    {
        lock (_lock)
        {
            _accessToken = null;
            _accessExpiresAtUtc = DateTime.MinValue;
            _userAccessCode = 0;
            _email = null;
        }

        return Task.CompletedTask;
    }

    public void SetToken(string token, DateTime expiresAt)
    {
        _ = SetTokenAsync(token, expiresAt);
    }

    public void ClearToken()
    {
        _ = ClearTokenAsync();
    }
}
