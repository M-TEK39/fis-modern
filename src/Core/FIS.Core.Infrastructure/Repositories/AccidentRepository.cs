using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Persists accident records against both the original client table and the
/// expanded schema. The legacy accident fields are negotiated at runtime so
/// an unexpanded client database remains usable, while modern claim/audit
/// fields are used when they exist.
/// </summary>
public sealed class AccidentRepository : IAccidentRepository
{
    private const string TableName = "accident";
    private const string VehicleTableName = "vehicle_master";

    private static readonly string[] RequiredColumns =
    [
        "accident_code",
        "vmf_code"
    ];

    private static readonly string[] LegacyColumns =
    [
        "accident_code",
        "vmf_code",
        "Call_Refer",
        "captured_person",
        "fin_year",
        "garage",
        "description",
        "driver_name",
        "driver_employ_number",
        "driver_telno",
        "driver_site_code",
        "transoffic_name",
        "transoffic_tel",
        "occurence_date",
        "occurence_time",
        "reported_date",
        "accident_km",
        "acc_type_code",
        "Flag_gg_hq",
        "Flag_gg_hq_date",
        "file_close_date",
        "gg_reference",
        "case_number",
        "reporting_authority",
        "cost_of_repair",
        "damage_description",
        "death",
        "injured",
        "third_party_regno",
        "third_party_owner",
        "third_party_tel",
        "third_party_claim",
        "SecondThirdPartyRegNo",
        "th_claim_receive",
        "claim_against_dept",
        "letterhead",
        "z181",
        "part3",
        "statement",
        "sketch",
        "iddoc",
        "drivelic",
        "docs_acc_relieve",
        "flag_case_num",
        "trip_author",
        "Flag_trip_author",
        "flag_trip_auth_date",
        "driver_fault",
        "attorney_insure",
        "insurance_claim",
        "priv_dampay_date",
        "th_claim_accept_reject",
        "th_claim_reject_reason",
        "write_off_amount",
        "write_off_date",
        "occurence_place",
        "notes",
        "date_updated",
        "Tow_need"
    ];

    private static readonly string[] ModernColumns =
    [
        "posting_month_code",
        "hq_reference",
        "sa_reference",
        "claim_amount",
        "excess_amount",
        "date_created",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted"
    ];

    private readonly FisDbContext _context;

    public AccidentRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Accident?> GetByIdAsync(int accidentCode)
        => (await QueryAsync(
            "[a].[accident_code] = @accidentCode",
            command => AddParameter(command, "@accidentCode", DbType.Int32, accidentCode)))
            .SingleOrDefault();

    public async Task<IEnumerable<Accident>> GetAllAsync()
        => await QueryAsync();

    public async Task<IEnumerable<Accident>> GetByVehicleAsync(int vmfCode)
        => await QueryAsync(
            "[a].[vmf_code] = @vmfCode",
            command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode));

    public async Task<IEnumerable<Accident>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
        => await QueryAsync(
            "[a].[occurence_date] >= @startDate AND [a].[occurence_date] <= @endDate",
            command =>
            {
                AddParameter(command, "@startDate", DbType.Date, startDate.Date);
                AddParameter(command, "@endDate", DbType.Date, endDate.Date);
            });

    public async Task<AccidentClaimsSummary> GetClaimsSummaryAsync()
    {
        var accidents = (await GetAllAsync()).ToList();
        var totalClaims = accidents.Sum(accident => accident.claim_amount ?? 0);
        var openClaims = accidents.Count(accident => accident.claim_amount > 0);
        var closedClaims = accidents.Count(accident => accident.claim_amount == 0 || !accident.claim_amount.HasValue);
        var pendingAmount = accidents
            .Where(accident => accident.claim_amount > 0)
            .Sum(accident => accident.claim_amount ?? 0);

        return new AccidentClaimsSummary
        {
            total_accidents = accidents.Count,
            total_claims_value = totalClaims,
            open_claims = openClaims,
            closed_claims = closedClaims,
            pending_claim_amount = pendingAmount
        };
    }

    public async Task<IEnumerable<AccidentReport>> GetRecentReportsAsync(int limit = 10)
        => (await GetAllAsync())
            .OrderByDescending(accident => accident.occurence_date)
            .ThenByDescending(accident => accident.accident_code)
            .Take(limit)
            .Select(accident => new AccidentReport
            {
                accident_code = accident.accident_code,
                accident_reference = accident.gg_reference ?? accident.hq_reference ?? string.Empty,
                accident_date = accident.occurence_date ?? DateTime.MinValue,
                vehicle_registration = accident.Vehicle?.fleet_number ?? string.Empty,
                severity = "Unknown",
                status = (accident.claim_amount ?? 0) > 0 ? "Open" : "Closed"
            });

    public async Task<IEnumerable<AccidentOutstandingClaim>> GetOutstandingClaimsAsync()
    {
        var today = DateTime.UtcNow;
        return (await GetAllAsync())
            .Where(accident => (accident.claim_amount ?? 0) > 0)
            .Select(accident => new AccidentOutstandingClaim
            {
                accident_id = accident.accident_code,
                accident_reference = accident.gg_reference ?? accident.hq_reference ?? string.Empty,
                vehicle_registration = accident.Vehicle?.fleet_number ?? string.Empty,
                accident_date = accident.occurence_date ?? DateTime.MinValue,
                claim_type = "Claim",
                claim_amount = accident.claim_amount,
                status = "Outstanding",
                days_outstanding = (int)(today - (accident.occurence_date ?? DateTime.MinValue)).TotalDays
            });
    }

    public async Task<AccidentStatistics> GetStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var accidents = (await GetAllAsync()).AsEnumerable();
        if (fromDate.HasValue)
        {
            accidents = accidents.Where(accident => accident.occurence_date >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            accidents = accidents.Where(accident => accident.occurence_date <= toDate.Value);
        }

        var accidentList = accidents.ToList();
        var totalClaims = accidentList.Sum(accident => accident.claim_amount ?? 0);
        var thisMonth = DateTime.UtcNow.Month;
        var thisYear = DateTime.UtcNow.Year;
        var accidentsThisMonth = accidentList.Count(accident =>
            accident.occurence_date?.Month == thisMonth && accident.occurence_date?.Year == thisYear);
        var accidentsThisYear = accidentList.Count(accident => accident.occurence_date?.Year == thisYear);
        var averageCost = accidentList.Count > 0 ? totalClaims / accidentList.Count : 0;

        return new AccidentStatistics
        {
            total_accidents = accidentList.Count,
            total_repair_costs = accidentList.Sum(accident => accident.cost_of_repair ?? 0),
            total_third_party_claims = accidentList.Sum(accident => accident.third_party_claim ?? 0),
            total_department_claims = accidentList.Sum(accident => accident.claim_against_dept ?? 0),
            accidents_this_month = accidentsThisMonth,
            accidents_this_year = accidentsThisYear,
            average_cost_per_accident = averageCost,
            most_common_severity = "Unknown",
            department_breakdown = new List<DepartmentAccidentSummary>(),
            monthly_breakdown = new List<MonthlySummary>()
        };
    }

    public async Task<Accident> CreateAsync(Accident accident, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(accident);

        var availableColumns = await GetAvailableColumnsAsync(TableName, RequiredColumns);
        var now = DateTime.UtcNow;
        var values = BuildWriteValues(accident, availableColumns).ToList();
        AddOptionalValue(values, availableColumns, "date_created", "@dateCreated", DbType.DateTime2, now);
        AddOptionalValue(
            values,
            availableColumns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null);
        AddOptionalValue(values, availableColumns, "is_deleted", "@isDeleted", DbType.Boolean, false);

        accident.accident_code = await ExecuteInsertAsync(values);
        accident.date_created = now;
        accident.created_by_user_code = currentUserId > 0 ? currentUserId : null;
        accident.modified_by_user_code = null;
        accident.is_deleted = false;
        return accident;
    }

    public async Task<Accident> UpdateAsync(Accident accident, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(accident);

        var existing = await GetByIdAsync(accident.accident_code)
            ?? throw new InvalidOperationException(
                $"Accident with accident_code {accident.accident_code} not found");
        var availableColumns = await GetAvailableColumnsAsync(TableName, RequiredColumns);
        var now = DateTime.UtcNow;

        // These references are assigned by the legacy workflow and are not
        // editable on the original garage screen.
        accident.vmf_code = existing.vmf_code;
        accident.gg_reference = existing.gg_reference;
        accident.hq_reference = existing.hq_reference;
        accident.sa_reference = existing.sa_reference;
        accident.date_created = existing.date_created;
        accident.created_by_user_code = existing.created_by_user_code;
        accident.is_deleted = existing.is_deleted;

        var values = BuildWriteValues(accident, availableColumns)
            .Where(value => !string.Equals(value.Column, "accident_code", StringComparison.OrdinalIgnoreCase))
            .ToList();
        AddOptionalValue(values, availableColumns, "date_updated", "@dateUpdated", DbType.DateTime2, now);
        AddOptionalValue(
            values,
            availableColumns,
            "modified_by_user_code",
            "@modifiedByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : existing.modified_by_user_code);
        AddOptionalValue(values, availableColumns, "is_deleted", "@isDeleted", DbType.Boolean, existing.is_deleted);

        await ExecuteUpdateAsync(accident.accident_code, values, availableColumns);
        accident.date_updated = now;
        accident.modified_by_user_code = currentUserId > 0 ? currentUserId : existing.modified_by_user_code;
        return accident;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "DeleteAsync selects between fixed compatibility statements and uses a parameter for the accident identifier.")]
    public async Task DeleteAsync(int accidentCode, int currentUserId)
    {
        var availableColumns = await GetAvailableColumnsAsync(TableName, RequiredColumns);
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
            if (availableColumns.Contains("is_deleted"))
            {
                var assignments = new List<string> { "[is_deleted] = @isDeleted" };
                AddParameter(command, "@isDeleted", DbType.Boolean, true);

                if (availableColumns.Contains("date_updated"))
                {
                    assignments.Add("[date_updated] = @dateUpdated");
                    AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
                }

                if (availableColumns.Contains("modified_by_user_code"))
                {
                    assignments.Add("[modified_by_user_code] = @modifiedByUserCode");
                    AddParameter(
                        command,
                        "@modifiedByUserCode",
                        DbType.Int32,
                        currentUserId > 0 ? currentUserId : null);
                }

                command.CommandText = $"""
                    UPDATE [dbo].[{TableName}]
                    SET {string.Join(", ", assignments)}
                    WHERE [accident_code] = @accidentCode
                    AND {GetActiveFilter(availableColumns, "")}
                    """;
            }
            else
            {
                command.CommandText = $"""
                    DELETE FROM [dbo].[{TableName}]
                    WHERE [accident_code] = @accidentCode
                    """;
            }

            AddParameter(command, "@accidentCode", DbType.Int32, accidentCode);
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

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SELECT list and filters are composed only from fixed legacy columns and allowlisted optional columns; values are parameters.")]
    private async Task<List<Accident>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null)
    {
        var availableColumns = await GetAvailableColumnsAsync(TableName, RequiredColumns);
        var vehicleColumns = await GetAvailableColumnsAsync(VehicleTableName);
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
            var vehicleJoinAvailable = vehicleColumns.Contains("vmf_code");
            var projection = LegacyColumns
                .Concat(ModernColumns)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(column => GetProjection(availableColumns, column, "a"))
                .Concat(
                [
                    GetVehicleProjection(vehicleColumns, "vmf_code", vehicleJoinAvailable),
                    GetVehicleProjection(vehicleColumns, "fleet_number", vehicleJoinAvailable),
                    GetVehicleProjection(vehicleColumns, "registration_number", vehicleJoinAvailable),
                    GetVehicleProjection(vehicleColumns, "type_code", vehicleJoinAvailable)
                ])
                .ToArray();
            var conditions = new List<string> { GetActiveFilter(availableColumns, "a") };
            if (!string.IsNullOrWhiteSpace(predicate))
            {
                conditions.Add(predicate);
            }

            var vehicleJoin = vehicleJoinAvailable
                ? "LEFT JOIN [dbo].[vehicle_master] AS [v] ON [v].[vmf_code] = [a].[vmf_code]"
                : string.Empty;
            var orderExpression = availableColumns.Contains("occurence_date")
                ? "[a].[occurence_date]"
                : "[a].[accident_code]";

            command.CommandText = $"""
                SELECT {string.Join(", ", projection)}
                FROM [dbo].[{TableName}] AS [a]
                {vehicleJoin}
                WHERE {string.Join(" AND ", conditions)}
                ORDER BY {orderExpression} DESC, [a].[accident_code] DESC
                """;
            configure?.Invoke(command);

            var results = new List<Accident>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapAccident(reader));
            }

            return results;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The INSERT statement is composed from fixed legacy columns and allowlisted optional values; every value is parameterized.")]
    private async Task<int> ExecuteInsertAsync(IReadOnlyList<WriteValue> values)
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
            command.CommandText = $"""
                INSERT INTO [dbo].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))})
                OUTPUT INSERTED.[accident_code]
                VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
                """;
            AddParameters(command, values);
            return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The UPDATE statement is composed from fixed legacy columns and allowlisted optional values; every value is parameterized.")]
    private async Task ExecuteUpdateAsync(
        int accidentCode,
        IReadOnlyList<WriteValue> values,
        IReadOnlySet<string> availableColumns)
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
            command.CommandText = $"""
                UPDATE [dbo].[{TableName}]
                SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))}
                WHERE [accident_code] = @accidentCode
                AND {GetActiveFilter(availableColumns, "")}
                """;
            AddParameters(command, values);
            AddParameter(command, "@accidentCode", DbType.Int32, accidentCode);
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

    private async Task<HashSet<string>> GetAvailableColumnsAsync(
        string tableName,
        IReadOnlyCollection<string>? requiredColumns = null)
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
            {
                columns.Add(Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty);
            }

            if (requiredColumns is not null)
            {
                var missingColumns = requiredColumns.Where(column => !columns.Contains(column)).ToArray();
                if (missingColumns.Length > 0)
                {
                    throw new InvalidOperationException(
                        $"The required accident compatibility columns are not available: {string.Join(", ", missingColumns)}");
                }
            }

            return columns;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static Accident MapAccident(DbDataReader reader)
    {
        var accident = new Accident
        {
            accident_code = ReadInt32(reader, "accident_code") ?? 0,
            vmf_code = ReadInt32(reader, "vmf_code") ?? 0,
            posting_month_code = ReadInt16(reader, "posting_month_code"),
            Call_Refer = ReadDecimal(reader, "Call_Refer"),
            captured_person = ReadString(reader, "captured_person"),
            fin_year = ReadString(reader, "fin_year"),
            garage = ReadString(reader, "garage"),
            description = ReadString(reader, "description"),
            driver_name = ReadString(reader, "driver_name"),
            driver_employ_number = ReadString(reader, "driver_employ_number"),
            driver_telno = ReadString(reader, "driver_telno"),
            driver_site_code = ReadInt16(reader, "driver_site_code"),
            transoffic_name = ReadString(reader, "transoffic_name"),
            transoffic_tel = ReadString(reader, "transoffic_tel"),
            occurence_date = ReadDateTime(reader, "occurence_date"),
            occurence_time = ReadTime(reader, "occurence_time"),
            reported_date = ReadDateTime(reader, "reported_date"),
            accident_km = ReadDecimal(reader, "accident_km"),
            acc_type_code = ReadInt16(reader, "acc_type_code"),
            Flag_gg_hq = ReadString(reader, "Flag_gg_hq"),
            Flag_gg_hq_date = ReadDateTime(reader, "Flag_gg_hq_date"),
            file_close_date = ReadDateTime(reader, "file_close_date"),
            gg_reference = ReadString(reader, "gg_reference"),
            hq_reference = ReadString(reader, "hq_reference"),
            sa_reference = ReadString(reader, "sa_reference"),
            case_number = ReadString(reader, "case_number"),
            reporting_authority = ReadString(reader, "reporting_authority"),
            cost_of_repair = ReadDecimal(reader, "cost_of_repair"),
            damage_description = ReadString(reader, "damage_description"),
            death = ReadString(reader, "death"),
            injured = ReadString(reader, "injured"),
            third_party_regno = ReadString(reader, "third_party_regno"),
            third_party_owner = ReadString(reader, "third_party_owner"),
            third_party_tel = ReadString(reader, "third_party_tel"),
            third_party_claim = ReadDecimal(reader, "third_party_claim"),
            SecondThirdPartyRegNo = ReadString(reader, "SecondThirdPartyRegNo"),
            th_claim_receive = ReadString(reader, "th_claim_receive"),
            claim_against_dept = ReadDecimal(reader, "claim_against_dept"),
            letterhead = ReadString(reader, "letterhead"),
            z181 = ReadString(reader, "z181"),
            part3 = ReadString(reader, "part3"),
            statement = ReadString(reader, "statement"),
            sketch = ReadString(reader, "sketch"),
            iddoc = ReadString(reader, "iddoc"),
            drivelic = ReadString(reader, "drivelic"),
            docs_acc_relieve = ReadString(reader, "docs_acc_relieve"),
            flag_case_num = ReadString(reader, "flag_case_num"),
            trip_author = ReadString(reader, "trip_author") ?? ReadString(reader, "Flag_trip_author"),
            Flag_trip_author = ReadString(reader, "Flag_trip_author"),
            Flag_trip_auth_date = ReadDateTime(reader, "flag_trip_auth_date"),
            driver_fault = ReadString(reader, "driver_fault"),
            attorney_insure = ReadString(reader, "attorney_insure"),
            insurance_claim = ReadString(reader, "insurance_claim"),
            priv_dampay_date = ReadDateTime(reader, "priv_dampay_date"),
            th_claim_accept_reject = ReadString(reader, "th_claim_accept_reject"),
            th_claim_reject_reason = ReadString(reader, "th_claim_reject_reason"),
            write_off_amount = ReadDecimal(reader, "write_off_amount"),
            write_off_date = ReadDateTime(reader, "write_off_date"),
            occurence_place = ReadString(reader, "occurence_place"),
            notes = ReadString(reader, "notes"),
            date_created = ReadDateTime(reader, "date_created")
                ?? ReadDateTime(reader, "date_updated")
                ?? ReadDateTime(reader, "reported_date")
                ?? DateTime.MinValue,
            date_updated = ReadDateTime(reader, "date_updated"),
            created_by_user_code = ReadInt32(reader, "created_by_user_code"),
            modified_by_user_code = ReadInt32(reader, "modified_by_user_code"),
            is_deleted = ReadBoolean(reader, "is_deleted") ?? false
        };

        accident.claim_amount = ReadDecimal(reader, "claim_amount") ?? accident.claim_against_dept;
        accident.excess_amount = ReadDecimal(reader, "excess_amount");
        accident.Tow_need = ReadString(reader, "Tow_need");
        accident.Vehicle = MapVehicle(reader);
        return accident;
    }

    private static Vehicle? MapVehicle(DbDataReader reader)
    {
        var vmfCode = ReadInt32(reader, "__vehicle_vmf_code");
        if (!vmfCode.HasValue)
        {
            return null;
        }

        return new Vehicle
        {
            vmf_code = vmfCode.Value,
            fleet_number = ReadString(reader, "__vehicle_fleet_number"),
            registration_number = ReadString(reader, "__vehicle_registration_number"),
            type_code = ReadInt16(reader, "__vehicle_type_code") ?? 0
        };
    }

    private static IEnumerable<WriteValue> BuildWriteValues(
        Accident accident,
        IReadOnlySet<string> availableColumns)
    {
        var values = new List<WriteValue>
        {
            new("vmf_code", "@vmfCode", DbType.Int32, accident.vmf_code),
            new("Call_Refer", "@callRefer", DbType.Decimal, accident.Call_Refer),
            new("captured_person", "@capturedPerson", DbType.String, accident.captured_person),
            new("fin_year", "@finYear", DbType.String, accident.fin_year),
            new("garage", "@garage", DbType.String, accident.garage),
            new("description", "@description", DbType.String, accident.description),
            new("driver_name", "@driverName", DbType.String, accident.driver_name),
            new("driver_employ_number", "@driverEmployNumber", DbType.String, accident.driver_employ_number),
            new("driver_telno", "@driverTelno", DbType.String, accident.driver_telno),
            new("driver_site_code", "@driverSiteCode", DbType.Int16, accident.driver_site_code),
            new("transoffic_name", "@transofficName", DbType.String, accident.transoffic_name),
            new("transoffic_tel", "@transofficTel", DbType.String, accident.transoffic_tel),
            new("occurence_date", "@occurenceDate", DbType.Date, accident.occurence_date?.Date),
            new("occurence_time", "@occurenceTime", DbType.DateTime2, accident.occurence_time),
            new("reported_date", "@reportedDate", DbType.Date, accident.reported_date?.Date),
            new("accident_km", "@accidentKm", DbType.Decimal, accident.accident_km),
            new("acc_type_code", "@accidentTypeCode", DbType.Int16, accident.acc_type_code),
            new("Flag_gg_hq", "@flagGgHq", DbType.String, accident.Flag_gg_hq),
            new("Flag_gg_hq_date", "@flagGgHqDate", DbType.DateTime2, accident.Flag_gg_hq_date),
            new("file_close_date", "@fileCloseDate", DbType.Date, accident.file_close_date?.Date),
            new("gg_reference", "@ggReference", DbType.String, accident.gg_reference),
            new("hq_reference", "@hqReference", DbType.String, accident.hq_reference),
            new("sa_reference", "@saReference", DbType.String, accident.sa_reference),
            new("case_number", "@caseNumber", DbType.String, accident.case_number),
            new("reporting_authority", "@reportingAuthority", DbType.String, accident.reporting_authority),
            new("cost_of_repair", "@costOfRepair", DbType.Decimal, accident.cost_of_repair),
            new("damage_description", "@damageDescription", DbType.String, accident.damage_description),
            new("death", "@death", DbType.String, accident.death),
            new("injured", "@injured", DbType.String, accident.injured),
            new("third_party_regno", "@thirdPartyRegno", DbType.String, accident.third_party_regno),
            new("third_party_owner", "@thirdPartyOwner", DbType.String, accident.third_party_owner),
            new("third_party_tel", "@thirdPartyTel", DbType.String, accident.third_party_tel),
            new("third_party_claim", "@thirdPartyClaim", DbType.Decimal, accident.third_party_claim),
            new("SecondThirdPartyRegNo", "@secondThirdPartyRegno", DbType.String, accident.SecondThirdPartyRegNo),
            new("th_claim_receive", "@claimReceived", DbType.String, accident.th_claim_receive),
            new("letterhead", "@letterhead", DbType.String, accident.letterhead),
            new("z181", "@z181", DbType.String, accident.z181),
            new("part3", "@part3", DbType.String, accident.part3),
            new("statement", "@statement", DbType.String, accident.statement),
            new("sketch", "@sketch", DbType.String, accident.sketch),
            new("iddoc", "@iddoc", DbType.String, accident.iddoc),
            new("drivelic", "@drivelic", DbType.String, accident.drivelic),
            new("docs_acc_relieve", "@docsAccRelieve", DbType.String, accident.docs_acc_relieve),
            new("flag_case_num", "@flagCaseNum", DbType.String, accident.flag_case_num),
            new("trip_author", "@tripAuthor", DbType.String, accident.trip_author),
            new("Flag_trip_author", "@flagTripAuthor", DbType.String, accident.Flag_trip_author ?? accident.trip_author),
            new("flag_trip_auth_date", "@flagTripAuthDate", DbType.DateTime2, accident.Flag_trip_auth_date),
            new("driver_fault", "@driverFault", DbType.String, accident.driver_fault),
            new("attorney_insure", "@attorneyInsure", DbType.String, accident.attorney_insure),
            new("insurance_claim", "@insuranceClaim", DbType.String, accident.insurance_claim),
            new("priv_dampay_date", "@privateDamagePaymentDate", DbType.Date, accident.priv_dampay_date?.Date),
            new("th_claim_accept_reject", "@thirdPartyClaimDecision", DbType.String, accident.th_claim_accept_reject),
            new("th_claim_reject_reason", "@thirdPartyClaimRejectReason", DbType.String, accident.th_claim_reject_reason),
            new("write_off_amount", "@writeOffAmount", DbType.Decimal, accident.write_off_amount),
            new("write_off_date", "@writeOffDate", DbType.Date, accident.write_off_date?.Date),
            new("occurence_place", "@occurencePlace", DbType.String, accident.occurence_place),
            new("notes", "@notes", DbType.String, accident.notes),
            new("Tow_need", "@towNeed", DbType.String, accident.Tow_need)
        };

        var departmentClaim = accident.claim_against_dept ?? accident.claim_amount;
        values.Add(new WriteValue("claim_against_dept", "@claimAgainstDept", DbType.Decimal, departmentClaim));
        values.Add(new WriteValue("claim_amount", "@claimAmount", DbType.Decimal, accident.claim_amount ?? accident.claim_against_dept));

        return values.Where(value => availableColumns.Contains(value.Column));
    }

    private static void AddOptionalValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> availableColumns,
        string column,
        string parameter,
        DbType type,
        object? value)
    {
        if (availableColumns.Contains(column))
        {
            values.Add(new WriteValue(column, parameter, type, value));
        }
    }

    private static void AddParameters(DbCommand command, IEnumerable<WriteValue> values)
    {
        foreach (var value in values)
        {
            AddParameter(command, value.Parameter, value.Type, value.Value);
        }
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string GetProjection(IReadOnlySet<string> columns, string column, string alias)
        => columns.Contains(column)
            ? $"[{alias}].[{column}] AS [{column}]"
            : $"CAST(NULL AS {GetSqlType(column)}) AS [{column}]";

    private static string GetVehicleProjection(
        IReadOnlySet<string> columns,
        string column,
        bool joinAvailable)
        => joinAvailable && columns.Contains(column)
            ? $"[v].[{column}] AS [__vehicle_{column}]"
            : $"CAST(NULL AS {GetSqlType(column)}) AS [__vehicle_{column}]";

    private static string GetActiveFilter(IReadOnlySet<string> columns, string alias)
    {
        var prefix = string.IsNullOrWhiteSpace(alias) ? string.Empty : $"[{alias}].";
        return columns.Contains("is_deleted")
            ? $"ISNULL({prefix}[is_deleted], 0) = 0"
            : "1 = 1";
    }

    private static string GetSqlType(string column)
        => column switch
        {
            "accident_code" or "vmf_code" => "int",
            "posting_month_code" or "driver_site_code" or "acc_type_code" => "smallint",
            "Call_Refer" or "claim_amount" or "excess_amount" or "accident_km" or "cost_of_repair" or "third_party_claim" or "claim_against_dept" => "decimal(18, 2)",
            "occurence_date" or "reported_date" or "file_close_date" or "priv_dampay_date" or "write_off_date" => "date",
            "occurence_time" => "datetime2",
            "Flag_gg_hq_date" or "flag_trip_auth_date" or "date_created" or "date_updated" => "datetime2",
            "created_by_user_code" or "modified_by_user_code" => "int",
            "is_deleted" => "bit",
            "type_code" => "smallint",
            _ => "nvarchar(1)"
        };

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)?.TrimEnd();
    }

    private static int? ReadInt32(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static short? ReadInt16(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToInt16(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static decimal? ReadDecimal(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToDateTime(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static DateTime? ReadTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetValue(ordinal);
        if (value is TimeSpan time)
        {
            return DateTime.Today.Add(time);
        }

        return Convert.ToDateTime(value, CultureInfo.InvariantCulture);
    }

    private static bool? ReadBoolean(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToBoolean(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}
