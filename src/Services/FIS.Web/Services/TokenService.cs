namespace FIS.Web.Services;

/// <summary>
/// Circuit-scoped auth session state. Does not persist tokens in browser storage.
/// </summary>
public class TokenService
{
    private readonly object _lock = new();

    private string? _accessToken;
    private DateTime _accessExpiresAtUtc;
    private int _userAccessCode;
    private string? _email;

    public string? Token
    {
        get
        {
            lock (_lock)
            {
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

    public Task InitializeAsync() => Task.CompletedTask;

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
