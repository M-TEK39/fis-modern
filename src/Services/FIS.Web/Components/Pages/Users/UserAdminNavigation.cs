using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;

namespace FIS.Web.Components.Pages.Users;

public static class UserAdminNavigation
{
    public static string GetMenuLink(NavigationManager navigation)
    {
        return GetLink(navigation, UserAdminRoutes.Menu);
    }

    public static string GetLink(NavigationManager navigation, string target)
    {
        var currentPath = GetCurrentPath(navigation);
        if (UserAdminRoutes.IsUserAdminPath(currentPath))
        {
            return target switch
            {
                UserAdminRoutes.Menu => "/users",
                _ => $"/users/{target}"
            };
        }

        var settingsValue = target == UserAdminRoutes.Menu
            ? "users"
            : $"users/{target}";

        return QueryHelpers.AddQueryString(navigation.Uri.Split('?')[0], "settings", settingsValue);
    }

    public static string GetLink(NavigationManager navigation, string target, string username)
    {
        var link = GetLink(navigation, target);
        return QueryHelpers.AddQueryString(link, "Username", username);
    }

    private static string GetCurrentPath(NavigationManager navigation)
    {
        var relative = navigation.ToBaseRelativePath(navigation.Uri);
        return "/" + (relative?.Split('?', '#')[0] ?? string.Empty).TrimEnd('/');
    }
}
