using System.ComponentModel.DataAnnotations;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Vehicles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ThirdPartyController : BaseApiController
{
    private const int DefaultPageSize = 24;
    private const int MaximumPageSize = 100;

    private readonly IThirdPartyRentalRepository _rentalRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ISiteRepository _siteRepository;
    private readonly IClassRepository _classRepository;
    private readonly ILogger<ThirdPartyController> _logger;

    public ThirdPartyController(
        IThirdPartyRentalRepository rentalRepository,
        IDepartmentRepository departmentRepository,
        ISiteRepository siteRepository,
        IClassRepository classRepository,
        ILogger<ThirdPartyController> logger
    )
    {
        _rentalRepository = rentalRepository;
        _departmentRepository = departmentRepository;
        _siteRepository = siteRepository;
        _classRepository = classRepository;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult GetRoot() =>
        Ok(
            new
            {
                module = "Third Party Rentals",
                endpoints = new[]
                {
                    "suppliers",
                    "services",
                    "projects",
                    "allocations/project/{projectId}",
                    "departments",
                    "sites/{departmentCode}",
                    "vehicles/{supplierId}",
                    "classes",
                },
            }
        );

    [HttpGet("suppliers")]
    public async Task<ActionResult<IEnumerable<ThirdPartySupplierRecord>>> GetSuppliers(
        CancellationToken cancellationToken
    )
    {
        try
        {
            return Ok(await _rentalRepository.GetSuppliersAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving third-party suppliers");
            return StatusCode(500);
        }
    }

    [HttpGet("suppliers/page")]
    public async Task<ActionResult> GetSuppliersPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await _rentalRepository.GetSuppliersPageAsync(
                new ThirdPartySupplierPageQuery(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, MaximumPageSize)
                ),
                cancellationToken
            );
            return Ok(
                new
                {
                    items = result.Items,
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.Total,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged third-party suppliers");
            return StatusCode(500);
        }
    }

    [HttpGet("suppliers/{id:int}")]
    public async Task<ActionResult<ThirdPartySupplierRecord>> GetSupplier(
        int id,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var supplier = await _rentalRepository.GetSupplierAsync(id, cancellationToken);
            return supplier is null ? NotFound() : Ok(supplier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving third-party supplier {SupplierId}", id);
            return StatusCode(500);
        }
    }

    [HttpPost("suppliers")]
    public async Task<ActionResult<ThirdPartySupplierRecord>> CreateSupplier(
        [FromBody] ThirdPartySupplierRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryCreateSupplierWrite(request, out var input, out var error))
            return BadRequest(error);
        try
        {
            var created = await _rentalRepository.CreateSupplierAsync(
                input!,
                GetCurrentUserId(),
                cancellationToken
            );
            return CreatedAtAction(nameof(GetSupplier), new { id = created.supplier_id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating third-party supplier");
            return StatusCode(500);
        }
    }

    [HttpPut("suppliers/{id:int}")]
    public async Task<ActionResult<ThirdPartySupplierRecord>> UpdateSupplier(
        int id,
        [FromBody] ThirdPartySupplierRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryCreateSupplierWrite(request, out var input, out var error))
            return BadRequest(error);
        try
        {
            return Ok(
                await _rentalRepository.UpdateSupplierAsync(
                    id,
                    input!,
                    GetCurrentUserId(),
                    cancellationToken
                )
            );
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating third-party supplier {SupplierId}", id);
            return StatusCode(500);
        }
    }

    [HttpGet("services")]
    public async Task<ActionResult<IEnumerable<ThirdPartyServiceOption>>> GetServices(
        CancellationToken cancellationToken
    )
    {
        try
        {
            return Ok(await _rentalRepository.GetServiceOptionsAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving third-party service options");
            return StatusCode(500);
        }
    }

    [HttpGet("projects")]
    public async Task<ActionResult<IEnumerable<ThirdPartyProjectRecord>>> GetProjects(
        CancellationToken cancellationToken
    )
    {
        try
        {
            return Ok(await _rentalRepository.GetProjectsAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving third-party projects");
            return StatusCode(500);
        }
    }

    [HttpGet("projects/page")]
    public async Task<ActionResult> GetProjectsPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] short? departmentCode = null,
        CancellationToken cancellationToken = default
    )
    {
        if (departmentCode is <= 0)
            return BadRequest(new { message = "departmentCode must be greater than zero." });

        try
        {
            var result = await _rentalRepository.GetProjectsPageAsync(
                new ThirdPartyProjectPageQuery(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, MaximumPageSize),
                    departmentCode
                ),
                cancellationToken
            );
            return Ok(
                new
                {
                    items = result.Items,
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.Total,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving paged third-party projects for department {DepartmentCode}",
                departmentCode
            );
            return StatusCode(500);
        }
    }

    [HttpGet("projects/{id:int}")]
    public async Task<ActionResult<ThirdPartyProjectRecord>> GetProject(
        int id,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var project = await _rentalRepository.GetProjectAsync(id, cancellationToken);
            return project is null ? NotFound() : Ok(project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving third-party project {ProjectId}", id);
            return StatusCode(500);
        }
    }

    [HttpGet("projects/department/{departmentCode:int}")]
    public async Task<ActionResult<IEnumerable<ThirdPartyProjectRecord>>> GetProjectsByDepartment(
        short departmentCode,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return Ok(
                await _rentalRepository.GetProjectsByDepartmentAsync(
                    departmentCode,
                    cancellationToken
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving third-party projects for department {DepartmentCode}",
                departmentCode
            );
            return StatusCode(500);
        }
    }

    [HttpPost("projects")]
    public async Task<ActionResult<ThirdPartyProjectRecord>> CreateProject(
        [FromBody] ThirdPartyProjectRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryCreateProjectWrite(request, out var input, out var error))
            return BadRequest(error);
        try
        {
            var created = await _rentalRepository.CreateProjectAsync(
                input!,
                GetCurrentUserId(),
                cancellationToken
            );
            return CreatedAtAction(nameof(GetProject), new { id = created.project_id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating third-party project");
            return StatusCode(500);
        }
    }

    [HttpPut("projects/{id:int}")]
    public async Task<ActionResult<ThirdPartyProjectRecord>> UpdateProject(
        int id,
        [FromBody] ThirdPartyProjectRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryCreateProjectWrite(request, out var input, out var error))
            return BadRequest(error);
        try
        {
            return Ok(
                await _rentalRepository.UpdateProjectAsync(
                    id,
                    input!,
                    GetCurrentUserId(),
                    cancellationToken
                )
            );
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating third-party project {ProjectId}", id);
            return StatusCode(500);
        }
    }

    [HttpGet("projects/{projectId:int}/requirements")]
    public async Task<
        ActionResult<IEnumerable<ThirdPartyClassRequirementRecord>>
    > GetProjectRequirements(int projectId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(
                await _rentalRepository.GetClassRequirementsAsync(projectId, cancellationToken)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving class requirements for project {ProjectId}",
                projectId
            );
            return StatusCode(500);
        }
    }

    [HttpGet("allocations/project/{projectId:int}")]
    public async Task<
        ActionResult<IEnumerable<ThirdPartyAllocationRecord>>
    > GetAllocationsByProject(int projectId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(
                await _rentalRepository.GetAllocationsByProjectAsync(projectId, cancellationToken)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving third-party allocations for project {ProjectId}",
                projectId
            );
            return StatusCode(500);
        }
    }

    [HttpGet("allocations/page")]
    public async Task<ActionResult> GetAllocationsByProjectPage(
        [FromQuery] int? projectId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default
    )
    {
        if (!projectId.HasValue || projectId.Value <= 0)
            return BadRequest(new { message = "projectId is required." });

        try
        {
            var result = await _rentalRepository.GetAllocationsByProjectPageAsync(
                new ThirdPartyAllocationPageQuery(
                    projectId.Value,
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, MaximumPageSize)
                ),
                cancellationToken
            );
            return Ok(
                new
                {
                    items = result.Items,
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.Total,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving paged third-party allocations for project {ProjectId}",
                projectId
            );
            return StatusCode(500);
        }
    }

    [HttpPost("allocations")]
    public async Task<ActionResult<ThirdPartyAllocationRecord>> CreateAllocation(
        [FromBody] ThirdPartyAllocationRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryCreateAllocationWrite(request, out var input, out var error))
            return BadRequest(error);
        try
        {
            return Ok(
                await _rentalRepository.CreateAllocationAsync(
                    input!,
                    GetCurrentUserId(),
                    cancellationToken
                )
            );
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating third-party allocation");
            return StatusCode(500);
        }
    }

    [HttpDelete("allocations/{allocationId:int}")]
    public async Task<ActionResult> DeleteAllocation(
        int allocationId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await _rentalRepository.DeleteAllocationAsync(
                allocationId,
                GetCurrentUserId(),
                cancellationToken
            );
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error deleting third-party allocation {AllocationId}",
                allocationId
            );
            return StatusCode(500);
        }
    }

    [HttpGet("departments")]
    public async Task<ActionResult<IEnumerable<Department>>> GetDepartments()
    {
        try
        {
            return Ok(await _departmentRepository.GetActiveDepartmentsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving departments for third-party rentals");
            return StatusCode(500);
        }
    }

    [HttpGet("sites/{departmentCode:int}")]
    public async Task<ActionResult<IEnumerable<Site>>> GetSitesByDepartment(short departmentCode)
    {
        try
        {
            var sites = await _siteRepository.GetActiveSitesAsync();
            return Ok(sites.Where(site => site.Depatrment_code == departmentCode));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving sites for department {DepartmentCode}",
                departmentCode
            );
            return StatusCode(500);
        }
    }

    [HttpGet("vehicles/{supplierId:int}")]
    public async Task<ActionResult<IEnumerable<ThirdPartyVehicleRecord>>> GetVehiclesBySupplier(
        int supplierId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return Ok(
                await _rentalRepository.GetVehiclesBySupplierAsync(supplierId, cancellationToken)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving vehicles for third-party supplier {SupplierId}",
                supplierId
            );
            return StatusCode(500);
        }
    }

    [HttpGet("vehicles/page")]
    public async Task<ActionResult> GetVehiclesBySupplierPage(
        [FromQuery] int? supplierId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default
    )
    {
        if (!supplierId.HasValue || supplierId.Value <= 0)
            return BadRequest(new { message = "supplierId is required." });

        try
        {
            var result = await _rentalRepository.GetVehiclesBySupplierPageAsync(
                new ThirdPartyVehiclePageQuery(
                    supplierId.Value,
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, MaximumPageSize)
                ),
                cancellationToken
            );
            return Ok(
                new
                {
                    items = result.Items,
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.Total,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving paged vehicles for third-party supplier {SupplierId}",
                supplierId
            );
            return StatusCode(500);
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
            _logger.LogError(ex, "Error retrieving vehicle classes");
            return StatusCode(500);
        }
    }

    private static bool TryCreateSupplierWrite(
        ThirdPartySupplierRequest request,
        out ThirdPartySupplierWrite? input,
        out object error
    )
    {
        if (string.IsNullOrWhiteSpace(request.name))
        {
            input = null;
            error = new { message = "Supplier name is required." };
            return false;
        }

        input = new ThirdPartySupplierWrite(
            request.name.Trim(),
            request.address,
            request.postal_address,
            request.tel,
            request.fax,
            request.cell,
            request.email,
            request.contact_person,
            request.notes,
            request.service_code,
            request.active ?? true,
            request.ctg_code ?? 2,
            request.is_third_party ?? true
        );
        error = new { message = string.Empty };
        return true;
    }

    private static bool TryCreateProjectWrite(
        ThirdPartyProjectRequest request,
        out ThirdPartyProjectWrite? input,
        out object error
    )
    {
        if (
            !request.department_code.HasValue
            || !request.start_date.HasValue
            || !request.end_date.HasValue
            || string.IsNullOrWhiteSpace(request.description)
        )
        {
            input = null;
            error = new
            {
                message = "Department, description, start date, and end date are required.",
            };
            return false;
        }

        input = new ThirdPartyProjectWrite(
            request.department_code.Value,
            request.site_code,
            request.description.Trim(),
            request.start_date.Value,
            request.end_date.Value,
            request.responsible_person,
            request.rp_physical_address,
            request.rp_postal_address,
            request.rp_tel,
            request.rp_fax,
            request.rp_email,
            request.rp_cell,
            request.notes,
            request.order_reference,
            request.class_configuration
        );
        error = new { message = string.Empty };
        return true;
    }

    private static bool TryCreateAllocationWrite(
        ThirdPartyAllocationRequest request,
        out ThirdPartyAllocationWrite? input,
        out object error
    )
    {
        if (!request.project_id.HasValue || request.project_id <= 0)
        {
            input = null;
            error = new { message = "A project is required." };
            return false;
        }

        input = new ThirdPartyAllocationWrite(
            request.project_id.Value,
            request.supplier_id,
            request.vehicle_id,
            request.class_id,
            request.quantity ?? 1
        );
        error = new { message = string.Empty };
        return true;
    }
}

public sealed class ThirdPartySupplierRequest
{
    [Required]
    public string? name { get; set; }
    public string? address { get; set; }
    public string? postal_address { get; set; }
    public string? tel { get; set; }
    public string? fax { get; set; }
    public string? cell { get; set; }
    public string? email { get; set; }
    public string? contact_person { get; set; }
    public string? notes { get; set; }
    public string? service_code { get; set; }
    public bool? active { get; set; }
    public int? ctg_code { get; set; }
    public bool? is_third_party { get; set; }
}

public sealed class ThirdPartyProjectRequest
{
    public short? department_code { get; set; }
    public short? site_code { get; set; }
    public string? description { get; set; }
    public DateTime? start_date { get; set; }
    public DateTime? end_date { get; set; }
    public string? responsible_person { get; set; }
    public string? rp_physical_address { get; set; }
    public string? rp_postal_address { get; set; }
    public string? rp_tel { get; set; }
    public string? rp_fax { get; set; }
    public string? rp_email { get; set; }
    public string? rp_cell { get; set; }
    public string? notes { get; set; }
    public string? order_reference { get; set; }
    public string? class_configuration { get; set; }
}

public sealed class ThirdPartyAllocationRequest
{
    public int? project_id { get; set; }
    public int? supplier_id { get; set; }
    public int? vehicle_id { get; set; }
    public int? class_id { get; set; }
    public int? quantity { get; set; }
}
