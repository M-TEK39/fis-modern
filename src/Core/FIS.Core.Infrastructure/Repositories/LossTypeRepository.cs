using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.ReferenceData;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories
{
    /// <summary>
    /// Repository implementation for LossType entity operations
    /// Provides CRUD operations for loss types/classifications
    /// </summary>
    public class LossTypeRepository : ILossTypeRepository
    {
        private readonly FisDbContext _context;

        public LossTypeRepository(FisDbContext context)
        {
            _context = context;
        }

        public async Task<LossType?> GetByIdAsync(short lossTypeCode)
        {
            return await _context.LossTypes
                .Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(l => l.loss_type_code == lossTypeCode);
        }

        public async Task<LossType?> GetByDescriptionAsync(string description)
        {
            return await _context.LossTypes
                .Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(l => l.loss_description != null && l.loss_description.ToLower() == description.ToLower());
        }

        public async Task<IEnumerable<LossType>> GetAllAsync()
        {
            return await _context.LossTypes
                .Where(x => !x.is_deleted)
                .OrderBy(l => l.loss_description)
                .ToListAsync();
        }

        public async Task<IEnumerable<LossType>> SearchAsync(string searchTerm)
        {
            return await _context.LossTypes
                .Where(x => !x.is_deleted)
                .Where(l => l.loss_description != null && l.loss_description.ToLower().Contains(searchTerm.ToLower()))
                .OrderBy(l => l.loss_description)
                .ToListAsync();
        }

        public async Task<LossType> CreateAsync(LossType lossType, int currentUserId)
        {
            lossType.date_created = DateTime.UtcNow;
            lossType.created_by_user_code = currentUserId;
            lossType.is_deleted = false;

            _context.LossTypes.Add(lossType);
            await _context.SaveChangesAsync();
            return lossType;
        }

        public async Task UpdateAsync(LossType lossType, int currentUserId)
        {
            if (lossType == null)
                throw new ArgumentNullException(nameof(lossType));

            var existing = await _context.LossTypes.FindAsync(lossType.loss_type_code);
            if (existing == null || existing.is_deleted)
                throw new InvalidOperationException($"LossType with loss_type_code {lossType.loss_type_code} not found");

            lossType.date_updated = DateTime.UtcNow;
            lossType.modified_by_user_code = currentUserId;

            _context.Entry(existing).CurrentValues.SetValues(lossType);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(short lossTypeCode, int currentUserId)
        {
            var lossType = await _context.LossTypes.FindAsync(lossTypeCode);
            if (lossType != null)
            {
                lossType.is_deleted = true;
                lossType.date_updated = DateTime.UtcNow;
                lossType.modified_by_user_code = currentUserId;
                await _context.SaveChangesAsync();
            }
        }
    }
}
