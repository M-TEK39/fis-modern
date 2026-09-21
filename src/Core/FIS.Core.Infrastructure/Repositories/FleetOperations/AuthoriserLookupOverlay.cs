using System.Data;
using System.Data.Common;
using FIS.Data.SqlServer;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Live authoriser-maintenance lookups from MaintainApprovers.ascx.
/// DEV_SEL_ApproversLookup is the EditLookupValues dropdown, not the
/// maintain list. DEV_SEL_Departments_ForEdit is commented out in ascx.
/// </summary>
public sealed class AuthoriserLookupOverlay
{
    private readonly FisDbContext _context;

    public AuthoriserLookupOverlay(FisDbContext context)
    {
        _context = context;
    }

    public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>?> ReadApproversForEditRowsAsync(
        int siteId,
        int departmentId
    )
    {
        return LegacySelectorProcedure.TryReadRowsAsync(
            _context,
            "DEV_SEL_ApproversLookup_ForEdit",
            [["@SiteID", "@DepartmentID"]],
            _ =>
                command =>
                {
                    AddParameter(command, "@SiteID", DbType.Int32, siteId);
                    AddParameter(command, "@DepartmentID", DbType.Int32, departmentId);
                }
        );
    }

    public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>?> ReadSingleApproverRowsAsync(
        int approverCode
    )
    {
        return LegacySelectorProcedure.TryReadRowsAsync(
            _context,
            "DEV_SEL_SingleApprover",
            [["@Code"]],
            _ => command => AddParameter(command, "@Code", DbType.Int32, approverCode)
        );
    }

    public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>?> ReadRankRowsAsync()
    {
        return LegacySelectorProcedure.TryReadRowsAsync(
            _context,
            "DEV_SEL_Ranks",
            [[], ["@FilterID"]],
            actualParameters =>
                actualParameters.Any(parameter =>
                    string.Equals(parameter, "@FilterID", StringComparison.OrdinalIgnoreCase)
                )
                    ? command => AddParameter(command, "@FilterID", DbType.Int32, -1)
                    : null
        );
    }

    public Task<IReadOnlyList<string>?> TryGetProcedureParametersAsync(string procedureName)
    {
        return LegacySelectorProcedure.TryGetProcedureParametersAsync(_context, procedureName);
    }

    public static int? ReadInt32(
        IReadOnlyDictionary<string, object?> row,
        params string[] keys
    ) => LegacySelectorProcedure.ReadInt32(row, keys);

    public static string? ReadString(
        IReadOnlyDictionary<string, object?> row,
        params string[] keys
    ) => LegacySelectorProcedure.ReadString(row, keys);

    public static bool MatchesParameterSet(
        IReadOnlyList<string> actualParameters,
        params string[][] acceptedParameterSets
    )
    {
        return acceptedParameterSets.Any(expected =>
            actualParameters.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase)
        );
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
