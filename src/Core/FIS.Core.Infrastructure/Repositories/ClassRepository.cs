using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Vehicles;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories
{
    /// <summary>
    /// Repository implementation for Class entity operations
    /// Provides CRUD operations with audit trail and soft delete support
    /// </summary>
    public class ClassRepository : IClassRepository
    {
        private readonly FisDbContext _context;

        public ClassRepository(FisDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Gets a class by its unique code (excludes soft deleted)
        /// </summary>
        public async Task<Class?> GetByIdAsync(short classCode)
        {
            return await _context.Classes
                .Where(c => !c.is_deleted)
                .FirstOrDefaultAsync(c => c.class_code == classCode);
        }

        /// <summary>
        /// Gets all non-deleted classes
        /// </summary>
        public async Task<IEnumerable<Class>> GetAllAsync()
        {
            return await _context.Classes
                .Where(c => !c.is_deleted)
                .OrderBy(c => c.description)
                .ToListAsync();
        }

        /// <summary>
        /// Searches classes by description containing the search term
        /// </summary>
        public async Task<IEnumerable<Class>> SearchAsync(string searchTerm)
        {
            return await _context.Classes
                .Where(c => !c.is_deleted && 
                           (c.description != null && c.description.ToLower().Contains(searchTerm.ToLower())))
                .OrderBy(c => c.description)
                .ToListAsync();
        }

        /// <summary>
        /// Creates a new class with audit fields auto-populated
        /// </summary>
        public async Task<Class> CreateAsync(Class classEntity, int currentUserId)
        {
            // Auto-populate audit fields
            classEntity.date_created = DateTime.UtcNow;
            classEntity.created_by_user_code = currentUserId;
            classEntity.is_deleted = false;

            _context.Classes.Add(classEntity);
            await _context.SaveChangesAsync();
            return classEntity;
        }

        /// <summary>
        /// Updates an existing class with audit fields auto-populated
        /// </summary>
        public async Task<Class> UpdateAsync(Class classEntity, int currentUserId)
        {
            if (classEntity == null)
                throw new ArgumentNullException(nameof(classEntity));

            var existing = await _context.Classes.FindAsync(classEntity.class_code);
            if (existing == null)
                throw new InvalidOperationException($"Class with class_code {classEntity.class_code} not found");

            // Auto-populate audit fields
            classEntity.date_updated = DateTime.UtcNow;
            classEntity.modified_by_user_code = currentUserId;
            
            // Preserve creation audit fields
            classEntity.date_created = existing.date_created;
            classEntity.created_by_user_code = existing.created_by_user_code;
            classEntity.is_deleted = existing.is_deleted;

            _context.Entry(existing).CurrentValues.SetValues(classEntity);
            await _context.SaveChangesAsync();
            return existing;
        }

        /// <summary>
        /// Soft deletes a class by setting is_deleted flag
        /// </summary>
        public async Task DeleteAsync(short classCode, int currentUserId)
        {
            var classEntity = await _context.Classes.FindAsync(classCode);
            if (classEntity != null)
            {
                classEntity.is_deleted = true;
                classEntity.date_updated = DateTime.UtcNow;
                classEntity.modified_by_user_code = currentUserId;
                
                await _context.SaveChangesAsync();
            }
        }
    }
}
