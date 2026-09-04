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
        Task<Auction> CreateAsync(Auction auction, int currentUserId);
        Task<Auction> UpdateAsync(Auction auction, int currentUserId);
        Task<Auction> UpdateMaintenanceAsync(Auction auction, int currentUserId);
        Task DeleteAsync(short auctionCode, int currentUserId);
    }
}
