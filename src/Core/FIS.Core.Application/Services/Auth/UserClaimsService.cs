using System.Security.Claims;
using FIS.Core.Application.Interfaces;
using FIS.Core.Application.Interfaces.Auth;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Application.Services.Auth;

/// <summary>
/// Service for enriching Entra ID tokens with legacy FIS user claims.
/// Bridges modern Azure Entra ID authentication with legacy TS_Users permissions.
/// </summary>
public class UserClaimsService : IUserClaimsService
{
    private readonly IEntraIdUserMappingRepository _mappingRepository;
    private readonly IUserRepository _userRepository;

    public UserClaimsService(
        IEntraIdUserMappingRepository mappingRepository,
        IUserRepository userRepository)
    {
        _mappingRepository = mappingRepository;
        _userRepository = userRepository;
    }

    public async Task<IEnumerable<Claim>> GetClaimsAsync(string entraObjectId)
    {
        var claims = new List<Claim>();

        // Find mapping to legacy user
        var mapping = await _mappingRepository.GetByEntraObjectIdAsync(entraObjectId);
        if (mapping == null)
        {
            // User not mapped yet - return empty claims
            // Frontend should redirect to user mapping page
            return claims;
        }

        // Get legacy user details from TS_Users
        var user = await _userRepository.GetByIdAsync(mapping.user_access_code);
        if (user == null)
        {
            // Orphaned mapping - should not happen
            throw new InvalidOperationException(
                $"Entra ID user '{entraObjectId}' is mapped to non-existent user_access_code {mapping.user_access_code}"
            );
        }

        // Add FIS-specific claims from TS_Users
        // Note: TS_Users is a legacy 3-field table (user_access_code, tel_no, email)
        // Additional user metadata (site, department, etc.) may be stored elsewhere
        claims.Add(new Claim("user_access_code", user.user_access_code.ToString()));

        // Add contact information if available
        if (!string.IsNullOrWhiteSpace(user.tel_no))
        {
            claims.Add(new Claim(ClaimTypes.HomePhone, user.tel_no));
        }

        if (!string.IsNullOrWhiteSpace(user.email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.email));
        }

        return claims;
    }

    public async Task<bool> CreateOrUpdateMappingAsync(string entraObjectId, int userAccessCode)
    {
        try
        {
            // Check if mapping already exists
            var existingMapping = await _mappingRepository.GetByEntraObjectIdAsync(entraObjectId);

            if (existingMapping != null)
            {
                // Update existing mapping
                existingMapping.user_access_code = userAccessCode;
                existingMapping.created_date = DateTime.UtcNow; // Update timestamp
                await _mappingRepository.UpdateAsync(existingMapping);
            }
            else
            {
                // Create new mapping
                var newMapping = new EntraIdUserMapping
                {
                    entra_object_id = entraObjectId,
                    user_access_code = userAccessCode,
                    created_date = DateTime.UtcNow
                };
                await _mappingRepository.CreateAsync(newMapping);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsMappedAsync(string entraObjectId)
    {
        return await _mappingRepository.ExistsAsync(entraObjectId);
    }
}
