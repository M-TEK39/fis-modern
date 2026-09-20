using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Drivers;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Reads and writes trip-driver assignments against the archived
/// <c>trip_drivers</c> contract. Procedure-first execution uses
/// <c>DEV_SEL_TripDrivers</c>, <c>DEV_SEL_TripDrivers_PerTripID</c>,
/// <c>DEV_INS_TripDrivers</c>, and <c>DEV_UPD_TripDrivers</c> when those
/// objects are present. Direct DML is a labelled fallback only when the
/// relevant procedure is genuinely absent, and it never projects the optional
/// modern audit columns unless the live table contains them.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers come only from fixed compatibility allowlists; submitted values are parameters."
)]
public sealed class TripDriverRepository : ITripDriverRepository
{
    private const string InsertProcedure = "DEV_INS_TripDrivers";
    private const string UpdateProcedure = "DEV_UPD_TripDrivers";
    private const string SelectByTripProcedure = "DEV_SEL_TripDrivers";
    private const string SelectByTripDisplayProcedure = "DEV_SEL_TripDrivers_PerTripID";

    private static readonly string[] TableNames = ["trip_drivers", "trip_driver"];

    private static readonly string[] RequiredColumns =
    [
        "trip_driver_code",
        "trip_driver_name",
        "trip_authority_code",
        "trip_driver_primary",
    ];

    private static readonly string[] BusinessColumns =
    [
        "trip_driver_code",
        "trip_driver_name",
        "trip_driver_id",
        "trip_authority_code",
        "trip_driver_primary",
        "site_code",
        "driver_licence_type_id",
        "driver_passportnumber",
        "driver_persalnumber",
        "driver_contractnumber",
        "driver_licence_number",
        "driver_licence_issuedate",
        "driver_licence_lastVerifiedDate",
        "driver_hasPDP",
        "driver_PDP_ExpiryDate",
        "driver_licence_ExpiryDate",
        "driver_active",
    ];

    private static readonly string[] OptionalAuditColumns =
    [
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private static readonly string[] InsertProcedureParameters =
    [
        "@NewTripDriverID",
        "@TripID",
        "@DriverName",
        "@DriverSAID",
        "@DriverIsPrimary",
        "@SiteDriverID",
    ];

    // Archived dbo.DEV_UPD_TripDrivers names the key parameter
    // @NewTripTriverID. Match that contract exactly; do not silently accept a
    // renamed parameter or fall back to direct DML.
    private static readonly string[] UpdateProcedureParameters =
    [
        "@NewTripTriverID",
        "@TripID",
        "@DriverName",
        "@DriverSAID",
        "@DriverIsPrimary",
        "@SiteDriverID",
    ];

    private static readonly string[] SelectByTripParameters = ["@TripID"];

    private readonly FisDbContext _context;

    public TripDriverRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<TripDriver?> GetByIdAsync(int tripDriverCode) =>
        (
            await QueryAsync(
                "[trip_driver_code] = @tripDriverCode",
                command => AddParameter(command, "@tripDriverCode", DbType.Int32, tripDriverCode)
            )
        ).SingleOrDefault();

    public async Task<TripDriver?> GetByNameAsync(string tripDriverName)
    {
        if (string.IsNullOrWhiteSpace(tripDriverName))
            return null;

        return (
            await QueryAsync(
                "LOWER(LTRIM(RTRIM(COALESCE([trip_driver_name], '')))) = @tripDriverName",
                command =>
                    AddParameter(
                        command,
                        "@tripDriverName",
                        DbType.String,
                        tripDriverName.Trim().ToLowerInvariant()
                    )
            )
        ).FirstOrDefault();
    }

    public async Task<IEnumerable<TripDriver>> GetByTripAuthorityAsync(int tripAuthorityCode)
    {
        if (await IsLegacyProcedureAvailableAsync(SelectByTripProcedure, SelectByTripParameters))
        {
            return await ExecuteSelectProcedureAsync(
                SelectByTripProcedure,
                command => AddParameter(command, "@TripID", DbType.Int32, tripAuthorityCode),
                tripAuthorityCode
            );
        }

        if (
            await IsLegacyProcedureAvailableAsync(
                SelectByTripDisplayProcedure,
                SelectByTripParameters
            )
        )
        {
            return await ExecuteSelectProcedureAsync(
                SelectByTripDisplayProcedure,
                command => AddParameter(command, "@TripID", DbType.Int32, tripAuthorityCode),
                tripAuthorityCode,
                mapDisplayAliases: true
            );
        }

        // Compatibility fallback: DEV_SEL_TripDrivers and
        // DEV_SEL_TripDrivers_PerTripID are genuinely absent.
        return await QueryAsync(
            "[trip_authority_code] = @tripAuthorityCode",
            command => AddParameter(command, "@tripAuthorityCode", DbType.Int32, tripAuthorityCode)
        );
    }

    public async Task<IEnumerable<TripDriver>> GetBySiteAsync(int siteCode) =>
        await QueryAsync(
            "[site_code] = @siteCode",
            command => AddParameter(command, "@siteCode", DbType.Int32, siteCode),
            extraRequiredColumns: ["site_code"]
        );

    public async Task<IEnumerable<TripDriver>> GetPrimaryDriversAsync() =>
        await QueryAsync(
            "[trip_driver_primary] = 1",
            orderBy: "[trip_driver_code]"
        );

    public async Task<IEnumerable<TripDriver>> GetActiveDriversAsync() =>
        await QueryAsync(
            "[driver_active] = 1",
            orderBy: "[trip_driver_name], [trip_driver_code]",
            extraRequiredColumns: ["driver_active"]
        );

    public async Task<IEnumerable<TripDriver>> GetByLicenseTypeAsync(int licenseTypeId) =>
        await QueryAsync(
            "[driver_licence_type_id] = @licenseTypeId",
            command => AddParameter(command, "@licenseTypeId", DbType.Int32, licenseTypeId),
            extraRequiredColumns: ["driver_licence_type_id"]
        );

    public async Task<TripDriver> CreateAsync(TripDriver tripDriver, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(tripDriver);
        if (tripDriver.trip_authority_code <= 0)
        {
            throw new InvalidOperationException("A valid trip authority is required.");
        }

        var siteDriverId = await ResolveSiteDriverIdAsync(tripDriver);
        if (
            await IsLegacyProcedureAvailableAsync(InsertProcedure, InsertProcedureParameters)
        )
        {
            var createdCode = await ExecuteInsertProcedureAsync(tripDriver, siteDriverId);
            return await GetByIdAsync(createdCode)
                ?? throw new InvalidOperationException(
                    "The legacy DEV_INS_TripDrivers procedure completed without returning a readable trip-driver row."
                );
        }

        // Compatibility fallback: DEV_INS_TripDrivers is genuinely absent.
        // Copy the site-driver identity/licence fields the archived procedure
        // would have copied; do not persist browser-supplied licence values.
        var tableName =
            await ResolveTripDriverTableAsync()
            ?? throw new InvalidOperationException(
                "Neither trip_drivers nor trip_driver contains the required legacy trip-driver columns."
            );
        var columns = await GetTableColumnsAsync(tableName);
        var copied = await LoadSiteDriverCopyAsync(siteDriverId);
        var values = new List<WriteValue>();
        AddValue(
            values,
            columns,
            "trip_driver_name",
            "@tripDriverName",
            DbType.String,
            NullIfWhiteSpace(tripDriver.trip_driver_name)
        );
        AddValue(
            values,
            columns,
            "trip_driver_id",
            "@tripDriverId",
            DbType.String,
            NullIfWhiteSpace(tripDriver.trip_driver_id)
        );
        AddValue(
            values,
            columns,
            "trip_authority_code",
            "@tripAuthorityCode",
            DbType.Int32,
            tripDriver.trip_authority_code
        );
        AddValue(
            values,
            columns,
            "trip_driver_primary",
            "@tripDriverPrimary",
            DbType.Boolean,
            tripDriver.trip_driver_primary
        );
        AddValue(values, columns, "site_code", "@siteCode", DbType.Int32, copied.SiteCode);
        AddValue(
            values,
            columns,
            "driver_licence_type_id",
            "@licenceTypeId",
            DbType.Int32,
            copied.LicenceTypeId
        );
        AddValue(
            values,
            columns,
            "driver_passportnumber",
            "@passportNumber",
            DbType.String,
            copied.PassportNumber
        );
        AddValue(
            values,
            columns,
            "driver_persalnumber",
            "@persalNumber",
            DbType.String,
            copied.PersalNumber
        );
        AddValue(
            values,
            columns,
            "driver_contractnumber",
            "@contractNumber",
            DbType.String,
            copied.ContractNumber
        );
        AddValue(
            values,
            columns,
            "driver_licence_number",
            "@licenceNumber",
            DbType.String,
            copied.LicenceNumber
        );
        AddValue(
            values,
            columns,
            "driver_licence_issuedate",
            "@licenceIssueDate",
            DbType.DateTime,
            copied.LicenceIssueDate
        );
        AddValue(
            values,
            columns,
            "driver_licence_lastVerifiedDate",
            "@licenceLastVerifiedDate",
            DbType.DateTime,
            copied.LicenceLastVerifiedDate
        );
        AddValue(values, columns, "driver_hasPDP", "@hasPdp", DbType.Boolean, copied.HasPdp);
        AddValue(
            values,
            columns,
            "driver_PDP_ExpiryDate",
            "@pdpExpiryDate",
            DbType.DateTime,
            copied.PdpExpiryDate
        );
        AddValue(
            values,
            columns,
            "driver_licence_ExpiryDate",
            "@licenceExpiryDate",
            DbType.DateTime,
            copied.LicenceExpiryDate
        );
        AddValue(values, columns, "driver_active", "@driverActive", DbType.Boolean, copied.IsActive);
        AddValue(values, columns, "date_created", "@dateCreated", DbType.DateTime2, DateTime.UtcNow);
        AddValue(
            values,
            columns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );
        AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);

        var createdId = await ExecuteInsertAsync(tableName, values);
        return await GetByIdAsync(createdId)
            ?? throw new InvalidOperationException("Created trip driver could not be read.");
    }

    public async Task UpdateAsync(TripDriver tripDriver, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(tripDriver);
        if (tripDriver.trip_driver_code <= 0)
        {
            throw new InvalidOperationException("A valid trip-driver code is required.");
        }

        _ = await GetByIdAsync(tripDriver.trip_driver_code)
            ?? throw new InvalidOperationException(
                $"TripDriver with trip_driver_code {tripDriver.trip_driver_code} not found"
            );

        if (await IsLegacyProcedureAvailableAsync(UpdateProcedure, UpdateProcedureParameters))
        {
            // Archived DEV_UPD_TripDrivers only writes name, SA ID, primary
            // flag, and trip authority. @SiteDriverID is accepted and unused.
            await ExecuteUpdateProcedureAsync(tripDriver);
            return;
        }

        // Compatibility fallback: DEV_UPD_TripDrivers is genuinely absent.
        // Mirror the archived update column set; do not overwrite licence or
        // site fields copied from the site driver at insert time.
        var tableName =
            await ResolveTripDriverTableAsync()
            ?? throw new InvalidOperationException(
                "Neither trip_drivers nor trip_driver contains the required legacy trip-driver columns."
            );
        var columns = await GetTableColumnsAsync(tableName);
        var assignments = new List<string>();
        var values = new List<WriteValue>();
        AddAssignment(
            assignments,
            values,
            columns,
            "trip_authority_code",
            "@tripAuthorityCode",
            DbType.Int32,
            tripDriver.trip_authority_code
        );
        AddAssignment(
            assignments,
            values,
            columns,
            "trip_driver_name",
            "@tripDriverName",
            DbType.String,
            NullIfWhiteSpace(tripDriver.trip_driver_name)
        );
        AddAssignment(
            assignments,
            values,
            columns,
            "trip_driver_id",
            "@tripDriverId",
            DbType.String,
            NullIfWhiteSpace(tripDriver.trip_driver_id)
        );
        AddAssignment(
            assignments,
            values,
            columns,
            "trip_driver_primary",
            "@tripDriverPrimary",
            DbType.Boolean,
            tripDriver.trip_driver_primary
        );
        AddAssignment(
            assignments,
            values,
            columns,
            "date_updated",
            "@dateUpdated",
            DbType.DateTime2,
            DateTime.UtcNow
        );
        AddAssignment(
            assignments,
            values,
            columns,
            "modified_by_user_code",
            "@modifiedByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );

        if (assignments.Count == 0)
        {
            throw new InvalidOperationException(
                $"No compatible columns are available for updating {tableName}."
            );
        }

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"""
                UPDATE [dbo].[{tableName}]
                SET {string.Join(", ", assignments)}
                WHERE [trip_driver_code] = @tripDriverCode
                """;
            AddParameters(command, values);
            AddParameter(command, "@tripDriverCode", DbType.Int32, tripDriver.trip_driver_code);
            if (await command.ExecuteNonQueryAsync() == 0)
            {
                throw new InvalidOperationException(
                    $"TripDriver with trip_driver_code {tripDriver.trip_driver_code} not found"
                );
            }
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    public async Task DeleteAsync(int tripDriverCode, int currentUserId)
    {
        var existing = await GetByIdAsync(tripDriverCode);
        if (existing is null)
            return;

        // No archived DEV_DEL_TripDrivers exists. Individual trip-driver rows
        // are replaced by DEV_UPD_TripXML (delete-and-reinsert for the trip).
        // Standalone delete is a labelled compatibility fallback: hard-delete
        // the row. Do not invent a soft-delete against is_deleted, which the
        // original trip_drivers table does not have.
        var tableName =
            await ResolveTripDriverTableAsync()
            ?? throw new InvalidOperationException(
                "Neither trip_drivers nor trip_driver contains the required legacy trip-driver columns."
            );

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"""
                DELETE FROM [dbo].[{tableName}]
                WHERE [trip_driver_code] = @tripDriverCode
                """;
            AddParameter(command, "@tripDriverCode", DbType.Int32, tripDriverCode);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    public async Task<IEnumerable<TripDriver>> SearchDriversAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await QueryAsync(orderBy: "[trip_driver_name], [trip_driver_code]");

        var search = $"%{searchTerm.Trim().ToLowerInvariant()}%";
        return await QueryAsync(
            """
            LOWER(LTRIM(RTRIM(COALESCE([trip_driver_name], '')))) LIKE @search
            OR LOWER(LTRIM(RTRIM(COALESCE([trip_driver_id], '')))) LIKE @search
            OR LOWER(LTRIM(RTRIM(COALESCE([driver_licence_number], '')))) LIKE @search
            """,
            command => AddParameter(command, "@search", DbType.String, search),
            "[trip_driver_name], [trip_driver_code]",
            extraRequiredColumns: ["trip_driver_id", "driver_licence_number"]
        );
    }

    private async Task<List<TripDriver>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null,
        string orderBy = "[trip_driver_code]",
        IReadOnlyList<string>? extraRequiredColumns = null
    )
    {
        var results = new List<TripDriver>();
        foreach (var tableName in TableNames)
        {
            var columns = await GetTableColumnsAsync(tableName);
            if (!RequiredColumns.All(columns.Contains))
                continue;
            if (
                extraRequiredColumns is not null
                && extraRequiredColumns.Any(column => !columns.Contains(column))
            )
            {
                continue;
            }

            var projection = BusinessColumns
                .Concat(OptionalAuditColumns)
                .Select(column => GetProjection(columns, column));
            var conditions = new List<string>();
            if (!string.IsNullOrWhiteSpace(predicate))
                conditions.Add(predicate);
            if (columns.Contains("is_deleted"))
                conditions.Add("ISNULL([is_deleted], 0) = 0");

            var whereClause = conditions.Count == 0 ? string.Empty : $"WHERE {string.Join(" AND ", conditions)}";
            var connection = _context.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
                await connection.OpenAsync();

            try
            {
                await using var command = connection.CreateCommand();
                command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
                command.CommandText = $"""
                    SELECT {string.Join(", ", projection)}
                    FROM [dbo].[{tableName}]
                    {whereClause}
                    ORDER BY {orderBy}
                    """;
                configure?.Invoke(command);

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    results.Add(MapTripDriver(reader));
            }
            finally
            {
                if (shouldClose)
                    await connection.CloseAsync();
            }
        }

        return results
            .GroupBy(driver => driver.trip_driver_code)
            .Select(group => group.First())
            .ToList();
    }

    private async Task<List<TripDriver>> ExecuteSelectProcedureAsync(
        string procedureName,
        Action<DbCommand> configure,
        int tripAuthorityCode,
        bool mapDisplayAliases = false
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = procedureName;
            configure(command);

            var drivers = new List<TripDriver>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var driver = mapDisplayAliases
                    ? MapTripDriverFromDisplayAliases(reader)
                    : MapTripDriver(reader);
                if (driver.trip_authority_code <= 0)
                    driver.trip_authority_code = tripAuthorityCode;
                drivers.Add(driver);
            }

            return drivers;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<int> ExecuteInsertProcedureAsync(TripDriver tripDriver, int siteDriverId)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = InsertProcedure;

            var output = command.CreateParameter();
            output.ParameterName = "@NewTripDriverID";
            output.DbType = DbType.Int32;
            output.Direction = ParameterDirection.Output;
            command.Parameters.Add(output);
            AddTripDriverProcedureParameters(command, tripDriver, siteDriverId);
            await command.ExecuteNonQueryAsync();

            if (output.Value is null or DBNull || Convert.ToInt32(output.Value) <= 0)
            {
                throw new InvalidOperationException(
                    "The legacy DEV_INS_TripDrivers procedure completed without returning the created trip-driver code."
                );
            }

            return Convert.ToInt32(output.Value);
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task ExecuteUpdateProcedureAsync(TripDriver tripDriver)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = UpdateProcedure;
            AddParameter(command, "@NewTripTriverID", DbType.Int32, tripDriver.trip_driver_code);
            AddTripDriverProcedureParameters(command, tripDriver, tripDriver.site_driver_code);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static void AddTripDriverProcedureParameters(
        DbCommand command,
        TripDriver tripDriver,
        int siteDriverId
    )
    {
        AddParameter(command, "@TripID", DbType.Int32, tripDriver.trip_authority_code);
        AddParameter(
            command,
            "@DriverName",
            DbType.String,
            NullIfWhiteSpace(tripDriver.trip_driver_name) ?? string.Empty
        );
        AddParameter(command, "@DriverSAID", DbType.String, NullIfWhiteSpace(tripDriver.trip_driver_id));
        AddParameter(command, "@DriverIsPrimary", DbType.Boolean, tripDriver.trip_driver_primary);
        AddParameter(command, "@SiteDriverID", DbType.Int32, siteDriverId);
    }

    private async Task<int> ResolveSiteDriverIdAsync(TripDriver tripDriver)
    {
        if (tripDriver.site_driver_code > 0)
        {
            await EnsureSiteDriverExistsAsync(tripDriver.site_driver_code);
            return tripDriver.site_driver_code;
        }

        throw new InvalidOperationException(
            "A site driver is required to assign a trip driver. The archived DEV_INS_TripDrivers procedure copies licence and identity fields from site_drivers."
        );
    }

    private async Task EnsureSiteDriverExistsAsync(int siteDriverId)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT TOP (1) [site_driver_code]
                FROM [dbo].[site_drivers]
                WHERE [site_driver_code] = @siteDriverId
                """;
            AddParameter(command, "@siteDriverId", DbType.Int32, siteDriverId);
            var value = await command.ExecuteScalarAsync();
            if (value is null or DBNull)
            {
                throw new InvalidOperationException(
                    $"Site driver {siteDriverId} was not found. A trip driver can only be assigned from an existing site driver."
                );
            }
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<SiteDriverCopy> LoadSiteDriverCopyAsync(int siteDriverId)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT [site_code], [driver_licence_type_id], [driver_passportnumber],
                       [driver_persalnumber], [driver_contractnumber], [driver_licence_number],
                       [driver_licence_issuedate], [driver_licence_lastVerifiedDate],
                       [driver_hasPDP], [driver_PDP_ExpiryDate], [driver_licence_ExpiryDate],
                       [driver_active]
                FROM [dbo].[site_drivers]
                WHERE [site_driver_code] = @siteDriverId
                """;
            AddParameter(command, "@siteDriverId", DbType.Int32, siteDriverId);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException(
                    $"Site driver {siteDriverId} was not found. A trip driver can only be assigned from an existing site driver."
                );
            }

            return new SiteDriverCopy(
                ReadInt32(reader, "site_code"),
                ReadInt32(reader, "driver_licence_type_id"),
                ReadString(reader, "driver_passportnumber"),
                ReadString(reader, "driver_persalnumber"),
                ReadString(reader, "driver_contractnumber"),
                ReadString(reader, "driver_licence_number"),
                ReadDateTime(reader, "driver_licence_issuedate"),
                ReadDateTime(reader, "driver_licence_lastVerifiedDate"),
                ReadBoolean(reader, "driver_hasPDP"),
                ReadDateTime(reader, "driver_PDP_ExpiryDate"),
                ReadDateTime(reader, "driver_licence_ExpiryDate"),
                ReadBoolean(reader, "driver_active")
            );
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<string?> ResolveTripDriverTableAsync()
    {
        foreach (var tableName in TableNames)
        {
            var columns = await GetTableColumnsAsync(tableName);
            if (RequiredColumns.All(columns.Contains))
                return tableName;
        }

        return null;
    }

    private async Task<int> ExecuteInsertAsync(string tableName, IReadOnlyList<WriteValue> values)
    {
        if (values.Count == 0)
        {
            throw new InvalidOperationException(
                $"No compatible columns are available for inserting into {tableName}."
            );
        }

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"""
                INSERT INTO [dbo].[{tableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))})
                OUTPUT INSERTED.[trip_driver_code]
                VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
                """;
            AddParameters(command, values);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<bool> IsLegacyProcedureAvailableAsync(
        string procedureName,
        IReadOnlyList<string> expectedParameters
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT [parameterObject].[name]
                FROM [sys].[procedures] AS [procedureObject]
                INNER JOIN [sys].[schemas] AS [schemaObject]
                    ON [schemaObject].[schema_id] = [procedureObject].[schema_id]
                LEFT JOIN [sys].[parameters] AS [parameterObject]
                    ON [parameterObject].[object_id] = [procedureObject].[object_id]
                   AND [parameterObject].[parameter_id] > 0
                WHERE [schemaObject].[name] = N'dbo'
                  AND [procedureObject].[name] = @procedureName
                ORDER BY [parameterObject].[parameter_id]
                """;
            AddParameter(command, "@procedureName", DbType.String, procedureName);

            var actualParameters = new List<string>();
            var procedureFound = false;
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                procedureFound = true;
                if (!reader.IsDBNull(0))
                    actualParameters.Add(reader.GetString(0));
            }

            if (!procedureFound)
                return false;

            if (!actualParameters.SequenceEqual(expectedParameters, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"The deployed legacy procedure {procedureName} does not match the archived parameter contract. No direct-DML fallback was run."
                );
            }

            return true;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<HashSet<string>> GetTableColumnsAsync(string tableName)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT [COLUMN_NAME]
                FROM [INFORMATION_SCHEMA].[COLUMNS]
                WHERE [TABLE_SCHEMA] = @schema
                  AND [TABLE_NAME] = @table
                """;
            AddParameter(command, "@schema", DbType.String, "dbo");
            AddParameter(command, "@table", DbType.String, tableName);

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                columns.Add(reader.GetString(0));

            return columns;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static string GetProjection(IReadOnlySet<string> columns, string column) =>
        columns.Contains(column) ? $"[{column}] AS [{column}]" : $"NULL AS [{column}]";

    private static void AddValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> columns,
        string column,
        string parameter,
        DbType dbType,
        object? value
    )
    {
        if (columns.Contains(column))
            values.Add(new WriteValue(column, parameter, dbType, value));
    }

    private static void AddAssignment(
        ICollection<string> assignments,
        ICollection<WriteValue> values,
        IReadOnlySet<string> columns,
        string column,
        string parameter,
        DbType dbType,
        object? value
    )
    {
        if (!columns.Contains(column))
            return;

        assignments.Add($"[{column}] = {parameter}");
        values.Add(new WriteValue(column, parameter, dbType, value));
    }

    private static void AddParameters(DbCommand command, IEnumerable<WriteValue> values)
    {
        foreach (var value in values)
            AddParameter(command, value.Parameter, value.DbType, value.Value);
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static TripDriver MapTripDriver(DbDataReader reader) =>
        new()
        {
            trip_driver_code = ReadInt32(reader, "trip_driver_code") ?? 0,
            trip_driver_name = ReadString(reader, "trip_driver_name"),
            trip_driver_id = ReadString(reader, "trip_driver_id"),
            trip_authority_code = ReadInt32(reader, "trip_authority_code") ?? 0,
            trip_driver_primary = ReadBoolean(reader, "trip_driver_primary"),
            site_code = ReadInt32(reader, "site_code"),
            driver_licence_type_id = ReadInt32(reader, "driver_licence_type_id"),
            driver_passportnumber = ReadString(reader, "driver_passportnumber"),
            driver_persalnumber = ReadString(reader, "driver_persalnumber"),
            driver_contractnumber = ReadString(reader, "driver_contractnumber"),
            driver_licence_number = ReadString(reader, "driver_licence_number"),
            driver_licence_issuedate = ReadDateTime(reader, "driver_licence_issuedate"),
            driver_licence_lastVerifiedDate = ReadDateTime(
                reader,
                "driver_licence_lastVerifiedDate"
            ),
            driver_hasPDP = ReadBoolean(reader, "driver_hasPDP"),
            driver_PDP_ExpiryDate = ReadDateTime(reader, "driver_PDP_ExpiryDate"),
            driver_licence_ExpiryDate = ReadDateTime(reader, "driver_licence_ExpiryDate"),
            driver_active = ReadBoolean(reader, "driver_active"),
            date_created = ReadDateTime(reader, "date_created") ?? default,
            date_updated = ReadDateTime(reader, "date_updated"),
            created_by_user_code = ReadInt32(reader, "created_by_user_code"),
            modified_by_user_code = ReadInt32(reader, "modified_by_user_code"),
            is_deleted = ReadBoolean(reader, "is_deleted"),
        };

    private static TripDriver MapTripDriverFromDisplayAliases(DbDataReader reader) =>
        new()
        {
            trip_driver_code = ReadInt32(reader, "DriverDBId") ?? 0,
            trip_driver_name = ReadString(reader, "DriverName"),
            trip_driver_id = ReadString(reader, "DriverSAID"),
            trip_driver_primary = ReadBoolean(reader, "DriverIsPrimary"),
            driver_licence_type_id = ReadInt32(reader, "DriverLicenseTypeId"),
            driver_passportnumber = ReadString(reader, "DriverPassportNumber"),
            driver_persalnumber = ReadString(reader, "DriverPersalNumber"),
            driver_contractnumber = ReadString(reader, "DriverContractNo"),
            driver_licence_number = ReadString(reader, "DriverLicenceNumber"),
            driver_licence_issuedate = ReadDateTime(reader, "DriverLicenceIssueDate"),
            driver_licence_lastVerifiedDate = ReadDateTime(
                reader,
                "DriverLicenceLastVerifiedDate"
            ),
            driver_hasPDP = ReadBoolean(reader, "DriverHasPDP"),
            driver_PDP_ExpiryDate = ReadDateTime(reader, "DriverPDPExpiryDate"),
            driver_licence_ExpiryDate = ReadDateTime(reader, "DriverLicenseExpiryDate"),
            driver_active = ReadBoolean(reader, "DriverIsActive"),
        };

    private static int? ReadInt32(DbDataReader reader, string name)
    {
        var ordinal = TryGetOrdinal(reader, name);
        if (ordinal is null || reader.IsDBNull(ordinal.Value))
            return null;

        var value = reader.GetValue(ordinal.Value);
        return value is int typed ? typed : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static bool ReadBoolean(DbDataReader reader, string name)
    {
        var ordinal = TryGetOrdinal(reader, name);
        if (ordinal is null || reader.IsDBNull(ordinal.Value))
            return false;

        var value = reader.GetValue(ordinal.Value);
        return value is bool typed ? typed : Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    }

    private static string? ReadString(DbDataReader reader, string name)
    {
        var ordinal = TryGetOrdinal(reader, name);
        if (ordinal is null || reader.IsDBNull(ordinal.Value))
            return null;

        var value = Convert.ToString(reader.GetValue(ordinal.Value), CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static DateTime? ReadDateTime(DbDataReader reader, string name)
    {
        var ordinal = TryGetOrdinal(reader, name);
        if (ordinal is null || reader.IsDBNull(ordinal.Value))
            return null;

        var value = reader.GetValue(ordinal.Value);
        if (value is DateTime typed)
            return typed;

        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (
            DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed
            )
        )
        {
            return parsed;
        }

        if (
            DateTime.TryParseExact(
                text,
                "MM/dd/yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsed
            )
        )
        {
            return parsed;
        }

        return null;
    }

    private static int? TryGetOrdinal(DbDataReader reader, string name)
    {
        for (var index = 0; index < reader.FieldCount; index++)
        {
            if (string.Equals(reader.GetName(index), name, StringComparison.OrdinalIgnoreCase))
                return index;
        }

        return null;
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record WriteValue(string Column, string Parameter, DbType DbType, object? Value);

    private sealed record SiteDriverCopy(
        int? SiteCode,
        int? LicenceTypeId,
        string? PassportNumber,
        string? PersalNumber,
        string? ContractNumber,
        string? LicenceNumber,
        DateTime? LicenceIssueDate,
        DateTime? LicenceLastVerifiedDate,
        bool HasPdp,
        DateTime? PdpExpiryDate,
        DateTime? LicenceExpiryDate,
        bool IsActive
    );
}
