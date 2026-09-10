using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Operations;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

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
            .Include(p => p.Department)
            .Include(p => p.Site)
            .FirstOrDefaultAsync(p => p.project_id == projectId && !p.is_deleted);
    }

    public async Task<IEnumerable<ThirdPartyProject>> GetAllAsync()
    {
        return await _context
            .Set<ThirdPartyProject>()
            .Include(p => p.Department)
            .Include(p => p.Site)
            .Where(p => !p.is_deleted)
            .OrderByDescending(p => p.date_created)
            .ToListAsync();
    }

    public async Task<IEnumerable<ThirdPartyProject>> GetByDepartmentAsync(short departmentCode)
    {
        return await _context
            .Set<ThirdPartyProject>()
            .Include(p => p.Department)
            .Include(p => p.Site)
            .Where(p => p.department_code == departmentCode && !p.is_deleted)
            .OrderByDescending(p => p.date_created)
            .ToListAsync();
    }

    public async Task<ThirdPartyProject> CreateAsync(ThirdPartyProject project, int currentUserId)
    {
        project.date_created = DateTime.UtcNow;
        project.created_by_user_code = currentUserId;
        project.is_deleted = false;

        _context.Set<ThirdPartyProject>().Add(project);
        await _context.SaveChangesAsync();
        return project;
    }

    public async Task<ThirdPartyProject> UpdateAsync(ThirdPartyProject project, int currentUserId)
    {
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
        existing.date_updated = DateTime.UtcNow;
        existing.modified_by_user_code = currentUserId;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int projectId, int currentUserId)
    {
        var project =
            await _context
                .Set<ThirdPartyProject>()
                .FirstOrDefaultAsync(p => p.project_id == projectId)
            ?? throw new KeyNotFoundException($"Project {projectId} not found");

        // Soft delete
        project.is_deleted = true;
        project.date_updated = DateTime.UtcNow;
        project.modified_by_user_code = currentUserId;

        await _context.SaveChangesAsync();
    }
}
