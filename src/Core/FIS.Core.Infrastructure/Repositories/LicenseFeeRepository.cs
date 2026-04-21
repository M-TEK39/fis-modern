using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.ReferenceData;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories
{
    /// <summary>
    /// Repository implementation for LicenseFee entity operations
    /// Provides CRUD operations for licence fees
    /// </summary>
    public class LicenseFeeRepository : ILicenseFeeRepository
    {
        private readonly FisDbContext _context;

        public LicenseFeeRepository(FisDbContext context)
        {
            _context = context;
        }

        public async Task<LicenseFee?> GetByIdAsync(short licenceFeeCode)
        {
            return await _context.LicenseFees
                .Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(l => l.licence_fee_code == licenceFeeCode);
        }

        public async Task<LicenseFee?> GetByDescriptionAsync(string description)
        {
            return await _context.LicenseFees
                .Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(l => l.licence_description != null && l.licence_description.ToLower() == description.ToLower());
        }

        public async Task<IEnumerable<LicenseFee>> GetAllAsync()
        {
            return await _context.LicenseFees
                .Where(x => !x.is_deleted)
                .OrderBy(l => l.licence_description)
                .ToListAsync();
        }

        public async Task<IEnumerable<LicenseFee>> SearchAsync(string searchTerm)
        {
            return await _context.LicenseFees
                .Where(x => !x.is_deleted)
                .Where(l => l.licence_description != null && l.licence_description.ToLower().Contains(searchTerm.ToLower()))
                .OrderBy(l => l.licence_description)
                .ToListAsync();
        }

        public async Task<LicenseFee> CreateAsync(LicenseFee licenseFee, int currentUserId)
        {
            licenseFee.date_created = DateTime.UtcNow;
            licenseFee.created_by_user_code = currentUserId;
            licenseFee.is_deleted = false;

            _context.LicenseFees.Add(licenseFee);
            await _context.SaveChangesAsync();
            return licenseFee;
        }

        public async Task UpdateAsync(LicenseFee licenseFee, int currentUserId)
        {
            if (licenseFee == null)
                throw new ArgumentNullException(nameof(licenseFee));

            var existing = await _context.LicenseFees.FindAsync(licenseFee.licence_fee_code);
            if (existing == null || existing.is_deleted)
                throw new InvalidOperationException($"LicenseFee with licence_fee_code {licenseFee.licence_fee_code} not found");

            licenseFee.date_updated = DateTime.UtcNow;
            licenseFee.modified_by_user_code = currentUserId;

            _context.Entry(existing).CurrentValues.SetValues(licenseFee);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(short licenceFeeCode, int currentUserId)
        {
            var licenseFee = await _context.LicenseFees.FindAsync(licenceFeeCode);
            if (licenseFee != null)
            {
                licenseFee.is_deleted = true;
                licenseFee.date_updated = DateTime.UtcNow;
                licenseFee.modified_by_user_code = currentUserId;
                await _context.SaveChangesAsync();
            }
        }
    }
}
