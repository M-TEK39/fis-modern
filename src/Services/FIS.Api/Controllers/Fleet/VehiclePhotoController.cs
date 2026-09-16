using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize(Roles = "Vehicle Master")]
[Route("api/[controller]")]
public class VehiclePhotoController : BaseApiController
{
    private readonly IVehiclePhotoRepository _repository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IConfiguration _config;
    private readonly ILogger<VehiclePhotoController> _logger;

    public VehiclePhotoController(
        IVehiclePhotoRepository repository,
        IVehicleRepository vehicleRepository,
        IConfiguration config,
        ILogger<VehiclePhotoController> logger
    )
    {
        _repository = repository;
        _vehicleRepository = vehicleRepository;
        _config = config;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<VehiclePhoto>>> GetAll()
    {
        try
        {
            return Ok(await _repository.GetAllAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<VehiclePhoto>> GetById(int id)
    {
        try
        {
            var item = await _repository.GetByIdAsync(id);
            return item == null ? NotFound() : Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpGet("vehicle/{vmfCode}")]
    public async Task<ActionResult<IEnumerable<VehiclePhoto>>> GetByVehicle(int vmfCode)
    {
        try
        {
            return Ok(await _repository.GetByVehicleAsync(vmfCode));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    // ── POST api/vehiclephoto/upload ──────────────────────────────────────
    /// <summary>
    /// Upload a vehicle photo from any device including mobile camera.
    /// Accepts multipart/form-data. On mobile use:
    ///   input type="file" accept="image/*" capture="environment"
    /// to open the rear camera directly.
    ///
    /// Stores the file under PhotoStorage:BasePath/{vmfCode}/{guid}{ext}
    /// and saves the relative path in VehiclePhoto.FileUrl (≤500 chars).
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(20_971_520)] // 20 MB
    public async Task<ActionResult<VehiclePhoto>> Upload(
        [FromForm] int vmfCode,
        IFormFile file,
        [FromForm] string? description = null,
        [FromForm] int? orientation = null
    )
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided" });

            var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode);
            if (vehicle == null)
                return NotFound(new { error = $"Vehicle {vmfCode} not found" });

            var allowedMimes =
                _config.GetSection("PhotoStorage:AllowedMimeTypes").Get<string[]>() ?? new[]
                {
                    "image/jpeg",
                    "image/png",
                    "image/gif",
                    "image/webp",
                    "image/heic",
                    "image/heif",
                };

            var maxSize = _config.GetValue<long>("PhotoStorage:MaxFileSizeBytes", 20_971_520);

            if (file.Length > maxSize)
                return BadRequest(
                    new { error = $"File exceeds maximum size of {maxSize / 1_048_576} MB" }
                );

            var mimeType = file.ContentType?.ToLower() ?? "application/octet-stream";
            if (!allowedMimes.Contains(mimeType))
                return BadRequest(
                    new
                    {
                        error = $"File type '{mimeType}' is not allowed. Allowed: {string.Join(", ", allowedMimes)}",
                    }
                );

            // Build path: {basePath}/{vmfCode}/{guid}{ext}  — always ≤500 chars
            var basePath = _config["PhotoStorage:BasePath"] ?? "uploads/photos";
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var storedName = $"{Guid.NewGuid()}{ext}";
            var relPath = $"{vmfCode}/{storedName}"; // e.g. "1234/abc-guid.jpg"
            var absDir = Path.Combine(
                Directory.GetCurrentDirectory(),
                basePath,
                vmfCode.ToString()
            );
            var absPath = Path.Combine(absDir, storedName);

            Directory.CreateDirectory(absDir);

            await using (var stream = new FileStream(absPath, FileMode.Create))
                await file.CopyToAsync(stream);

            var photo = new VehiclePhoto
            {
                VehicleMasterCode = vmfCode,
                FileUrl = relPath, // relative path, ≤500 chars
                Description = description,
                Orientation = orientation,
                created_by_user_code = GetCurrentUserId(),
            };

            var created = await _repository.CreateAsync(photo, GetCurrentUserId());

            _logger.LogInformation(
                "Photo uploaded: vehicle {VmfCode}, file {FileName}, size {Size} bytes",
                vmfCode,
                file.FileName,
                file.Length
            );

            return CreatedAtAction(
                nameof(GetById),
                new { id = created.VehiclePhotoInfoCode },
                created
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading photo for vehicle {VmfCode}", vmfCode);
            return StatusCode(500, new { error = "Failed to upload photo" });
        }
    }

    // ── GET api/vehiclephoto/{id}/file ────────────────────────────────────
    /// <summary>
    /// Serve the raw photo bytes so browsers / mobile apps can render inline.
    /// </summary>
    [HttpGet("{id}/file")]
    public async Task<ActionResult> GetFile(int id)
    {
        try
        {
            var photo = await _repository.GetByIdAsync(id);
            if (photo == null || string.IsNullOrEmpty(photo.FileUrl))
                return NotFound(new { error = "Photo not found" });

            var absPath = ResolveStoredPath(photo.FileUrl);

            if (absPath is null || !System.IO.File.Exists(absPath))
                return NotFound(new { error = "File not found on server" });

            var ext = Path.GetExtension(absPath).ToLowerInvariant();
            var mime = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".heic" => "image/heic",
                ".heif" => "image/heif",
                _ => "application/octet-stream",
            };

            var bytes = await System.IO.File.ReadAllBytesAsync(absPath);
            return File(bytes, mime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving photo {Id}", id);
            return StatusCode(500, new { error = "Failed to retrieve photo" });
        }
    }

    [HttpPost]
    public async Task<ActionResult<VehiclePhoto>> Create([FromBody] VehiclePhoto item)
    {
        try
        {
            var created = await _repository.CreateAsync(item, GetCurrentUserId());
            return CreatedAtAction(
                nameof(GetById),
                new { id = created.VehiclePhotoInfoCode },
                created
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<VehiclePhoto>> Update(int id, [FromBody] VehiclePhoto item)
    {
        try
        {
            if (id != item.VehiclePhotoInfoCode)
                return BadRequest();
            return Ok(await _repository.UpdateAsync(item, GetCurrentUserId()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            // Also remove physical file if present
            var photo = await _repository.GetByIdAsync(id);
            if (photo != null && !string.IsNullOrEmpty(photo.FileUrl))
            {
                var absPath = ResolveStoredPath(photo.FileUrl);
                if (absPath is not null && System.IO.File.Exists(absPath))
                    System.IO.File.Delete(absPath);
            }

            await _repository.DeleteAsync(id, GetCurrentUserId());
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    private string? ResolveStoredPath(string? fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
            return null;

        var basePath = _config["PhotoStorage:BasePath"] ?? "uploads/photos";
        var storageRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), basePath));
        var relativePath = fileUrl
            .Trim()
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(storageRoot, relativePath));
        var relativeCandidate = Path.GetRelativePath(storageRoot, candidate);

        return
            relativeCandidate == "."
            || relativeCandidate.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal
            )
            || relativeCandidate == ".."
            || Path.IsPathRooted(relativeCandidate)
            ? null
            : candidate;
    }
}
