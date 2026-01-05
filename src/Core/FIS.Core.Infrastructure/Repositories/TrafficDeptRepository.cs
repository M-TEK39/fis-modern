using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
namespace FIS.Core.Infrastructure.Repositories;
public class TrafficDeptRepository : ITrafficDeptRepository
{
    private readonly FisDbContext _context;
    public TrafficDeptRepository(FisDbContext context) { _context = context; }
    public async Task<TrafficDept?> GetByIdAsync(short deptCode) { return await _context.Set<TrafficDept>().FirstOrDefaultAsync(d => d.Traffic_dept_code == deptCode); }
    public async Task<IEnumerable<TrafficDept>> GetAllAsync() { return await _context.Set<TrafficDept>().ToListAsync(); }
    public async Task<TrafficDept?> GetByNameAsync(string name) { return await _context.Set<TrafficDept>().FirstOrDefaultAsync(d => d.Traf_name == name); }
    public async Task<TrafficDept> CreateAsync(TrafficDept dept) { _context.Set<TrafficDept>().Add(dept); await _context.SaveChangesAsync(); return dept; }
    public async Task<TrafficDept> UpdateAsync(TrafficDept dept) { _context.Set<TrafficDept>().Update(dept); await _context.SaveChangesAsync(); return dept; }
    public async Task DeleteAsync(short deptCode) { var dept = await GetByIdAsync(deptCode); if (dept != null) { _context.Set<TrafficDept>().Remove(dept); await _context.SaveChangesAsync(); } }
}
