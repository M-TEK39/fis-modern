using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;

namespace FIS.Api.Services;

/// <summary>
/// Fuel card business operations over the compatibility repository. The
/// repository negotiates the legacy and expanded table shapes; this service
/// keeps the legacy ExpReason status transitions unchanged.
/// </summary>
public sealed class FuelCardManagementService
{
    private readonly IFuelCardRepository _fuelCardRepository;
    private readonly IVehicleRepository _vehicleRepository;

    public FuelCardManagementService(
        IFuelCardRepository fuelCardRepository,
        IVehicleRepository vehicleRepository
    )
    {
        _fuelCardRepository = fuelCardRepository;
        _vehicleRepository = vehicleRepository;
    }

    public async Task<FuelCard> IssueFuelCardAsync(
        int vmfCode,
        string receiverName,
        string receiverTel,
        int siteCode,
        int currentUserId = 0
    )
    {
        var vehicle =
            await _vehicleRepository.GetByIdAsync(vmfCode)
            ?? throw new InvalidOperationException($"Vehicle with VMF code {vmfCode} not found");

        var existingCard = (
            await _fuelCardRepository.GetFuelCardsByVehicleAsync(vmfCode)
        ).FirstOrDefault(card =>
            string.Equals(card.ExpReason?.Trim(), "In Service", StringComparison.OrdinalIgnoreCase)
        );
        if (existingCard is not null)
        {
            throw new InvalidOperationException(
                $"Vehicle {vmfCode} already has an active fuel card"
            );
        }

        var now = DateTime.Now;
        var fuelCard = new FuelCard
        {
            vmf_code = vehicle.vmf_code,
            Counter = 1,
            card_number = $"FC{vmfCode:D6}",
            PAN_number = $"PAN{now:yyyyMM}{vmfCode:D4}",
            PetReceiver = receiverName,
            PetRecTel = receiverTel,
            PetTaken = now,
            PetExpire = now.AddYears(2),
            ExpReason = "In Service",
            PetComment = "Auto-issued via modern system",
            Status_date = now,
            Petrecsite = (short)siteCode,
            Petprint = "Y",
            Garage = "P",
        };

        return await _fuelCardRepository.CreateAsync(fuelCard, currentUserId);
    }

    public async Task<bool> ReturnFuelCardAsync(
        int fuelCardCode,
        string reason,
        int currentUserId = 0
    )
    {
        var fuelCard = await _fuelCardRepository.GetByIdAsync(fuelCardCode);
        if (fuelCard is null)
            return false;

        var validReasons = new[]
        {
            "Card to Bank",
            "Card to GG",
            "Withdrawn",
            "Sold",
            "Privatised",
            "Hijacked",
        };
        if (!validReasons.Contains(reason, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Invalid return reason. Must be one of: {string.Join(", ", validReasons)}"
            );
        }

        fuelCard.ExpReason = reason;
        fuelCard.Status_date = DateTime.Now;
        fuelCard.PetComment = string.IsNullOrWhiteSpace(fuelCard.PetComment)
            ? $"Returned: {reason} on {DateTime.Now:yyyy-MM-dd}"
            : $"{fuelCard.PetComment} | Returned: {reason} on {DateTime.Now:yyyy-MM-dd}";

        await _fuelCardRepository.UpdateAsync(fuelCard, currentUserId);
        return true;
    }

    public async Task<FuelCardAllocationReport> GetAllocationReportAsync(
        int? siteCode = null,
        int page = 1,
        int pageSize = 24,
        CancellationToken cancellationToken = default
    )
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var fuelCards = (await _fuelCardRepository.GetActiveFuelCardsAsync()).ToList();
        var filteredCards = siteCode.HasValue
            ? fuelCards.Where(card => card.Petrecsite == siteCode.Value).ToList()
            : fuelCards.ToList();
        var recentActivityPage = await _fuelCardRepository.GetRecentActivityPageAsync(
            siteCode,
            page,
            pageSize,
            cancellationToken
        );

        var statusGroups = filteredCards
            .GroupBy(card =>
                string.IsNullOrWhiteSpace(card.ExpReason) ? "Unknown" : card.ExpReason.Trim()
            )
            .ToDictionary(group => group.Key, group => group.Count());

        return new FuelCardAllocationReport
        {
            TotalCards = filteredCards.Count,
            ActiveCards = filteredCards.Count(card =>
                string.Equals(
                    card.ExpReason?.Trim(),
                    "In Service",
                    StringComparison.OrdinalIgnoreCase
                )
            ),
            ReturnedCards = filteredCards.Count(card =>
                !string.Equals(
                    card.ExpReason?.Trim(),
                    "In Service",
                    StringComparison.OrdinalIgnoreCase
                )
            ),
            StatusBreakdown = statusGroups,
            CardsByGarage = filteredCards
                .GroupBy(card =>
                    string.IsNullOrWhiteSpace(card.Garage) ? "Unknown" : card.Garage.Trim()
                )
                .ToDictionary(group => group.Key, group => group.Count()),
            ExpiringCards = filteredCards.Count(card =>
                card.PetExpire.HasValue
                && card.PetExpire.Value <= DateTime.Now.AddMonths(3)
                && string.Equals(
                    card.ExpReason?.Trim(),
                    "In Service",
                    StringComparison.OrdinalIgnoreCase
                )
            ),
            RecentActivity = recentActivityPage
                .Items.Select(card => new FuelCardActivity
                {
                    CardNumber = card.card_number ?? "Unknown",
                    VmfCode = card.vmf_code ?? 0,
                    Action = card.ExpReason ?? "Unknown",
                    Date = card.Status_date ?? DateTime.MinValue,
                    Receiver = card.PetReceiver ?? "Unknown",
                })
                .ToList(),
            Page = recentActivityPage.Page,
            PageSize = recentActivityPage.PageSize,
            Total = recentActivityPage.Total,
            TotalPages = recentActivityPage.TotalPages,
        };
    }
}

public sealed class FuelCardAllocationReport
{
    public int TotalCards { get; set; }
    public int ActiveCards { get; set; }
    public int ReturnedCards { get; set; }
    public int ExpiringCards { get; set; }
    public Dictionary<string, int> StatusBreakdown { get; set; } = new();
    public Dictionary<string, int> CardsByGarage { get; set; } = new();
    public List<FuelCardActivity> RecentActivity { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 24;
    public int Total { get; set; }
    public int TotalPages { get; set; } = 1;
}

public sealed class FuelCardActivity
{
    public string CardNumber { get; set; } = string.Empty;
    public int VmfCode { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Receiver { get; set; } = string.Empty;
}
