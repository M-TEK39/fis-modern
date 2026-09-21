using System.Data;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Operations;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// dbo.third_party_projects has no is_deleted/audit columns. Column names
/// follow the 2012 archive, not the expanded entity aliases.
/// </summary>
public class ThirdPartyProjectRepository : IThirdPartyProjectRepository
{
    private readonly FisDbContext _context;

    public ThirdPartyProjectRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<ThirdPartyProject?> GetByIdAsync(int projectId)
    {
        return await _context
            .Set<ThirdPartyProject>()
            .FirstOrDefaultAsync(p => p.project_id == projectId);
    }

    public async Task<IEnumerable<ThirdPartyProject>> GetAllAsync()
    {
        return await _context
            .Set<ThirdPartyProject>()
            .OrderBy(p => p.description)
            .ToListAsync();
    }

    public async Task<IEnumerable<ThirdPartyProject>> GetByDepartmentAsync(short departmentCode)
    {
        return await _context
            .Set<ThirdPartyProject>()
            .Where(p => p.department_code == departmentCode)
            .OrderBy(p => p.description)
            .ToListAsync();
    }

    public async Task<ThirdPartyProject> CreateAsync(ThirdPartyProject project, int currentUserId)
    {
        _ = currentUserId;
        _context.Set<ThirdPartyProject>().Add(project);
        await _context.SaveChangesAsync();
        return project;
    }

    public async Task<ThirdPartyProject> UpdateAsync(ThirdPartyProject project, int currentUserId)
    {
        _ = currentUserId;
        var existing =
            await _context
                .Set<ThirdPartyProject>()
                .FirstOrDefaultAsync(p => p.project_id == project.project_id)
            ?? throw new KeyNotFoundException($"Project {project.project_id} not found");

        existing.department_code = project.department_code;
        existing.site_code = project.site_code;
        existing.description = project.description;
        existing.start_date = project.start_date;
        existing.end_date = project.end_date;
        existing.responsible_person = project.responsible_person;
        existing.rp_physical_address = project.rp_physical_address;
        existing.rp_postal_address = project.rp_postal_address;
        existing.rp_tel = project.rp_tel;
        existing.rp_fax = project.rp_fax;
        existing.rp_email = project.rp_email;
        existing.rp_cell = project.rp_cell;
        existing.notes = project.notes;
        existing.order_reference = project.order_reference;
        existing.class_configuration = project.class_configuration;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int projectId, int currentUserId)
    {
        _ = currentUserId;
        var project =
            await _context
                .Set<ThirdPartyProject>()
                .FirstOrDefaultAsync(p => p.project_id == projectId)
            ?? throw new KeyNotFoundException($"Project {projectId} not found");

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                IF COL_LENGTH('dbo.third_party_projects', 'is_deleted') IS NOT NULL
                    UPDATE [dbo].[third_party_projects]
                    SET [is_deleted] = 1
                    WHERE [Project_id] = @projectId;
                ELSE
                    DELETE FROM [dbo].[third_party_projects]
                    WHERE [Project_id] = @projectId;
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@projectId";
            parameter.DbType = DbType.Int32;
            parameter.Value = projectId;
            command.Parameters.Add(parameter);
            await command.ExecuteNonQueryAsync();
            _context.Entry(project).State = EntityState.Detached;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }
}
