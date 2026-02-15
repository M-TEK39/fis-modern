namespace FIS.Web.Models;

public class ResetLoginRequest
{
    public string Username { get; set; } = string.Empty;
}

public class ForcePasswordRequest
{
    public string Username { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class ForgotPasswordStartRequest
{
    public string Username { get; set; } = string.Empty;
}

public class ForgotPasswordConfirmRequest
{
    public string Username { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
}

public class ActivateUserRequest
{
    public string Username { get; set; } = string.Empty;
}

public class DeactivateExpiredPasswordRequest
{
    public string Username { get; set; } = string.Empty;
}

public class UserAdminResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Question { get; set; }
    public string? NewPassword { get; set; }
}

public class UserSummaryDto
{
    public int UserAccessCode { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? SiteName { get; set; }
    public string? Position { get; set; }
    public string? Telephone { get; set; }
    public string? LastLoginDate { get; set; }
    public long AccessLevel { get; set; }
}
