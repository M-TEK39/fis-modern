using FIS.Web.Models;
using FIS.Web.Services;

namespace FIS.Web.Components.Pages.Drivers;

internal static class DriverManagementNavigation
{
    public static short? ParseNullableShort(string? value)
        => short.TryParse(value, out var parsed) ? parsed : null;

    public static int? ParseNullableInt(string? value)
        => int.TryParse(value, out var parsed) ? parsed : null;

    public static string Index(short? departmentCode = null, short? siteCode = null)
        => BuildPath("/drivers", departmentCode, siteCode);

    public static string Authorisers(short? departmentCode, short? siteCode)
        => BuildPath("/drivers/authorisers", departmentCode, siteCode);

    public static string AuthoriserEdit(short? departmentCode, short? siteCode, short? userAccessCode = null)
    {
        var query = BuildContextQuery(departmentCode, siteCode);
        if (userAccessCode.HasValue)
        {
            query.Add($"userAccessCode={userAccessCode.Value}");
        }

        return query.Count == 0
            ? "/drivers/authorisers/edit"
            : $"/drivers/authorisers/edit?{string.Join("&", query)}";
    }

    public static string SiteDrivers(short? departmentCode, short? siteCode)
        => BuildPath("/drivers/site-drivers", departmentCode, siteCode);

    public static string SiteDriverEdit(short? departmentCode, short? siteCode, int? driverCode = null)
    {
        var query = BuildContextQuery(departmentCode, siteCode);
        if (driverCode.HasValue)
        {
            query.Add($"driverCode={driverCode.Value}");
        }

        return query.Count == 0
            ? "/drivers/site-drivers/edit"
            : $"/drivers/site-drivers/edit?{string.Join("&", query)}";
    }

    public static string BuildSiteLabel(SiteResponseDto? site)
    {
        if (site is null)
        {
            return "Selected site";
        }

        return string.IsNullOrWhiteSpace(site.Description)
            ? $"Site {site.SiteCode}"
            : $"{site.Description} ({site.SiteCode})";
    }

    public static string BuildDepartmentLabel(DepartmentDto? department)
    {
        if (department is null)
        {
            return "Selected department";
        }

        return string.IsNullOrWhiteSpace(department.department_description)
            ? $"Department {department.department_code}"
            : $"{department.department_description} ({department.department_code})";
    }

    public static string BuildDriverLabel(DriverDto driver)
    {
        var name = $"{driver.driver_firstname} {driver.driver_surname}".Trim();
        return string.IsNullOrWhiteSpace(name) ? $"Driver {driver.site_driver_code}" : name;
    }

    public static string BuildAuthoriserLabel(UserProfileDto profile)
    {
        var name = $"{profile.FirstName} {profile.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? $"Authoriser {profile.UserAccessCode}" : name;
    }

    public static IReadOnlyList<RankOption> BuildRankOptions(
        IEnumerable<ApproverRankDto> ranks,
        IEnumerable<UserProfileDto> profiles)
    {
        var explicitRanks = ranks
            .Where(rank => rank.Id >= byte.MinValue && rank.Id <= byte.MaxValue)
            .Select(rank => new RankOption(
                (byte)rank.Id,
                string.IsNullOrWhiteSpace(rank.Description)
                    ? (!string.IsNullOrWhiteSpace(rank.RankName) ? rank.RankName! : $"Rank {rank.Id}")
                    : rank.Description!));

        var profileRanks = profiles
            .Where(profile => profile.PositionCode.HasValue)
            .Select(profile => new RankOption(profile.PositionCode!.Value, $"Rank {profile.PositionCode.Value}"));

        return explicitRanks
            .Concat(profileRanks)
            .GroupBy(rank => rank.Value)
            .Select(group => group.First())
            .OrderBy(rank => rank.Label)
            .ThenBy(rank => rank.Value)
            .ToList();
    }

    public static string ResolveRankLabel(byte? positionCode, IReadOnlyList<RankOption> rankOptions)
    {
        if (!positionCode.HasValue)
        {
            return "-";
        }

        return rankOptions.FirstOrDefault(rank => rank.Value == positionCode.Value)?.Label
            ?? $"Rank {positionCode.Value}";
    }

    private static string BuildPath(string basePath, short? departmentCode, short? siteCode)
    {
        var query = BuildContextQuery(departmentCode, siteCode);
        return query.Count == 0 ? basePath : $"{basePath}?{string.Join("&", query)}";
    }

    private static List<string> BuildContextQuery(short? departmentCode, short? siteCode)
    {
        var query = new List<string>();
        if (departmentCode.HasValue)
        {
            query.Add($"departmentCode={departmentCode.Value}");
        }

        if (siteCode.HasValue)
        {
            query.Add($"siteCode={siteCode.Value}");
        }

        return query;
    }
}

internal sealed record RankOption(byte Value, string Label);

internal sealed class AuthoriserEditorModel
{
    public short? UserAccessCode { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int? PersalNumber { get; set; }
    public string? Telephone { get; set; }
    public byte? PositionCode { get; set; }

    public static AuthoriserEditorModel FromProfile(UserProfileDto profile)
        => new()
        {
            UserAccessCode = profile.UserAccessCode,
            FirstName = profile.FirstName ?? string.Empty,
            LastName = profile.LastName ?? string.Empty,
            PersalNumber = profile.PersalNumber,
            Telephone = profile.Telephone,
            PositionCode = profile.PositionCode
        };

    public UpdateUserProfileRequest ToUpdateRequest(short siteCode)
        => new()
        {
            FirstName = FirstName.Trim(),
            LastName = LastName.Trim(),
            Telephone = string.IsNullOrWhiteSpace(Telephone) ? null : Telephone.Trim(),
            SiteCode = siteCode,
            PositionCode = PositionCode,
            PersalNumber = PersalNumber
        };

    public CreateUserProfileRequest ToCreateRequest(short siteCode)
        => new()
        {
            FirstName = FirstName.Trim(),
            LastName = LastName.Trim(),
            Telephone = string.IsNullOrWhiteSpace(Telephone) ? null : Telephone.Trim(),
            SiteCode = siteCode,
            PositionCode = PositionCode,
            PersalNumber = PersalNumber,
            Password = "Temp#1234",
            AccessLevel = 1
        };
}
