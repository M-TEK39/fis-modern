using FIS.Core.Domain.Entities;
namespace FIS.Core.Application.Interfaces;
public interface IVehicleAssessmentRepository
{
    Task<VehicleAssessment?> GetByIdAsync(int assessmentCode);
    Task<IEnumerable<VehicleAssessment>> GetAllAsync();
    Task<VehicleAssessment?> GetByVehicleAsync(int vmfCode);
    Task<IEnumerable<VehicleAssessment>> GetRecentAssessmentsAsync(int days);
    Task<VehicleAssessment> CreateAsync(VehicleAssessment assessment);
    Task<VehicleAssessment> UpdateAsync(VehicleAssessment assessment);
    Task DeleteAsync(int assessmentCode);
}
