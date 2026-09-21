using System.Data;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Auth;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// dbo.AccessLevels is AccessLevelID, AccessLevelName, AccessLevelValue, and
/// computed AccessLevelCalc. There is no is_deleted or audit column.
/// </summary>
public class AccessLevelRepository : IAccessLevelRepository
{
    private readonly FisDbContext _context;

    public AccessLevelRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<AccessLevel?> GetByIdAsync(short accessLevelId)
    {
        return await _context.AccessLevels.FirstOrDefaultAsync(a =>
            a.AccessLevelID == accessLevelId
        );
    }

    public async Task<AccessLevel?> GetByNameAsync(string accessLevelName)
    {
        if (string.IsNullOrWhiteSpace(accessLevelName))
            return null;

        var normalized = accessLevelName.Trim();
        return await _context.AccessLevels.FirstOrDefaultAsync(a =>
            a.AccessLevelName != null && a.AccessLevelName == normalized
        );
    }

    public async Task<IEnumerable<AccessLevel>> GetAllAsync()
    {
        return await _context.AccessLevels.OrderBy(a => a.AccessLevelValue).ToListAsync();
    }

    public async Task<AccessLevel> CreateAsync(AccessLevel accessLevel, int currentUserId)
    {
        _ = currentUserId;
        _context.AccessLevels.Add(accessLevel);
        await _context.SaveChangesAsync();
        return accessLevel;
    }

    public async Task UpdateAsync(AccessLevel accessLevel, int currentUserId)
    {
        _ = currentUserId;
        var existing = await _context.AccessLevels.FirstOrDefaultAsync(a =>
            a.AccessLevelID == accessLevel.AccessLevelID
        );

        if (existing == null)
            throw new KeyNotFoundException(
                $"Access level with ID {accessLevel.AccessLevelID} not found"
            );

        existing.AccessLevelName = accessLevel.AccessLevelName;
        existing.AccessLevelValue = accessLevel.AccessLevelValue;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(short accessLevelId, int currentUserId)
    {
        _ = currentUserId;
        var accessLevel = await GetByIdAsync(accessLevelId);
        if (accessLevel == null)
            throw new KeyNotFoundException($"Access level with ID {accessLevelId} not found");

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                IF COL_LENGTH('dbo.AccessLevels', 'is_deleted') IS NOT NULL
                    UPDATE [dbo].[AccessLevels]
                    SET [is_deleted] = 1
                    WHERE [AccessLevelID] = @accessLevelId;
                ELSE
                    DELETE FROM [dbo].[AccessLevels]
                    WHERE [AccessLevelID] = @accessLevelId;
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@accessLevelId";
            parameter.DbType = DbType.Int16;
            parameter.Value = accessLevelId;
            command.Parameters.Add(parameter);
            await command.ExecuteNonQueryAsync();
            _context.Entry(accessLevel).State = EntityState.Detached;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    public async Task<bool> UserHasPermissionAsync(long userAccessLevel, string permissionName)
    {
        var permission = await GetByNameAsync(permissionName);
        if (permission == null)
            return false;

        return (userAccessLevel & permission.AccessLevelValue) == permission.AccessLevelValue;
    }

    public async Task<IEnumerable<string>> GetUserPermissionsAsync(long userAccessLevel)
    {
        var allPermissions = await GetAllAsync();
        var userPermissions = new List<string>();

        foreach (var permission in allPermissions)
        {
            if ((userAccessLevel & permission.AccessLevelValue) == permission.AccessLevelValue)
            {
                if (!string.IsNullOrEmpty(permission.AccessLevelName))
                {
                    userPermissions.Add(permission.AccessLevelName);
                }
            }
        }

        return userPermissions;
    }
}
