using System.Collections.Concurrent;

namespace FIS.Web.Services;

public class AuthSessionTokenCache
{
    private readonly ConcurrentDictionary<string, string> _accessTokens = new(StringComparer.Ordinal);

    public void SetAccessToken(string sessionKey, string accessToken)
    {
        if (string.IsNullOrWhiteSpace(sessionKey) || string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        _accessTokens[sessionKey] = accessToken;
    }

    public bool TryGetAccessToken(string sessionKey, out string accessToken)
    {
        accessToken = string.Empty;
        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            return false;
        }

        if (!_accessTokens.TryGetValue(sessionKey, out var cachedToken) || string.IsNullOrWhiteSpace(cachedToken))
        {
            return false;
        }

        accessToken = cachedToken;
        return true;
    }

    public void RemoveAccessToken(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            return;
        }

        _accessTokens.TryRemove(sessionKey, out _);
    }
}
