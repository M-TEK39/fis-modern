using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Operations;
using FIS.Core.Domain.Entities.ReferenceData;
using FIS.Core.Domain.Entities.Vehicles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ThirdPartyController : BaseApiController
{
    private readonly ISupplierRepository _supplierRepository;
    private readonly IThirdPartyProjectRepository _projectRepository;
    private readonly IThirdPartyAllocationRepository _allocationRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ISiteRepository _siteRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IClassRepository _classRepository;
    private readonly IClassRequirementRepository _classRequirementRepository;
    private readonly ILogger<ThirdPartyController> _logger;

    public ThirdPartyController(
        ISupplierRepository supplierRepository,
        IThirdPartyProjectRepository projectRepository,
        IThirdPartyAllocationRepository allocationRepository,
        IDepartmentRepository departmentRepository,
        ISiteRepository siteRepository,
        IVehicleRepository vehicleRepository,
        IClassRepository classRepository,
        IClassRequirementRepository classRequirementRepository,
        ILogger<ThirdPartyController> logger)
    {
        _supplierRepository = supplierRepository;
        _projectRepository = projectRepository;
        _allocationRepository = allocationRepository;
        _departmentRepository = departmentRepository;
        _siteRepository = siteRepository;
        _vehicleRepository = vehicleRepository;
        _classRepository = classRepository;
        _classRequirementRepository = classRequirementRepository;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult GetRoot()
    {
        return Ok(new
        {
            module = "Third Party Rentals",
            endpoints = new[]
            {
                "suppliers",
                "projects",
                "allocations",
                "departments",
                "sites/{departmentCode}",
                "vehicles/{supplierId}",
                "classes"
            }
        });
    }

    #region Supplier Endpoints

    [HttpGet("suppliers")]
    public async Task<ActionResult<IEnumerable<Supplier>>> GetSuppliers()
    {
        try
        {
            return Ok(await _supplierRepository.GetAllAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving suppliers");
            return StatusCode(500);
        }
    }

    [HttpGet("suppliers/{id}")]
    public async Task<ActionResult<Supplier>> GetSupplier(short id)
    {
        try
        {
            var supplier = await _supplierRepository.GetByIdAsync(id);
            return supplier == null ? NotFound() : Ok(supplier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving supplier {SupplierId}", id);
            return StatusCode(500);
        }
    }

    [HttpPost("suppliers")]
    public async Task<ActionResult<Supplier>> CreateSupplier([FromBody] Supplier supplier)
    {
        try
        {
            var created = await _supplierRepository.CreateAsync(supplier, GetCurrentUserId());
            return CreatedAtAction(nameof(GetSupplier), new { id = created.supplier_id }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating supplier");
            return StatusCode(500);
        }
    }

    [HttpPut("suppliers/{id}")]
    public async Task<ActionResult<Supplier>> UpdateSupplier(short id, [FromBody] Supplier supplier)
    {
        try
        {
            if (id != supplier.supplier_id)
                return BadRequest();

            var updated = await _supplierRepository.UpdateAsync(supplier, GetCurrentUserId());
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating supplier {SupplierId}", id);
            return StatusCode(500);
        }
    }

    #endregion

    #region Project Endpoints

    [HttpGet("projects")]
    public async Task<ActionResult<IEnumerable<ThirdPartyProject>>> GetProjects()
    {
        try
        {
            return Ok(await _projectRepository.GetAllAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving projects");
            return StatusCode(500);
        }
    }

    [HttpGet("projects/{id}")]
    public async Task<ActionResult<ThirdPartyProject>> GetProject(int id)
    {
        try
        {
            var project = await _projectRepository.GetByIdAsync(id);
            return project == null ? NotFound() : Ok(project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project {ProjectId}", id);
            return StatusCode(500);
        }
    }

    [HttpGet("projects/department/{departmentCode}")]
    public async Task<ActionResult<IEnumerable<ThirdPartyProject>>> GetProjectsByDepartment(short departmentCode)
    {
        try
        {
            return Ok(await _projectRepository.GetByDepartmentAsync(departmentCode));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving projects for department {DepartmentCode}", departmentCode);
            return StatusCode(500);
        }
    }

    [HttpPost("projects")]
    public async Task<ActionResult<ThirdPartyProject>> CreateProject([FromBody] ThirdPartyProject project)
    {
        try
        {
            var created = await _projectRepository.CreateAsync(project, GetCurrentUserId());
            return CreatedAtAction(nameof(GetProject), new { id = created.project_id }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating project");
            return StatusCode(500);
        }
    }

    [HttpPut("projects/{id}")]
    public async Task<ActionResult<ThirdPartyProject>> UpdateProject(int id, [FromBody] ThirdPartyProject project)
    {
        try
        {
            if (id != project.project_id)
                return BadRequest();

            var updated = await _projectRepository.UpdateAsync(project, GetCurrentUserId());
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating project {ProjectId}", id);
            return StatusCode(500);
        }
    }

    [HttpGet("projects/{projectId}/requirements")]
    public async Task<ActionResult<IEnumerable<ClassRequirement>>> GetProjectRequirements(int projectId)
    {
        try
        {
            _logger.LogInformation("Getting class requirements for project {ProjectId}", projectId);

            // Get class requirements for the specified project
            var requirements = await _classRequirementRepository.GetByProjectIdAsync(projectId);

            // Map to DTO format expected by the frontend
            var requirementDtos = requirements.Select(r => new ClassRequirement
            {
                class_id = r.class_id,
                class_name = r.Class?.description,
                required_count = r.required_count
            });

            return Ok(requirementDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving requirements for project {ProjectId}", projectId);
            return StatusCode(500, "Error retrieving project requirements");
        }
    }

    #endregion

    #region Allocation Endpoints

    [HttpGet("allocations/project/{projectId}")]
    public async Task<ActionResult<IEnumerable<ThirdPartyAllocation>>> GetAllocationsByProject(int projectId)
    {
        try
        {
            return Ok(await _allocationRepository.GetByProjectAsync(projectId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving allocations for project {ProjectId}", projectId);
            return StatusCode(500);
        }
    }

    [HttpPost("allocations")]
    public async Task<ActionResult<ThirdPartyAllocation>> CreateAllocation([FromBody] ThirdPartyAllocation allocation)
    {
        try
        {
            var created = await _allocationRepository.CreateAsync(allocation, GetCurrentUserId());
            return Ok(created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating allocation");
            return StatusCode(500);
        }
    }

    [HttpDelete("allocations/{allocationId}")]
    public async Task<ActionResult> DeleteAllocation(int allocationId)
    {
        try
        {
            await _allocationRepository.DeleteAsync(allocationId, GetCurrentUserId());
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting allocation {AllocationId}", allocationId);
            return StatusCode(500);
        }
    }

    #endregion

    #region Lookup Endpoints

    [HttpGet("departments")]
    public async Task<ActionResult<IEnumerable<Department>>> GetDepartments()
    {
        try
        {
            return Ok(await _departmentRepository.GetActiveDepartmentsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving departments");
            return StatusCode(500);
        }
    }

    [HttpGet("sites/{departmentCode}")]
    public async Task<ActionResult<IEnumerable<Site>>> GetSitesByDepartment(short departmentCode)
    {
        try
        {
            var sites = await _siteRepository.GetActiveSitesAsync();
            var filtered = sites.Where(s => s.Depatrment_code == departmentCode);
            return Ok(filtered);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sites for department {DepartmentCode}", departmentCode);
            return StatusCode(500);
        }
    }

    [HttpGet("vehicles/{supplierId}")]
    public async Task<ActionResult<IEnumerable<VehicleDto>>> GetVehiclesBySupplier(short supplierId)
    {
        try
        {
            _logger.LogInformation("Getting vehicles for supplier {SupplierId}", supplierId);

            // Verify supplier exists
            var supplier = await _supplierRepository.GetByIdAsync(supplierId);
            if (supplier == null)
            {
                return NotFound(new { message = $"Supplier with ID {supplierId} not found" });
            }

            // Get all vehicles and filter by supplier_id
            var vehicles = await _vehicleRepository.GetAllAsync();
            var vehicleDtos = vehicles
                .Where(v => !v.is_deleted && v.supplier_id == supplierId)
                .Select(v => new VehicleDto
                {
                    vehicle_id = v.vmf_code,
                    registration_number = v.registration_number,
                    model_description = v.Model?.model_description ?? "Unknown",
                    model_year = v.year_manufactured?.ToString(),
                    chassis_number = v.chassis_number
                })
                .ToList();

            _logger.LogInformation("Returning {Count} vehicles for supplier {SupplierId}",
                vehicleDtos.Count, supplierId);

            return Ok(vehicleDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vehicles for supplier {SupplierId}", supplierId);
            return StatusCode(500, "Error retrieving vehicles");
        }
    }

    [HttpGet("classes")]
    public async Task<ActionResult<IEnumerable<Class>>> GetClasses()
    {
        try
        {
            return Ok(await _classRepository.GetAllAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving classes");
            return StatusCode(500);
        }
    }

    #endregion
}

#region DTOs

public class VehicleDto
{
    public int vehicle_id { get; set; }
    public string? registration_number { get; set; }
    public string? model_description { get; set; }
    public string? model_year { get; set; }
    public string? chassis_number { get; set; }
}

public class ClassRequirement
{
    public int class_id { get; set; }
    public string? class_name { get; set; }
    public int? required_count { get; set; }
}

#endregion
