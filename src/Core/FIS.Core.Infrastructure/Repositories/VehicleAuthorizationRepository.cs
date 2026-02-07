using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Vehicles;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for vehicle authorization (pre-capture) operations
/// Manages vehicle inception approval workflow
/// </summary>
public class VehicleAuthorizationRepository : IVehicleAuthorizationRepository
{
    private readonly FisDbContext _context;

    public VehicleAuthorizationRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get vehicle authorization by ID
    /// </summary>
    public async Task<PreVehicleMaster?> GetByIdAsync(int preVehicleId)
    {
        return await _context.PreVehicleMasters
            .Include(v => v.Model)
            .Include(v => v.AuthorizedByUser)
            .Where(v => !v.is_deleted)
            .FirstOrDefaultAsync(v => v.temp_vmf_code == preVehicleId);
    }

    /// <summary>
    /// Get vehicle authorization by chassis number
    /// </summary>
    public async Task<PreVehicleMaster?> GetByChassisNumberAsync(string chassisNumber)
    {
        if (string.IsNullOrWhiteSpace(chassisNumber))
            return null;

        return await _context.PreVehicleMasters
            .Include(v => v.Model)
            .Include(v => v.AuthorizedByUser)
            .Where(v => !v.is_deleted)
            .FirstOrDefaultAsync(v => v.chassis_number == chassisNumber.Trim());
    }

    /// <summary>
    /// Get all vehicles awaiting authorization
    /// </summary>
    public async Task<IEnumerable<PreVehicleMaster>> GetPendingAuthorizationsAsync()
    {
        return await _context.PreVehicleMasters
            .Include(v => v.Model)
            .Include(v => v.CreatedByUser)
            .Where(v => !v.is_deleted && v.Authority_Status == "Awaiting Authorization")
            .OrderBy(v => v.date_created)
            .ToListAsync();
    }

    /// <summary>
    /// Get all authorized vehicles
    /// </summary>
    public async Task<IEnumerable<PreVehicleMaster>> GetAuthorizedVehiclesAsync()
    {
        return await _context.PreVehicleMasters
            .Include(v => v.Model)
            .Include(v => v.AuthorizedByUser)
            .Where(v => !v.is_deleted && v.Authority_Status == "Authorized")
            .OrderByDescending(v => v.authorization_date)
            .ToListAsync();
    }

    /// <summary>
    /// Get all rejected vehicles
    /// </summary>
    public async Task<IEnumerable<PreVehicleMaster>> GetRejectedVehiclesAsync()
    {
        return await _context.PreVehicleMasters
            .Include(v => v.Model)
            .Include(v => v.AuthorizedByUser)
            .Where(v => !v.is_deleted && v.Authority_Status == "Rejected")
            .OrderByDescending(v => v.authorization_date)
            .ToListAsync();
    }

    /// <summary>
    /// Get vehicles by authorization status
    /// </summary>
    public async Task<IEnumerable<PreVehicleMaster>> GetByStatusAsync(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return Enumerable.Empty<PreVehicleMaster>();

        return await _context.PreVehicleMasters
            .Include(v => v.Model)
            .Include(v => v.AuthorizedByUser)
            .Where(v => !v.is_deleted && v.Authority_Status == status)
            .OrderByDescending(v => v.date_created)
            .ToListAsync();
    }

    /// <summary>
    /// Get authorization history with optional date range filter
    /// </summary>
    public async Task<IEnumerable<PreVehicleMaster>> GetAuthorizationHistoryAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.PreVehicleMasters
            .Include(v => v.Model)
            .Include(v => v.AuthorizedByUser)
            .Where(v => !v.is_deleted &&
                   (v.Authority_Status == "Authorized" || v.Authority_Status == "Rejected"));

        if (startDate.HasValue)
            query = query.Where(v => v.authorization_date >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(v => v.authorization_date <= endDate.Value);

        return await query
            .OrderByDescending(v => v.authorization_date)
            .ToListAsync();
    }

    /// <summary>
    /// Create new vehicle authorization entry
    /// </summary>
    public async Task<PreVehicleMaster> CreateAsync(PreVehicleMaster vehicleAuth, int currentUserId)
    {
        vehicleAuth.date_created = DateTime.Now;
        vehicleAuth.created_by_user_code = currentUserId;
        vehicleAuth.is_deleted = false;
        vehicleAuth.Authority_Status = "Awaiting Authorization";

        _context.PreVehicleMasters.Add(vehicleAuth);
        await _context.SaveChangesAsync();
        return vehicleAuth;
    }

    /// <summary>
    /// Update vehicle authorization
    /// </summary>
    public async Task UpdateAsync(PreVehicleMaster vehicleAuth, int currentUserId)
    {
        var existing = await _context.PreVehicleMasters
            .FirstOrDefaultAsync(v => v.temp_vmf_code == vehicleAuth.temp_vmf_code);

        if (existing == null)
            throw new KeyNotFoundException($"Vehicle authorization with ID {vehicleAuth.temp_vmf_code} not found");

        existing.date_updated = DateTime.Now;
        existing.modified_by_user_code = currentUserId;

        // Use CurrentValues.SetValues for tracking-safe updates
        _context.Entry(existing).CurrentValues.SetValues(vehicleAuth);
        _context.Entry(existing).Property(x => x.date_created).IsModified = false;
        _context.Entry(existing).Property(x => x.created_by_user_code).IsModified = false;

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Approve vehicle authorization
    /// </summary>
    public async Task ApproveAsync(int preVehicleId, int authorizedByUserId, string? comment = null)
    {
        var vehicleAuth = await _context.PreVehicleMasters
            .FirstOrDefaultAsync(v => v.temp_vmf_code == preVehicleId && !v.is_deleted);

        if (vehicleAuth == null)
            throw new KeyNotFoundException($"Vehicle authorization with ID {preVehicleId} not found");

        if (vehicleAuth.Authority_Status == "Authorized")
            throw new InvalidOperationException($"Vehicle authorization {preVehicleId} is already approved");

        vehicleAuth.Authority_Status = "Authorized";
        vehicleAuth.authorized_by_user_code = authorizedByUserId;
        vehicleAuth.authorization_date = DateTime.Now;
        vehicleAuth.authorization_comment = comment;
        vehicleAuth.rejection_reason = null; // Clear any previous rejection reason
        vehicleAuth.date_updated = DateTime.Now;
        vehicleAuth.modified_by_user_code = authorizedByUserId;

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Reject vehicle authorization
    /// </summary>
    public async Task RejectAsync(int preVehicleId, int rejectedByUserId, string rejectionReason, string? comment = null)
    {
        var vehicleAuth = await _context.PreVehicleMasters
            .FirstOrDefaultAsync(v => v.temp_vmf_code == preVehicleId && !v.is_deleted);

        if (vehicleAuth == null)
            throw new KeyNotFoundException($"Vehicle authorization with ID {preVehicleId} not found");

        if (string.IsNullOrWhiteSpace(rejectionReason))
            throw new ArgumentException("Rejection reason is required", nameof(rejectionReason));

        vehicleAuth.Authority_Status = "Rejected";
        vehicleAuth.authorized_by_user_code = rejectedByUserId;
        vehicleAuth.authorization_date = DateTime.Now;
        vehicleAuth.rejection_reason = rejectionReason;
        vehicleAuth.authorization_comment = comment;
        vehicleAuth.date_updated = DateTime.Now;
        vehicleAuth.modified_by_user_code = rejectedByUserId;

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Add comment to vehicle authorization
    /// </summary>
    public async Task AddCommentAsync(int preVehicleId, string comment, int modifiedByUserId)
    {
        var vehicleAuth = await _context.PreVehicleMasters
            .FirstOrDefaultAsync(v => v.temp_vmf_code == preVehicleId && !v.is_deleted);

        if (vehicleAuth == null)
            throw new KeyNotFoundException($"Vehicle authorization with ID {preVehicleId} not found");

        if (string.IsNullOrWhiteSpace(comment))
            throw new ArgumentException("Comment cannot be empty", nameof(comment));

        // Append comment to existing comments with timestamp
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        var newComment = $"[{timestamp}] {comment}";

        vehicleAuth.authorization_comment = string.IsNullOrWhiteSpace(vehicleAuth.authorization_comment)
            ? newComment
            : $"{vehicleAuth.authorization_comment}\n{newComment}";

        vehicleAuth.date_updated = DateTime.Now;
        vehicleAuth.modified_by_user_code = modifiedByUserId;

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Soft delete vehicle authorization
    /// </summary>
    public async Task DeleteAsync(int preVehicleId, int currentUserId)
    {
        var vehicleAuth = await _context.PreVehicleMasters
            .FirstOrDefaultAsync(v => v.temp_vmf_code == preVehicleId);

        if (vehicleAuth == null)
            throw new KeyNotFoundException($"Vehicle authorization with ID {preVehicleId} not found");

        vehicleAuth.is_deleted = true;
        vehicleAuth.date_updated = DateTime.Now;
        vehicleAuth.modified_by_user_code = currentUserId;

        await _context.SaveChangesAsync();
    }
}
