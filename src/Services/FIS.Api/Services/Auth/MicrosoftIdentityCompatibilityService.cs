using System.Data;
using System.Data.Common;
using System.Security.Claims;
using FIS.Core.Domain.Entities.Auth;
using FIS.Data.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Api.Services;

/// <summary>
/// Resolves a validated Microsoft identity to the FIS account that owns its
/// authorization. The Entra mapping table is optional during rollout; the
/// legacy user_access_old1 email is the guarded compatibility path.
/// </summary>
public sealed class MicrosoftIdentityCompatibilityService
{
    private readonly FisDbContext _context;
    private readonly ILogger<MicrosoftIdentityCompatibilityService> _logger;

    public MicrosoftIdentityCompatibilityService(
        FisDbContext context,
        ILogger<MicrosoftIdentityCompatibilityService> logger
    )
    {
        _context = context;
        _logger = logger;
    }

    public async Task<LegacyMicrosoftIdentityUser?> ResolveAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default
    )
    {
        var objectId = ResolveClaim(
            principal,
            "oid",
            "http://schemas.microsoft.com/identity/claims/objectidentifier"
        );
        var email = ResolveClaim(principal, ClaimTypes.Email, "email", "preferred_username", "upn");

        var mapping = !string.IsNullOrWhiteSpace(objectId)
            ? await TryGetMappingAsync(objectId, cancellationToken)
            : new MappingLookup(false, null);

        if (mapping.UserAccessCode.HasValue)
        {
            var mappedUser = await GetLegacyUserByCodeAsync(
                mapping.UserAccessCode.Value,
                cancellationToken
            );
            if (mappedUser is null)
            {
                _logger.LogWarning(
                    "Microsoft identity mapping points to a missing legacy user_access_code {UserAccessCode}",
                    mapping.UserAccessCode.Value
                );
                return null;
            }

            return CreateResolvedUser(mappedUser, email);
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalizedEmail = email.ToLowerInvariant();
        var emailMatches = await _context
            .UserAccessOlds.AsNoTracking()
            .Where(user => user.E_Mail != null && user.E_Mail.ToLower() == normalizedEmail)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (emailMatches.Count != 1)
        {
            if (emailMatches.Count > 1)
            {
                _logger.LogWarning(
                    "Microsoft sign-in email matched multiple legacy user_access_code records; refusing automatic account resolution"
                );
            }

            return null;
        }

        if (mapping.Available)
        {
            _logger.LogInformation(
                "No Entra mapping was found; using the unique legacy email match for Microsoft sign-in"
            );
        }

        return CreateResolvedUser(emailMatches[0], email);
    }

    private async Task<MappingLookup> TryGetMappingAsync(
        string objectId,
        CancellationToken cancellationToken
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT TOP (1) [user_access_code]
                FROM [dbo].[EntraId_User_Mapping]
                WHERE LOWER(LTRIM(RTRIM([entra_object_id]))) = LOWER(LTRIM(RTRIM(@entraObjectId)))
                """;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@entraObjectId";
            parameter.DbType = DbType.String;
            parameter.Size = 100;
            parameter.Value = objectId.Trim();
            command.Parameters.Add(parameter);

            var value = await command.ExecuteScalarAsync(cancellationToken);
            return new MappingLookup(
                Available: true,
                UserAccessCode: value is null or DBNull ? null : Convert.ToInt32(value)
            );
        }
        catch (SqlException ex) when (IsMissingSchemaObject(ex))
        {
            _logger.LogInformation(
                "Entra identity mapping table is unavailable; Microsoft sign-in will use the legacy email compatibility path"
            );
            return new MappingLookup(false, null);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<UserAccessOld?> GetLegacyUserByCodeAsync(
        int userAccessCode,
        CancellationToken cancellationToken
    )
    {
        if (userAccessCode is <= 0 or > short.MaxValue)
        {
            return null;
        }

        return await _context
            .UserAccessOlds.AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.user_access_code == userAccessCode,
                cancellationToken
            );
    }

    private static LegacyMicrosoftIdentityUser CreateResolvedUser(
        UserAccessOld user,
        string? identityEmail
    )
    {
        var email = string.IsNullOrWhiteSpace(user.E_Mail)
            ? identityEmail?.Trim() ?? string.Empty
            : user.E_Mail.Trim();

        return new LegacyMicrosoftIdentityUser(
            UserAccessCode: user.user_access_code,
            Username: user.name?.Trim() ?? email,
            Email: email,
            AccessLevel: user.AccessLevel,
            AccessString: user.Access_str,
            IsActive: user.user_active
        );
    }

    private static string? ResolveClaim(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool IsMissingSchemaObject(SqlException exception)
    {
        return exception.Number is 207 or 208;
    }

    private sealed record MappingLookup(bool Available, int? UserAccessCode);
}

public sealed record LegacyMicrosoftIdentityUser(
    short UserAccessCode,
    string Username,
    string Email,
    long AccessLevel,
    string? AccessString,
    bool IsActive
);
