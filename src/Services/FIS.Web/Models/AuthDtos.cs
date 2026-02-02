namespace FIS.Web.Models;

public class ChangePasswordRequest
{
    public string Username { get; set; } = string.Empty;
    public string OldPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public class ChangePasswordQuestionRequest
{
    public string Username { get; set; } = string.Empty;
    public string OldPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmNewPassword { get; set; } = string.Empty;
    public string SecurityQuestion { get; set; } = string.Empty;
    public string SecurityAnswer { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class ChangePasswordResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}
