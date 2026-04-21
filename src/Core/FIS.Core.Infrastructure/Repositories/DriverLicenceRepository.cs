using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Drivers;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories
{
    /// <summary>
    /// Repository implementation for DriverLicence entity operations
    /// Provides CRUD operations for driver licence types
    /// </summary>
    public class DriverLicenceRepository : IDriverLicenceRepository
    {
        private readonly FisDbContext _context;

        public DriverLicenceRepository(FisDbContext context)
        {
            _context = context;
        }

        public async Task<DriverLicence?> GetByIdAsync(short licenceCode)
        {
            return await _context.DriverLicences
                .Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(d => d.licence_code == licenceCode);
        }

        public async Task<DriverLicence?> GetByDescriptionAsync(string description)
        {
            return await _context.DriverLicences
                .Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(d => d.description != null && d.description.ToLower() == description.ToLower());
        }

        public async Task<IEnumerable<DriverLicence>> GetAllAsync()
        {
            return await _context.DriverLicences
                .Where(x => !x.is_deleted)
                .OrderBy(d => d.description)
                .ToListAsync();
        }

        public async Task<IEnumerable<DriverLicence>> SearchAsync(string searchTerm)
        {
            return await _context.DriverLicences
                .Where(x => !x.is_deleted)
                .Where(d => d.description != null && d.description.ToLower().Contains(searchTerm.ToLower()))
                .OrderBy(d => d.description)
                .ToListAsync();
        }

        public async Task<DriverLicence> CreateAsync(DriverLicence driverLicence, int currentUserId)
        {
            driverLicence.date_created = DateTime.UtcNow;
            driverLicence.created_by_user_code = currentUserId;
            driverLicence.is_deleted = false;

            _context.DriverLicences.Add(driverLicence);
            await _context.SaveChangesAsync();
            return driverLicence;
        }

        public async Task UpdateAsync(DriverLicence driverLicence, int currentUserId)
        {
            if (driverLicence == null)
                throw new ArgumentNullException(nameof(driverLicence));

            var existing = await _context.DriverLicences.FindAsync(driverLicence.licence_code);
            if (existing == null || existing.is_deleted)
                throw new InvalidOperationException($"DriverLicence with licence_code {driverLicence.licence_code} not found");

            driverLicence.date_updated = DateTime.UtcNow;
            driverLicence.modified_by_user_code = currentUserId;

            _context.Entry(existing).CurrentValues.SetValues(driverLicence);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(short licenceCode, int currentUserId)
        {
            var driverLicence = await _context.DriverLicences.FindAsync(licenceCode);
            if (driverLicence != null)
            {
                driverLicence.is_deleted = true;
                driverLicence.date_updated = DateTime.UtcNow;
                driverLicence.modified_by_user_code = currentUserId;
                await _context.SaveChangesAsync();
            }
        }
    }
}
