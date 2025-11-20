using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Services;

/// <summary>
/// Fuel Card Management Service - Legacy Compatible Business Logic
///
/// Demonstrates how to create modern business logic that works with legacy database schemas
/// without requiring migrations. Uses real legacy Fuel_card table structure.
///
/// Key Legacy Compatibility Patterns:
/// - Works with existing CHAR fields and legacy naming conventions
/// - Handles legacy business rules (ExpReason values, status transitions)
/// - Preserves legacy audit trail and workflow patterns
/// - Uses vmf_code for vehicle relationships (not modern foreign keys)
/// </summary>
public class FuelCardManagementService
{
    private readonly FisDbContext _context;

    public FuelCardManagementService(FisDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Issue a new fuel card to a vehicle using legacy workflow patterns
    /// </summary>
    public async Task<FuelCard> IssueFuelCardAsync(
        int vmfCode,
        string receiverName,
        string receiverTel,
        int siteCode
    )
    {
        // Check if vehicle exists using legacy vmf_code pattern with explicit no tracking
        var vehicle = await _context
            .Vehicles.AsNoTracking()
            .Where(v => v.vmf_code == vmfCode) // Use exact legacy field name
            .Select(v => new
            {
                VehicleId = v.vmf_code, // Use vmf_code as vehicle ID
                VmfCode = v.vmf_code,
                RegistrationNumber = v.registration_number, // Use exact legacy field name
            })
            .FirstOrDefaultAsync();

        if (vehicle == null)
            throw new InvalidOperationException($"Vehicle with VMF code {vmfCode} not found");

        // Check for existing active fuel card (legacy business rule)
        var existingCard = await _context
            .FuelCards.AsNoTracking()
            .Where(fc => fc.vmf_code == vmfCode && fc.ExpReason == "In Service") // Use exact legacy field names
            .FirstOrDefaultAsync();

        if (existingCard != null)
            throw new InvalidOperationException(
                $"Vehicle {vmfCode} already has an active fuel card"
            );

        // Generate card number using legacy pattern (simplified for demo)
        var cardNumber = $"FC{vmfCode:D6}";
        var panNumber = $"PAN{DateTime.Now:yyyyMM}{vmfCode:D4}";

        // Create fuel card using legacy field patterns
        var fuelCard = new FuelCard
        {
            vmf_code = vmfCode, // Use exact legacy field name
            Counter = 1, // Legacy counter field
            card_number = cardNumber, // Use exact legacy field name
            PAN_number = panNumber, // Use exact legacy field name
            PetReceiver = receiverName,
            PetRecTel = receiverTel,
            PetTaken = DateTime.Now,
            PetExpire = DateTime.Now.AddYears(2), // 2-year expiry
            ExpReason = "In Service", // Legacy status field
            PetComment = "Auto-issued via modern system",
            Status_date = DateTime.Now, // Use exact legacy field name
            Petrecsite = (short)siteCode,
            Petprint = "Y", // Legacy print flag
            Garage = "P", // P = Production, J = Johannesburg legacy codes
        };

        _context.FuelCards.Add(fuelCard);
        await _context.SaveChangesAsync();

        return fuelCard;
    }

    /// <summary>
    /// Return/cancel a fuel card using legacy status transition patterns
    /// </summary>
    public async Task<bool> ReturnFuelCardAsync(int fuelCardCode, string reason)
    {
        var fuelCard = await _context.FuelCards.FindAsync(fuelCardCode);
        if (fuelCard == null)
            return false;

        // Legacy status transition - update ExpReason field
        var validReasons = new[]
        {
            "Card to Bank",
            "Card to GG",
            "Withdrawn",
            "Sold",
            "Privatised",
            "Hijacked",
        };
        if (!validReasons.Contains(reason))
            throw new ArgumentException(
                $"Invalid return reason. Must be one of: {string.Join(", ", validReasons)}"
            );

        fuelCard.ExpReason = reason;
        fuelCard.Status_date = DateTime.Now; // Use exact legacy field name
        fuelCard.PetComment += $" | Returned: {reason} on {DateTime.Now:yyyy-MM-dd}";

        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Get fuel card allocation report using legacy data patterns
    /// </summary>
    public async Task<FuelCardAllocationReport> GetAllocationReportAsync(int? siteCode = null)
    {
        var query = _context
            .FuelCards.AsNoTracking()
            .AsQueryable();

        if (siteCode.HasValue)
            query = query.Where(fc => fc.Petrecsite == siteCode);

        var fuelCards = await query.ToListAsync();

        // Group by legacy ExpReason status
        var statusGroups = fuelCards
            .GroupBy(fc => fc.ExpReason)
            .ToDictionary(g => g.Key ?? "Unknown", g => g.Count());

        return new FuelCardAllocationReport
        {
            TotalCards = fuelCards.Count,
            ActiveCards = fuelCards.Count(fc => fc.ExpReason == "In Service"),
            ReturnedCards = fuelCards.Count(fc => fc.ExpReason != "In Service"),
            StatusBreakdown = statusGroups,
            CardsByGarage = fuelCards
                .GroupBy(fc => fc.Garage)
                .ToDictionary(g => g.Key ?? "Unknown", g => g.Count()),
            ExpiringCards = fuelCards
                .Where(fc =>
                    fc.PetExpire.HasValue
                    && fc.PetExpire.Value <= DateTime.Now.AddMonths(3)
                    && fc.ExpReason == "In Service"
                )
                .Count(),
            RecentActivity = fuelCards
                .Where(fc =>
                    fc.Status_date.HasValue && fc.Status_date.Value >= DateTime.Now.AddDays(-30) // Use exact legacy field name
                )
                .OrderByDescending(fc => fc.Status_date) // Use exact legacy field name
                .Take(10)
                .Select(fc => new FuelCardActivity
                {
                    CardNumber = fc.card_number ?? "Unknown", // Use exact legacy field name
                    VmfCode = fc.vmf_code ?? 0, // Use exact legacy field name with null coalescing
                    Action = fc.ExpReason ?? "Unknown",
                    Date = fc.Status_date ?? DateTime.MinValue, // Use exact legacy field name
                    Receiver = fc.PetReceiver ?? "Unknown",
                })
                .ToList(),
        };
    }
}

// DTOs for business logic responses
public class FuelCardAllocationReport
{
    public int TotalCards { get; set; }
    public int ActiveCards { get; set; }
    public int ReturnedCards { get; set; }
    public int ExpiringCards { get; set; }
    public Dictionary<string, int> StatusBreakdown { get; set; } = new();
    public Dictionary<string, int> CardsByGarage { get; set; } = new();
    public List<FuelCardActivity> RecentActivity { get; set; } = new();
}

public class FuelCardActivity
{
    public string CardNumber { get; set; } = string.Empty;
    public int VmfCode { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Receiver { get; set; } = string.Empty;
}
