using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
namespace FIS.Core.Infrastructure.Repositories;
public class VehicleAssessmentRepository : IVehicleAssessmentRepository
{
    private readonly FisDbContext _context;
    public VehicleAssessmentRepository(FisDbContext context) { _context = context; }
    public async Task<VehicleAssessment?> GetByIdAsync(int assessmentCode) { return await _context.Set<VehicleAssessment>().Include(v => v.Vehicle).FirstOrDefaultAsync(v => v.vehicle_assessment_code == assessmentCode); }
    public async Task<IEnumerable<VehicleAssessment>> GetAllAsync() { return await _context.Set<VehicleAssessment>().Include(v => v.Vehicle).ToListAsync(); }
    public async Task<VehicleAssessment?> GetByVehicleAsync(int vmfCode) { return await _context.Set<VehicleAssessment>().Where(v => v.vmf_code == vmfCode).Include(v => v.Vehicle).OrderByDescending(v => v.assessment_date).FirstOrDefaultAsync(); }
    public async Task<IEnumerable<VehicleAssessment>> GetRecentAssessmentsAsync(int days) { var cutoffDate = DateTime.Now.AddDays(-days); return await _context.Set<VehicleAssessment>().Where(v => v.assessment_date >= cutoffDate).Include(v => v.Vehicle).ToListAsync(); }
    public async Task<VehicleAssessment> CreateAsync(VehicleAssessment assessment) { _context.Set<VehicleAssessment>().Add(assessment); await _context.SaveChangesAsync(); return assessment; }
    public async Task<VehicleAssessment> UpdateAsync(VehicleAssessment assessment) { _context.Set<VehicleAssessment>().Update(assessment); await _context.SaveChangesAsync(); return assessment; }
    public async Task DeleteAsync(int assessmentCode) { var assessment = await GetByIdAsync(assessmentCode); if (assessment != null) { _context.Set<VehicleAssessment>().Remove(assessment); await _context.SaveChangesAsync(); } }
}
