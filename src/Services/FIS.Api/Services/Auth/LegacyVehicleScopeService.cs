using System.Security.Claims;
using FIS.Core.Application.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Services;

/// <summary>
/// Resolves the location boundary used by vehicle lookups and Vehicle Master.
/// The browser may request a vehicle number, but it never supplies the
/// authority that decides whether that vehicle is visible. That authority is
/// derived from the authenticated legacy profile and role claims on every
/// request.
/// </summary>
public sealed class LegacyVehicleScopeService
{
    private readonly FisDbContext _context;
    private readonly ISiteRepository _siteRepository;

    public LegacyVehicleScopeService(FisDbContext context, ISiteRepository siteRepository)
    {
        _context = context;
        _siteRepository = siteRepository;
    }

    public async Task<IReadOnlySet<short>?> ResolveAllowedSiteCodesAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default
    )
    {
        if (HasGlobalScope(principal))
        {
            return null;
        }

        var userAccessCode = ReadUserAccessCode(principal);
        if (userAccessCode is not > 0)
        {
            return new HashSet<short>();
        }

        var profileSiteCode = await _context.UserAccessOlds.AsNoTracking()
            .Where(user => user.user_access_code == userAccessCode.Value)
            .Select(user => user.Site_code)
            .SingleOrDefaultAsync(cancellationToken);
        if (profileSiteCode is not > 0)
        {
            return new HashSet<short>();
        }

        var profileSite = await _siteRepository.GetByIdAsync(profileSiteCode.Value);
        if (profileSite is null)
        {
            return new HashSet<short>();
        }

        var sites = await _siteRepository.GetActiveSitesAsync();
        if (HasRole(principal, "Vehicle List for All Departments in Province")
            && profileSite.province_code.HasValue)
        {
            sites = sites.Where(site => site.province_code == profileSite.province_code.Value);
        }
        else if (HasRole(principal, "Vehicle List for All Sites in Department")
            && profileSite.Depatrment_code.HasValue)
        {
            sites = sites.Where(site => site.Depatrment_code == profileSite.Depatrment_code.Value);
        }
        else
        {
            sites = sites.Where(site => site.Site_code == profileSite.Site_code);
        }

        return sites.Select(site => site.Site_code).ToHashSet();
    }

    private static int? ReadUserAccessCode(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirst("user_access_code")?.Value, out var value)
            ? value
            : null;

    private static bool HasGlobalScope(ClaimsPrincipal principal) =>
        HasAnyRole(
            principal,
            "Admin",
            "Administrator",
            "System Administrator",
            "SystemAdministrator"
        );

    private static bool HasRole(ClaimsPrincipal principal, string expectedRole) =>
        HasAnyRole(principal, expectedRole);

    private static bool HasAnyRole(ClaimsPrincipal principal, params string[] expectedRoles)
    {
        var roleClaims = principal.Claims
            .Where(claim =>
                claim.Type == ClaimTypes.Role
                || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
                || claim.Type.Equals("roles", StringComparison.OrdinalIgnoreCase)
            )
            .SelectMany(claim => claim.Value.Split(
                ',',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
            ));

        return roleClaims.Any(role => expectedRoles.Any(expected =>
            string.Equals(role, expected, StringComparison.OrdinalIgnoreCase)));
    }
}
