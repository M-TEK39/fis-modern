using System.Data;
using System.Data.Common;
using FIS.Core.Application.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Services.Finance;

/// <summary>
/// Executes the fixed set of Finance report procedures used by the legacy
/// Finance menu.  The procedure key is deliberately allowlisted: browser
/// input can select report values, but can never select a database object.
/// </summary>
public sealed class LegacyFinanceReportExecutionService
{
    private static readonly IReadOnlyDictionary<string, string> ProcedureNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["profitability"] = "DEV_REP_IncomeVsExpensesVIPPool",
            ["outstanding-department-site-vehicle"] = "DEV_REP_ExpenditureToDatePerDepartment",
            ["outstanding-department"] = "DEV_REP_ALLOutstandingAmountsPerDepartment",
            ["outstanding-department-site"] = "DEV_REP_ALLOutstandingAmountsPerDepartmentAndSite",
            ["outstanding-month-end-vehicle"] = "DEV_REP_ALLOutstandingAmountsAtMonthEndPerVehicle",
            ["outstanding-allocation-exception"] = "DEV_REP_AllocationException",
            ["wesbank-summary-all"] = "DEV_REP_WesbankExpensesPerProvinceAndMonth",
            ["wesbank-department-summary"] = "DEV_REP_WesbankExpensesPerProvincePerDepartmentAndMonth",
            ["wesbank-site-summary"] = "DEV_REP_WesbankExpensesPerProvincePerDepartmentSiteAndMonth",
            ["wesbank-summary-province"] = "DEV_REP_WesbankExpensesOneProvinceAndAllMonths",
            ["wesbank-department-province-summary"] = "DEV_REP_WesbankExpensesOneProvinceAllDepartmentSubTotalAndMonth",
            ["wesbank-site-province-summary"] = "DEV_REP_WesbankExpensesOneProvinceAllDepartmentSiteAndMonth",
            ["wesbank-detailed-fuel"] = "DEV_REP_WesbankExpensesPerProvinceAllDepartmentSiteAndMonthDetailedFuel",
            ["wesbank-detailed-other"] = "DEV_REP_WesbankExpensesPerProvinceAllDepartmentSiteAndMonthDetailedOther",
            ["wesbank-detailed-fuel-province"] = "DEV_REP_WesbankExpensesOneProvinceAllDepartmentSiteAndMonthDetailedFuel",
            ["wesbank-detailed-other-province"] = "DEV_REP_WesbankExpensesOneProvinceAllDepartmentSiteAndMonthDetailedOther",
            ["regional-summary"] = "DEV_REP_SummaryReport",
            ["regional-department-cost-type"] = "DEV_REP_SummaryReportDeptCostType",
            ["regional-site-cost-type"] = "DEV_REP_SummaryReportByCostType",
            ["regional-summary-province"] = "DEV_REP_SummaryReportPerProvince",
            ["regional-department-cost-type-province"] = "DEV_REP_SummaryReportPerProvinceDeptCostType",
            ["regional-site-cost-type-province"] = "DEV_REP_SummaryReportPerProvinceByCostType",
            ["audit-els"] = "DEV_REP_ELSAuditTrailReport",
            ["audit-manual-kilos"] = "DEV_REP_ManualKilosAuditTrailReport",
            ["audit-contracts"] = "DEV_REP_VehicleContractsAuditTrailReport",
            ["audit-vip-taxi"] = "DEV_REP_VIPTAXIAuditTrailReport",
            // These reports are drill-down pages, rather than menu reports. The
            // legacy pages build their procedure name from a fixed Item value;
            // retain each verified value explicitly so query input cannot select
            // an arbitrary database object.
            ["wesbank-site-vehicle-detail"] = "DEV_REP_SiteVehicleDetail",
            ["wesbank-registration-number-detail"] = "DEV_REP_RegistrationNumberDetail",
            ["wesbank-vehicle-detail"] = "DEV_REP_VehicleDetail",
            ["regional-total-cost-province"] = "DEV_REP_TotalCostPerProvince",
            ["regional-total-cost-province-department"] = "DEV_REP_TotalCostPerProvincePerDepartment",
            ["regional-total-cost-province-department-site"] = "DEV_REP_TotalCostPerProvincePerDepartmentPerSite",
            ["regional-total-cost-province-department-site-cost-type"] = "DEV_REP_TotalCostPerProvincePerDepartmentPerSitePerCostType",
            ["journal-detailed-invoice"] = "DEV_REP_DetailedInvoiceFromJournalNumber",
            ["trip-routes-over-25000"] = "DEV_REP_AllRoutesOver25000KM",
            ["trip-day-routes-over-3500"] = "DEV_REP_AllDayTripsOver3500KM",
            // These three report types are opened from the Reports menu via
            // Finance/OpenReport.aspx. They are not Finance-menu reports, but
            // their Web Forms host is not an authorization boundary either.
            // Keep their procedure names explicit and let the report endpoint
            // enforce the role of the originating legacy menu.
            ["trip-number-interval"] = "DEV_REP_NumberOfTripsOnMontInterval",
            ["els-log"] = "DEV_REP_ELSLogReport",
            ["unallocated-vehicles"] = "DEV_REP_VehiclesNoContractCurrentAndFuelTransactions",
            ["vehicle-status"] = "DEV_REP_VehicleStatusReport",
            ["missing-fuel-consumption"] = "DEV_REP_CompareBilledKilosAndFuelConsumption",
            ["missing-no-kilos-consuming-fuel"] = "DEV_REP_AllVehiclesWithNoKilosButConsumingFuel",
            ["reversals-tree"] = "DEV_REP_ViewReversalTreeFromJournalNumber",
            ["vehicle-billing-history"] = "DEV_REP_VehicleBillingHistory",
            ["invoice-summary"] = "DEV_REP_DetailedInvoicedReport",
            ["invoice-by-cost-type"] = "DEV_REP_SummaryInvoiceByJournalDetailType",
            ["invoice-detailed"] = "DEV_REP_DetailedInvoicedReport",
            ["invoice-vip-taxi"] = "DEV_REP_DetailedInvoicedVIPandTaxiReport",
            ["invoice-fuel"] = "DEV_REP_FuelDetailedInvoicedReport",
            ["invoice-toll-oil"] = "DEV_REP_DetailedInvoicedTollAndOil",
            ["invoice-surcharge"] = "DEV_REP_SurchargeDetailedInvoicedReport",
            ["pastel-csv"] = "DEV_REP_ExportPastelCSV",
            ["pastel-csv-customer"] = "DEV_REP_ExportPastelCSVWithClientName",
            ["income-by-department"] = "DEV_REP_InvoicedAmountsPerMonth",
            ["income-by-department-site"] = "DEV_REP_InvoicedAmountsPerMonthPerSite",
            ["income-by-department-site-vehicle"] = "DEV_REP_InvoicedAmountsPerMonthPerSitePerVehicle",
            ["income-split"] = "DEV_REP_PreviousYearsIncomeSplit",
            ["invalid-bas-journals"] = "DEV_REP_JournalsWithInvalidBASCodes",
            ["fund-code-allocation"] = "DEV_REP_VehicleJournalBASCodesMap",
            ["fix-invalid-bas"] = "DEV_UPD_FixJournalWithInvalidBASCodes",
            ["assign-fund-bas"] = "DEV_INS_VehicleJournalSegmentMap",
        };

    private readonly FisDbContext _context;
    private readonly ILogger<LegacyFinanceReportExecutionService> _logger;

    public LegacyFinanceReportExecutionService(
        FisDbContext context,
        ILogger<LegacyFinanceReportExecutionService> logger
    )
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Returns null only when the selected legacy procedure does not exist.
    /// A present procedure with a different required contract is a deployment
    /// mismatch, not permission to substitute an approximation.
    /// </summary>
    public async Task<UniversalReport?> TryExecuteAsync(
        LegacyFinanceProcedureRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (!ProcedureNames.TryGetValue(request.ProcedureKey, out var procedureName))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.ProcedureKey,
                "The Finance procedure is not allowlisted."
            );
        }

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var procedureParameters = await GetProcedureParametersAsync(
                connection,
                procedureName,
                cancellationToken
            );
            if (procedureParameters is null)
            {
                return null;
            }

            var providedNames = request.Parameters
                .Where(parameter => parameter.Value is not null)
                .Select(parameter =>
                    ResolveProcedureParameterName(
                        request.ProcedureKey,
                        procedureParameters,
                        parameter.Key
                    )
                    ?? parameter.Key
                )
                .Select(parameter => parameter.TrimStart('@'))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missingRequired = procedureParameters
                .Where(parameter => !parameter.HasDefaultValue)
                .Where(parameter => !providedNames.Contains(parameter.Name.TrimStart('@')))
                .Select(parameter => parameter.Name)
                .ToArray();
            if (missingRequired.Length > 0)
            {
                _logger.LogWarning(
                    "Legacy Finance procedure {ProcedureName} has unsupported required parameters {Parameters}.",
                    procedureName,
                    string.Join(", ", missingRequired)
                );
                throw new LegacyFinanceProcedureContractException(procedureName, missingRequired);
            }

            await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // ProcedureNames is a private fixed allowlist, never request input.
            command.CommandText = $"[dbo].[{procedureName}]";
#pragma warning restore CA2100
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 180;
            foreach (var parameter in request.Parameters)
            {
                if (parameter.Value is null)
                {
                    continue;
                }

                var normalizedName = ResolveProcedureParameterName(
                    request.ProcedureKey,
                    procedureParameters,
                    parameter.Key
                );
                if (normalizedName is null)
                {
                    continue;
                }

                AddParameter(command, normalizedName, parameter.Value);
            }

            var rows = new List<Dictionary<string, object>>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var columns = GetColumnNames(reader);
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                for (var index = 0; index < reader.FieldCount; index++)
                {
                    row[columns[index]] = reader.IsDBNull(index) ? string.Empty : reader.GetValue(index);
                }

                rows.Add(row);
            }

            return new UniversalReport
            {
                ReportType = "financial",
                Title = request.Title,
                GeneratedDate = DateTime.UtcNow,
                DataRows = rows,
                ReportData = request.Parameters
                    .Where(parameter => parameter.Value is not null)
                    .ToDictionary(parameter => parameter.Key, parameter => parameter.Value!),
                Summary = new Dictionary<string, object>
                {
                    ["Source"] = "legacy-procedure",
                    ["Procedure"] = procedureName,
                    ["Total_Rows"] = rows.Count,
                },
                SupportsDateFilter = false,
            };
        }
        catch (SqlException ex) when (ex.Number == 2812)
        {
            _logger.LogInformation(
                "Legacy Finance procedure {ProcedureName} became unavailable.",
                procedureName
            );
            return null;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    /// <summary>
    /// Executes the two FinanceMain.aspx BAS correction operations through
    /// their legacy procedures. A false result means the connected database
    /// does not expose a compatible procedure; callers must not substitute a
    /// guessed write against the legacy schema.
    /// </summary>
    public async Task<bool> TryExecuteMutationAsync(
        string procedureKey,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default
    )
    {
        if (!ProcedureNames.TryGetValue(procedureKey, out var procedureName))
        {
            throw new ArgumentOutOfRangeException(
                nameof(procedureKey),
                procedureKey,
                "The Finance procedure is not allowlisted."
            );
        }

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var procedureParameters = await GetProcedureParametersAsync(
                connection,
                procedureName,
                cancellationToken
            );
            if (procedureParameters is null)
            {
                return false;
            }

            var providedNames = parameters
                .Where(parameter => parameter.Value is not null)
                .Select(parameter => parameter.Key.TrimStart('@'))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (procedureParameters.Any(parameter =>
                    !parameter.HasDefaultValue
                    && !providedNames.Contains(parameter.Name.TrimStart('@'))
                ))
            {
                _logger.LogWarning(
                    "Legacy Finance mutation {ProcedureName} has unsupported required parameters.",
                    procedureName
                );
                return false;
            }

            await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // ProcedureNames is a private fixed allowlist, never request input.
            command.CommandText = $"[dbo].[{procedureName}]";
#pragma warning restore CA2100
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 180;
            foreach (var parameter in parameters)
            {
                if (parameter.Value is null)
                {
                    continue;
                }

                var normalizedName = parameter.Key.StartsWith('@')
                    ? parameter.Key
                    : $"@{parameter.Key}";
                if (procedureParameters.Any(item =>
                        string.Equals(item.Name, normalizedName, StringComparison.OrdinalIgnoreCase)
                    ))
                {
                    AddParameter(command, normalizedName, parameter.Value);
                }
            }

            await command.ExecuteNonQueryAsync(cancellationToken);
            return true;
        }
        catch (SqlException ex) when (ex.Number == 2812)
        {
            _logger.LogInformation(
                "Legacy Finance mutation procedure {ProcedureName} is unavailable.",
                procedureName
            );
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

    /// <summary>
    /// Reads the financial-year picker used by the legacy Finance selector.
    /// The legacy application called DEV_SEL_FinancialYears rather than
    /// shipping a fixed list of years. Use that procedure when it exists,
    /// then retain a narrowly-scoped table fallback for installations where
    /// the report procedures were not migrated with the application data.
    /// </summary>
    public async Task<IReadOnlyList<LegacyFinanceFinancialYear>> GetFinancialYearsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            if (await ProcedureExistsAsync(connection, "DEV_SEL_FinancialYears", cancellationToken))
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "[dbo].[DEV_SEL_FinancialYears]";
                command.CommandType = CommandType.StoredProcedure;
                var procedureYears = await ReadFinancialYearsAsync(command, cancellationToken);
                if (procedureYears.Count > 0)
                {
                    return procedureYears;
                }
            }

            var columns = await GetTableColumnsAsync(connection, "financial_year", cancellationToken);
            if (!columns.Contains("financial_year_code"))
            {
                return [];
            }

            var displayColumn = columns.Contains("financial_year")
                ? "[financial_year]"
                : "CONVERT(varchar(20), [financial_year_code])";
            var startDateColumn = columns.Contains("start_date") ? "[start_date]" : "NULL";
            var endDateColumn = columns.Contains("end_date") ? "[end_date]" : "NULL";
            var activeFilter = columns.Contains("is_deleted")
                ? "WHERE ISNULL([is_deleted], 0) = 0"
                : string.Empty;
            await using var compatibilityCommand = connection.CreateCommand();
            compatibilityCommand.CommandText = $"""
                SELECT
                    [financial_year_code] AS [FinancialYear],
                    {displayColumn} AS [FinancialYearString],
                    {startDateColumn} AS [StartDate],
                    {endDateColumn} AS [EndDate]
                FROM [dbo].[financial_year]
                {activeFilter}
                ORDER BY [financial_year_code] DESC
                """;
            return await ReadFinancialYearsAsync(compatibilityCommand, cancellationToken);
        }
        catch (SqlException ex) when (ex.Number is 207 or 208 or 2812)
        {
            _logger.LogInformation(
                ex,
                "Legacy Finance financial-year lookup is unavailable on this schema."
            );
            return [];
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<IReadOnlyList<ProcedureParameter>?> GetProcedureParametersAsync(
        DbConnection connection,
        string procedureName,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT [name], [has_default_value]
            FROM [sys].[parameters]
            WHERE [object_id] = OBJECT_ID(@procedureName, 'P')
              AND [parameter_id] > 0
              AND [is_output] = 0
            ORDER BY [parameter_id]
            """;
        AddParameter(command, "@procedureName", $"dbo.{procedureName}");

        var parameters = new List<ProcedureParameter>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0))
                {
                    parameters.Add(
                        new ProcedureParameter(
                            reader.GetString(0),
                            !reader.IsDBNull(1) && reader.GetBoolean(1)
                        )
                    );
                }
            }
        }

        // An empty list has two meanings: no input parameters, or a missing
        // procedure. Resolve the latter explicitly without relying on errors.
        await using var existsCommand = connection.CreateCommand();
        existsCommand.CommandText = "SELECT OBJECT_ID(@procedureName, 'P');";
        AddParameter(existsCommand, "@procedureName", $"dbo.{procedureName}");
        return await existsCommand.ExecuteScalarAsync(cancellationToken) is null or DBNull
            ? null
            : parameters;
    }

    private static string? ResolveProcedureParameterName(
        string procedureKey,
        IReadOnlyList<ProcedureParameter> procedureParameters,
        string requestedName
    )
    {
        var normalizedRequestedName = requestedName.StartsWith('@')
            ? requestedName
            : $"@{requestedName}";
        var exact = procedureParameters.FirstOrDefault(parameter =>
            string.Equals(parameter.Name, normalizedRequestedName, StringComparison.OrdinalIgnoreCase)
        );
        if (exact is not null)
        {
            return exact.Name;
        }

        // The archived cost-type ActiveReport invokes the procedure
        // positionally as FilterBy, BatchDate, SiteOrDeptCode, while the
        // database script names the third parameter @id. Named binding lets
        // the modern API support either deployed contract without guessing a
        // result shape or calling a different procedure.
        if (
            string.Equals(procedureKey, "invoice-by-cost-type", StringComparison.OrdinalIgnoreCase)
            && string.Equals(normalizedRequestedName, "@ID", StringComparison.OrdinalIgnoreCase)
        )
        {
            return procedureParameters
                .Select(parameter => parameter.Name)
                .FirstOrDefault(name =>
                    string.Equals(name, "@SiteOrDeptCode", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "@SiteOrDepartmentCode", StringComparison.OrdinalIgnoreCase)
                );
        }

        return null;
    }

    private static async Task<bool> ProcedureExistsAsync(
        DbConnection connection,
        string procedureName,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT OBJECT_ID(@procedureName, 'P');";
        AddParameter(command, "@procedureName", $"dbo.{procedureName}");
        return await command.ExecuteScalarAsync(cancellationToken) is not null and not DBNull;
    }

    private static async Task<HashSet<string>> GetTableColumnsAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT [name]
            FROM [sys].[columns]
            WHERE [object_id] = OBJECT_ID(@tableName, 'U')
            """;
        AddParameter(command, "@tableName", $"dbo.{tableName}");
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                columns.Add(reader.GetString(0));
            }
        }

        return columns;
    }

    private static async Task<IReadOnlyList<LegacyFinanceFinancialYear>> ReadFinancialYearsAsync(
        DbCommand command,
        CancellationToken cancellationToken
    )
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columnOrdinals = Enumerable.Range(0, reader.FieldCount).ToDictionary(
            reader.GetName,
            index => index,
            StringComparer.OrdinalIgnoreCase
        );
        if (
            !TryGetOrdinal(columnOrdinals, out var codeOrdinal, "FinancialYear", "financial_year_code")
        )
        {
            return [];
        }

        TryGetOrdinal(columnOrdinals, out var nameOrdinal, "FinancialYearString", "financial_year");
        TryGetOrdinal(columnOrdinals, out var startDateOrdinal, "StartDate", "start_date");
        TryGetOrdinal(columnOrdinals, out var endDateOrdinal, "EndDate", "end_date");
        var years = new List<LegacyFinanceFinancialYear>();
        while (await reader.ReadAsync(cancellationToken))
        {
            if (
                reader.IsDBNull(codeOrdinal)
                || !short.TryParse(
                    Convert.ToString(
                        reader.GetValue(codeOrdinal),
                        System.Globalization.CultureInfo.InvariantCulture
                    ),
                    out var code
                )
            )
            {
                continue;
            }

            var name = nameOrdinal < 0 || reader.IsDBNull(nameOrdinal)
                ? code.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : Convert.ToString(
                    reader.GetValue(nameOrdinal),
                    System.Globalization.CultureInfo.InvariantCulture
                );
            years.Add(
                new LegacyFinanceFinancialYear(
                    code,
                    string.IsNullOrWhiteSpace(name)
                        ? code.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        : name,
                    ReadNullableDate(reader, startDateOrdinal),
                    ReadNullableDate(reader, endDateOrdinal)
                )
            );
        }

        return years;
    }

    private static bool TryGetOrdinal(
        IReadOnlyDictionary<string, int> ordinals,
        out int ordinal,
        params string[] names
    )
    {
        foreach (var name in names)
        {
            if (ordinals.TryGetValue(name, out ordinal))
            {
                return true;
            }
        }

        ordinal = -1;
        return false;
    }

    private static DateTime? ReadNullableDate(DbDataReader reader, int ordinal) =>
        ordinal < 0 || reader.IsDBNull(ordinal)
            ? null
            : reader.GetValue(ordinal) is DateTime value
                ? value
                : DateTime.TryParse(
                    Convert.ToString(
                        reader.GetValue(ordinal),
                        System.Globalization.CultureInfo.InvariantCulture
                    ),
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var parsed
                )
                    ? parsed
                    : null;

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = value switch
        {
            DateTime => DbType.DateTime,
            bool => DbType.Boolean,
            byte => DbType.Byte,
            short => DbType.Int16,
            int => DbType.Int32,
            long => DbType.Int64,
            decimal => DbType.Decimal,
            double => DbType.Double,
            _ => DbType.String,
        };
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string[] GetColumnNames(DbDataReader reader)
    {
        var names = new string[reader.FieldCount];
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < reader.FieldCount; index++)
        {
            var original = reader.GetName(index);
            var name = string.IsNullOrWhiteSpace(original) ? $"column_{index + 1}" : original;
            var uniqueName = name;
            var duplicate = 2;
            while (!used.Add(uniqueName))
            {
                uniqueName = $"{name}_{duplicate++}";
            }

            names[index] = uniqueName;
        }

        return names;
    }

    private sealed record ProcedureParameter(string Name, bool HasDefaultValue);
}

public sealed record LegacyFinanceFinancialYear(
    short Code,
    string Name,
    DateTime? StartDate,
    DateTime? EndDate
);

public sealed record LegacyFinanceProcedureRequest(
    string ProcedureKey,
    string Title,
    IReadOnlyDictionary<string, object?> Parameters
);

public sealed class LegacyFinanceProcedureContractException : Exception
{
    public LegacyFinanceProcedureContractException(
        string procedureName,
        IReadOnlyCollection<string> missingRequiredParameters
    )
        : base($"Legacy Finance procedure {procedureName} has an incompatible parameter contract.")
    {
        ProcedureName = procedureName;
        MissingRequiredParameters = missingRequiredParameters;
    }

    public string ProcedureName { get; }

    public IReadOnlyCollection<string> MissingRequiredParameters { get; }
}

public sealed class LegacyFinanceProcedureUnavailableException : InvalidOperationException
{
    public LegacyFinanceProcedureUnavailableException(string procedureName, string? reportTitle = null)
        : base(
            string.IsNullOrWhiteSpace(reportTitle)
                ? $"Legacy Finance procedure {procedureName} is unavailable on this database."
                : $"Legacy Finance procedure {procedureName} for {reportTitle} is unavailable on this database."
        )
    {
        ProcedureName = procedureName;
    }

    public string ProcedureName { get; }
}
