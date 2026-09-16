using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for driver operations against legacy site_drivers table
/// Handles fleet driver management and license tracking
/// </summary>
public class DriverRepository : IDriverRepository
{
    private readonly FisDbContext _context;

    public DriverRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get driver by ID (site_driver_code)
    /// Note: Interface expects string but entity uses int - convert accordingly
    /// </summary>
    public async Task<Driver?> GetByIdAsync(string driverId)
    {
        if (!int.TryParse(driverId, out int driverCode))
            return null;

        return (
            await QueryLegacyAsync(
                "[site_driver_code] = @driverCode",
                command => AddParameter(command, "@driverCode", DbType.Int32, driverCode)
            )
        ).SingleOrDefault();
    }

    /// <summary>
    /// Get driver by licence number
    /// </summary>
    public async Task<Driver?> GetByLicenceNumberAsync(string licenceNumber)
    {
        if (string.IsNullOrEmpty(licenceNumber))
            return null;

        return (
            await QueryLegacyAsync(
                "[driver_active] = 1 AND LOWER(LTRIM(RTRIM([driver_licence_number]))) = @licenceNumber",
                command =>
                    AddParameter(
                        command,
                        "@licenceNumber",
                        DbType.String,
                        licenceNumber.Trim().ToLowerInvariant()
                    )
            )
        ).FirstOrDefault();
    }

    /// <summary>
    /// Get all active drivers
    /// </summary>
    public async Task<IEnumerable<Driver>> GetActiveDriversAsync()
    {
        return await QueryLegacyAsync(
            "[driver_active] = 1",
            orderBy: "[driver_surname], [driver_firstname], [site_driver_code]"
        );
    }

    public async Task<IEnumerable<Driver>> GetAllDriversAsync()
    {
        return await QueryLegacyAsync(
            orderBy: "[driver_surname], [driver_firstname], [site_driver_code]"
        );
    }

    /// <summary>
    /// Search drivers by surname, firstname, licence number, or personal number
    /// </summary>
    public async Task<IEnumerable<Driver>> SearchDriversAsync(string searchTerm)
    {
        if (string.IsNullOrEmpty(searchTerm))
            return await GetActiveDriversAsync();

        var search = searchTerm.ToLower();
        return await QueryLegacyAsync(
            """
            [driver_active] = 1 AND
            (
                LOWER(LTRIM(RTRIM(COALESCE([driver_surname], '')))) LIKE @search
                OR LOWER(LTRIM(RTRIM(COALESCE([driver_firstname], '')))) LIKE @search
                OR LOWER(LTRIM(RTRIM(COALESCE([driver_licence_number], '')))) LIKE @search
                OR LOWER(LTRIM(RTRIM(COALESCE([driver_persalnumber], '')))) LIKE @search
                OR LOWER(LTRIM(RTRIM(COALESCE([driver_SA_id], '')))) LIKE @search
                OR LOWER(LTRIM(RTRIM(COALESCE([driver_passportnumber], '')))) LIKE @search
            )
            """,
            command => AddParameter(command, "@search", DbType.String, $"%{search}%"),
            "[driver_surname], [driver_firstname], [site_driver_code]"
        );
    }

    /// <summary>
    /// Create new driver
    /// </summary>
    public async Task<Driver> CreateAsync(Driver driver, int currentUserId)
    {
        if (driver == null)
            throw new ArgumentNullException(nameof(driver));

        await EnsureIdentityUniqueAcrossSitesAsync(driver);

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            var procedureParameters = await GetProcedureParametersAsync(
                connection,
                "DEV_INS_SiteDrivers"
            );
            if (procedureParameters is not null)
            {
                EnsureDriverProcedureContract("DEV_INS_SiteDrivers", procedureParameters, outputCode: true);
                await using var command = connection.CreateCommand();
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "[dbo].[DEV_INS_SiteDrivers]";
                AddDriverProcedureParameters(command, driver, outputCode: true);
                await command.ExecuteNonQueryAsync();
                driver.site_driver_code = Convert.ToInt32(command.Parameters["@SiteDriverCode"].Value);
            }
            else
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO [dbo].[site_drivers]
                    (
                        [site_code], [driver_licence_type_id], [driver_surname], [driver_firstname],
                        [driver_SA_id], [driver_passportnumber], [driver_persalnumber],
                        [driver_contractnumber], [driver_licence_number],
                        [driver_licence_issuedate], [driver_licence_lastVerifiedDate],
                        [driver_hasPDP], [driver_PDP_ExpiryDate], [driver_licence_ExpiryDate], [driver_active]
                    )
                    OUTPUT INSERTED.[site_driver_code]
                    VALUES
                    (
                        @siteCode, @licenceTypeId, @surname, @firstName, @southAfricanId,
                        @passportNumber, @persalNumber, @contractNumber, @licenceNumber,
                        @licenceIssueDate, @licenceLastVerifiedDate, @hasPdp, @pdpExpiryDate,
                        @licenceExpiryDate, @active
                    )
                    """;
                AddDriverFallbackParameters(command, driver, includeCode: false);
                driver.site_driver_code = Convert.ToInt32(await command.ExecuteScalarAsync());
            }
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }

        return await GetByIdAsync(driver.site_driver_code.ToString())
            ?? throw new InvalidOperationException("Created driver could not be read.");
    }

    /// <summary>
    /// Update existing driver
    /// </summary>
    public async Task UpdateAsync(Driver driver, int currentUserId)
    {
        if (driver == null)
            throw new ArgumentNullException(nameof(driver));

        await EnsureIdentityUniqueAcrossSitesAsync(driver, driver.site_driver_code);

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            var procedureParameters = await GetProcedureParametersAsync(
                connection,
                "DEV_UPD_SiteDrivers"
            );
            if (procedureParameters is not null)
            {
                EnsureDriverProcedureContract("DEV_UPD_SiteDrivers", procedureParameters, outputCode: false);
                await using var command = connection.CreateCommand();
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "[dbo].[DEV_UPD_SiteDrivers]";
                AddDriverProcedureParameters(command, driver, outputCode: false);
                await command.ExecuteNonQueryAsync();
            }
            else
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    UPDATE [dbo].[site_drivers]
                    SET [site_code] = @siteCode,
                        [driver_licence_type_id] = @licenceTypeId,
                        [driver_surname] = @surname,
                        [driver_firstname] = @firstName,
                        [driver_SA_id] = @southAfricanId,
                        [driver_passportnumber] = @passportNumber,
                        [driver_persalnumber] = @persalNumber,
                        [driver_contractnumber] = @contractNumber,
                        [driver_licence_number] = @licenceNumber,
                        [driver_licence_issuedate] = @licenceIssueDate,
                        [driver_licence_lastVerifiedDate] = @licenceLastVerifiedDate,
                        [driver_hasPDP] = @hasPdp,
                        [driver_PDP_ExpiryDate] = @pdpExpiryDate,
                        [driver_licence_ExpiryDate] = @licenceExpiryDate,
                        [driver_active] = @active
                    WHERE [site_driver_code] = @siteDriverCode
                    """;
                AddDriverFallbackParameters(command, driver, includeCode: true);
                if (await command.ExecuteNonQueryAsync() == 0)
                    throw new KeyNotFoundException($"Driver with site_driver_code {driver.site_driver_code} not found.");
            }
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    /// <summary>
    /// Delete driver by ID
    /// Note: Interface expects string but entity uses int - convert accordingly
    /// </summary>
    public async Task DeleteAsync(string driverId, int currentUserId)
    {
        if (!int.TryParse(driverId, out int driverCode))
            return;

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            var procedureParameters = await GetProcedureParametersAsync(
                connection,
                "DEV_DEL_SiteDrivers"
            );
            if (procedureParameters is not null)
            {
                if (procedureParameters.Count != 1 || !string.Equals(procedureParameters[0], "@ID", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The deployed legacy procedure DEV_DEL_SiteDrivers has an incompatible parameter contract. No direct-DML fallback was used.");
                await using var command = connection.CreateCommand();
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "[dbo].[DEV_DEL_SiteDrivers]";
                AddParameter(command, "@ID", DbType.Int32, driverCode);
                await command.ExecuteNonQueryAsync();
            }
            else
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "UPDATE [dbo].[site_drivers] SET [driver_active] = 0 WHERE [site_driver_code] = @driverCode";
                AddParameter(command, "@driverCode", DbType.Int32, driverCode);
                if (await command.ExecuteNonQueryAsync() == 0)
                    throw new KeyNotFoundException($"Driver with site_driver_code {driverCode} not found.");
            }
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
        Justification = "Table, projection, predicates, and ordering are fixed repository constants; all submitted values are DbParameters."
    )]
    private async Task<List<Driver>> QueryLegacyAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null,
        string orderBy = "[site_driver_code]"
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
                SELECT [site_driver_code], [site_code], [driver_licence_type_id], [driver_surname], [driver_firstname],
                       [driver_SA_id], [driver_passportnumber], [driver_persalnumber], [driver_contractnumber],
                       [driver_licence_number], [driver_licence_issuedate], [driver_licence_lastVerifiedDate],
                       [driver_hasPDP], [driver_PDP_ExpiryDate], [driver_licence_ExpiryDate], [driver_active]
                FROM [dbo].[site_drivers]
                """;
            if (!string.IsNullOrWhiteSpace(predicate))
                command.CommandText += $" WHERE {predicate}";
            command.CommandText += $" ORDER BY {orderBy}";
            configure?.Invoke(command);

            var drivers = new List<Driver>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                drivers.Add(new Driver
                {
                    site_driver_code = Convert.ToInt32(reader["site_driver_code"]),
                    site_code = Convert.ToInt32(reader["site_code"]),
                    driver_licence_type_id = Convert.ToInt32(reader["driver_licence_type_id"]),
                    driver_surname = ReadString(reader, "driver_surname"),
                    driver_firstname = ReadString(reader, "driver_firstname"),
                    driver_SA_id = ReadString(reader, "driver_SA_id"),
                    driver_passportnumber = ReadString(reader, "driver_passportnumber"),
                    driver_persalnumber = ReadString(reader, "driver_persalnumber"),
                    driver_contractnumber = ReadString(reader, "driver_contractnumber"),
                    driver_licence_number = ReadString(reader, "driver_licence_number"),
                    driver_licence_issuedate = Convert.ToDateTime(reader["driver_licence_issuedate"]),
                    driver_licence_lastVerifiedDate = Convert.ToDateTime(reader["driver_licence_lastVerifiedDate"]),
                    driver_hasPDP = Convert.ToBoolean(reader["driver_hasPDP"]),
                    driver_PDP_ExpiryDate = ReadDate(reader, "driver_PDP_ExpiryDate"),
                    driver_licence_ExpiryDate = ReadDate(reader, "driver_licence_ExpiryDate"),
                    driver_active = Convert.ToBoolean(reader["driver_active"]),
                });
            }
            return drivers;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static string? ReadString(DbDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? null : Convert.ToString(reader[name])?.Trim();

    private static DateTime? ReadDate(DbDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? null : Convert.ToDateTime(reader[name]);

    private async Task<IReadOnlyList<string>?> GetProcedureParametersAsync(DbConnection connection, string procedureName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT [p].[name]
            FROM [sys].[procedures] AS [sp]
            INNER JOIN [sys].[schemas] AS [sc] ON [sc].[schema_id] = [sp].[schema_id]
            LEFT JOIN [sys].[parameters] AS [p]
                ON [p].[object_id] = [sp].[object_id]
               AND [p].[parameter_id] > 0
            WHERE [sc].[name] = N'dbo' AND [sp].[name] = @procedureName
            ORDER BY [p].[parameter_id]
            """;
        AddParameter(command, "@procedureName", DbType.String, procedureName);
        var parameters = new List<string>();
        var found = false;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            found = true;
            if (!reader.IsDBNull(0)) parameters.Add(reader.GetString(0));
        }
        return found ? parameters : null;
    }

    private static void EnsureDriverProcedureContract(string procedureName, IReadOnlyList<string> actual, bool outputCode)
    {
        var expected = new List<string> { "@SiteDriverCode", "@SiteCode", "@DriverLicenceTypeID", "@Surname", "@FirstName", "@SAIDNumber", "@PassportNumber", "@PersalNumber", "@DriverContractNumber", "@DriverLicenceNumber", "@DriverLicenceIssueDate", "@DriverLicenceLastVerifiedDate", "@HasPDP", "@PDPExpiryDate", "@LicenceExpiryDate", "@Active" };
        if (!actual.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"The deployed legacy procedure {procedureName} has an incompatible parameter contract. No direct-DML fallback was used.");
    }

    private static void AddDriverProcedureParameters(DbCommand command, Driver driver, bool outputCode)
    {
        var code = command.CreateParameter();
        code.ParameterName = "@SiteDriverCode";
        code.DbType = DbType.Int32;
        code.Direction = outputCode ? ParameterDirection.Output : ParameterDirection.Input;
        code.Value = outputCode ? DBNull.Value : driver.site_driver_code;
        command.Parameters.Add(code);
        AddDriverFallbackParameters(command, driver, includeCode: false, legacyNames: true);
    }

    private static void AddDriverFallbackParameters(DbCommand command, Driver driver, bool includeCode, bool legacyNames = false)
    {
        if (includeCode) AddParameter(command, "@siteDriverCode", DbType.Int32, driver.site_driver_code);
        AddParameter(command, legacyNames ? "@SiteCode" : "@siteCode", DbType.Int32, driver.site_code);
        AddParameter(command, legacyNames ? "@DriverLicenceTypeID" : "@licenceTypeId", DbType.Int32, driver.driver_licence_type_id);
        AddParameter(command, legacyNames ? "@Surname" : "@surname", DbType.String, driver.driver_surname?.Trim());
        AddParameter(command, legacyNames ? "@FirstName" : "@firstName", DbType.String, driver.driver_firstname?.Trim());
        AddParameter(command, legacyNames ? "@SAIDNumber" : "@southAfricanId", DbType.String, driver.driver_SA_id?.Trim());
        AddParameter(command, legacyNames ? "@PassportNumber" : "@passportNumber", DbType.String, driver.driver_passportnumber?.Trim());
        AddParameter(command, legacyNames ? "@PersalNumber" : "@persalNumber", DbType.String, driver.driver_persalnumber?.Trim());
        AddParameter(command, legacyNames ? "@DriverContractNumber" : "@contractNumber", DbType.String, driver.driver_contractnumber?.Trim());
        AddParameter(command, legacyNames ? "@DriverLicenceNumber" : "@licenceNumber", DbType.String, driver.driver_licence_number?.Trim());
        AddParameter(command, legacyNames ? "@DriverLicenceIssueDate" : "@licenceIssueDate", DbType.DateTime, driver.driver_licence_issuedate);
        AddParameter(command, legacyNames ? "@DriverLicenceLastVerifiedDate" : "@licenceLastVerifiedDate", DbType.DateTime, driver.driver_licence_lastVerifiedDate);
        AddParameter(command, legacyNames ? "@HasPDP" : "@hasPdp", DbType.Boolean, driver.driver_hasPDP);
        AddParameter(command, legacyNames ? "@PDPExpiryDate" : "@pdpExpiryDate", DbType.DateTime, driver.driver_PDP_ExpiryDate);
        AddParameter(command, legacyNames ? "@LicenceExpiryDate" : "@licenceExpiryDate", DbType.DateTime, driver.driver_licence_ExpiryDate);
        AddParameter(command, legacyNames ? "@Active" : "@active", DbType.Boolean, driver.driver_active);
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SQL statement uses fixed legacy identity column names and parameterized identity values."
    )]
    private async Task EnsureIdentityUniqueAcrossSitesAsync(Driver candidate, int? excludedCode = null)
    {
        var identities = new (string Column, string? Value)[]
        {
            ("driver_SA_id", NormalizeIdentity(candidate.driver_SA_id)),
            ("driver_passportnumber", NormalizeIdentity(candidate.driver_passportnumber)),
            ("driver_persalnumber", NormalizeIdentity(candidate.driver_persalnumber)),
            ("driver_contractnumber", NormalizeIdentity(candidate.driver_contractnumber)),
            ("driver_licence_number", NormalizeIdentity(candidate.driver_licence_number)),
        };
        var populated = identities.Where(identity => identity.Value is not null).ToArray();
        if (populated.Length == 0)
            return;

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            var identityPredicates = populated
                .Select((identity, index) =>
                    $"NULLIF(UPPER(REPLACE(LTRIM(RTRIM([{identity.Column}])), ' ', '')), '') = @identity{index}")
                .ToArray();
            var excludedPredicate = excludedCode.HasValue
                ? " AND [site_driver_code] <> @excludedCode"
                : string.Empty;
            command.CommandText = $"""
                SELECT TOP (1) [site_driver_code], [site_code]
                FROM [dbo].[site_drivers]
                WHERE ({string.Join(" OR ", identityPredicates)}){excludedPredicate}
                """;
            for (var index = 0; index < populated.Length; index++)
                AddParameter(command, $"@identity{index}", DbType.String, populated[index].Value);
            if (excludedCode.HasValue)
                AddParameter(command, "@excludedCode", DbType.Int32, excludedCode.Value);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return;

            var existingCode = Convert.ToInt32(reader["site_driver_code"]);
            var existingSite = Convert.ToInt32(reader["site_code"]);
            throw new DriverIdentityDuplicateException(
                $"This driver identity already exists at site {existingSite} (site-driver {existingCode}). A driver cannot be captured or assigned to more than one site."
            );
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static string? NormalizeIdentity(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
}

public sealed class DriverIdentityDuplicateException : InvalidOperationException
{
    public DriverIdentityDuplicateException(string message)
        : base(message) { }
}
