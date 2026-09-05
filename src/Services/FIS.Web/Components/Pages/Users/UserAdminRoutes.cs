namespace FIS.Web.Components.Pages.Users;

public static class UserAdminRoutes
{
    public const string Menu = "menu";
    public const string Edit = "edit";
    public const string ResetLogin = "reset-login";
    public const string Deactivate = "deactivate";
    public const string ViewUsers = "view-users";
    public const string ChangePasswordQuestion = "change-password-question";
    public const string ForcePassword = "reset-password";
    public const string ForgotPassword = "forgot-password";

    public static bool IsUserAdminPath(string path)
    {
        return path == "/users"
            || path.StartsWith("/users/");
    }

    public static string FromPath(string path)
    {
        if (path == "/users")
        {
            return Menu;
        }

        return path switch
        {
            "/users/edit" => Edit,
            "/users/reset-login" => ResetLogin,
            "/users/deactivate" => Deactivate,
            "/users/view" => ViewUsers,
            "/users/view-users" => ViewUsers,
            "/users/change-password-question" => ChangePasswordQuestion,
            "/users/reset-password" => ForcePassword,
            "/users/forgot-password" => ForgotPassword,
            _ => Menu
        };
    }
}
