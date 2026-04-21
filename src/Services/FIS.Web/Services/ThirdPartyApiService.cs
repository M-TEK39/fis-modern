using FIS.Web.Models;
using Microsoft.Extensions.Logging;

namespace FIS.Web.Services;

public class ThirdPartyApiService(HttpClient httpClient, TokenService tokenService, ILogger<ThirdPartyApiService> logger)
    : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/thirdparty";

    public Task<List<ThirdPartySupplierDto>> GetSuppliersAsync() =>
        GetListAsync<ThirdPartySupplierDto>($"{BasePath}/suppliers");

    public Task<ThirdPartySupplierDto?> GetSupplierAsync(int id) =>
        GetAsync<ThirdPartySupplierDto>($"{BasePath}/suppliers/{id}");

    public Task<ThirdPartySupplierDto?> CreateSupplierAsync(ThirdPartySupplierCreateDto payload) =>
        PostAsync<ThirdPartySupplierCreateDto, ThirdPartySupplierDto>($"{BasePath}/suppliers", payload);

    public Task<ThirdPartySupplierDto?> UpdateSupplierAsync(int id, ThirdPartySupplierCreateDto payload) =>
        PutAsync<ThirdPartySupplierCreateDto, ThirdPartySupplierDto>($"{BasePath}/suppliers/{id}", payload);

    public Task<List<ThirdPartyProjectDto>> GetProjectsAsync() =>
        GetListAsync<ThirdPartyProjectDto>($"{BasePath}/projects");

    public Task<List<ThirdPartyProjectDto>> GetProjectsByDepartmentAsync(short departmentCode) =>
        GetListAsync<ThirdPartyProjectDto>($"{BasePath}/projects/department/{departmentCode}");

    public Task<ThirdPartyProjectDto?> GetProjectAsync(int id) =>
        GetAsync<ThirdPartyProjectDto>($"{BasePath}/projects/{id}");

    public Task<ThirdPartyProjectDto?> CreateProjectAsync(ThirdPartyProjectCreateDto payload) =>
        PostAsync<ThirdPartyProjectCreateDto, ThirdPartyProjectDto>($"{BasePath}/projects", payload);

    public Task<ThirdPartyProjectDto?> UpdateProjectAsync(int id, ThirdPartyProjectCreateDto payload) =>
        PutAsync<ThirdPartyProjectCreateDto, ThirdPartyProjectDto>($"{BasePath}/projects/{id}", payload);

    public Task<List<ThirdPartyAllocationDto>> GetAllocationsByProjectAsync(int projectId) =>
        GetListAsync<ThirdPartyAllocationDto>($"{BasePath}/allocations/project/{projectId}");

    public Task<ThirdPartyAllocationDto?> CreateAllocationAsync(ThirdPartyAllocationCreateDto payload) =>
        PostAsync<ThirdPartyAllocationCreateDto, ThirdPartyAllocationDto>($"{BasePath}/allocations", payload);

    public Task DeleteAllocationAsync(int allocationId) =>
        DeleteAsync($"{BasePath}/allocations/{allocationId}");

    public Task<List<DepartmentDto>> GetDepartmentsAsync() =>
        GetListAsync<DepartmentDto>($"{BasePath}/departments");

    public Task<List<SiteResponseDto>> GetSitesByDepartmentAsync(short departmentCode) =>
        GetListAsync<SiteResponseDto>($"{BasePath}/sites/{departmentCode}");

    public Task<List<ThirdPartyVehicleDto>> GetVehiclesBySupplierAsync(int supplierId) =>
        GetListAsync<ThirdPartyVehicleDto>($"{BasePath}/vehicles/{supplierId}");

    public Task<List<ClassDto>> GetClassesAsync() =>
        GetListAsync<ClassDto>($"{BasePath}/classes");

    public Task<List<ThirdPartyClassRequirementDto>> GetClassRequirementsAsync(int projectId) =>
        GetListAsync<ThirdPartyClassRequirementDto>($"{BasePath}/projects/{projectId}/requirements");
}
