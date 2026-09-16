using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FuelCardController : BaseApiController
{
    private readonly IFuelCardRepository _repository;
    private readonly IPrivateHireFuelCardRepository _privateHireFuelCardRepository;
    private readonly ILogger<FuelCardController> _logger;

    public FuelCardController(
        IFuelCardRepository repository,
        IPrivateHireFuelCardRepository privateHireFuelCardRepository,
        ILogger<FuelCardController> logger
    )
    {
        _repository = repository;
        _privateHireFuelCardRepository = privateHireFuelCardRepository;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Roles = "Fuelcards")]
    public async Task<ActionResult<IEnumerable<FuelCard>>> GetAll()
    {
        try
        {
            var items = await _repository.GetActiveFuelCardsAsync();
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fuel cards");
            return StatusCode(500, "Error retrieving fuel cards");
        }
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Fuelcards")]
    public async Task<ActionResult<FuelCard>> GetById(int id)
    {
        try
        {
            var item = await _repository.GetByIdAsync(id);
            return item == null ? NotFound() : Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fuel card {Id}", id);
            return StatusCode(500, "Error retrieving fuel card");
        }
    }

    [HttpGet("vehicle/{vmfCode}")]
    [Authorize(Roles = "Fuelcards")]
    public async Task<ActionResult<IEnumerable<FuelCard>>> GetByVehicle(int vmfCode)
    {
        try
        {
            var items = await _repository.GetFuelCardsByVehicleAsync(vmfCode);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fuel cards for vehicle {VmfCode}", vmfCode);
            return StatusCode(500, "Error retrieving fuel cards");
        }
    }

    [HttpPost]
    [Authorize(Roles = "Fuelcards")]
    public async Task<ActionResult<FuelCard>> Create([FromBody] FuelCard item)
    {
        try
        {
            var created = await _repository.CreateAsync(item, GetCurrentUserId());
            return CreatedAtAction(nameof(GetById), new { id = created.Fuel_card_code }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating fuel card");
            return StatusCode(500, "Error creating fuel card");
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Fuelcards")]
    public async Task<ActionResult> Update(int id, [FromBody] FuelCard item)
    {
        try
        {
            if (id != item.Fuel_card_code)
                return BadRequest("ID mismatch");

            await _repository.UpdateAsync(item, GetCurrentUserId());
            return Ok(new { message = "Fuel card updated successfully", id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating fuel card {Id}", id);
            return StatusCode(500, "Error updating fuel card");
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Fuelcards")]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            await _repository.DeleteAsync(id, GetCurrentUserId());
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting fuel card {Id}", id);
            return StatusCode(500, "Error deleting fuel card");
        }
    }

    #region Specialized Operations

    /// <summary>
    /// Get fuel card menu options
    /// </summary>
    [HttpGet("menu")]
    [Authorize(Roles = "Fuelcards")]
    public ActionResult<FuelCardMenuDto> GetMenu()
    {
        var menu = new FuelCardMenuDto
        {
            Options = new List<string>
            {
                "Maintenance",
                "Collection (Multiple)",
                "Delete Card",
                "Private Hire Maintenance",
                "Private Hire Collection",
                "Private Hire Delete",
                "Reports",
            },
        };
        return Ok(menu);
    }

    /// <summary>
    /// Search fuel cards for vehicle maintenance
    /// </summary>
    [HttpPost("maintenance/vehicle/search")]
    [Authorize(Roles = "Fuelcards")]
    public async Task<ActionResult<FuelCardSearchResultDto>> SearchVehicleForMaintenance(
        [FromBody] FuelCardVehicleSearchDto request
    )
    {
        try
        {
            // Try to parse as VMF code (numeric), otherwise search by card number
            if (int.TryParse(request.VehicleIdentifier, out int vmfCode))
            {
                var cards = await _repository.GetFuelCardsByVehicleAsync(vmfCode);
                var cardsList = cards.ToList();

                return Ok(
                    new FuelCardSearchResultDto
                    {
                        Found = cardsList.Any(),
                        Message = cardsList.Any()
                            ? $"Found {cardsList.Count} fuel card(s)"
                            : "No fuel cards found for this vehicle",
                        FuelCards = cardsList
                            .Select(c => new FuelCardSummaryDto
                            {
                                FuelCardCode = c.Fuel_card_code,
                                CardNumber = c.card_number,
                                VmfCode = c.vmf_code,
                                Status = !c.is_deleted ? "Active" : "Inactive",
                            })
                            .ToList(),
                    }
                );
            }
            else
            {
                var card = await _repository.GetByCardNumberAsync(request.VehicleIdentifier);
                return Ok(
                    new FuelCardSearchResultDto
                    {
                        Found = card != null,
                        Message = card != null ? "Fuel card found" : "Fuel card not found",
                        FuelCards =
                            card != null
                                ? new List<FuelCardSummaryDto>
                                {
                                    new FuelCardSummaryDto
                                    {
                                        FuelCardCode = card.Fuel_card_code,
                                        CardNumber = card.card_number,
                                        VmfCode = card.vmf_code,
                                        Status = !card.is_deleted ? "Active" : "Inactive",
                                    },
                                }
                                : new List<FuelCardSummaryDto>(),
                    }
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error searching fuel cards for vehicle {VehicleIdentifier}",
                request.VehicleIdentifier
            );
            return StatusCode(500, "Error searching fuel cards");
        }
    }

    /// <summary>
    /// Search fuel cards for multiple collection
    /// </summary>
    [HttpPost("collection/multiple/search")]
    [Authorize(Roles = "Fuelcards")]
    public async Task<ActionResult<FuelCardCollectionResultDto>> SearchCollectionMultiple(
        [FromBody] FuelCardCollectionSearchDto request
    )
    {
        try
        {
            var allCards = await _repository.GetActiveFuelCardsAsync();

            // Filter by date range and site if provided
            var filteredCards = allCards
                .Where(c =>
                    c.date_created >= request.StartDate && c.date_created <= request.EndDate
                )
                .Where(c => !request.SiteCode.HasValue || c.Petrecsite == request.SiteCode)
                .ToList();

            return Ok(
                new FuelCardCollectionResultDto
                {
                    Success = true,
                    Message = $"Found {filteredCards.Count} fuel card(s) for collection",
                    Cards = filteredCards
                        .Select(c => new FuelCardSummaryDto
                        {
                            FuelCardCode = c.Fuel_card_code,
                            CardNumber = c.card_number,
                            VmfCode = c.vmf_code,
                            Status = !c.is_deleted ? "Active" : "Inactive",
                        })
                        .ToList(),
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching fuel cards for collection");
            return StatusCode(500, "Error searching fuel cards for collection");
        }
    }

    /// <summary>
    /// Search fuel cards for deletion
    /// </summary>
    [HttpPost("delete/search")]
    [Authorize(Roles = "Fuelcards")]
    public async Task<ActionResult<FuelCardSearchResultDto>> SearchForDelete(
        [FromBody] FuelCardDeleteSearchDto request
    )
    {
        try
        {
            var card = await _repository.GetByCardNumberAsync(request.CardNumber);

            return Ok(
                new FuelCardSearchResultDto
                {
                    Found = card != null,
                    Message =
                        card != null
                            ? "Fuel card found and ready for deletion"
                            : "Fuel card not found",
                    FuelCards =
                        card != null
                            ? new List<FuelCardSummaryDto>
                            {
                                new FuelCardSummaryDto
                                {
                                    FuelCardCode = card.Fuel_card_code,
                                    CardNumber = card.card_number,
                                    VmfCode = card.vmf_code,
                                    Status = !card.is_deleted ? "Active" : "Inactive",
                                },
                            }
                            : new List<FuelCardSummaryDto>(),
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error searching fuel card for deletion: {CardNumber}",
                request.CardNumber
            );
            return StatusCode(500, "Error searching fuel card");
        }
    }

    /// <summary>
    /// Search private hire fuel cards for maintenance
    /// </summary>
    [HttpPost("privatehire/maintenance/vehicle/search")]
    [Authorize(Roles = "Fuelcards")]
    public async Task<ActionResult<FuelCardSearchResultDto>> SearchPrivateHireVehicleForMaintenance(
        [FromBody] FuelCardVehicleSearchDto request
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.VehicleIdentifier))
            {
                return BadRequest("Vehicle identifier is required.");
            }

            var identifier = request.VehicleIdentifier.Trim();
            List<FuelCardSummaryDto> summaries;

            if (int.TryParse(identifier, out var privateHireCode))
            {
                var cards = await _privateHireFuelCardRepository.GetByPrivateHireCodeAsync(
                    privateHireCode
                );
                summaries = cards
                    .Select(c => new FuelCardSummaryDto
                    {
                        FuelCardCode = c.PHFuel_card_code,
                        CardNumber = c.card_number,
                        VmfCode = c.phv_code,
                        Status = !c.is_deleted ? "Active" : "Inactive",
                    })
                    .ToList();
            }
            else
            {
                var cardsByRegistration =
                    await _privateHireFuelCardRepository.GetByRegistrationNumberAsync(identifier);
                summaries = cardsByRegistration
                    .Select(c => new FuelCardSummaryDto
                    {
                        FuelCardCode = c.PHFuel_card_code,
                        CardNumber = c.card_number,
                        VmfCode = c.phv_code,
                        Status = !c.is_deleted ? "Active" : "Inactive",
                    })
                    .ToList();

                if (summaries.Count == 0)
                {
                    var cardByNumber = await _privateHireFuelCardRepository.GetByCardNumberAsync(
                        identifier
                    );
                    if (cardByNumber != null)
                    {
                        summaries.Add(
                            new FuelCardSummaryDto
                            {
                                FuelCardCode = cardByNumber.PHFuel_card_code,
                                CardNumber = cardByNumber.card_number,
                                VmfCode = cardByNumber.phv_code,
                                Status = !cardByNumber.is_deleted ? "Active" : "Inactive",
                            }
                        );
                    }
                }
            }

            return Ok(
                new FuelCardSearchResultDto
                {
                    Found = summaries.Count > 0,
                    Message =
                        summaries.Count > 0
                            ? $"Found {summaries.Count} private hire fuel card(s)"
                            : "No private hire fuel cards found",
                    FuelCards = summaries,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error searching private hire fuel cards for vehicle {VehicleIdentifier}",
                request.VehicleIdentifier
            );
            return StatusCode(500, "Error searching private hire fuel cards");
        }
    }

    /// <summary>
    /// Search private hire fuel cards for multiple collection
    /// </summary>
    [HttpPost("privatehire/collection/multiple/search")]
    [Authorize(Roles = "Fuelcards")]
    public async Task<
        ActionResult<FuelCardCollectionResultDto>
    > SearchPrivateHireCollectionMultiple([FromBody] FuelCardCollectionSearchDto request)
    {
        try
        {
            var allCards = await _privateHireFuelCardRepository.GetActiveFuelCardsAsync();

            var filteredCards = allCards.Where(c =>
                c.date_created >= request.StartDate && c.date_created <= request.EndDate
            );

            if (request.SiteCode.HasValue)
            {
                var siteCode = request.SiteCode.Value;
                var siteCards = await _privateHireFuelCardRepository.GetActiveFuelCardsBySiteAsync(
                    siteCode
                );
                var siteCardCodes = siteCards.Select(card => card.PHFuel_card_code).ToHashSet();
                filteredCards = filteredCards.Where(card =>
                    siteCardCodes.Contains(card.PHFuel_card_code)
                );
            }

            var resultCards = filteredCards.ToList();

            return Ok(
                new FuelCardCollectionResultDto
                {
                    Success = true,
                    Message = $"Found {resultCards.Count} private hire fuel card(s) for collection",
                    Cards = resultCards
                        .Select(c => new FuelCardSummaryDto
                        {
                            FuelCardCode = c.PHFuel_card_code,
                            CardNumber = c.card_number,
                            VmfCode = c.phv_code,
                            Status = !c.is_deleted ? "Active" : "Inactive",
                        })
                        .ToList(),
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching private hire fuel cards for collection");
            return StatusCode(500, "Error searching private hire fuel cards for collection");
        }
    }

    /// <summary>
    /// Search private hire fuel cards for deletion
    /// </summary>
    [HttpPost("privatehire/delete/search")]
    [Authorize(Roles = "Fuelcards")]
    public async Task<ActionResult<FuelCardSearchResultDto>> SearchPrivateHireForDelete(
        [FromBody] FuelCardDeleteSearchDto request
    )
    {
        try
        {
            var card = await _privateHireFuelCardRepository.GetByCardNumberAsync(
                request.CardNumber
            );

            return Ok(
                new FuelCardSearchResultDto
                {
                    Found = card != null,
                    Message =
                        card != null
                            ? "Private hire fuel card found and ready for deletion"
                            : "Private hire fuel card not found",
                    FuelCards =
                        card != null
                            ? new List<FuelCardSummaryDto>
                            {
                                new FuelCardSummaryDto
                                {
                                    FuelCardCode = card.PHFuel_card_code,
                                    CardNumber = card.card_number,
                                    VmfCode = card.phv_code,
                                    Status = !card.is_deleted ? "Active" : "Inactive",
                                },
                            }
                            : new List<FuelCardSummaryDto>(),
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error searching private hire fuel card for deletion: {CardNumber}",
                request.CardNumber
            );
            return StatusCode(500, "Error searching private hire fuel card");
        }
    }

    #endregion

    #region Reports

    /// <summary>
    /// Get fuel card reports menu
    /// </summary>
    [HttpGet("reports/menu")]
    [Authorize(Roles = "Reports")]
    public ActionResult<FuelCardReportMenuDto> GetReportsMenu()
    {
        var menu = new FuelCardReportMenuDto
        {
            Reports = new List<string>
            {
                "One Vehicle",
                "All Vehicles",
            },
        };
        return Ok(menu);
    }

    /// <summary>
    /// Generate latest fuel card report
    /// </summary>
    [HttpPost("reports/latest")]
    [Authorize(Roles = "Reports")]
    public async Task<ActionResult<FuelCardReportDto>> GetReportLatest(
        [FromBody] FuelCardLatestReportRequestDto request
    )
    {
        try
        {
            var cards = await _repository.GetActiveFuelCardsAsync();
            var asOfDate = request.AsOfDate ?? DateTime.Now;

            // Filter cards created/modified up to the specified date
            var reportCards = cards
                .Where(c => c.date_created <= asOfDate)
                .OrderByDescending(c => c.date_created)
                .Select(c => new
                {
                    c.Fuel_card_code,
                    c.card_number,
                    c.vmf_code,
                    SiteCode = c.Petrecsite,
                    IsActive = !c.is_deleted,
                    CreatedDate = c.date_created,
                    c.date_updated,
                })
                .ToList<object>();

            return Ok(new FuelCardReportDto { ReportType = "Latest", Data = reportCards });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating latest fuel card report");
            return StatusCode(500, "Error generating report");
        }
    }

    /// <summary>
    /// Generate fuel card report for one vehicle
    /// </summary>
    [HttpPost("reports/one-vehicle")]
    [Authorize(Roles = "Reports")]
    public async Task<ActionResult<FuelCardReportDto>> GetReportOneVehicle(
        [FromBody] FuelCardOneVehicleReportRequestDto request
    )
    {
        try
        {
            var cards = await _repository.GetFuelCardsByVehicleAsync(request.VmfCode);

            // Filter by date range
            var reportCards = cards
                .Where(c =>
                    c.date_created >= request.StartDate && c.date_created <= request.EndDate
                )
                .OrderBy(c => c.date_created)
                .Select(c => new
                {
                    c.Fuel_card_code,
                    c.card_number,
                    c.vmf_code,
                    SiteCode = c.Petrecsite,
                    IsActive = !c.is_deleted,
                    CreatedDate = c.date_created,
                    UpdatedDate = c.date_updated,
                    c.created_by_user_code,
                    c.modified_by_user_code,
                })
                .ToList<object>();

            return Ok(new FuelCardReportDto { ReportType = "OneVehicle", Data = reportCards });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error generating one vehicle fuel card report for VMF {VmfCode}",
                request.VmfCode
            );
            return StatusCode(500, "Error generating report");
        }
    }

    /// <summary>
    /// Generate fuel card report for all vehicles
    /// </summary>
    [HttpPost("reports/all-vehicles")]
    [Authorize(Roles = "Reports")]
    public async Task<ActionResult<FuelCardReportDto>> GetReportAllVehicles(
        [FromBody] FuelCardAllVehiclesReportRequestDto request
    )
    {
        try
        {
            var cards = await _repository.GetActiveFuelCardsAsync();

            // Filter by date range (department filtering removed - not in FuelCard entity)
            var reportCards = cards
                .Where(c =>
                    c.date_created >= request.StartDate && c.date_created <= request.EndDate
                )
                .OrderBy(c => c.vmf_code)
                .ThenBy(c => c.date_created)
                .Select(c => new
                {
                    c.Fuel_card_code,
                    c.card_number,
                    c.vmf_code,
                    SiteCode = c.Petrecsite,
                    IsActive = !c.is_deleted,
                    CreatedDate = c.date_created,
                    UpdatedDate = c.date_updated,
                })
                .ToList<object>();

            return Ok(new FuelCardReportDto { ReportType = "AllVehicles", Data = reportCards });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating all vehicles fuel card report");
            return StatusCode(500, "Error generating report");
        }
    }

    /// <summary>
    /// Generate private hire fuel card report for all vehicles
    /// </summary>
    [HttpPost("privatehire/reports/all")]
    [Authorize(Roles = "Reports")]
    public async Task<ActionResult<FuelCardReportDto>> GetReportPrivateHireAll(
        [FromBody] FuelCardPrivateHireReportRequestDto request
    )
    {
        try
        {
            var cards = await _repository.GetActiveFuelCardsAsync();

            // Filter by date range (private hire specific filtering would require vehicle type check)
            var reportCards = cards
                .Where(c =>
                    c.date_created >= request.StartDate && c.date_created <= request.EndDate
                )
                .OrderBy(c => c.vmf_code)
                .ThenBy(c => c.date_created)
                .Select(c => new
                {
                    c.Fuel_card_code,
                    c.card_number,
                    c.vmf_code,
                    SiteCode = c.Petrecsite,
                    IsActive = !c.is_deleted,
                    CreatedDate = c.date_created,
                    UpdatedDate = c.date_updated,
                })
                .ToList<object>();

            return Ok(new FuelCardReportDto { ReportType = "PrivateHireAll", Data = reportCards });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating private hire fuel card report");
            return StatusCode(500, "Error generating report");
        }
    }

    #endregion
}

#region Fuel Card DTOs
public class FuelCardMenuDto
{
    public List<string> Options { get; set; } = new();
}

public class FuelCardVehicleSearchDto
{
    public string VehicleIdentifier { get; set; } = "";
}

public class FuelCardSearchResultDto
{
    public bool Found { get; set; }
    public string Message { get; set; } = "";
    public List<FuelCardSummaryDto> FuelCards { get; set; } = new();
}

public class FuelCardSummaryDto
{
    public int FuelCardCode { get; set; }
    public string? CardNumber { get; set; }
    public int? VmfCode { get; set; }
    public string? Status { get; set; }
}

public class FuelCardCollectionSearchDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? SiteCode { get; set; }
}

public class FuelCardCollectionResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public List<FuelCardSummaryDto> Cards { get; set; } = new();
}

public class FuelCardDeleteSearchDto
{
    public string CardNumber { get; set; } = "";
}

public class FuelCardReportMenuDto
{
    public List<string> Reports { get; set; } = new();
}

public class FuelCardLatestReportRequestDto
{
    public DateTime? AsOfDate { get; set; }
}

public class FuelCardOneVehicleReportRequestDto
{
    public int VmfCode { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class FuelCardAllVehiclesReportRequestDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? DepartmentCode { get; set; }
}

public class FuelCardPrivateHireReportRequestDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class FuelCardReportDto
{
    public string ReportType { get; set; } = "";
    public List<object> Data { get; set; } = new();
}
#endregion
