using System.Security.Claims;
using FIS.Core.Application.Interfaces;

namespace FIS.Api.Services;

public sealed class CurrentUserContextService : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserContextService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int GetCurrentUserIdOrDefault(int fallbackUserId = 1)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return fallbackUserId;
        }

        var userAccessCodeRaw = user.FindFirst("user_access_code")?.Value;
        return int.TryParse(userAccessCodeRaw, out var userAccessCode) && userAccessCode > 0
            ? userAccessCode
            : fallbackUserId;
    }

    public string GetCurrentUserDisplayName(int? fallbackUserId = null)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return fallbackUserId.HasValue ? $"User {fallbackUserId.Value}" : "System";
        }

        var name =
            user.FindFirst(ClaimTypes.Name)?.Value
            ?? user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var userId = GetCurrentUserIdOrDefault(fallbackUserId ?? 1);
        return $"User {userId}";
    }
}
