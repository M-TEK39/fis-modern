using System.Security.Claims;
using System.Text.Json;
using FIS.Core.Domain.Entities.Auth;
using FIS.Core.Domain.Entities.System;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FIS.Data.SqlServer.Interceptors;

/// <summary>
/// EF Core interceptor that automatically writes a row to Workflow.Audit for every
/// INSERT / UPDATE / DELETE across all entities (except Audit and UserStatusHistory themselves).
///
/// Sensitive fields (password, hash, salt, token) are redacted from snapshots.
/// The current user is resolved from the HTTP context JWT claims.
/// </summary>
public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    // These entity CLR types are never audited – they ARE the audit infrastructure.
    private static readonly HashSet<Type> _excludedTypes = new()
    {
        typeof(Audit),
        typeof(UserStatusHistory)
    };

    private static readonly HashSet<string> _sensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "password_hash", "password_salt", "password_reset_token",
        "hash", "salt", "token", "secret"
    };

    public AuditInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // ── Async path ────────────────────────────────────────────────────────────

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is FisDbContext ctx)
        {
            var entries = BuildAuditEntries(ctx);
            if (entries.Count > 0)
                await ctx.Audits.AddRangeAsync(entries, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    // ── Sync path (kept for completeness) ─────────────────────────────────────

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is FisDbContext ctx)
        {
            var entries = BuildAuditEntries(ctx);
            if (entries.Count > 0)
                ctx.Audits.AddRange(entries);
        }

        return base.SavingChanges(eventData, result);
    }

    // ── Core logic ────────────────────────────────────────────────────────────

    private List<Audit> BuildAuditEntries(FisDbContext context)
    {
        var result = new List<Audit>();
        var now = DateTime.UtcNow;
        var (userCode, userEmail) = ResolveCurrentUser();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (_excludedTypes.Contains(entry.Entity.GetType()))
                continue;

            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var tableName = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name;
            var primaryKey = ResolveKey(entry);

            var action = entry.State switch
            {
                EntityState.Added    => "INSERT",
                EntityState.Modified => "UPDATE",
                EntityState.Deleted  => "DELETE",
                _                    => entry.State.ToString()
            };

            string changesJson;
            try
            {
                changesJson = entry.State switch
                {
                    EntityState.Modified => BuildUpdateDiff(entry),
                    EntityState.Added    => BuildSnapshot(entry, isDeleted: false),
                    EntityState.Deleted  => BuildSnapshot(entry, isDeleted: true),
                    _                    => "{}"
                };
            }
            catch
            {
                changesJson = "{}";
            }

            // Don't log a no-op UPDATE (all values unchanged)
            if (action == "UPDATE" && changesJson == "{}")
                continue;

            result.Add(new Audit
            {
                Action              = action,
                TableName           = tableName,
                PrimaryKey          = primaryKey,
                Changes             = changesJson,
                ActionedBy          = userEmail,
                date_created        = now,
                created_by_user_code = userCode > 0 ? userCode : null
            });
        }

        return result;
    }

    // ── Diff builders ─────────────────────────────────────────────────────────

    private static string BuildUpdateDiff(EntityEntry entry)
    {
        var changes = new Dictionary<string, object?>();

        foreach (var prop in entry.Properties)
        {
            if (!prop.IsModified) continue;
            if (IsSensitive(prop.Metadata.Name)) continue;
            if (Equals(prop.OriginalValue, prop.CurrentValue)) continue;

            changes[prop.Metadata.Name] = new
            {
                old = FormatValue(prop.OriginalValue),
                @new = FormatValue(prop.CurrentValue)
            };
        }

        return JsonSerializer.Serialize(changes);
    }

    private static string BuildSnapshot(EntityEntry entry, bool isDeleted)
    {
        var values = new Dictionary<string, object?>();

        foreach (var prop in entry.Properties)
        {
            if (IsSensitive(prop.Metadata.Name)) continue;
            values[prop.Metadata.Name] = FormatValue(
                isDeleted ? (prop.OriginalValue ?? prop.CurrentValue) : prop.CurrentValue);
        }

        var key = isDeleted ? "deleted" : "inserted";
        return JsonSerializer.Serialize(new Dictionary<string, object> { [key] = values });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsSensitive(string name)
        => _sensitiveFields.Any(s => name.Contains(s, StringComparison.OrdinalIgnoreCase));

    private static string? FormatValue(object? val) => val switch
    {
        null          => null,
        DateTime dt   => dt.ToString("o"),
        _             => val.ToString()
    };

    private static string ResolveKey(EntityEntry entry)
    {
        var keys = entry.Properties
            .Where(p => p.Metadata.IsPrimaryKey())
            .Select(p => p.CurrentValue?.ToString() ?? "?")
            .ToList();

        if (!keys.Any()) return "unknown";

        // For identity PKs on INSERT the value is 0/temp; mark clearly.
        var raw = string.Join(",", keys);
        return (entry.State == EntityState.Added && raw is "0" or "")
            ? "[new]"
            : raw;
    }

    private (int userCode, string email) ResolveCurrentUser()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null) return (0, "system");

        var codeClaim = user.FindFirst("user_access_code")?.Value;
        var email = user.FindFirst(ClaimTypes.Email)?.Value
                 ?? user.FindFirst(ClaimTypes.Name)?.Value
                 ?? "unknown";

        int.TryParse(codeClaim, out int code);
        return (code, email);
    }
}
