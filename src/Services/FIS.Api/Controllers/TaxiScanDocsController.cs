using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/taxi-scan-docs")]
public sealed class TaxiScanDocsController : BaseApiController
{
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp",
    };

    private readonly ITaxiScanDocRepository _repository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TaxiScanDocsController> _logger;

    public TaxiScanDocsController(
        ITaxiScanDocRepository repository,
        IVehicleRepository vehicleRepository,
        IConfiguration configuration,
        ILogger<TaxiScanDocsController> logger)
    {
        _repository = repository;
        _vehicleRepository = vehicleRepository;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> GetAll()
    {
        try
        {
            var documents = await _repository.GetAllAsync();
            return Ok(await AddVehicleDetailsAsync(documents));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing taxi requisition scans");
            return StatusCode(500, new { error = "Failed to retrieve taxi requisition scans" });
        }
    }

    [HttpGet("vehicle/{vmfCode:int}")]
    public async Task<ActionResult> GetByVehicle(int vmfCode)
    {
        try
        {
            var documents = await _repository.GetByVehicleAsync(vmfCode);
            return Ok(await AddVehicleDetailsAsync(documents));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing taxi requisition scans for vehicle {VmfCode}", vmfCode);
            return StatusCode(500, new { error = "Failed to retrieve taxi requisition scans" });
        }
    }

    [HttpPost]
    [RequestSizeLimit(20_971_520)]
    public async Task<ActionResult> Upload(
        [FromForm] int vmfCode,
        [FromForm] DateTime periodBegin,
        [FromForm] DateTime periodEnd,
        IFormFile file)
    {
        try
        {
            if (periodEnd < periodBegin) return BadRequest(new { error = "The certificate end date must be on or after the begin date." });
            if (file is null || file.Length == 0) return BadRequest(new { error = "A scanned requisition certificate is required." });
            if (!AllowedMimeTypes.Contains(file.ContentType ?? string.Empty)) return BadRequest(new { error = "Only JPG, PNG, GIF, or WEBP image scans are accepted." });

            var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode);
            if (vehicle is null) return NotFound(new { error = $"Vehicle {vmfCode} not found" });

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(extension) || extension.Length > 10) return BadRequest(new { error = "The uploaded scan has an invalid file name." });
            var storedName = $"{Guid.NewGuid():N}{extension}";
            var basePath = _configuration["TaxiScanStorage:BasePath"] ?? "uploads/taxi-scan";
            var absoluteDirectory = Path.Combine(Directory.GetCurrentDirectory(), basePath);
            var absolutePath = Path.Combine(absoluteDirectory, storedName);
            Directory.CreateDirectory(absoluteDirectory);

            await using (var stream = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await file.CopyToAsync(stream);

            var created = await _repository.CreateAsync(new TaxiScanDoc
            {
                vmf_code = vmfCode,
                image = storedName,
                period_begin = periodBegin,
                period_end = periodEnd,
            }, GetCurrentUserId());

            return CreatedAtAction(nameof(GetById), new { scanDocCode = created.taxi_scandoc_code }, await MapDocumentAsync(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading taxi requisition scan for vehicle {VmfCode}", vmfCode);
            return StatusCode(500, new { error = "Failed to upload taxi requisition scan" });
        }
    }

    [HttpGet("{scanDocCode:int}")]
    public async Task<ActionResult> GetById(int scanDocCode)
    {
        try
        {
            var document = await _repository.GetByIdAsync(scanDocCode);
            return document is null ? NotFound() : Ok(await MapDocumentAsync(document));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading taxi requisition scan {ScanDocCode}", scanDocCode);
            return StatusCode(500, new { error = "Failed to retrieve taxi requisition scan" });
        }
    }

    [HttpGet("{scanDocCode:int}/file")]
    public async Task<ActionResult> Download(int scanDocCode)
    {
        var document = await _repository.GetByIdAsync(scanDocCode);
        if (document is null || string.IsNullOrWhiteSpace(document.image)) return NotFound();

        var path = ResolveStoredPath(document.image);
        if (path is null || !System.IO.File.Exists(path)) return NotFound(new { error = "The scan file is not available." });
        return PhysicalFile(path, GetContentType(path), enableRangeProcessing: true);
    }

    [HttpDelete("{scanDocCode:int}")]
    public async Task<ActionResult> Delete(int scanDocCode)
    {
        try
        {
            var document = await _repository.GetByIdAsync(scanDocCode);
            if (document is null) return NotFound();
            await _repository.DeleteAsync(scanDocCode, GetCurrentUserId());
            var path = ResolveStoredPath(document.image);
            if (path is not null && System.IO.File.Exists(path)) System.IO.File.Delete(path);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting taxi requisition scan {ScanDocCode}", scanDocCode);
            return StatusCode(500, new { error = "Failed to delete taxi requisition scan" });
        }
    }

    private async Task<IReadOnlyList<object>> AddVehicleDetailsAsync(IEnumerable<TaxiScanDoc> documents)
    {
        var vehicles = (await _vehicleRepository.GetAllAsync()).ToDictionary(vehicle => vehicle.vmf_code);
        return documents.Select(document => MapDocument(document, vehicles.GetValueOrDefault(document.vmf_code))).ToList();
    }

    private async Task<object> MapDocumentAsync(TaxiScanDoc document)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(document.vmf_code);
        return MapDocument(document, vehicle);
    }

    private static object MapDocument(TaxiScanDoc document, FIS.Core.Domain.Entities.Vehicle? vehicle) => new
    {
        scan_doc_code = document.taxi_scandoc_code,
        vmf_code = document.vmf_code,
        image = document.image,
        period_begin = document.period_begin,
        period_end = document.period_end,
        date_created = document.date_created == default ? (DateTime?)null : document.date_created,
        date_updated = document.date_updated,
        fleet_number = vehicle?.fleet_number,
        registration_number = vehicle?.registration_number,
        file_url = $"/api/taxi-scan-docs/{document.taxi_scandoc_code}/file",
    };

    private string? ResolveStoredPath(string? image)
    {
        if (string.IsNullOrWhiteSpace(image)) return null;
        var fileName = Path.GetFileName(image);
        if (!string.Equals(fileName, image, StringComparison.Ordinal)) return null;

        var configuredBasePath = _configuration["TaxiScanStorage:BasePath"] ?? "uploads/taxi-scan";
        var configuredPath = Path.Combine(Directory.GetCurrentDirectory(), configuredBasePath, fileName);
        if (System.IO.File.Exists(configuredPath)) return configuredPath;

        // Existing deployments may still keep the original scan_img directory.
        return Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "scandocs", "scan_img", fileName);
    }

    private static string GetContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        _ => "application/octet-stream",
    };
}
