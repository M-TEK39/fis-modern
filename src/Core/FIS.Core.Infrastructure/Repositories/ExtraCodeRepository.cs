using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.ReferenceData;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories
{
    /// <summary>
    /// Repository implementation for ExtraCode entity operations
    /// Provides CRUD operations for extra codes/classifications
    /// </summary>
    public class ExtraCodeRepository : IExtraCodeRepository
    {
        private readonly FisDbContext _context;

        public ExtraCodeRepository(FisDbContext context)
        {
            _context = context;
        }

        public async Task<ExtraCode?> GetByIdAsync(short extraCode)
        {
            return await _context.ExtraCodes
                .Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(e => e.extra_code == extraCode);
        }

        public async Task<ExtraCode?> GetByDescriptionAsync(string description)
        {
            return await _context.ExtraCodes
                .Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(e => e.extra_description != null && e.extra_description.ToLower() == description.ToLower());
        }

        public async Task<IEnumerable<ExtraCode>> GetAllAsync()
        {
            return await _context.ExtraCodes
                .Where(x => !x.is_deleted)
                .OrderBy(e => e.extra_description)
                .ToListAsync();
        }

        public async Task<IEnumerable<ExtraCode>> SearchAsync(string searchTerm)
        {
            return await _context.ExtraCodes
                .Where(x => !x.is_deleted)
                .Where(e => e.extra_description != null && e.extra_description.ToLower().Contains(searchTerm.ToLower()))
                .OrderBy(e => e.extra_description)
                .ToListAsync();
        }

        public async Task<IEnumerable<ExtraCode>> GetByCategoryAsync(int categoryTypeCode)
        {
            return await _context.ExtraCodes
                .Where(x => !x.is_deleted)
                .Where(e => e.category_type_code == categoryTypeCode)
                .OrderBy(e => e.extra_description)
                .ToListAsync();
        }

        public async Task<ExtraCode> CreateAsync(ExtraCode extraCode, int currentUserId)
        {
            extraCode.date_created = DateTime.UtcNow;
            extraCode.created_by_user_code = currentUserId;
            extraCode.is_deleted = false;

            _context.ExtraCodes.Add(extraCode);
            await _context.SaveChangesAsync();
            return extraCode;
        }

        public async Task UpdateAsync(ExtraCode extraCode, int currentUserId)
        {
            if (extraCode == null)
                throw new ArgumentNullException(nameof(extraCode));

            var existing = await _context.ExtraCodes.FindAsync(extraCode.extra_code);
            if (existing == null || existing.is_deleted)
                throw new InvalidOperationException($"ExtraCode with extra_code {extraCode.extra_code} not found");

            extraCode.date_updated = DateTime.UtcNow;
            extraCode.modified_by_user_code = currentUserId;

            _context.Entry(existing).CurrentValues.SetValues(extraCode);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(short extraCode, int currentUserId)
        {
            var entity = await _context.ExtraCodes.FindAsync(extraCode);
            if (entity != null)
            {
                entity.is_deleted = true;
                entity.date_updated = DateTime.UtcNow;
                entity.modified_by_user_code = currentUserId;
                await _context.SaveChangesAsync();
            }
        }
    }
}
