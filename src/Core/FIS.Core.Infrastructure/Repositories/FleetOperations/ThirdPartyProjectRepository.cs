using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Operations;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Compatibility facade over the third-party rental repository. Project
/// writes use archived DEV_INS_Third_party_projects / DEV_UPD_Third_party_project
/// with labelled DML fallback; leftover EF is not used for mutations.
/// </summary>
public class ThirdPartyProjectRepository : IThirdPartyProjectRepository
{
    private readonly FisDbContext _context;
    private readonly IThirdPartyRentalRepository _rental;

    public ThirdPartyProjectRepository(
        FisDbContext context,
        IThirdPartyRentalRepository rental
    )
    {
        _context = context;
        _rental = rental;
    }

    public async Task<ThirdPartyProject?> GetByIdAsync(int projectId)
    {
        var record = await _rental.GetProjectAsync(projectId);
        return record is null ? null : Map(record);
    }

    public async Task<IEnumerable<ThirdPartyProject>> GetAllAsync()
    {
        var records = await _rental.GetProjectsAsync();
        return records.Select(Map).ToList();
    }

    public async Task<IEnumerable<ThirdPartyProject>> GetByDepartmentAsync(short departmentCode)
    {
        var records = await _rental.GetProjectsByDepartmentAsync(departmentCode);
        return records.Select(Map).ToList();
    }

    public async Task<ThirdPartyProject> CreateAsync(ThirdPartyProject project, int currentUserId)
    {
        var created = await _rental.CreateProjectAsync(ToWrite(project), currentUserId);
        return Map(created);
    }

    public async Task<ThirdPartyProject> UpdateAsync(ThirdPartyProject project, int currentUserId)
    {
        var updated = await _rental.UpdateProjectAsync(
            project.project_id,
            ToWrite(project),
            currentUserId
        );
        return Map(updated);
    }

    public async Task DeleteAsync(int projectId, int currentUserId)
    {
        await _rental.DeleteProjectAsync(projectId, currentUserId);
        var tracked = await _context
            .Set<ThirdPartyProject>()
            .FirstOrDefaultAsync(p => p.project_id == projectId);
        if (tracked is not null)
        {
            _context.Entry(tracked).State = EntityState.Detached;
        }
    }

    private static ThirdPartyProjectWrite ToWrite(ThirdPartyProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return new ThirdPartyProjectWrite(
            project.department_code ?? 0,
            project.site_code,
            project.description ?? string.Empty,
            project.start_date ?? default,
            project.end_date ?? default,
            project.responsible_person,
            project.rp_physical_address,
            project.rp_postal_address,
            project.rp_tel,
            project.rp_fax,
            project.rp_email,
            project.rp_cell,
            project.notes,
            project.order_reference,
            project.class_configuration
        );
    }

    private static ThirdPartyProject Map(ThirdPartyProjectRecord record) =>
        new()
        {
            project_id = record.project_id,
            department_code = record.department_code,
            site_code = record.site_code,
            description = record.description,
            start_date = record.start_date,
            end_date = record.end_date,
            responsible_person = record.responsible_person,
            rp_physical_address = record.rp_physical_address,
            rp_postal_address = record.rp_postal_address,
            rp_tel = record.rp_tel,
            rp_fax = record.rp_fax,
            rp_email = record.rp_email,
            rp_cell = record.rp_cell,
            notes = record.notes,
            order_reference = record.order_reference,
            class_configuration = record.class_configuration,
        };
}
