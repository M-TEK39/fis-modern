using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces;

public interface ILeaseContractTermsRepository
{
    Task<LeaseContractTerms?> GetByIdAsync(int termId);
    Task<IEnumerable<LeaseContractTerms>> GetAllAsync();
    Task<LeaseContractTermsPage> GetPageAsync(LeaseContractTermsPageQuery query);
    Task<LeaseContractTerms?> GetByVehicleAsync(int vmfCode);
    Task<string?> GetCapturedByUsernameAsync(int vmfCode);
    Task<IEnumerable<LeaseContractTerms>> GetActiveTermsAsync();
    Task<LeaseContractTerms> CreateAsync(LeaseContractTerms terms, int currentUserId);
    Task<LeaseContractTerms> UpdateAsync(LeaseContractTerms terms, int currentUserId);
    Task<LeaseContractTerms> CaptureOrResubmitAsync(
        LeaseContractTerms terms,
        string username,
        int currentUserId = 0
    );
    Task<LeaseContractTerms> AuthorizeAsync(
        LeaseContractTerms terms,
        string comment,
        string username,
        int currentUserId = 0
    );
    Task<LeaseContractTerms> RejectAsync(
        LeaseContractTerms terms,
        string comment,
        string username,
        int currentUserId = 0
    );
    Task<LeaseContractTerms> RecallAsync(LeaseContractTerms terms);
    Task DeleteAsync(int termId, int currentUserId);
}

public sealed record LeaseContractTermsPageQuery(
    int Page = 1,
    int PageSize = 24,
    string? Search = null,
    string? Mode = null,
    string? Status = null,
    IReadOnlySet<short>? AllowedSiteCodes = null,
    int? CurrentUserId = null
);

public sealed record LeaseContractTermsPage(
    IReadOnlyList<LeaseContractTerms> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}
