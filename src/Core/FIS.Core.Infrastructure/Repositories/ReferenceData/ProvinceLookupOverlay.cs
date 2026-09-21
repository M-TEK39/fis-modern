using FIS.Data.SqlServer;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Province capture lookups from Finance/GetFinancialReportsProvince.aspx:117,
/// FISReports/SelectSearchParameters_Dept_Prov_DateRange.aspx.vb:14-24 and
/// AReports/WesbankUniversalReport.aspx.vb:36. dev_sel_provinces takes no
/// parameters and returns province_code/province_name.
/// </summary>
public sealed class ProvinceLookupOverlay
{
    private readonly FisDbContext _context;

    public ProvinceLookupOverlay(FisDbContext context)
    {
        _context = context;
    }

    public Task<IReadOnlyList<int>?> GetOrderedProvinceCodesAsync() =>
        LegacySelectorProcedure.TryReadOrderedKeysAsync(
            _context,
            "dev_sel_provinces",
            [],
            null,
            "province_code",
            "ProvinceCode",
            "Province Code"
        );
}
