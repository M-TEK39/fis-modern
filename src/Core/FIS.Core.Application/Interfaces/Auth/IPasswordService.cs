namespace FIS.Core.Application.Interfaces.Auth;

public interface IPasswordService
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
    bool IsPasswordStrong(string password, out string errorMessage);
}
