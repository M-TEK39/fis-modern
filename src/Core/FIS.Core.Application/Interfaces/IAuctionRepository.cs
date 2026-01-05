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
        Task<Auction> CreateAsync(Auction auction);
        Task<Auction> UpdateAsync(Auction auction);
        Task DeleteAsync(short auctionCode);
    }
}
