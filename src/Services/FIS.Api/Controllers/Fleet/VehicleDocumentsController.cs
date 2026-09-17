using FIS.Api.Services;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Vehicles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// Vehicle document management API.
///
/// Supports scanning and uploading documents from any device including mobile/tablet.
/// On mobile the frontend uses input[accept="image/*,application/pdf" capture="environment"]
/// to open the rear camera directly for scanning.
///
/// All documents are stored on the filesystem under DocumentStorage:BasePath
/// and linked to a vehicle via vmf_code. An optional reference_type/reference_id
/// links a document to a specific module record (e.g. Accident #42, Fine #17).
///
/// Supported categories:
///   Accident | Licence | Fine | Logbook | Maintenance |
///   Contract | Registration | Insurance | RoadWorthy | Other
/// </summary>
[ApiController]
// Vehicle documents are exposed from the Vehicle Master detail workflow. Keep
// the file and metadata endpoints behind the same legacy entitlement as that
// workflow; authentication alone must not disclose fleet documents.
[Authorize(Roles = "Vehicle Master")]
[Route("api/vehicles/{vmfCode:int}/documents")]
public class VehicleDocumentsController : BaseApiController
{
    private static readonly HashSet<string> ValidCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Accident",
        "Licence",
        "Fine",
        "Logbook",
        "Maintenance",
        "Contract",
        "Registration",
        "Insurance",
        "RoadWorthy",
        "Other",
    };

    private readonly IVehicleDocumentRepository _repository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly LegacyVehicleScopeService _vehicleScope;
    private readonly IConfiguration _config;
    private readonly ILogger<VehicleDocumentsController> _logger;

    public VehicleDocumentsController(
        IVehicleDocumentRepository repository,
        IVehicleRepository vehicleRepository,
        LegacyVehicleScopeService vehicleScope,
        IConfiguration config,
        ILogger<VehicleDocumentsController> logger
    )
    {
        _repository = repository;
        _vehicleRepository = vehicleRepository;
        _vehicleScope = vehicleScope;
        _config = config;
        _logger = logger;
    }

    // ── GET /api/vehicles/{vmfCode}/documents ─────────────────────────────
    /// <summary>
    /// List all documents for a vehicle, optionally filtered by category.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> GetDocuments(int vmfCode, [FromQuery] string? category = null)
    {
        try
        {
            if (await GetAccessibleVehicleAsync(vmfCode) is null)
                return NotFound(new { error = "Vehicle not found" });
            var docs = await _repository.GetByVehicleAsync(vmfCode, category);
            var result = docs.Select(MapToDto).ToList();

            return Ok(
                new
                {
                    vmf_code = vmfCode,
                    category_filter = category,
                    total_count = result.Count,
                    documents = result,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing documents for vehicle {VmfCode}", vmfCode);
            return StatusCode(500, new { error = "Failed to retrieve documents" });
        }
    }

    // ── GET /api/vehicles/{vmfCode}/documents/categories ─────────────────
    /// <summary>Returns the valid document category list for populating dropdowns.</summary>
    [HttpGet("categories")]
    public ActionResult GetCategories() => Ok(ValidCategories.OrderBy(c => c).ToList());

    // ── POST /api/vehicles/{vmfCode}/documents ────────────────────────────
    /// <summary>
    /// Upload a document (or camera scan) for a vehicle.
    /// Accepts multipart/form-data. On mobile/tablet the frontend should use:
    ///   input type="file" accept="image/*,application/pdf" capture="environment"
    /// to trigger the rear camera for direct scanning.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(20_971_520)] // 20 MB
    public async Task<ActionResult> Upload(
        int vmfCode,
        IFormFile file,
        [FromForm] string category = "Other",
        [FromForm] string? description = null,
        [FromForm] string? reference_type = null,
        [FromForm] int? reference_id = null
    )
    {
        try
        {
            // Validate vehicle exists
            var vehicle = await GetAccessibleVehicleAsync(vmfCode);
            if (vehicle == null)
                return NotFound(new { error = $"Vehicle {vmfCode} not found" });

            // Validate category
            if (!ValidCategories.Contains(category))
                return BadRequest(
                    new
                    {
                        error = $"Invalid category '{category}'. Valid values: {string.Join(", ", ValidCategories.OrderBy(c => c))}",
                    }
                );

            // Validate file
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided" });

            var allowedMimes =
                _config.GetSection("DocumentStorage:AllowedMimeTypes").Get<string[]>() ?? new[]
                {
                    "image/jpeg",
                    "image/png",
                    "application/pdf",
                };

            var maxSize = _config.GetValue<long>("DocumentStorage:MaxFileSizeBytes", 20_971_520);

            if (file.Length > maxSize)
                return BadRequest(
                    new { error = $"File exceeds maximum size of {maxSize / 1_048_576} MB" }
                );

            var mimeType = file.ContentType?.ToLower() ?? "application/octet-stream";
            if (!allowedMimes.Contains(mimeType))
                return BadRequest(new { error = $"File type '{mimeType}' is not allowed" });

            // Build storage path: {basePath}/{vmfCode}/{category}/{guid}{ext}
            var basePath = _config["DocumentStorage:BasePath"] ?? "uploads/documents";
            var ext = Path.GetExtension(file.FileName);
            var storedName = $"{Guid.NewGuid()}{ext}";
            var relDir = Path.Combine(vmfCode.ToString(), category);
            var relPath = Path.Combine(relDir, storedName);
            var absDir = Path.Combine(Directory.GetCurrentDirectory(), basePath, relDir);
            var absPath = Path.Combine(absDir, storedName);

            Directory.CreateDirectory(absDir);

            await using (var stream = new FileStream(absPath, FileMode.Create))
                await file.CopyToAsync(stream);

            var document = new VehicleDocument
            {
                vmf_code = vmfCode,
                document_category = category,
                document_description = description,
                original_file_name = file.FileName,
                stored_file_path = relPath.Replace('\\', '/'),
                mime_type = mimeType,
                file_size_bytes = file.Length,
                reference_type = reference_type,
                reference_id = reference_id,
                created_by_user_code = GetCurrentUserId(),
            };

            var created = await _repository.CreateAsync(document);

            _logger.LogInformation(
                "Document uploaded: vehicle {VmfCode}, category {Category}, file {FileName}, size {Size} bytes",
                vmfCode,
                category,
                file.FileName,
                file.Length
            );

            return CreatedAtAction(
                nameof(Download),
                new { vmfCode, documentId = created.document_id },
                MapToDto(created)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document for vehicle {VmfCode}", vmfCode);
            return StatusCode(500, new { error = "Failed to upload document" });
        }
    }

    // ── GET /api/vehicles/{vmfCode}/documents/{documentId}/download ───────
    /// <summary>
    /// Download / view a document. Returns the raw file bytes with the correct
    /// Content-Type so browsers and mobile apps can render it inline.
    /// </summary>
    [HttpGet("{documentId:int}/download")]
    public async Task<ActionResult> Download(int vmfCode, int documentId)
    {
        try
        {
            var doc = await _repository.GetByIdAsync(documentId);
            if (doc == null || doc.vmf_code != vmfCode || await GetAccessibleVehicleAsync(vmfCode) is null)
                return NotFound(new { error = "Document not found" });

            var basePath = _config["DocumentStorage:BasePath"] ?? "uploads/documents";
            var absPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                basePath,
                doc.stored_file_path.Replace('/', Path.DirectorySeparatorChar)
            );

            if (!System.IO.File.Exists(absPath))
                return NotFound(new { error = "File not found on server" });

            var bytes = await System.IO.File.ReadAllBytesAsync(absPath);
            return File(bytes, doc.mime_type, doc.original_file_name);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error downloading document {DocumentId} for vehicle {VmfCode}",
                documentId,
                vmfCode
            );
            return StatusCode(500, new { error = "Failed to retrieve document" });
        }
    }

    // ── DELETE /api/vehicles/{vmfCode}/documents/{documentId} ────────────
    /// <summary>Soft-deletes a document record and removes the physical file.</summary>
    [HttpDelete("{documentId:int}")]
    public async Task<ActionResult> Delete(int vmfCode, int documentId)
    {
        try
        {
            var doc = await _repository.GetByIdAsync(documentId);
            if (doc == null || doc.vmf_code != vmfCode || await GetAccessibleVehicleAsync(vmfCode) is null)
                return NotFound(new { error = "Document not found" });

            // Remove physical file
            var basePath = _config["DocumentStorage:BasePath"] ?? "uploads/documents";
            var absPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                basePath,
                doc.stored_file_path.Replace('/', Path.DirectorySeparatorChar)
            );
            if (System.IO.File.Exists(absPath))
                System.IO.File.Delete(absPath);

            await _repository.DeleteAsync(documentId);

            _logger.LogInformation(
                "Document {DocumentId} deleted for vehicle {VmfCode}",
                documentId,
                vmfCode
            );
            return Ok(new { message = "Document deleted", document_id = documentId });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error deleting document {DocumentId} for vehicle {VmfCode}",
                documentId,
                vmfCode
            );
            return StatusCode(500, new { error = "Failed to delete document" });
        }
    }

    // ── GET /api/vehicles/{vmfCode}/documents/by-reference ───────────────
    /// <summary>
    /// Fetch all documents linked to a specific module record.
    /// e.g. GET .../documents/by-reference?type=Accident&amp;id=42
    /// </summary>
    [HttpGet("by-reference")]
    public async Task<ActionResult> GetByReference(
        int vmfCode,
        [FromQuery] string type,
        [FromQuery] int id
    )
    {
        try
        {
            if (await GetAccessibleVehicleAsync(vmfCode) is null)
                return NotFound(new { error = "Vehicle not found" });
            var docs = await _repository.GetByReferenceAsync(type, id);
            var filtered = docs.Where(d => d.vmf_code == vmfCode).Select(MapToDto).ToList();
            return Ok(
                new
                {
                    reference_type = type,
                    reference_id = id,
                    total_count = filtered.Count,
                    documents = filtered,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching documents by reference {Type}/{Id}", type, id);
            return StatusCode(500, new { error = "Failed to retrieve documents" });
        }
    }

    private async Task<Vehicle?> GetAccessibleVehicleAsync(int vmfCode) =>
        await _vehicleRepository.GetByIdAsync(
            vmfCode,
            await ResolveAllowedVehicleSiteCodesAsync(),
            GetCurrentUserId()
        );

    private Task<IReadOnlySet<short>?> ResolveAllowedVehicleSiteCodesAsync() =>
        _vehicleScope.ResolveAllowedSiteCodesAsync(User, HttpContext.RequestAborted);

    // ── Mapping ───────────────────────────────────────────────────────────
    private static object MapToDto(VehicleDocument d) =>
        new
        {
            d.document_id,
            d.vmf_code,
            d.document_category,
            d.document_description,
            d.original_file_name,
            d.mime_type,
            file_size_kb = Math.Round(d.file_size_bytes / 1024.0, 1),
            d.reference_type,
            d.reference_id,
            d.date_created,
            download_url = $"/api/vehicles/{d.vmf_code}/documents/{d.document_id}/download",
            is_image = d.mime_type.StartsWith("image/"),
            is_pdf = d.mime_type == "application/pdf",
        };
}
