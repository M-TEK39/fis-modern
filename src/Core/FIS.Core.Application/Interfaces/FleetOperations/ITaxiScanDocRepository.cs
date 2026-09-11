using FIS.Core.Domain.Entities.Operations;

namespace FIS.Core.Application.Interfaces;

public interface ITaxiScanDocRepository
{
    Task<TaxiScanDoc?> GetByIdAsync(int scanDocCode);
    Task<IEnumerable<TaxiScanDoc>> GetAllAsync();
    Task<TaxiScanDocPage> GetPageAsync(TaxiScanDocPageQuery query);
    Task<IEnumerable<TaxiScanDoc>> GetByVehicleAsync(int vmfCode);
    Task<TaxiScanDoc> CreateAsync(TaxiScanDoc document, int currentUserId);
    Task DeleteAsync(int scanDocCode, int currentUserId);
}

public sealed record TaxiScanDocPageQuery(
    int Page = 1,
    int PageSize = 24,
    string? SearchTerm = null
);

public sealed record TaxiScanDocPageItem(
    TaxiScanDoc Document,
    string? FleetNumber,
    string? RegistrationNumber
);

public sealed record TaxiScanDocPage(
    IReadOnlyList<TaxiScanDocPageItem> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}
