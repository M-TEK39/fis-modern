using FIS.Core.Application.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using TypeEntity = FIS.Data.Entities.Type;

namespace FIS.Core.Infrastructure.Repositories
{
    /// <summary>
    /// Repository implementation for Type entity operations
    /// Provides CRUD operations for vehicle types/classifications
    /// </summary>
    public class TypeRepository : ITypeRepository
    {
        private readonly FisDbContext _context;

        public TypeRepository(FisDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Gets a type by its unique code
        /// </summary>
        /// <param name="typeCode">The type code to search for</param>
        /// <returns>Type entity if found, null otherwise</returns>
        public async Task<TypeEntity?> GetByIdAsync(short typeCode)
        {
            return await _context.Types
                .FirstOrDefaultAsync(t => t.type_code == typeCode);
        }

        /// <summary>
        /// Gets a type by its description/name
        /// </summary>
        /// <param name="typeName">The type description to search for</param>
        /// <returns>Type entity if found, null otherwise</returns>
        public async Task<TypeEntity?> GetByNameAsync(string typeName)
        {
            return await _context.Types
                .FirstOrDefaultAsync(t => t.type_description.ToLower() == typeName.ToLower());
        }

        /// <summary>
        /// Gets all types
        /// </summary>
        /// <returns>Collection of all type entities</returns>
        public async Task<IEnumerable<TypeEntity>> GetAllTypesAsync()
        {
            return await _context.Types
                .OrderBy(t => t.type_description)
                .ToListAsync();
        }

        /// <summary>
        /// Searches types by description containing the search term
        /// </summary>
        /// <param name="searchTerm">The search term to filter by</param>
        /// <returns>Collection of matching type entities</returns>
        public async Task<IEnumerable<TypeEntity>> SearchTypesAsync(string searchTerm)
        {
            return await _context.Types
                .Where(t => t.type_description.ToLower().Contains(searchTerm.ToLower()))
                .OrderBy(t => t.type_description)
                .ToListAsync();
        }

        /// <summary>
        /// Creates a new type
        /// </summary>
        /// <param name="type">The type entity to create</param>
        /// <returns>The created type entity</returns>
        public async Task<TypeEntity> CreateAsync(TypeEntity type)
        {
            _context.Types.Add(type);
            await _context.SaveChangesAsync();
            return type;
        }

        /// <summary>
        /// Updates an existing type
        /// </summary>
        /// <param name="type">The type entity to update</param>
        /// <returns>The updated type entity</returns>
        public async Task<TypeEntity> UpdateAsync(TypeEntity type)
        {
            _context.Entry(type).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return type;
        }

        /// <summary>
        /// Deletes a type by its code
        /// </summary>
        /// <param name="typeCode">The type code to delete</param>
        public async Task DeleteAsync(short typeCode)
        {
            var type = await _context.Types.FindAsync(typeCode);
            if (type != null)
            {
                _context.Types.Remove(type);
                await _context.SaveChangesAsync();
            }
        }
    }
}