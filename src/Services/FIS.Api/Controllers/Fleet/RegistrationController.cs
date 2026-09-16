using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Api.Controllers;

/// <summary>
/// Vehicle registration (GP number) history.
/// Solves the problem where traffic tickets carry old GP numbers that no longer
/// match the vehicle's current registration_number — users can search by any
/// historical registration to find the correct vehicle.
/// </summary>
[ApiController]
// Historical GP registrations are consumed by Fines searches and Vehicle
// Master maintenance. Do not expose fleet registration history to every
// authenticated account.
[Authorize(Roles = "Vehicle Master,Fines,Reports")]
[Route("api/[controller]")]
[Produces("application/json")]
public class RegistrationController : BaseApiController
{
    private readonly FisDbContext _context;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ILogger<RegistrationController> _logger;

    public RegistrationController(
        FisDbContext context,
        IVehicleRepository vehicleRepository,
        ILogger<RegistrationController> logger
    )
    {
        _context = context;
        _vehicleRepository = vehicleRepository;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult GetRoot()
    {
        return Ok(
            new
            {
                module = "Registration History",
                endpoints = new[]
                {
                    "search?q={value}",
                    "vehicle/{vmfCode}",
                    "vehicle/{vmfCode} [POST]",
                },
            }
        );
    }

    /// <summary>
    /// Get all historical registration numbers for a vehicle.
    /// Returns both the current registration and all previous ones with timestamps.
    /// </summary>
    [HttpGet("vehicle/{vmfCode}")]
    public async Task<ActionResult> GetByVehicle(int vmfCode)
    {
        try
        {
            var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode);

            if (vehicle == null)
                return NotFound(new { message = $"Vehicle {vmfCode} not found" });

            var history = await ReadHistoryAsync(vmfCode, HttpContext.RequestAborted);

            return Ok(
                new
                {
                    vmf_code = vehicle.vmf_code,
                    fleet_number = vehicle.fleet_number,
                    current_registration = vehicle.registration_number,
                    history,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving registration history for vehicle {VmfCode}",
                vmfCode
            );
            return StatusCode(500, new { error = "Failed to retrieve registration history" });
        }
    }

    /// <summary>
    /// Search for a vehicle by registration number — checks both the current
    /// registration_number on vehicle_master AND all historical entries in Registrations.
    /// This is the key fix for the traffic ticket problem: a ticket with an old GP
    /// number will still resolve to the correct vehicle.
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Search term is required" });

        try
        {
            var term = q.Trim().ToUpperInvariant();

            // 1. Vehicles whose CURRENT registration matches
            var currentMatches = (await _vehicleRepository.SearchVehiclesAsync(term))
                .Where(vehicle =>
                    !string.IsNullOrWhiteSpace(vehicle.registration_number)
                    && vehicle.registration_number.Contains(
                        term,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                .Select(vehicle => new RegistrationSearchResult(
                    vehicle.vmf_code,
                    vehicle.fleet_number,
                    vehicle.registration_number,
                    vehicle.registration_number,
                    false,
                    null
                ))
                .ToList();

            // 2. Vehicles found via HISTORICAL registrations that aren't already in current matches
            var currentVmfCodes = currentMatches.Select(m => m.vmf_code).ToHashSet();

            var historicalMatches = await ReadHistoricalMatchesAsync(
                term,
                currentVmfCodes,
                HttpContext.RequestAborted
            );

            var results = currentMatches
                .Concat(historicalMatches)
                .OrderBy(m => m.is_historical_match)
                .ThenBy(m => m.fleet_number)
                .ToList();

            _logger.LogInformation(
                "Registration search for '{Term}': {Current} current, {Historical} historical matches",
                q,
                currentMatches.Count,
                historicalMatches.Count
            );

            return Ok(
                new
                {
                    search_term = q,
                    total = results.Count,
                    results,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching registrations for '{Term}'", q);
            return StatusCode(500, new { error = "Failed to search registrations" });
        }
    }

    /// <summary>
    /// Manually add a historical registration record for a vehicle.
    /// Use this to backfill historical GP numbers that were lost in the old system.
    /// </summary>
    [HttpPost("vehicle/{vmfCode}")]
    public async Task<ActionResult> AddHistorical(
        int vmfCode,
        [FromBody] AddRegistrationDto request
    )
    {
        if (!HasRole("Vehicle Master"))
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.registration_number))
            return BadRequest(new { error = "registration_number is required" });

        try
        {
            var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode);

            if (vehicle == null)
                return NotFound(new { message = $"Vehicle {vmfCode} not found" });

            var registrationNumber = request.registration_number.Trim().ToUpperInvariant();
            if (registrationNumber.Length > 8)
                return BadRequest(new { error = "registration_number cannot exceed 8 characters" });

            var recordedDate = request.effective_date ?? DateTime.UtcNow;
            var registrationId = await InsertHistoricalRegistrationAsync(
                vmfCode,
                registrationNumber,
                recordedDate,
                GetCurrentUserId(),
                HttpContext.RequestAborted
            );

            _logger.LogInformation(
                "Manually added historical registration '{Reg}' for vehicle {VmfCode}",
                request.registration_number,
                vmfCode
            );

            return Ok(
                new
                {
                    message = "Historical registration recorded",
                    registration_id = registrationId,
                    vmf_code = vmfCode,
                    registration_number = registrationNumber,
                    recorded_date = recordedDate,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error adding historical registration for vehicle {VmfCode}",
                vmfCode
            );
            return StatusCode(500, new { error = "Failed to add historical registration" });
        }
    }

    private bool HasRole(string expectedRole) =>
        User.Claims.Any(claim =>
            (claim.Type == System.Security.Claims.ClaimTypes.Role
                || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
                || claim.Type.Equals("roles", StringComparison.OrdinalIgnoreCase))
            && claim.Value.Split(
                ',',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
            ).Any(role => string.Equals(role, expectedRole, StringComparison.OrdinalIgnoreCase))
        );

    private async Task<HashSet<string>> GetRegistrationColumnsAsync(
        DbConnection connection,
        DbTransaction? transaction = null
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction ?? _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            SELECT [COLUMN_NAME]
            FROM [INFORMATION_SCHEMA].[COLUMNS]
            WHERE [TABLE_SCHEMA] = N'dbo' AND [TABLE_NAME] = N'Registrations'
            """;

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(0));
        return columns;
    }

    private async Task<List<object>> ReadHistoryAsync(int vmfCode, CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync(cancellationToken);

        try
        {
            var columns = await GetRegistrationColumnsAsync(connection);
            if (!RequiredRegistrationColumns.All(column => columns.Contains(column)))
                return [];

            var deleted = columns.Contains("is_deleted")
                ? " AND ([r].[is_deleted] = 0 OR [r].[is_deleted] IS NULL)"
                : string.Empty;
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT [r].[RegistrationID], [r].[RegistrationNumber], [r].[RegistrationDate]
                FROM [dbo].[Registrations] AS [r]
                WHERE [r].[vmf_code] = @vmfCode{deleted}
                ORDER BY [r].[RegistrationDate] DESC, [r].[RegistrationID] DESC
                """;
            AddParameter(command, "@vmfCode", DbType.Int32, vmfCode);
            var results = new List<object>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(
                    new
                    {
                        registration_id = Convert.ToInt32(reader.GetValue(0)),
                        registration_number = reader.IsDBNull(1) ? null : reader.GetValue(1).ToString()?.Trim(),
                        recorded_date = reader.IsDBNull(2) ? (DateTime?)null : Convert.ToDateTime(reader.GetValue(2)),
                        is_current = false,
                    }
                );
            }
            return results;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<List<RegistrationSearchResult>> ReadHistoricalMatchesAsync(
        string term,
        IReadOnlySet<int> excludedVmfCodes,
        CancellationToken cancellationToken
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync(cancellationToken);

        try
        {
            var columns = await GetRegistrationColumnsAsync(connection);
            if (!RequiredRegistrationColumns.All(column => columns.Contains(column)))
                return [];

            var deleted = columns.Contains("is_deleted")
                ? " AND ([r].[is_deleted] = 0 OR [r].[is_deleted] IS NULL)"
                : string.Empty;
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT [r].[vmf_code], [r].[RegistrationNumber], [r].[RegistrationDate]
                FROM [dbo].[Registrations] AS [r]
                WHERE LOWER(LTRIM(RTRIM([r].[RegistrationNumber]))) = @registrationNumber{deleted}
                ORDER BY [r].[RegistrationDate] DESC, [r].[RegistrationID] DESC
                """;
            AddParameter(command, "@registrationNumber", DbType.String, term.ToLowerInvariant());
            var matches = new List<(int VmfCode, string? Registration, DateTime? RecordedDate)>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var vmfCode = Convert.ToInt32(reader.GetValue(0));
                if (excludedVmfCodes.Contains(vmfCode))
                    continue;
                matches.Add(
                    (
                        vmfCode,
                        reader.IsDBNull(1) ? null : reader.GetValue(1).ToString()?.Trim(),
                        reader.IsDBNull(2) ? null : Convert.ToDateTime(reader.GetValue(2))
                    )
                );
            }

            var results = new List<RegistrationSearchResult>();
            foreach (var match in matches)
            {
                var vehicle = await _vehicleRepository.GetByIdAsync(match.VmfCode);
                if (vehicle is null)
                    continue;

                results.Add(
                    new RegistrationSearchResult(
                        match.VmfCode,
                        vehicle.fleet_number,
                        vehicle.registration_number,
                        match.Registration,
                        true,
                        match.RecordedDate
                    )
                );
            }
            return results;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Insert identifiers are selected only from fixed legacy/optional column allowlists; all submitted values are parameters."
    )]
    private async Task<int> InsertHistoricalRegistrationAsync(
        int vmfCode,
        string registrationNumber,
        DateTime recordedDate,
        int currentUserId,
        CancellationToken cancellationToken
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );
        try
        {
            var columns = await GetRegistrationColumnsAsync(connection, transaction);
            if (!RequiredRegistrationColumns.All(column => columns.Contains(column)))
                throw new NotSupportedException("The legacy Registrations table is unavailable; the historical registration was not saved.");

            var insertColumns = new List<string>(InsertRegistrationColumns);
            var insertValues = new List<string> { "@registrationId", "@registrationNumber", "@recordedDate", "@vmfCode" };
            await using var identityCommand = connection.CreateCommand();
            identityCommand.Transaction = transaction;
            identityCommand.CommandText = "SELECT COLUMNPROPERTY(OBJECT_ID(N'[dbo].[Registrations]'), N'RegistrationID', 'IsIdentity')";
            var registrationIdIsIdentity = Convert.ToInt32(
                await identityCommand.ExecuteScalarAsync(cancellationToken)
            ) == 1;
            int? nextId = null;
            if (!registrationIdIsIdentity)
            {
                await using var nextCommand = connection.CreateCommand();
                nextCommand.Transaction = transaction;
                nextCommand.CommandText = "SELECT COALESCE(MAX([RegistrationID]), 0) + 1 FROM [dbo].[Registrations] WITH (UPDLOCK, HOLDLOCK)";
                nextId = Convert.ToInt32(await nextCommand.ExecuteScalarAsync(cancellationToken));
                if (nextId > short.MaxValue)
                    throw new InvalidOperationException("The legacy registration identifier range is exhausted.");
                insertColumns.Insert(0, "RegistrationID");
            }
            else
            {
                insertValues.RemoveAt(0);
            }
            if (columns.Contains("date_created"))
            {
                insertColumns.Add("date_created");
                insertValues.Add("@dateCreated");
            }
            if (columns.Contains("created_by_user_code"))
            {
                insertColumns.Add("created_by_user_code");
                insertValues.Add("@createdBy");
            }
            if (columns.Contains("is_deleted"))
            {
                insertColumns.Add("is_deleted");
                insertValues.Add("@isDeleted");
            }

            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = $"INSERT INTO [dbo].[Registrations] ({string.Join(", ", insertColumns.Select(column => $"[{column}]"))}) VALUES ({string.Join(", ", insertValues)}); SELECT CAST(SCOPE_IDENTITY() AS int);";
            if (!registrationIdIsIdentity)
                AddParameter(insertCommand, "@registrationId", DbType.Int16, (short)nextId!.Value);
            AddParameter(insertCommand, "@registrationNumber", DbType.String, registrationNumber);
            AddParameter(insertCommand, "@recordedDate", DbType.DateTime2, recordedDate);
            AddParameter(insertCommand, "@vmfCode", DbType.Int32, vmfCode);
            if (columns.Contains("date_created"))
                AddParameter(insertCommand, "@dateCreated", DbType.DateTime2, DateTime.UtcNow);
            if (columns.Contains("created_by_user_code"))
                AddParameter(insertCommand, "@createdBy", DbType.Int32, currentUserId > 0 ? currentUserId : null);
            if (columns.Contains("is_deleted"))
                AddParameter(insertCommand, "@isDeleted", DbType.Boolean, false);
            var insertedId = registrationIdIsIdentity
                ? Convert.ToInt32(await insertCommand.ExecuteScalarAsync(cancellationToken))
                : (int)nextId!.Value;
            await transaction.CommitAsync(cancellationToken);
            return insertedId;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static readonly string[] RequiredRegistrationColumns =
    [
        "RegistrationID",
        "RegistrationNumber",
        "RegistrationDate",
        "vmf_code",
    ];

    private static readonly string[] InsertRegistrationColumns =
    [
        "RegistrationNumber",
        "RegistrationDate",
        "vmf_code",
    ];

    private sealed record RegistrationSearchResult(
        int vmf_code,
        string? fleet_number,
        string? current_registration,
        string? matched_registration,
        bool is_historical_match,
        DateTime? recorded_date
    );

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}

public class AddRegistrationDto
{
    public string registration_number { get; set; } = string.Empty;
    public DateTime? effective_date { get; set; }
}
