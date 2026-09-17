using System.Globalization;
using FIS.Api.Services;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// Licence certificate scanning API. Modern vehicle_documents records are
/// preferred when that expanded table exists; original scan_docs records are
/// read and written when it is the available client-era source.
/// </summary>
[ApiController]
[Authorize(Roles = "Licence")]
[Route("api/licence-certificates")]
public sealed class LicenseCertificatesController : BaseApiController
{
    private const int DefaultPageSize = 24;
    private const int MaximumPageSize = 100;

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/gif",
    };

    private readonly ILicenseCertificateRepository _repository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly LegacyVehicleScopeService _vehicleScope;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LicenseCertificatesController> _logger;

    public LicenseCertificatesController(
        ILicenseCertificateRepository repository,
        IVehicleRepository vehicleRepository,
        LegacyVehicleScopeService vehicleScope,
        IConfiguration configuration,
        ILogger<LicenseCertificatesController> logger
    )
    {
        _repository = repository;
        _vehicleRepository = vehicleRepository;
        _vehicleScope = vehicleScope;
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
            _logger.LogError(ex, "Error listing licence certificates");
            return StatusCode(500, new { error = "Failed to retrieve licence certificates" });
        }
    }

    [HttpGet("page")]
    public async Task<ActionResult> GetPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize
    )
    {
        try
        {
            var allowedSites = await ResolveAllowedVehicleSiteCodesAsync();
            var result = allowedSites is null
                ? await _repository.GetPageAsync(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, MaximumPageSize)
                )
                : await GetAccessibleCertificatePageAsync(page, pageSize, allowedSites);
            return Ok(
                new
                {
                    items = result.Items.Select(item =>
                            MapDocument(item.Document, item.FleetNumber, item.RegistrationNumber)
                        )
                        .ToList(),
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.Total,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing a page of licence certificates");
            return StatusCode(500, new { error = "Failed to retrieve licence certificates" });
        }
    }

    [HttpGet("vehicle/{vmfCode:int}")]
    public async Task<ActionResult> GetByVehicle(int vmfCode)
    {
        try
        {
            if (await GetAccessibleVehicleAsync(vmfCode) is null)
                return NotFound(new { error = "Vehicle not found" });
            var documents = await _repository.GetByVehicleAsync(vmfCode);
            return Ok(await AddVehicleDetailsAsync(documents));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error listing licence certificates for vehicle {VmfCode}",
                vmfCode
            );
            return StatusCode(500, new { error = "Failed to retrieve licence certificates" });
        }
    }

    [HttpGet("missing")]
    public async Task<ActionResult> GetMissing([FromQuery] string? location = null)
    {
        try
        {
            var normalizedLocation = location?.Trim().ToLowerInvariant();
            short? locationCode = normalizedLocation switch
            {
                null or "" or "all" => null,
                "jhb" => (short)1,
                "pta" => (short)2,
                _ => null,
            };
            if (location is not null && locationCode is null)
                return BadRequest(new { error = "Location must be jhb or pta." });

            var vehicles = (await _vehicleRepository.GetActiveVehiclesAsync(
                await ResolveAllowedVehicleSiteCodesAsync(),
                GetCurrentUserId()
            ))
                .Where(vehicle =>
                    !locationCode.HasValue || vehicle.location_code == locationCode.Value
                )
                .ToList();
            var certificateVehicleCodes = (await _repository.GetAllAsync())
                .Select(document => document.vmf_code)
                .ToHashSet();
            var missing = vehicles
                .Where(vehicle => !certificateVehicleCodes.Contains(vehicle.vmf_code))
                .OrderBy(vehicle => vehicle.fleet_number)
                .Select(
                    (vehicle, index) =>
                        new
                        {
                            number = index + 1,
                            vmf_code = vehicle.vmf_code,
                            fleet_number = vehicle.fleet_number,
                            registration_number = vehicle.registration_number,
                            location_code = vehicle.location_code,
                        }
                )
                .ToList();
            return Ok(
                new
                {
                    location = normalizedLocation is "jhb" or "pta" ? normalizedLocation : "all",
                    total_count = missing.Count,
                    vehicles = missing,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing vehicles without licence certificates");
            return StatusCode(
                500,
                new { error = "Failed to retrieve vehicles without licence certificates" }
            );
        }
    }

    [HttpGet("missing/page")]
    public async Task<ActionResult> GetMissingPage(
        [FromQuery] string? location = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize
    )
    {
        try
        {
            var normalizedLocation = location?.Trim().ToLowerInvariant();
            short? locationCode = normalizedLocation switch
            {
                null or "" or "all" => null,
                "jhb" => (short)1,
                "pta" => (short)2,
                _ => null,
            };
            if (location is not null && locationCode is null)
                return BadRequest(new { error = "Location must be jhb or pta." });

            var result = await GetAccessibleMissingCertificatePageAsync(
                locationCode,
                page,
                pageSize
            );
            var numberOffset = checked((result.Page - 1) * result.PageSize);
            return Ok(
                new
                {
                    location = normalizedLocation is "jhb" or "pta" ? normalizedLocation : "all",
                    vehicles = result.Items.Select(
                            (vehicle, index) =>
                                new
                                {
                                    number = numberOffset + index + 1,
                                    vmf_code = vehicle.VmfCode,
                                    fleet_number = vehicle.FleetNumber,
                                    registration_number = vehicle.RegistrationNumber,
                                    location_code = vehicle.LocationCode,
                                }
                        )
                        .ToList(),
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.Total,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing a page of vehicles without licence certificates");
            return StatusCode(
                500,
                new { error = "Failed to retrieve vehicles without licence certificates" }
            );
        }
    }

    [HttpPost]
    [RequestSizeLimit(20_971_520)]
    public async Task<ActionResult> Upload(
        [FromForm] int vmfCode,
        IFormFile file,
        [FromForm] DateTime? periodBegin = null,
        [FromForm] DateTime? periodEnd = null
    )
    {
        string? absolutePath = null;
        try
        {
            if (vmfCode <= 0)
                return BadRequest(new { error = "A valid vehicle is required." });
            if (periodBegin.HasValue && periodEnd.HasValue && periodEnd.Value < periodBegin.Value)
                return BadRequest(
                    new { error = "The certificate end date must be on or after the begin date." }
                );
            if (file is null || file.Length == 0)
                return BadRequest(new { error = "A scanned licence certificate is required." });
            if (!AllowedMimeTypes.Contains(file.ContentType ?? string.Empty))
                return BadRequest(
                    new { error = "Only JPG, PNG, or GIF image scans are accepted." }
                );

            var vehicle = await GetAccessibleVehicleAsync(vmfCode);
            if (vehicle is null)
                return NotFound(new { error = $"Vehicle {vmfCode} not found" });
            if (await _repository.HasAnyForVehicleAsync(vmfCode))
                return Conflict(
                    new
                    {
                        error = "A licence certificate is already uploaded for this vehicle. Delete the existing scan before uploading a replacement.",
                    }
                );

            var source = await _repository.GetPreferredWriteSourceAsync();
            if (source is null)
                return StatusCode(
                    503,
                    new
                    {
                        error = "Neither the modern vehicle document table nor the legacy scan table is available.",
                    }
                );

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (
                !new[] { ".jpg", ".jpeg", ".png", ".gif" }.Contains(
                    extension,
                    StringComparer.OrdinalIgnoreCase
                )
            )
                return BadRequest(
                    new { error = "The uploaded scan must use a JPG, PNG, or GIF file extension." }
                );
            var storedName = $"{Guid.NewGuid():N}{extension}";
            if (source.Equals("modern", StringComparison.OrdinalIgnoreCase))
            {
                var basePath = _configuration["DocumentStorage:BasePath"] ?? "uploads/documents";
                var relativePath = Path.Combine(vmfCode.ToString(), "Licence", storedName)
                    .Replace('\\', '/');
                absolutePath = ResolveSafePath(basePath, relativePath);
                if (absolutePath is null)
                    return StatusCode(500, new { error = "The document storage path is invalid." });
                Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
                await using (
                    var stream = new FileStream(
                        absolutePath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None
                    )
                )
                    await file.CopyToAsync(stream);

                var document = await _repository.CreateAsync(
                    new LicenseCertificateDocument
                    {
                        Source = "modern",
                        vmf_code = vmfCode,
                        document_description = BuildModernDescription(periodBegin, periodEnd),
                        original_file_name = Path.GetFileName(file.FileName),
                        stored_file_path = relativePath,
                        mime_type = file.ContentType!.ToLowerInvariant(),
                        file_size_bytes = file.Length,
                        period_begin = periodBegin,
                        period_end = periodEnd,
                    },
                    GetCurrentUserId()
                );
                return CreatedAtAction(
                    nameof(GetByKey),
                    new
                    {
                        source = document.Source,
                        documentKey = document.DocumentKey,
                        vmfCode,
                    },
                    await MapDocumentAsync(document)
                );
            }

            var legacyBasePath =
                _configuration["LegacyScanStorage:BasePath"] ?? "wwwroot/scandocs/scan_img";
            absolutePath = ResolveSafePath(legacyBasePath, storedName);
            if (absolutePath is null)
                return StatusCode(500, new { error = "The legacy scan storage path is invalid." });
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            await using (
                var stream = new FileStream(
                    absolutePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None
                )
            )
                await file.CopyToAsync(stream);
            var legacyDocument = await _repository.CreateAsync(
                new LicenseCertificateDocument
                {
                    Source = "legacy",
                    vmf_code = vmfCode,
                    image = storedName,
                    mime_type = file.ContentType!.ToLowerInvariant(),
                    period_begin = periodBegin,
                    period_end = periodEnd,
                },
                GetCurrentUserId()
            );
            return CreatedAtAction(
                nameof(GetByKey),
                new
                {
                    source = legacyDocument.Source,
                    documentKey = legacyDocument.DocumentKey,
                    vmfCode,
                },
                await MapDocumentAsync(legacyDocument)
            );
        }
        catch (Exception ex)
        {
            if (absolutePath is not null && System.IO.File.Exists(absolutePath))
                System.IO.File.Delete(absolutePath);
            _logger.LogError(
                ex,
                "Error uploading licence certificate for vehicle {VmfCode}",
                vmfCode
            );
            return StatusCode(500, new { error = "Failed to upload licence certificate" });
        }
    }

    [HttpGet("{source}/{documentKey}")]
    public async Task<ActionResult> GetByKey(
        string source,
        string documentKey,
        [FromQuery] int vmfCode
    )
    {
        var document = await _repository.GetByKeyAsync(source, vmfCode, documentKey);
        if (document is not null && await GetAccessibleVehicleAsync(vmfCode) is null)
            document = null;
        return document is null
            ? NotFound(new { error = "Licence certificate not found" })
            : Ok(await MapDocumentAsync(document));
    }

    [HttpGet("{source}/{documentKey}/file")]
    public async Task<ActionResult> Download(
        string source,
        string documentKey,
        [FromQuery] int vmfCode
    )
    {
        var document = await _repository.GetByKeyAsync(source, vmfCode, documentKey);
        if (document is not null && await GetAccessibleVehicleAsync(vmfCode) is null)
            document = null;
        if (document is null)
            return NotFound(new { error = "Licence certificate not found" });
        var path = ResolveDocumentPath(document);
        return path is null || !System.IO.File.Exists(path)
            ? NotFound(new { error = "The certificate scan file is not available." })
            : PhysicalFile(path, document.mime_type, enableRangeProcessing: true);
    }

    [HttpDelete("{source}/{documentKey}")]
    public async Task<ActionResult> Delete(
        string source,
        string documentKey,
        [FromQuery] int vmfCode
    )
    {
        try
        {
            var document = await _repository.GetByKeyAsync(source, vmfCode, documentKey);
            if (document is not null && await GetAccessibleVehicleAsync(vmfCode) is null)
                document = null;
            if (document is null)
                return NotFound(new { error = "Licence certificate not found" });
            await _repository.DeleteAsync(source, vmfCode, documentKey, GetCurrentUserId());
            var path = ResolveDocumentPath(document);
            if (path is not null && System.IO.File.Exists(path))
                System.IO.File.Delete(path);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error deleting licence certificate {Source}/{DocumentKey}",
                source,
                documentKey
            );
            return StatusCode(500, new { error = "Failed to delete licence certificate" });
        }
    }

    private async Task<IReadOnlySet<short>?> ResolveAllowedVehicleSiteCodesAsync() =>
        await _vehicleScope.ResolveAllowedSiteCodesAsync(User, HttpContext.RequestAborted);

    private async Task<FIS.Core.Domain.Entities.Vehicle?> GetAccessibleVehicleAsync(int vmfCode) =>
        await _vehicleRepository.GetByIdAsync(
            vmfCode,
            await ResolveAllowedVehicleSiteCodesAsync(),
            GetCurrentUserId()
        );

    private async Task<LicenseCertificatePage> GetAccessibleCertificatePageAsync(
        int page,
        int pageSize,
        IReadOnlySet<short> allowedSiteCodes
    )
    {
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
        var vehicles = (await _vehicleRepository.GetActiveVehiclesAsync(
            allowedSiteCodes,
            GetCurrentUserId()
        )).ToDictionary(vehicle => vehicle.vmf_code);
        var items = (await _repository.GetAllAsync())
            .Where(document => vehicles.ContainsKey(document.vmf_code))
            .Select(document => new LicenseCertificatePageItem(
                document,
                vehicles[document.vmf_code].fleet_number,
                vehicles[document.vmf_code].registration_number
            ))
            .OrderBy(item => item.FleetNumber ?? string.Empty)
            .ThenBy(item => item.Document.vmf_code)
            .ThenByDescending(item => item.Document.period_begin ?? DateTime.MinValue)
            .ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(items.Count / (double)normalizedPageSize));
        var normalizedPage = Math.Min(Math.Max(1, page), totalPages);
        return new LicenseCertificatePage(
            items.Skip((normalizedPage - 1) * normalizedPageSize).Take(normalizedPageSize).ToList(),
            normalizedPage,
            normalizedPageSize,
            items.Count
        );
    }

    private async Task<MissingLicenseCertificatePage> GetAccessibleMissingCertificatePageAsync(
        short? locationCode,
        int page,
        int pageSize
    )
    {
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
        var vehicles = (await _vehicleRepository.GetActiveVehiclesAsync(
            await ResolveAllowedVehicleSiteCodesAsync(),
            GetCurrentUserId()
        ))
            .Where(vehicle => !locationCode.HasValue || vehicle.location_code == locationCode.Value)
            .ToList();
        var certificateVehicleCodes = (await _repository.GetAllAsync())
            .Select(document => document.vmf_code)
            .ToHashSet();
        var items = vehicles
            .Where(vehicle => !certificateVehicleCodes.Contains(vehicle.vmf_code))
            .OrderBy(vehicle => vehicle.fleet_number)
            .Select(vehicle => new MissingLicenseCertificatePageItem(
                vehicle.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                vehicle.location_code
            ))
            .ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(items.Count / (double)normalizedPageSize));
        var normalizedPage = Math.Min(Math.Max(1, page), totalPages);
        return new MissingLicenseCertificatePage(
            items.Skip((normalizedPage - 1) * normalizedPageSize).Take(normalizedPageSize).ToList(),
            normalizedPage,
            normalizedPageSize,
            items.Count
        );
    }

    private async Task<IReadOnlyList<object>> AddVehicleDetailsAsync(
        IEnumerable<LicenseCertificateDocument> documents
    )
    {
        var vehicles = (await _vehicleRepository.GetActiveVehiclesAsync(
            await ResolveAllowedVehicleSiteCodesAsync(),
            GetCurrentUserId()
        )).ToDictionary(vehicle =>
            vehicle.vmf_code
        );
        return documents
            .Where(document => vehicles.ContainsKey(document.vmf_code))
            .Select(document =>
                MapDocument(document, vehicles.GetValueOrDefault(document.vmf_code))
            )
            .ToList();
    }

    private async Task<object> MapDocumentAsync(LicenseCertificateDocument document) =>
        MapDocument(document, await GetAccessibleVehicleAsync(document.vmf_code));

    private object MapDocument(
        LicenseCertificateDocument document,
        FIS.Core.Domain.Entities.Vehicle? vehicle
    ) => MapDocument(document, vehicle?.fleet_number, vehicle?.registration_number);

    private object MapDocument(
        LicenseCertificateDocument document,
        string? fleetNumber,
        string? registrationNumber
    ) =>
        new
        {
            source = document.Source,
            document_key = document.DocumentKey,
            vmf_code = document.vmf_code,
            fleet_number = fleetNumber,
            registration_number = registrationNumber,
            image = document.image,
            original_file_name = document.original_file_name ?? document.image,
            mime_type = document.mime_type,
            file_size_bytes = document.file_size_bytes > 0 ? document.file_size_bytes : (long?)null,
            period_begin = document.period_begin,
            period_end = document.period_end,
            date_created = document.date_created,
            file_url = $"/api/licence-certificates/{document.Source}/{Uri.EscapeDataString(document.DocumentKey)}/file?vmfCode={document.vmf_code}",
        };

    private static string BuildModernDescription(DateTime? periodBegin, DateTime? periodEnd)
    {
        var begin =
            periodBegin?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
        var end = periodEnd?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
        return periodBegin.HasValue || periodEnd.HasValue
            ? $"Scanned licence certificate [period:{begin}..{end}]"
            : "Scanned licence certificate";
    }

    private string? ResolveDocumentPath(LicenseCertificateDocument document) =>
        document.Source.Equals("modern", StringComparison.OrdinalIgnoreCase)
            ? ResolveSafePath(
                _configuration["DocumentStorage:BasePath"] ?? "uploads/documents",
                document.stored_file_path
            )
            : ResolveLegacyPath(document.image);

    private string? ResolveLegacyPath(string? image)
    {
        if (
            string.IsNullOrWhiteSpace(image)
            || !string.Equals(Path.GetFileName(image), image, StringComparison.Ordinal)
        )
            return null;
        var configured = ResolveSafePath(
            _configuration["LegacyScanStorage:BasePath"] ?? "wwwroot/scandocs/scan_img",
            image
        );
        if (configured is not null && System.IO.File.Exists(configured))
            return configured;
        var historical = ResolveSafePath("wwwroot/ScanDocs/scan_img", image);
        return historical is not null && System.IO.File.Exists(historical)
            ? historical
            : configured;
    }

    private static string? ResolveSafePath(string basePath, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;
        var root = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), basePath));
        var fullPath = Path.GetFullPath(
            Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar))
        );
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : null;
    }
}
