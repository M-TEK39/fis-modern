using System.Data;
using System.Data.Common;
using System.Security.Claims;
using System.Text.Json;
using FIS.Core.Domain.Entities.Auth;
using FIS.Core.Domain.Entities.System;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace FIS.Data.SqlServer.Interceptors;

/// <summary>
/// EF Core interceptor that automatically writes a row to the available audit table for every
/// INSERT / UPDATE / DELETE across all entities (except Audit and UserStatusHistory themselves).
///
/// Sensitive fields (password, hash, salt, token) are redacted from snapshots.
/// The current user is resolved from the HTTP context JWT claims.
/// </summary>
public class AuditInterceptor : SaveChangesInterceptor
{
    private static readonly string[] RequiredAuditColumns =
    [
        "AuditID",
        "Action",
        "TableName",
        "PrimaryKey",
        "Changes",
        "ActionedBy",
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditInterceptor> _logger;

    // These entity CLR types are never audited – they ARE the audit infrastructure.
    private static readonly HashSet<Type> _excludedTypes = new()
    {
        typeof(Audit),
        typeof(LegacyAudit),
        typeof(UserStatusHistory),
    };

    private static readonly HashSet<string> _sensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "password_hash",
        "password_salt",
        "password_reset_token",
        "hash",
        "salt",
        "token",
        "secret",
    };

    public AuditInterceptor(
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditInterceptor> logger
    )
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    // ── Async path ────────────────────────────────────────────────────────────

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        if (eventData.Context is FisDbContext ctx)
        {
            var entries = BuildAuditEntries(ctx);
            if (entries.Count > 0)
            {
                var auditStore = await ResolveAuditStoreAsync(ctx, cancellationToken);
                AddAuditEntries(ctx, entries, auditStore);
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    // ── Sync path (kept for completeness) ─────────────────────────────────────

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result
    )
    {
        if (eventData.Context is FisDbContext ctx)
        {
            var entries = BuildAuditEntries(ctx);
            if (entries.Count > 0)
            {
                var auditStore = ResolveAuditStore(ctx);
                AddAuditEntries(ctx, entries, auditStore);
            }
        }

        return base.SavingChanges(eventData, result);
    }

    // ── Core logic ────────────────────────────────────────────────────────────

    private void AddAuditEntries(
        FisDbContext context,
        IReadOnlyCollection<Audit> entries,
        AuditStore auditStore
    )
    {
        switch (auditStore)
        {
            case AuditStore.Workflow:
                context.Audits.AddRange(entries);
                break;
            case AuditStore.Legacy:
                context.LegacyAudits.AddRange(entries.Select(MapLegacyAudit));
                break;
            case AuditStore.Unavailable:
                _logger.LogWarning(
                    "No compatible audit table was found; continuing without persisting {AuditCount} audit entries",
                    entries.Count
                );
                break;
        }
    }

    private async Task<AuditStore> ResolveAuditStoreAsync(
        FisDbContext context,
        CancellationToken cancellationToken
    )
    {
        if (
            await TableHasRequiredColumnsAsync(
                context,
                "Workflow",
                "Audit",
                cancellationToken
            )
        )
        {
            return AuditStore.Workflow;
        }

        if (
            await TableHasRequiredColumnsAsync(context, "dbo", "Audit", cancellationToken)
        )
        {
            return AuditStore.Legacy;
        }

        return AuditStore.Unavailable;
    }

    private AuditStore ResolveAuditStore(FisDbContext context)
    {
        if (TableHasRequiredColumns(context, "Workflow", "Audit"))
        {
            return AuditStore.Workflow;
        }

        if (TableHasRequiredColumns(context, "dbo", "Audit"))
        {
            return AuditStore.Legacy;
        }

        return AuditStore.Unavailable;
    }

    private static LegacyAudit MapLegacyAudit(Audit entry) =>
        new()
        {
            Action = entry.Action,
            TableName = entry.TableName,
            PrimaryKey = entry.PrimaryKey,
            Changes = entry.Changes,
            ActionedBy = entry.ActionedBy,
            date_created = entry.date_created,
            date_updated = entry.date_updated,
            created_by_user_code = entry.created_by_user_code,
            modified_by_user_code = entry.modified_by_user_code,
            is_deleted = entry.is_deleted,
        };

    private static async Task<bool> TableExistsAsync(
        FisDbContext context,
        string schema,
        string table,
        CancellationToken cancellationToken
    )
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM [INFORMATION_SCHEMA].[TABLES]
                    WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table
                ) THEN 1 ELSE 0 END
                """;
            AddParameter(command, "@schema", DbType.String, schema);
            AddParameter(command, "@table", DbType.String, table);
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
        }
        catch (DbException)
        {
            return false;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static bool TableExists(FisDbContext context, string schema, string table)
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM [INFORMATION_SCHEMA].[TABLES]
                    WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table
                ) THEN 1 ELSE 0 END
                """;
            AddParameter(command, "@schema", DbType.String, schema);
            AddParameter(command, "@table", DbType.String, table);
            return Convert.ToInt32(command.ExecuteScalar()) == 1;
        }
        catch (DbException)
        {
            return false;
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }
    }

    private static async Task<bool> TableHasRequiredColumnsAsync(
        FisDbContext context,
        string schema,
        string table,
        CancellationToken cancellationToken
    )
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT COUNT(1)
                FROM [INFORMATION_SCHEMA].[COLUMNS]
                WHERE [TABLE_SCHEMA] = @schema
                  AND [TABLE_NAME] = @table
                  AND [COLUMN_NAME] IN
                  (
                      N'AuditID', N'Action', N'TableName', N'PrimaryKey', N'Changes',
                      N'ActionedBy', N'date_created', N'date_updated',
                      N'created_by_user_code', N'modified_by_user_code', N'is_deleted'
                  )
                """;
            AddParameter(command, "@schema", DbType.String, schema);
            AddParameter(command, "@table", DbType.String, table);
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken))
                == RequiredAuditColumns.Length;
        }
        catch (DbException)
        {
            return false;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static bool TableHasRequiredColumns(FisDbContext context, string schema, string table)
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT COUNT(1)
                FROM [INFORMATION_SCHEMA].[COLUMNS]
                WHERE [TABLE_SCHEMA] = @schema
                  AND [TABLE_NAME] = @table
                  AND [COLUMN_NAME] IN
                  (
                      N'AuditID', N'Action', N'TableName', N'PrimaryKey', N'Changes',
                      N'ActionedBy', N'date_created', N'date_updated',
                      N'created_by_user_code', N'modified_by_user_code', N'is_deleted'
                  )
                """;
            AddParameter(command, "@schema", DbType.String, schema);
            AddParameter(command, "@table", DbType.String, table);
            return Convert.ToInt32(command.ExecuteScalar()) == RequiredAuditColumns.Length;
        }
        catch (DbException)
        {
            return false;
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private enum AuditStore
    {
        Unavailable,
        Workflow,
        Legacy,
    }

    private List<Audit> BuildAuditEntries(FisDbContext context)
    {
        var result = new List<Audit>();
        var now = DateTime.UtcNow;
        var (userCode, userEmail) = ResolveCurrentUser();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (_excludedTypes.Contains(entry.Entity.GetType()))
                continue;

            if (
                entry.State
                is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)
            )
                continue;

            var tableName = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name;
            var primaryKey = ResolveKey(entry);

            var action = entry.State switch
            {
                EntityState.Added => "INSERT",
                EntityState.Modified => "UPDATE",
                EntityState.Deleted => "DELETE",
                _ => entry.State.ToString(),
            };

            string changesJson;
            try
            {
                changesJson = entry.State switch
                {
                    EntityState.Modified => BuildUpdateDiff(entry),
                    EntityState.Added => BuildSnapshot(entry, isDeleted: false),
                    EntityState.Deleted => BuildSnapshot(entry, isDeleted: true),
                    _ => "{}",
                };
            }
            catch
            {
                changesJson = "{}";
            }

            // Don't log a no-op UPDATE (all values unchanged)
            if (action == "UPDATE" && changesJson == "{}")
                continue;

            result.Add(
                new Audit
                {
                    Action = action,
                    TableName = tableName,
                    PrimaryKey = primaryKey,
                    Changes = changesJson,
                    ActionedBy = userEmail,
                    date_created = now,
                    created_by_user_code = userCode > 0 ? userCode : null,
                }
            );
        }

        return result;
    }

    // ── Diff builders ─────────────────────────────────────────────────────────

    private static string BuildUpdateDiff(EntityEntry entry)
    {
        var changes = new Dictionary<string, object?>();

        foreach (var prop in entry.Properties)
        {
            if (!prop.IsModified)
                continue;
            if (IsSensitive(prop.Metadata.Name))
                continue;
            if (Equals(prop.OriginalValue, prop.CurrentValue))
                continue;

            changes[prop.Metadata.Name] = new
            {
                old = FormatValue(prop.OriginalValue),
                @new = FormatValue(prop.CurrentValue),
            };
        }

        return JsonSerializer.Serialize(changes);
    }

    private static string BuildSnapshot(EntityEntry entry, bool isDeleted)
    {
        var values = new Dictionary<string, object?>();

        foreach (var prop in entry.Properties)
        {
            if (IsSensitive(prop.Metadata.Name))
                continue;
            values[prop.Metadata.Name] = FormatValue(
                isDeleted ? (prop.OriginalValue ?? prop.CurrentValue) : prop.CurrentValue
            );
        }

        var key = isDeleted ? "deleted" : "inserted";
        return JsonSerializer.Serialize(new Dictionary<string, object> { [key] = values });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsSensitive(string name) =>
        _sensitiveFields.Any(s => name.Contains(s, StringComparison.OrdinalIgnoreCase));

    private static string? FormatValue(object? val) =>
        val switch
        {
            null => null,
            DateTime dt => dt.ToString("o"),
            _ => val.ToString(),
        };

    private static string ResolveKey(EntityEntry entry)
    {
        var keys = entry
            .Properties.Where(p => p.Metadata.IsPrimaryKey())
            .Select(p => p.CurrentValue?.ToString() ?? "?")
            .ToList();

        if (!keys.Any())
            return "unknown";

        // For identity PKs on INSERT the value is 0/temp; mark clearly.
        var raw = string.Join(",", keys);
        return (entry.State == EntityState.Added && raw is "0" or "") ? "[new]" : raw;
    }

    private (int userCode, string email) ResolveCurrentUser()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null)
            return (0, "system");

        var codeClaim = user.FindFirst("user_access_code")?.Value;
        var email =
            user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst(ClaimTypes.Name)?.Value
            ?? "unknown";

        int.TryParse(codeClaim, out int code);
        return (code, email);
    }
}
