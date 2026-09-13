using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces
{
    /// <summary>
    /// Repository interface for Auction entity operations
    /// Provides contract for CRUD operations on vehicle auctions
    /// </summary>
    public interface IAuctionRepository
    {
        Task<Auction?> GetByIdAsync(short auctionCode);
        Task<IEnumerable<Auction>> GetAllAsync();
        Task<IEnumerable<Auction>> GetByVehicleAsync(int vmfCode);
        Task<AuctionReportPage> GetOneVehicleReportPageAsync(
            AuctionOneVehicleReportPageQuery query
        );
        Task<AuctionReportPage> GetAllVehiclesReportPageAsync(
            AuctionAllVehiclesReportPageQuery query
        );
        Task<AuctionReportPage> GetSaleToNameReportPageAsync(
            AuctionSaleToNameReportPageQuery query
        );
        Task<AuctionReportPage> GetAuctionGgReportPageAsync(AuctionGgReportPageQuery query);
        Task<AuctionReportPage> GetAuctionLotReportPageAsync(AuctionLotReportPageQuery query);
        Task<Auction> CreateAsync(Auction auction, int currentUserId);
        Task<Auction> UpdateAsync(Auction auction, int currentUserId);
        Task<Auction> UpdateMaintenanceAsync(Auction auction, int currentUserId);
        Task DeleteAsync(short auctionCode, int currentUserId);
    }

    public sealed record AuctionOneVehicleReportPageQuery(
        int VmfCode,
        int Page = 1,
        int PageSize = 24
    );

    public sealed record AuctionAllVehiclesReportPageQuery(
        DateTime StartDate,
        DateTime EndDate,
        string? AuctionNumber,
        string? Garage,
        int Page = 1,
        int PageSize = 24
    );

    public sealed record AuctionSaleToNameReportPageQuery(
        string? BuyerName,
        DateTime StartDate,
        DateTime EndDate,
        int Page = 1,
        int PageSize = 24
    );

    public sealed record AuctionGgReportPageQuery(
        string? GGNumber,
        string? AuctionNumber,
        string? Garage,
        int Page = 1,
        int PageSize = 24
    );

    public sealed record AuctionLotReportPageQuery(
        string? LotNumber,
        string? AuctionNumber,
        string? Garage,
        int Page = 1,
        int PageSize = 24
    );

    public sealed record AuctionReportPage(
        IReadOnlyList<Auction> Items,
        int Page,
        int PageSize,
        int Total
    )
    {
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    }
}
