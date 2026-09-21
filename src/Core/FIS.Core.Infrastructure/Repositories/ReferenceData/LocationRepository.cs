using System.Data;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// dbo.location is location_code + description only. Expanded address/audit
/// columns are not queried or written.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Predicates are fixed compatibility strings; values are parameterized."
)]
public class LocationRepository : ILocationRepository
{
    private readonly FisDbContext _context;

    public LocationRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<Location?> GetByIdAsync(int locationId)
    {
        var locations = await QueryLegacyLocationsAsync(
            "[location_code] = @locationCode",
            command => AddParameter(command, "@locationCode", DbType.Int32, locationId)
        );
        return locations.FirstOrDefault();
    }

    public async Task<Location?> GetByNameAsync(string locationName)
    {
        var locations = await QueryLegacyLocationsAsync(
            "[description] = @locationName",
            command => AddParameter(command, "@locationName", DbType.String, locationName)
        );
        return locations.FirstOrDefault();
    }

    public async Task<IEnumerable<Location>> GetAllLocationsAsync()
    {
        var leftover = await QueryLegacyLocationsAsync();
        var keys = await LegacySelectorProcedure.TryReadOrderedKeysAsync(
            _context,
            "DEV_SEL_locations",
            [],
            null,
            "location_code"
        );
        return keys is null
            ? leftover
            : LegacySelectorProcedure.OrderByKeys(leftover, keys, location => location.LocationId);
    }

    public async Task<LocationPage> GetPageAsync(int page = 1, int pageSize = 24)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var locations = await QueryLegacyLocationsAsync();
        var total = locations.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Min(page, totalPages);
        var items = locations.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new LocationPage(items, page, pageSize, total);
    }

    public Task<IEnumerable<Location>> GetByCountryAsync(string country)
    {
        _ = country;
        return Task.FromResult<IEnumerable<Location>>([]);
    }

    public Task<IEnumerable<Location>> GetByProvinceAsync(string province)
    {
        _ = province;
        return Task.FromResult<IEnumerable<Location>>([]);
    }

    public async Task<Location> CreateAsync(Location location, int currentUserId)
    {
        if (location.LocationId <= 0)
        {
            throw new InvalidOperationException(
                "dbo.location.location_code is not an identity column. No generated key was substituted."
            );
        }

        await ExecuteNonQueryAsync(
            """
            INSERT INTO [dbo].[location] ([location_code], [description])
            VALUES (@locationCode, @description);
            """,
            command =>
            {
                AddParameter(command, "@locationCode", DbType.Int32, location.LocationId);
                AddParameter(command, "@description", DbType.String, location.LocationName);
            }
        );
        return location;
    }

    public async Task UpdateAsync(Location location, int currentUserId)
    {
        if (location == null)
            throw new ArgumentNullException(nameof(location));

        var existing = await GetByIdAsync(location.LocationId);
        if (existing == null)
            throw new InvalidOperationException(
                $"Location with LocationId {location.LocationId} not found"
            );

        await ExecuteNonQueryAsync(
            """
            UPDATE [dbo].[location]
            SET [description] = @description
            WHERE [location_code] = @locationCode;
            """,
            command =>
            {
                AddParameter(command, "@locationCode", DbType.Int32, location.LocationId);
                AddParameter(command, "@description", DbType.String, location.LocationName);
            }
        );
    }

    public async Task DeleteAsync(int locationId, int currentUserId)
    {
        var location = await GetByIdAsync(locationId);
        if (location == null)
        {
            return;
        }

        await ExecuteNonQueryAsync(
            """
            DELETE FROM [dbo].[location]
            WHERE [location_code] = @locationCode;
            """,
            command => AddParameter(command, "@locationCode", DbType.Int32, locationId)
        );
    }

    public async Task<IEnumerable<Location>> SearchLocationsAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await GetAllLocationsAsync();

        return await QueryLegacyLocationsAsEnumerableAsync(
            "[description] LIKE @searchTerm",
            command => AddParameter(command, "@searchTerm", DbType.String, $"%{searchTerm}%")
        );
    }

    private async Task<IEnumerable<Location>> QueryLegacyLocationsAsEnumerableAsync(
        string? predicate = null,
        Action<System.Data.Common.DbCommand>? bind = null
    ) => await QueryLegacyLocationsAsync(predicate, bind);

    private async Task<List<Location>> QueryLegacyLocationsAsync(
        string? predicate = null,
        Action<System.Data.Common.DbCommand>? bind = null
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = string.IsNullOrWhiteSpace(predicate)
                ? """
                    SELECT [location_code], [description]
                    FROM [dbo].[location]
                    ORDER BY [description], [location_code]
                    """
                : $"""
                    SELECT [location_code], [description]
                    FROM [dbo].[location]
                    WHERE {predicate}
                    ORDER BY [description], [location_code]
                    """;
            bind?.Invoke(command);

            var locations = new List<Location>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var locationCode = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0));
                var description = reader.IsDBNull(1) ? string.Empty : reader.GetString(1).Trim();
                if (locationCode <= 0 || string.IsNullOrWhiteSpace(description))
                {
                    continue;
                }

                locations.Add(
                    new Location
                    {
                        LocationId = locationCode,
                        LocationName = description,
                        Description = description,
                        IsActive = true,
                    }
                );
            }

            return locations;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task ExecuteNonQueryAsync(
        string commandText,
        Action<System.Data.Common.DbCommand> bind
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = commandText;
            bind(command);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(
        System.Data.Common.DbCommand command,
        string name,
        DbType type,
        object value
    )
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
