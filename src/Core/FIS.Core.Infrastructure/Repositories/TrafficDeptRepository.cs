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
    public async Task<TrafficDept> CreateAsync(TrafficDept dept, int currentUserId) { _context.Set<TrafficDept>().Add(dept); await _context.SaveChangesAsync(); return dept; }
    public async Task<TrafficDept> UpdateAsync(TrafficDept dept, int currentUserId) { if (dept == null) throw new ArgumentNullException(nameof(dept)); var existing = await _context.Set<TrafficDept>().FindAsync(dept.Traffic_dept_code); if (existing == null) throw new InvalidOperationException($"TrafficDept with Traffic_dept_code {dept.Traffic_dept_code} not found"); _context.Entry(existing).CurrentValues.SetValues(dept); await _context.SaveChangesAsync(); return existing; }
    public async Task DeleteAsync(short deptCode, int currentUserId) { var dept = await GetByIdAsync(deptCode); if (dept != null) { _context.Set<TrafficDept>().Remove(dept); await _context.SaveChangesAsync(); } }
}
