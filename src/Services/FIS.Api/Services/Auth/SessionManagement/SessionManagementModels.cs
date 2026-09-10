namespace FIS.Api.Services.SessionManagement;

public enum SessionManagementStatus
{
    Succeeded,
    Unavailable,
    Failed,
}

public sealed record SessionStoreStatus(
    bool Available,
    string Description,
    DateTimeOffset CheckedAtUtc
);

public sealed record ActiveSession(
    int UserAccessCode,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    bool RememberMe,
    bool IsCurrent
);

public sealed record SessionListResult(
    SessionManagementStatus Status,
    IReadOnlyList<ActiveSession> Sessions,
    string Description
);

public sealed record SessionOperationResult(
    SessionManagementStatus Status,
    int AffectedSessionCount,
    string Description
);
