using System.Data;
using System.Data.Common;
using FIS.Data.SqlServer;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// BAS segment lists from Taxis/getCodes.aspx:129/:191/:259 and
/// GGFleet/Controls/Allocations.ascx.cs types 1-5. DEV_SEL_BASSegments takes
/// @FilterID int, @SegmentTypeCode tinyint (archived order) and owns membership
/// and order. The procedure selects from dbo.segment while the modern
/// BasSegment (bassegment) entity hydrates; the two can diverge, so membership
/// comes from the procedure.
/// </summary>
public sealed class BasSegmentLookupOverlay
{
    private readonly FisDbContext _context;

    public BasSegmentLookupOverlay(FisDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<string>?> GetOrderedSegmentNumbersAsync(
        int departmentCode,
        byte segmentTypeCode
    )
    {
        var rows = await LegacySelectorProcedure.TryReadRowsAsync(
            _context,
            "DEV_SEL_BASSegments",
            [["@FilterID", "@SegmentTypeCode"]],
            actual =>
                command =>
                {
                    AddParameter(command, actual[0], DbType.Int32, departmentCode);
                    AddParameter(command, actual[1], DbType.Byte, segmentTypeCode);
                },
            "dbo"
        );
        if (rows is null)
        {
            return null;
        }

        if (rows.Count == 0)
        {
            return [];
        }

        var keys = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var key = LegacySelectorProcedure.ReadString(
                row,
                "Segment_Number",
                "segment_number",
                "SegmentNumber"
            );
            if (string.IsNullOrWhiteSpace(key) || !seen.Add(key))
            {
                continue;
            }

            keys.Add(key);
        }

        return keys.Count == 0 ? null : keys;
    }

    public static IReadOnlyList<T> OrderByKeys<T>(
        IReadOnlyList<T> leftover,
        IReadOnlyList<string> keys,
        Func<T, string?> keySelector
    ) => LegacySelectorProcedure.OrderByKeys(leftover, keys, keySelector);

    private static void AddParameter(DbCommand command, string name, DbType type, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
