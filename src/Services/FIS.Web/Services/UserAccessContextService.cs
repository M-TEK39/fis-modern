namespace FIS.Web.Services;

public class UserAccessContextService
{
    private readonly UserProfileApiService _userProfileApiService;
    private readonly ILogger<UserAccessContextService> _logger;

    private int? _userAccessCode;
    private long _accessLevel;
    private string? _firstName;

    public UserAccessContextService(
        UserProfileApiService userProfileApiService,
        ILogger<UserAccessContextService> logger)
    {
        _userProfileApiService = userProfileApiService;
        _logger = logger;
    }

    public long AccessLevel => _accessLevel;

    public async Task PrimeFromLoginAsync(int? userAccessCode, string? firstName)
    {
        if (userAccessCode is > 0)
        {
            _userAccessCode = userAccessCode;
        }

        if (!string.IsNullOrWhiteSpace(firstName))
        {
            _firstName = firstName.Trim();
        }

        await RefreshAsync();
    }

    public async Task<long> EnsureAccessLevelAsync(int? userAccessCode, string? firstName = null)
    {
        if (_accessLevel > 0)
        {
            return _accessLevel;
        }

        if (userAccessCode is > 0)
        {
            _userAccessCode = userAccessCode;
        }

        if (!string.IsNullOrWhiteSpace(firstName))
        {
            _firstName = firstName.Trim();
        }

        await RefreshAsync();
        return _accessLevel;
    }

    public bool HasPermission(long permissionBit)
    {
        return (_accessLevel & permissionBit) == permissionBit;
    }

    public void Clear()
    {
        _userAccessCode = null;
        _accessLevel = 0;
        _firstName = null;
    }

    private async Task RefreshAsync()
    {
        UserProfileDto? profile = null;

        if (_userAccessCode is > 0)
        {
            profile = await _userProfileApiService.GetByUserAccessCodeAsync(_userAccessCode.Value);
        }

        if (profile is null && !string.IsNullOrWhiteSpace(_firstName))
        {
            profile = await _userProfileApiService.GetByFirstNameAsync(_firstName);
        }

        if (profile is null)
        {
            _logger.LogWarning("Could not resolve user profile for access context.");
            return;
        }

        _userAccessCode = profile.UserAccessCode;
        _accessLevel = profile.AccessLevel;
        _firstName = profile.FirstName;
        _logger.LogInformation("Loaded user access context. UserAccessCode: {UserAccessCode}, AccessLevel: {AccessLevel}",
            _userAccessCode, _accessLevel);
    }
}
