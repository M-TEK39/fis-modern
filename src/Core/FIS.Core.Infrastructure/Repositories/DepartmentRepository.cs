using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for department operations against legacy department table
/// Handles organizational department management for fleet administration
/// </summary>
public class DepartmentRepository : IDepartmentRepository
{
    private readonly FisDbContext _context;

    public DepartmentRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get department by department code (primary key)
    /// </summary>
    public async Task<Department?> GetByIdAsync(int departmentCode)
    {
        return await _context
            .Departments.Include(d => d.DefaultSite)
            .FirstOrDefaultAsync(d => d.department_code == departmentCode);
    }

    /// <summary>
    /// Get department by description/name
    /// </summary>
    public async Task<Department?> GetByNameAsync(string departmentName)
    {
        if (string.IsNullOrEmpty(departmentName))
            return null;

        return await _context
            .Departments.Include(d => d.DefaultSite)
            .FirstOrDefaultAsync(d => d.description == departmentName);
    }

    /// <summary>
    /// Get all active departments
    /// </summary>
    public async Task<IEnumerable<Department>> GetActiveDepartmentsAsync()
    {
        return await _context
            .Departments.Include(d => d.DefaultSite)
            .Where(d => d.dept_active)
            .OrderBy(d => d.description)
            .ToListAsync();
    }

    /// <summary>
    /// Get departments by company code
    /// </summary>
    public async Task<IEnumerable<Department>> GetByCompanyAsync(int companyCode)
    {
        return await _context
            .Departments.Include(d => d.DefaultSite)
            .Where(d => d.company_code == companyCode)
            .OrderBy(d => d.description)
            .ToListAsync();
    }

    /// <summary>
    /// Search departments by description, abbreviation, or notes
    /// </summary>
    public async Task<IEnumerable<Department>> SearchDepartmentsAsync(string searchTerm)
    {
        if (string.IsNullOrEmpty(searchTerm))
            return await GetActiveDepartmentsAsync();

        var search = searchTerm.ToLower();
        return await _context
            .Departments.Include(d => d.DefaultSite)
            .Where(d =>
                d.description != null && d.description.ToLower().Contains(search)
                || d.department_abbr != null && d.department_abbr.ToLower().Contains(search)
                || d.notes != null && d.notes.ToLower().Contains(search)
            )
            .OrderBy(d => d.description)
            .ToListAsync();
    }

    /// <summary>
    /// Create new department
    /// </summary>
    public async Task<Department> CreateAsync(Department department)
    {
        if (department == null)
            throw new ArgumentNullException(nameof(department));

        department.date_created = DateTime.Now;
        department.date_updated = DateTime.Now;

        _context.Departments.Add(department);
        await _context.SaveChangesAsync();

        return department;
    }

    /// <summary>
    /// Update existing department
    /// </summary>
    public async Task UpdateAsync(Department department)
    {
        if (department == null)
            throw new ArgumentNullException(nameof(department));

        department.date_updated = DateTime.Now;

        _context.Entry(department).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Delete department by ID
    /// </summary>
    public async Task DeleteAsync(int departmentCode)
    {
        var department = await _context.Departments.FindAsync(departmentCode);
        if (department != null)
        {
            _context.Departments.Remove(department);
            await _context.SaveChangesAsync();
        }
    }
}
