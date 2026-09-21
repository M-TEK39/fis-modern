using System.Data;
using System.Data.Common;
using FIS.Data.SqlServer;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Live driver-maintenance lookups from MaintainDriver.aspx.vb and
/// MaintainDrivers.ascx. CreateTrip CommandText lookups stay commented and
/// are not overlaid.
/// </summary>
public sealed class SiteDriverLookupOverlay
{
    private readonly FisDbContext _context;

    public SiteDriverLookupOverlay(FisDbContext context)
    {
        _context = context;
    }

    public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>?> ReadLicenceTypeRowsAsync()
    {
        return LegacySelectorProcedure.TryReadRowsAsync(
            _context,
            "DEV_SEL_DriverLicenceTypes",
            [[], ["@FilterID"]],
            actualParameters =>
                actualParameters.Any(parameter =>
                    string.Equals(parameter, "@FilterID", StringComparison.OrdinalIgnoreCase)
                )
                    ? command => AddParameter(command, "@FilterID", DbType.Int32, -1)
                    : null
        );
    }

    public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>?> ReadSingleSiteDriverRowsAsync(
        int siteDriverCode
    )
    {
        return LegacySelectorProcedure.TryReadRowsAsync(
            _context,
            "DEV_SEL_SingleSiteDriver",
            [["@SiteDriverCode"]],
            _ => command => AddParameter(command, "@SiteDriverCode", DbType.Int32, siteDriverCode)
        );
    }

    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>?> ReadSitesForEditRowsAsync(
        int departmentId
    )
    {
        var actualParameters = await LegacySelectorProcedure.TryGetProcedureParametersAsync(
            _context,
            "DEV_SEL_Sites_ForEdit"
        );
        if (actualParameters is null)
        {
            return null;
        }

        if (
            actualParameters.SequenceEqual(["@DepartmentId"], StringComparer.OrdinalIgnoreCase)
        )
        {
            return await LegacySelectorProcedure.TryReadRowsAsync(
                _context,
                "DEV_SEL_Sites_ForEdit",
                [["@DepartmentId"]],
                _ => command => AddParameter(command, "@DepartmentId", DbType.Int32, departmentId)
            );
        }

        if (
            actualParameters.Count == 0
            || actualParameters.SequenceEqual(["@SiteCode"], StringComparer.OrdinalIgnoreCase)
        )
        {
            return null;
        }

        throw new InvalidOperationException(
            "The deployed legacy procedure DEV_SEL_Sites_ForEdit does not match its verified parameter contract. No direct-DML fallback was run."
        );
    }

    public static int? ReadInt32(
        IReadOnlyDictionary<string, object?> row,
        params string[] keys
    ) => LegacySelectorProcedure.ReadInt32(row, keys);

    public static string? ReadString(
        IReadOnlyDictionary<string, object?> row,
        params string[] keys
    ) => LegacySelectorProcedure.ReadString(row, keys);

    public static DateTime? ReadDateTime(
        IReadOnlyDictionary<string, object?> row,
        params string[] keys
    )
    {
        foreach (var key in keys)
        {
            if (!row.TryGetValue(key, out var value) || value is null or DBNull)
            {
                continue;
            }

            if (value is DateTime dateTime)
            {
                return dateTime;
            }

            if (DateTime.TryParse(
                    Convert.ToString(value),
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var parsed
                ))
            {
                return parsed;
            }
        }

        return null;
    }

    public static bool? ReadBool(
        IReadOnlyDictionary<string, object?> row,
        params string[] keys
    )
    {
        foreach (var key in keys)
        {
            if (!row.TryGetValue(key, out var value) || value is null or DBNull)
            {
                continue;
            }

            if (value is bool flag)
            {
                return flag;
            }

            if (value is byte or short or int or long)
            {
                return Convert.ToInt64(value) != 0;
            }

            if (bool.TryParse(Convert.ToString(value), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
