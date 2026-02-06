using FIS.Core.Application.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using FuelTypeEntity = FIS.Core.Domain.Entities.ReferenceData.FuelType;

namespace FIS.Core.Infrastructure.Repositories
{
    /// <summary>
    /// Repository implementation for FuelType entity operations
    /// Provides CRUD operations for vehicle fuel types/classifications
    /// </summary>
    public class FuelTypeRepository : IFuelTypeRepository
    {
        private readonly FisDbContext _context;

        public FuelTypeRepository(FisDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Gets a fuel type by its unique code
        /// </summary>
        /// <param name="fuelTypeCode">The fuel type code to search for</param>
        /// <returns>FuelType entity if found, null otherwise</returns>
        public async Task<FuelTypeEntity?> GetByIdAsync(short fuelTypeCode)
        {
            return await _context.FuelTypes
                .Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(ft => ft.fuel_type_code == fuelTypeCode);
        }

        /// <summary>
        /// Gets a fuel type by its description/name
        /// </summary>
        /// <param name="fuelDescription">The fuel description to search for</param>
        /// <returns>FuelType entity if found, null otherwise</returns>
        public async Task<FuelTypeEntity?> GetByDescriptionAsync(string fuelDescription)
        {
            return await _context.FuelTypes
                .Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(ft => ft.fuel_description != null && ft.fuel_description.ToLower() == fuelDescription.ToLower());
        }

        /// <summary>
        /// Gets all fuel types
        /// </summary>
        /// <returns>Collection of all fuel type entities</returns>
        public async Task<IEnumerable<FuelTypeEntity>> GetAllFuelTypesAsync()
        {
            return await _context.FuelTypes
                .Where(x => !x.is_deleted)
                .OrderBy(ft => ft.fuel_description)
                .ToListAsync();
        }

        /// <summary>
        /// Searches fuel types by description containing the search term
        /// </summary>
        /// <param name="searchTerm">The search term to filter by</param>
        /// <returns>Collection of matching fuel type entities</returns>
        public async Task<IEnumerable<FuelTypeEntity>> SearchFuelTypesAsync(string searchTerm)
        {
            return await _context.FuelTypes
                .Where(ft => ft.fuel_description != null && ft.fuel_description.ToLower().Contains(searchTerm.ToLower()))
                .OrderBy(ft => ft.fuel_description)
                .ToListAsync();
        }

        /// <summary>
        /// Creates a new fuel type
        /// </summary>
        /// <param name="fuelType">The fuel type entity to create</param>
        /// <param name="currentUserId">The ID of the user performing the action</param>
        /// <returns>The created fuel type entity</returns>
        public async Task<FuelTypeEntity> CreateAsync(FuelTypeEntity fuelType, int currentUserId)
        {
            // Auto-populate audit fields
            fuelType.date_created = DateTime.UtcNow;
            fuelType.created_by_user_code = currentUserId;
            fuelType.is_deleted = false;
            
            _context.FuelTypes.Add(fuelType);
            await _context.SaveChangesAsync();
            return fuelType;
        }

        /// <summary>
        /// Updates an existing fuel type
        /// </summary>
        /// <param name="fuelType">The fuel type entity to update</param>
        /// <param name="currentUserId">The user updating the fuel type</param>
        /// <returns>The updated fuel type entity</returns>
        public async Task<FuelTypeEntity> UpdateAsync(FuelTypeEntity fuelType, int currentUserId)
        {
            if (fuelType == null)
                throw new ArgumentNullException(nameof(fuelType));

            var existing = await _context.FuelTypes.FindAsync(fuelType.fuel_type_code);
            if (existing == null)
                throw new InvalidOperationException($"FuelType with fuel_type_code {fuelType.fuel_type_code} not found");

            // Preserve creation audit fields
            fuelType.date_created = existing.date_created;
            fuelType.created_by_user_code = existing.created_by_user_code;
            // Set update audit fields
            fuelType.date_updated = DateTime.UtcNow;
            fuelType.modified_by_user_code = currentUserId;
            
            _context.Entry(existing).CurrentValues.SetValues(fuelType);
            await _context.SaveChangesAsync();
            return existing;
        }

        /// <summary>
        /// Deletes a fuel type by its code
        /// </summary>
        /// <param name="fuelTypeCode">The fuel type code to delete</param>
        /// <param name="currentUserId">The user deleting the fuel type</param>
        public async Task DeleteAsync(short fuelTypeCode, int currentUserId)
        {
            var fuelType = await _context.FuelTypes.FindAsync(fuelTypeCode);
            if (fuelType != null)
            {
                // Soft delete instead of hard delete
                fuelType.is_deleted = true;
                fuelType.date_updated = DateTime.UtcNow;
                fuelType.modified_by_user_code = currentUserId;
                await _context.SaveChangesAsync();
            }
        }
    }
}