using System.Data;
using FIS.Core.Application.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TypeEntity = FIS.Core.Domain.Entities.ReferenceData.VehicleType;

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
            return await _context
                .VehicleTypes.Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(t => t.type_code == typeCode);
        }

        /// <summary>
        /// Gets a type by its description/name
        /// </summary>
        /// <param name="typeName">The type description to search for</param>
        /// <returns>Type entity if found, null otherwise</returns>
        public async Task<TypeEntity?> GetByNameAsync(string typeName)
        {
            return await _context
                .VehicleTypes.Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(t => t.type_description.ToLower() == typeName.ToLower());
        }

        /// <summary>
        /// Gets all types
        /// </summary>
        /// <returns>Collection of all type entities</returns>
        public async Task<IEnumerable<TypeEntity>> GetAllTypesAsync()
        {
            // The legacy dbo.type table contains only type_code and
            // type_description. Vehicle capture needs that exact selector;
            // do not project expanded audit columns which are absent on the
            // restored client schema.
            return await QueryLegacyTypesAsync();
        }

        private async Task<List<TypeEntity>> QueryLegacyTypesAsync()
        {
            var connection = _context.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync();
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
                command.CommandText = """
                    SELECT [type_code], [type_description]
                    FROM [dbo].[type]
                    ORDER BY [type_description], [type_code]
                    """;

                var types = new List<TypeEntity>();
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var typeCode = reader.IsDBNull(0) ? (short)0 : Convert.ToInt16(reader.GetValue(0));
                    var description = reader.IsDBNull(1) ? string.Empty : reader.GetString(1).Trim();
                    if (typeCode <= 0 || string.IsNullOrWhiteSpace(description))
                    {
                        continue;
                    }

                    types.Add(new TypeEntity { type_code = typeCode, type_description = description });
                }

                return types;
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        /// <summary>
        /// Searches types by description containing the search term
        /// </summary>
        /// <param name="searchTerm">The search term to filter by</param>
        /// <returns>Collection of matching type entities</returns>
        public async Task<IEnumerable<TypeEntity>> SearchTypesAsync(string searchTerm)
        {
            return await _context
                .VehicleTypes.Where(t =>
                    t.type_description.ToLower().Contains(searchTerm.ToLower())
                )
                .OrderBy(t => t.type_description)
                .ToListAsync();
        }

        public async Task<TypePage> GetPageAsync(int page, int pageSize)
        {
            var resolvedPage = Math.Max(1, page);
            var resolvedPageSize = Math.Clamp(pageSize, 1, 100);
            var query = _context.VehicleTypes.AsNoTracking().Where(type => !type.is_deleted);
            var total = await query.CountAsync();
            var items = await query
                .OrderBy(type => type.type_description)
                .ThenBy(type => type.type_code)
                .Skip((resolvedPage - 1) * resolvedPageSize)
                .Take(resolvedPageSize)
                .ToListAsync();
            return new TypePage(items, resolvedPage, resolvedPageSize, total);
        }

        /// <summary>
        /// Creates a new type
        /// </summary>
        /// <param name="type">The type entity to create</param>
        /// <returns>The created type entity</returns>
        public async Task<TypeEntity> CreateAsync(TypeEntity type, int currentUserId)
        {
            // Auto-populate audit fields
            type.date_created = DateTime.UtcNow;
            type.is_deleted = false;

            _context.VehicleTypes.Add(type);
            await _context.SaveChangesAsync();
            return type;
        }

        /// <summary>
        /// Updates an existing type
        /// </summary>
        /// <param name="type">The type entity to update</param>
        /// <returns>The updated type entity</returns>
        public async Task<TypeEntity> UpdateAsync(TypeEntity type, int currentUserId)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            var existing = await _context.VehicleTypes.FindAsync(type.type_code);
            if (existing == null)
                throw new InvalidOperationException(
                    $"Type with type_code {type.type_code} not found"
                );

            _context.Entry(existing).CurrentValues.SetValues(type);
            await _context.SaveChangesAsync();
            return existing;
        }

        /// <summary>
        /// Deletes a type by its code
        /// </summary>
        /// <param name="typeCode">The type code to delete</param>
        public async Task DeleteAsync(short typeCode, int currentUserId)
        {
            var type = await _context.VehicleTypes.FindAsync(typeCode);
            if (type != null)
            {
                // Soft delete instead of hard delete
                type.is_deleted = true;
                type.date_updated = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }
}
