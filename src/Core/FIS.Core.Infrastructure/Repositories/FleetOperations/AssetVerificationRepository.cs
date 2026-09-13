using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Reads and writes Asset_Verification without assuming that the expanded
/// schema has replaced the client-era columns. The legacy table is the source
/// of truth; modern columns are used when present and legacy equivalents are
/// retained when they are the only available representation.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers are selected from INFORMATION_SCHEMA and values are always parameterized."
)]
public sealed class AssetVerificationRepository : IAssetVerificationRepository
{
    private readonly FisDbContext _context;

    public AssetVerificationRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<AssetVerification?> GetByIdAsync(int verificationCode)
    {
        var records = await QueryAsync(
            "av.[asset_verification_code] = @verificationCode",
            [new ParameterValue("@verificationCode", DbType.Int32, verificationCode)]
        );
        return records.FirstOrDefault();
    }

    public Task<IEnumerable<AssetVerification>> GetAllAsync() => QueryAsEnumerableAsync();

    public async Task<AssetVerificationPage> GetPageAsync(AssetVerificationPageQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var requestedPage = Math.Max(1, query.Page);
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var columns = await GetTableColumnsAsync(connection);
            if (!columns.Contains("asset_verification_code"))
            {
                throw new InvalidOperationException(
                    "The required dbo.Asset_Verification table is not available."
                );
            }

            var activePredicate = BuildActivePredicate(columns);

            await using var countCommand = connection.CreateCommand();
            countCommand.CommandText = $"""
                SELECT COUNT(1)
                FROM [dbo].[Asset_Verification] AS av
                WHERE {activePredicate};
                """;
            var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            var page = Math.Min(requestedPage, totalPages);
            var skip = checked((long)(page - 1) * pageSize);

            await using var dataCommand = connection.CreateCommand();
            dataCommand.CommandText = $"""
                SELECT
                    {BuildSelectList(columns)}
                FROM [dbo].[Asset_Verification] AS av
                OUTER APPLY (
                    SELECT TOP (1)
                        v.[vmf_code], v.[fleet_number], v.[registration_number]
                    FROM [dbo].[vehicle_master] AS v
                    WHERE {BuildVehicleMatchPredicate(columns)}
                    ORDER BY v.[vmf_code]
                ) AS v
                WHERE {activePredicate}
                ORDER BY av.[asset_verification_code]
                OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY;
                """;
            AddParameter(dataCommand, "@skip", DbType.Int64, skip);
            AddParameter(dataCommand, "@pageSize", DbType.Int32, pageSize);

            var items = new List<AssetVerification>();
            await using var reader = await dataCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(Map(reader));
            }

            return new AssetVerificationPage(items, page, pageSize, total);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<AssetVerificationReportPage> GetReportPageAsync(
        AssetVerificationReportPageQuery query,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        var requestedPage = Math.Max(1, query.Page);
        var requestedPageSize = Math.Clamp(query.PageSize, 1, 100);
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var assetVerificationColumns = await GetTableColumnsAsync(
                connection,
                "Asset_Verification",
                cancellationToken
            );
            EnsureRequiredColumns(
                assetVerificationColumns,
                "Asset_Verification",
                "asset_verification_code"
            );

            var reportSql = query.ReportMode switch
            {
                AssetVerificationReportMode.PerSiteProvinceDate
                or AssetVerificationReportMode.VerifiedByDateRange =>
                    await BuildVerificationReportSqlAsync(
                        connection,
                        assetVerificationColumns,
                        query,
                        cancellationToken
                    ),
                AssetVerificationReportMode.NotVerified => await BuildNotVerifiedReportSqlAsync(
                    connection,
                    assetVerificationColumns,
                    cancellationToken
                ),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(query.ReportMode),
                    query.ReportMode,
                    "Unsupported asset verification report mode."
                ),
            };

            await using var countCommand = connection.CreateCommand();
            countCommand.CommandText = $"""
                SELECT COUNT(1)
                {reportSql.FromClause}
                WHERE {reportSql.WhereClause};
                """;
            AddParameters(countCommand, reportSql.Parameters);
            var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));

            var pageSize = query.IncludeAll ? Math.Max(1, total) : requestedPageSize;
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            var page = query.IncludeAll ? 1 : Math.Min(requestedPage, totalPages);
            var skip = checked((long)(page - 1) * pageSize);

            await using var dataCommand = connection.CreateCommand();
            dataCommand.CommandText = $"""
                SELECT
                    {reportSql.SelectList}
                {reportSql.FromClause}
                WHERE {reportSql.WhereClause}
                ORDER BY {reportSql.OrderBy}
                OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY;
                """;
            AddParameters(dataCommand, reportSql.Parameters);
            AddParameter(dataCommand, "@skip", DbType.Int64, skip);
            AddParameter(dataCommand, "@pageSize", DbType.Int32, pageSize);

            var rows = new List<AssetVerificationReportRow>();
            await using var reader = await dataCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(MapReportRow(reader));
            }

            return new AssetVerificationReportPage(rows, page, pageSize, total);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<IEnumerable<AssetVerification>> GetByVehicleAsync(int vmfCode)
    {
        var records = await QueryAsync(
            $"{BuildVmfExpressionPlaceholder()} = @vmfCode",
            [new ParameterValue("@vmfCode", DbType.Int32, vmfCode)]
        );
        return records;
    }

    public async Task<IEnumerable<AssetVerification>> GetBySiteAsync(int siteCode)
    {
        var records = await QueryAsync(
            "av.[site_code] = @siteCode",
            [new ParameterValue("@siteCode", DbType.Int16, siteCode)]
        );
        return records;
    }

    public async Task<IEnumerable<AssetVerification>> GetByStatusAsync(string status)
    {
        var records = await QueryAsync(
            $"{BuildStatusExpressionPlaceholder()} = @verificationStatus",
            [new ParameterValue("@verificationStatus", DbType.String, status.Trim())]
        );
        return records;
    }

    public async Task<AssetVerification> CreateAsync(
        AssetVerification verification,
        int currentUserId
    )
    {
        ArgumentNullException.ThrowIfNull(verification);

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var columns = await GetTableColumnsAsync(connection);
            var values = BuildWriteValues(columns, verification, currentUserId, isCreate: true);
            var columnList = string.Join(", ", values.Select(value => $"[{value.Column}]"));
            var parameterList = string.Join(", ", values.Select(value => value.Parameter));

            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                INSERT INTO [dbo].[Asset_Verification] ({columnList})
                OUTPUT INSERTED.[asset_verification_code]
                VALUES ({parameterList});
                """;
            AddParameters(command, values);

            var createdId = await command.ExecuteScalarAsync();
            if (createdId is null or DBNull)
            {
                throw new InvalidOperationException(
                    "Asset verification was inserted without an identity value."
                );
            }

            var createdCode = Convert.ToInt32(createdId);
            var result = await GetByIdAsync(createdCode);
            if (result is not null)
            {
                return result;
            }

            verification.asset_verification_code = createdCode;
            return verification;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<AssetVerification> UpdateAsync(
        AssetVerification verification,
        int currentUserId
    )
    {
        ArgumentNullException.ThrowIfNull(verification);

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var columns = await GetTableColumnsAsync(connection);
            var values = BuildWriteValues(columns, verification, currentUserId, isCreate: false);
            var assignments = string.Join(
                ", ",
                values.Select(value => $"[{value.Column}] = {value.Parameter}")
            );

            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                UPDATE [dbo].[Asset_Verification]
                SET {assignments}
                WHERE [asset_verification_code] = @verificationCode;
                """;
            AddParameters(command, values);
            AddParameter(
                command,
                "@verificationCode",
                DbType.Int32,
                verification.asset_verification_code
            );

            if (await command.ExecuteNonQueryAsync() == 0)
            {
                throw new InvalidOperationException(
                    $"Asset verification with code {verification.asset_verification_code} was not found."
                );
            }

            return await GetByIdAsync(verification.asset_verification_code) ?? verification;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task DeleteAsync(int verificationCode, int currentUserId)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var columns = await GetTableColumnsAsync(connection);
            await using var command = connection.CreateCommand();
            if (columns.Contains("is_deleted"))
            {
                var assignments = new List<string> { "[is_deleted] = @isDeleted" };
                if (columns.Contains("date_updated"))
                    assignments.Add("[date_updated] = @dateUpdated");
                if (columns.Contains("modified_by_user_code"))
                    assignments.Add("[modified_by_user_code] = @modifiedBy");

                command.CommandText = $"""
                    UPDATE [dbo].[Asset_Verification]
                    SET {string.Join(", ", assignments)}
                    WHERE [asset_verification_code] = @verificationCode;
                    """;
                AddParameter(command, "@isDeleted", DbType.Boolean, true);
                AddParameter(
                    command,
                    "@dateUpdated",
                    DbType.DateTime2,
                    DateTime.UtcNow,
                    columns.Contains("date_updated")
                );
                AddParameter(
                    command,
                    "@modifiedBy",
                    DbType.Int32,
                    currentUserId,
                    columns.Contains("modified_by_user_code")
                );
            }
            else
            {
                command.CommandText =
                    "DELETE FROM [dbo].[Asset_Verification] WHERE [asset_verification_code] = @verificationCode;";
            }

            AddParameter(command, "@verificationCode", DbType.Int32, verificationCode);
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

    private async Task<AssetVerificationReportSql> BuildVerificationReportSqlAsync(
        DbConnection connection,
        IReadOnlySet<string> assetVerificationColumns,
        AssetVerificationReportPageQuery query,
        CancellationToken cancellationToken
    )
    {
        var vehicleColumns = await GetTableColumnsAsync(
            connection,
            "vehicle_master",
            cancellationToken
        );
        var predicates = new List<string> { BuildActivePredicate(assetVerificationColumns) };
        var parameters = new List<ParameterValue>();
        var verificationDate = BuildReportVerificationDateExpression(assetVerificationColumns);
        var vehicleRegNo = BuildReportVehicleRegNoExpression(
            assetVerificationColumns,
            vehicleColumns
        );

        if (query.ReportMode == AssetVerificationReportMode.PerSiteProvinceDate)
        {
            if (query.SiteCode.HasValue)
            {
                if (assetVerificationColumns.Contains("site_code"))
                {
                    predicates.Add("av.[site_code] = @siteCode");
                    parameters.Add(
                        new ParameterValue("@siteCode", DbType.Int16, query.SiteCode.Value)
                    );
                }
                else
                {
                    predicates.Add("1 = 0");
                }
            }

            if (!string.IsNullOrWhiteSpace(query.Province))
            {
                if (assetVerificationColumns.Contains("province"))
                {
                    predicates.Add(
                        "LOWER(LTRIM(RTRIM(CONVERT(nvarchar(200), av.[province])))) "
                            + "= LOWER(LTRIM(RTRIM(@province)))"
                    );
                    parameters.Add(
                        new ParameterValue("@province", DbType.String, query.Province.Trim())
                    );
                }
                else
                {
                    predicates.Add("1 = 0");
                }
            }

            AddDateRangePredicates(
                predicates,
                parameters,
                verificationDate,
                query.FromDate,
                query.ToDate
            );

            return new AssetVerificationReportSql(
                BuildVerificationFromClause(assetVerificationColumns, vehicleColumns),
                string.Join(" AND ", predicates),
                BuildVerificationReportSelectList(assetVerificationColumns, vehicleColumns),
                string.Join(
                    ", ",
                    [
                        OptionalExpression(
                            assetVerificationColumns,
                            "av",
                            "department_name",
                            "nvarchar(max)"
                        ),
                        OptionalExpression(
                            assetVerificationColumns,
                            "av",
                            "site_name",
                            "nvarchar(max)"
                        ),
                        vehicleRegNo,
                        "av.[asset_verification_code]",
                    ]
                ),
                parameters
            );
        }

        predicates.Add($"{verificationDate} IS NOT NULL");
        AddDateRangePredicates(
            predicates,
            parameters,
            verificationDate,
            query.FromDate,
            query.ToDate
        );

        return new AssetVerificationReportSql(
            BuildVerificationFromClause(assetVerificationColumns, vehicleColumns),
            string.Join(" AND ", predicates),
            BuildVerificationReportSelectList(assetVerificationColumns, vehicleColumns),
            string.Join(
                ", ",
                [
                    OptionalExpression(
                        assetVerificationColumns,
                        "av",
                        "department_name",
                        "nvarchar(max)"
                    ),
                    verificationDate,
                    vehicleRegNo,
                    "av.[asset_verification_code]",
                ]
            ),
            parameters
        );
    }

    private async Task<AssetVerificationReportSql> BuildNotVerifiedReportSqlAsync(
        DbConnection connection,
        IReadOnlySet<string> assetVerificationColumns,
        CancellationToken cancellationToken
    )
    {
        var vehicleColumns = await GetTableColumnsAsync(
            connection,
            "vehicle_master",
            cancellationToken
        );
        var contractColumns = await GetTableColumnsAsync(connection, "contract", cancellationToken);
        var siteColumns = await GetTableColumnsAsync(connection, "site", cancellationToken);
        var departmentColumns = await GetTableColumnsAsync(
            connection,
            "department",
            cancellationToken
        );

        EnsureRequiredColumns(
            vehicleColumns,
            "vehicle_master",
            "vmf_code",
            "vehicle_status_code",
            "fleet_number"
        );
        EnsureRequiredColumns(
            contractColumns,
            "contract",
            "vmf_code",
            "site_code",
            "still_current"
        );
        EnsureRequiredColumns(siteColumns, "site", "Site_code", "Depatrment_code", "description");
        EnsureRequiredColumns(departmentColumns, "department", "department_code", "description");

        var verifiedMatches = new List<string>();
        if (assetVerificationColumns.Contains("vmf_code"))
        {
            verifiedMatches.Add("avv.[vmf_code] > 0 AND avv.[vmf_code] = v.[vmf_code]");
        }

        var verifiedIdentifier = BuildTrimmedIdentifierExpression(
            assetVerificationColumns,
            "avv",
            "vehicle_reg_no"
        );
        var vehicleIdentifiers = new[]
        {
            BuildTrimmedIdentifierExpression(vehicleColumns, "v", "fleet_number"),
            BuildTrimmedIdentifierExpression(vehicleColumns, "v", "registration_number"),
        }
            .Where(expression => expression is not null)
            .Cast<string>()
            .ToArray();
        if (verifiedIdentifier is not null && vehicleIdentifiers.Length > 0)
        {
            var identifierMatches = vehicleIdentifiers.Select(vehicleIdentifier =>
                $"LOWER({verifiedIdentifier}) = LOWER({vehicleIdentifier})"
            );
            verifiedMatches.Add(
                $"{verifiedIdentifier} IS NOT NULL AND ({string.Join(" OR ", identifierMatches)})"
            );
        }

        var exclusionPredicate =
            verifiedMatches.Count == 0 ? "1 = 0" : $"({string.Join(" OR ", verifiedMatches)})";
        var activeVerificationPredicate = BuildActivePredicate(assetVerificationColumns, "avv");
        var currentContractApply = BuildCurrentContractApply(
            contractColumns,
            siteColumns,
            departmentColumns
        );

        return new AssetVerificationReportSql(
            $"""
            FROM [dbo].[vehicle_master] AS v
            {currentContractApply}
            """,
            $"""
            v.[vehicle_status_code] = 1
            AND NOT EXISTS (
                SELECT 1
                FROM [dbo].[Asset_Verification] AS avv
                WHERE {activeVerificationPredicate}
                  AND {exclusionPredicate}
            )
            """,
            BuildNotVerifiedReportSelectList(vehicleColumns),
            string.Join(
                ", ",
                [
                    "current_contract.[department_name]",
                    OptionalExpression(vehicleColumns, "v", "fleet_number", "nvarchar(max)"),
                    OptionalExpression(vehicleColumns, "v", "registration_number", "nvarchar(max)"),
                    "v.[vmf_code]",
                ]
            ),
            []
        );
    }

    private static string BuildVerificationFromClause(
        IReadOnlySet<string> assetVerificationColumns,
        IReadOnlySet<string> vehicleColumns
    ) =>
        $"""
            FROM [dbo].[Asset_Verification] AS av
            {BuildVehicleLookupApply(assetVerificationColumns, vehicleColumns)}
            """;

    private static string BuildVehicleLookupApply(
        IReadOnlySet<string> assetVerificationColumns,
        IReadOnlySet<string> vehicleColumns
    )
    {
        if (
            !vehicleColumns.Contains("vmf_code")
            && !vehicleColumns.Contains("fleet_number")
            && !vehicleColumns.Contains("registration_number")
        )
        {
            return "OUTER APPLY (SELECT CAST(NULL AS int) AS [vmf_code], CAST(NULL AS nvarchar(max)) AS [fleet_number], CAST(NULL AS nvarchar(max)) AS [registration_number], CAST(NULL AS datetime2) AS [licence_due_date], CAST(NULL AS nvarchar(max)) AS [barcode], CAST(NULL AS int) AS [current_odo]) AS v";
        }

        var matches = new List<string>();
        if (assetVerificationColumns.Contains("vmf_code") && vehicleColumns.Contains("vmf_code"))
        {
            matches.Add("(av.[vmf_code] IS NOT NULL AND v.[vmf_code] = av.[vmf_code])");
        }

        var verificationIdentifier = BuildTrimmedIdentifierExpression(
            assetVerificationColumns,
            "av",
            "vehicle_reg_no"
        );
        var vehicleFleetIdentifier = BuildTrimmedIdentifierExpression(
            vehicleColumns,
            "v",
            "fleet_number"
        );
        var vehicleRegistrationIdentifier = BuildTrimmedIdentifierExpression(
            vehicleColumns,
            "v",
            "registration_number"
        );
        var vehicleIdentifiers = new[] { vehicleFleetIdentifier, vehicleRegistrationIdentifier }
            .Where(expression => expression is not null)
            .Cast<string>()
            .ToArray();
        if (verificationIdentifier is not null && vehicleIdentifiers.Length > 0)
        {
            var identifierMatches = string.Join(
                " OR ",
                vehicleIdentifiers.Select(identifier =>
                    $"LOWER({verificationIdentifier}) = LOWER({identifier})"
                )
            );
            matches.Add($"({verificationIdentifier} IS NOT NULL AND ({identifierMatches}))");
        }

        var matchPredicate = matches.Count == 0 ? "1 = 0" : string.Join(" OR ", matches);
        var matchOrder = BuildVehicleMatchOrder(
            assetVerificationColumns,
            vehicleColumns.Contains("vmf_code"),
            verificationIdentifier,
            vehicleFleetIdentifier,
            vehicleRegistrationIdentifier
        );
        var vehicleVmfCode = OptionalExpression(vehicleColumns, "v", "vmf_code", "int");
        var vehicleOrder = vehicleColumns.Contains("vmf_code") ? "v.[vmf_code]" : "1";

        return $"""
            OUTER APPLY (
                SELECT TOP (1)
                    {vehicleVmfCode} AS [vmf_code],
                    {OptionalExpression(
                vehicleColumns,
                "v",
                "fleet_number",
                "nvarchar(max)"
            )} AS [fleet_number],
                    {OptionalExpression(
                vehicleColumns,
                "v",
                "registration_number",
                "nvarchar(max)"
            )} AS [registration_number],
                    {OptionalExpression(
                vehicleColumns,
                "v",
                "licence_due_date",
                "datetime2"
            )} AS [licence_due_date],
                    {OptionalExpression(
                vehicleColumns,
                "v",
                "barcode",
                "nvarchar(max)"
            )} AS [barcode],
                    {OptionalExpression(vehicleColumns, "v", "current_odo", "int")} AS [current_odo]
                FROM [dbo].[vehicle_master] AS v
                WHERE {matchPredicate}
                ORDER BY {matchOrder}, {vehicleOrder}
            ) AS v
            """;
    }

    private static string BuildVehicleMatchOrder(
        IReadOnlySet<string> assetVerificationColumns,
        bool vehicleHasVmfCode,
        string? verificationIdentifier,
        string? vehicleFleetIdentifier,
        string? vehicleRegistrationIdentifier
    )
    {
        var priorities = new List<string>();
        if (assetVerificationColumns.Contains("vmf_code") && vehicleHasVmfCode)
        {
            priorities.Add(
                "WHEN av.[vmf_code] IS NOT NULL AND v.[vmf_code] = av.[vmf_code] THEN 0"
            );
        }

        if (verificationIdentifier is not null && vehicleFleetIdentifier is not null)
        {
            priorities.Add(
                $"WHEN LOWER({verificationIdentifier}) = LOWER({vehicleFleetIdentifier}) THEN 1"
            );
        }

        if (verificationIdentifier is not null && vehicleRegistrationIdentifier is not null)
        {
            priorities.Add(
                $"WHEN LOWER({verificationIdentifier}) = LOWER({vehicleRegistrationIdentifier}) THEN 2"
            );
        }

        return priorities.Count == 0 ? "3" : $"CASE {string.Join(" ", priorities)} ELSE 3 END";
    }

    private static string BuildCurrentContractApply(
        IReadOnlySet<string> contractColumns,
        IReadOnlySet<string> siteColumns,
        IReadOnlySet<string> departmentColumns
    )
    {
        var contractOrder = contractColumns.Contains("contract_code")
            ? "c.[contract_code]"
            : "c.[site_code]";
        var siteResponsiblePerson = OptionalExpression(
            siteColumns,
            "s",
            "res_person",
            "nvarchar(max)"
        );

        return $"""
            CROSS APPLY (
                SELECT TOP (1)
                    c.[site_code] AS [site_code],
                    s.[description] AS [site_name],
                    d.[description] AS [department_name],
                    {siteResponsiblePerson} AS [responsible_manager]
                FROM [dbo].[contract] AS c
                INNER JOIN [dbo].[site] AS s ON s.[Site_code] = c.[site_code]
                INNER JOIN [dbo].[department] AS d ON d.[department_code] = s.[Depatrment_code]
                WHERE c.[vmf_code] = v.[vmf_code]
                  AND c.[still_current] = 'Y'
                ORDER BY {contractOrder}, c.[site_code]
            ) AS current_contract
            """;
    }

    private static string BuildVerificationReportSelectList(
        IReadOnlySet<string> assetVerificationColumns,
        IReadOnlySet<string> vehicleColumns
    )
    {
        var verificationDate = BuildReportVerificationDateExpression(assetVerificationColumns);
        var vehicleRegNo = BuildReportVehicleRegNoExpression(
            assetVerificationColumns,
            vehicleColumns
        );
        var vmfCode = BuildReportVmfCodeExpression(assetVerificationColumns, vehicleColumns);
        var status = BuildReportStatusExpression(assetVerificationColumns, verificationDate);

        return string.Join(
            ",\n                    ",
            [
                "av.[asset_verification_code] AS [asset_verification_code]",
                $"{vehicleRegNo} AS [vehicle_reg_no]",
                $"{vmfCode} AS [vmf_code]",
                OptionalExpression(vehicleColumns, "v", "fleet_number", "nvarchar(max)")
                    + " AS [fleet_number]",
                OptionalExpression(vehicleColumns, "v", "registration_number", "nvarchar(max)")
                    + " AS [registration_number]",
                OptionalExpression(
                    assetVerificationColumns,
                    "av",
                    "department_name",
                    "nvarchar(max)"
                ) + " AS [department_name]",
                OptionalExpression(assetVerificationColumns, "av", "site_name", "nvarchar(max)")
                    + " AS [site_name]",
                OptionalExpression(assetVerificationColumns, "av", "site_code", "smallint")
                    + " AS [site_code]",
                OptionalExpression(assetVerificationColumns, "av", "province", "nvarchar(max)")
                    + " AS [province]",
                OptionalExpression(assetVerificationColumns, "av", "vehicle_make", "nvarchar(max)")
                    + " AS [vehicle_make]",
                OptionalExpression(assetVerificationColumns, "av", "vehicle_model", "nvarchar(max)")
                    + " AS [vehicle_model]",
                $"COALESCE({OptionalExpression(assetVerificationColumns, "av", "licence_expiry_date", "datetime2")}, {OptionalExpression(vehicleColumns, "v", "licence_due_date", "datetime2")}) AS [licence_expiry_date]",
                $"{verificationDate} AS [date_last_verified]",
                $"{status} AS [status]",
                OptionalExpression(
                    assetVerificationColumns,
                    "av",
                    "responsible_manager",
                    "nvarchar(max)"
                ) + " AS [responsible_manager]",
                OptionalExpression(assetVerificationColumns, "av", "current_km", "int")
                    + " AS [current_km]",
                OptionalExpression(assetVerificationColumns, "av", "barcode", "nvarchar(max)")
                    + " AS [barcode]",
            ]
        );
    }

    private static string BuildNotVerifiedReportSelectList(IReadOnlySet<string> vehicleColumns) =>
        string.Join(
            ",\n                    ",
            [
                "CAST(NULL AS int) AS [asset_verification_code]",
                OptionalExpression(vehicleColumns, "v", "registration_number", "nvarchar(max)")
                    + " AS [vehicle_reg_no]",
                "v.[vmf_code] AS [vmf_code]",
                OptionalExpression(vehicleColumns, "v", "fleet_number", "nvarchar(max)")
                    + " AS [fleet_number]",
                OptionalExpression(vehicleColumns, "v", "registration_number", "nvarchar(max)")
                    + " AS [registration_number]",
                "current_contract.[department_name] AS [department_name]",
                "current_contract.[site_name] AS [site_name]",
                "current_contract.[site_code] AS [site_code]",
                "CAST(NULL AS nvarchar(max)) AS [province]",
                "CAST(NULL AS nvarchar(max)) AS [vehicle_make]",
                "CAST(NULL AS nvarchar(max)) AS [vehicle_model]",
                OptionalExpression(vehicleColumns, "v", "licence_due_date", "datetime2")
                    + " AS [licence_expiry_date]",
                "CAST(NULL AS datetime2) AS [date_last_verified]",
                "CAST(NULL AS nvarchar(max)) AS [status]",
                "current_contract.[responsible_manager] AS [responsible_manager]",
                OptionalExpression(vehicleColumns, "v", "current_odo", "int") + " AS [current_km]",
                OptionalExpression(vehicleColumns, "v", "barcode", "nvarchar(max)")
                    + " AS [barcode]",
            ]
        );

    private static string BuildReportVmfCodeExpression(
        IReadOnlySet<string> assetVerificationColumns,
        IReadOnlySet<string> vehicleColumns
    ) =>
        $"COALESCE({OptionalExpression(assetVerificationColumns, "av", "vmf_code", "int")}, {OptionalExpression(vehicleColumns, "v", "vmf_code", "int")})";

    private static string BuildReportVehicleRegNoExpression(
        IReadOnlySet<string> assetVerificationColumns,
        IReadOnlySet<string> vehicleColumns
    )
    {
        var verificationRegNo = OptionalExpression(
            assetVerificationColumns,
            "av",
            "vehicle_reg_no",
            "nvarchar(max)"
        );
        var vehicleRegNo = BuildAliasedCoalesceExpression(
            vehicleColumns,
            "v",
            ["registration_number", "fleet_number"],
            "nvarchar(max)"
        );
        return $"CASE WHEN {verificationRegNo} IS NULL THEN {vehicleRegNo} ELSE {verificationRegNo} END";
    }

    private static string BuildReportVerificationDateExpression(
        IReadOnlySet<string> assetVerificationColumns
    ) =>
        BuildAliasedCoalesceExpression(
            assetVerificationColumns,
            "av",
            ["date_last_verified", "verification_date"],
            "datetime2"
        );

    private static string BuildReportStatusExpression(
        IReadOnlySet<string> assetVerificationColumns,
        string verificationDate
    )
    {
        var status = OptionalExpression(
            assetVerificationColumns,
            "av",
            "verification_status",
            "nvarchar(max)"
        );
        return $"COALESCE(NULLIF({status}, N''), CASE WHEN {verificationDate} IS NULL THEN N'Pending' ELSE N'Verified' END)";
    }

    private static void AddDateRangePredicates(
        ICollection<string> predicates,
        ICollection<ParameterValue> parameters,
        string dateExpression,
        DateTime? fromDate,
        DateTime? toDate
    )
    {
        if (fromDate.HasValue)
        {
            predicates.Add($"{dateExpression} >= @fromDate");
            parameters.Add(new ParameterValue("@fromDate", DbType.DateTime2, fromDate.Value.Date));
        }

        if (toDate.HasValue)
        {
            predicates.Add($"{dateExpression} < DATEADD(day, 1, @toDate)");
            parameters.Add(new ParameterValue("@toDate", DbType.DateTime2, toDate.Value.Date));
        }
    }

    private static string? BuildTrimmedIdentifierExpression(
        IReadOnlySet<string> columns,
        string alias,
        string column
    ) =>
        columns.Contains(column)
            ? $"NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(4000), [{alias}].[{column}]))), N'')"
            : null;

    private static string BuildAliasedCoalesceExpression(
        IReadOnlySet<string> columns,
        string alias,
        IReadOnlyList<string> candidates,
        string sqlType
    )
    {
        var expressions = candidates
            .Where(columns.Contains)
            .Select(column => $"[{alias}].[{column}]")
            .ToArray();
        return expressions.Length switch
        {
            0 => $"CAST(NULL AS {sqlType})",
            1 => expressions[0],
            _ => $"COALESCE({string.Join(", ", expressions)})",
        };
    }

    private static string OptionalExpression(
        IReadOnlySet<string> columns,
        string alias,
        string column,
        string sqlType
    ) => columns.Contains(column) ? $"[{alias}].[{column}]" : $"CAST(NULL AS {sqlType})";

    private static string BuildActivePredicate(IReadOnlySet<string> columns, string alias) =>
        columns.Contains("is_deleted") ? $"{alias}.[is_deleted] = 0" : "1 = 1";

    private static void EnsureRequiredColumns(
        IReadOnlySet<string> columns,
        string tableName,
        params string[] requiredColumns
    )
    {
        var missingColumns = requiredColumns.Where(column => !columns.Contains(column)).ToArray();
        if (missingColumns.Length > 0)
        {
            throw new InvalidOperationException(
                $"The required asset verification report columns are not available on {tableName}: {string.Join(", ", missingColumns)}"
            );
        }
    }

    private async Task<IEnumerable<AssetVerification>> QueryAsEnumerableAsync() =>
        await QueryAsync(null, []);

    private async Task<List<AssetVerification>> QueryAsync(
        string? filter,
        IReadOnlyList<ParameterValue> parameters
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
            var columns = await GetTableColumnsAsync(connection);
            if (!columns.Contains("asset_verification_code"))
            {
                throw new InvalidOperationException(
                    "The required dbo.Asset_Verification table is not available."
                );
            }

            var activePredicate = BuildActivePredicate(columns);
            var predicates = new List<string> { activePredicate };
            if (!string.IsNullOrWhiteSpace(filter))
            {
                predicates.Add(ReplacePlaceholders(filter, columns));
            }

            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT
                    {BuildSelectList(columns)}
                FROM [dbo].[Asset_Verification] AS av
                OUTER APPLY (
                    SELECT TOP (1)
                        v.[vmf_code], v.[fleet_number], v.[registration_number]
                    FROM [dbo].[vehicle_master] AS v
                    WHERE {BuildVehicleMatchPredicate(columns)}
                    ORDER BY v.[vmf_code]
                ) AS v
                WHERE {string.Join(" AND ", predicates)}
                ORDER BY av.[asset_verification_code];
                """;
            foreach (var parameter in parameters)
            {
                AddParameter(command, parameter.Name, parameter.Type, parameter.Value);
            }

            var results = new List<AssetVerification>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(Map(reader));
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

    private static string BuildActivePredicate(IReadOnlySet<string> columns) =>
        columns.Contains("is_deleted") ? "av.[is_deleted] = 0" : "1 = 1";

    private static string ReplacePlaceholders(string filter, IReadOnlySet<string> columns) =>
        filter
            .Replace(
                BuildVmfExpressionPlaceholder(),
                BuildVmfExpression(columns),
                StringComparison.Ordinal
            )
            .Replace(
                BuildStatusExpressionPlaceholder(),
                BuildStatusExpression(columns),
                StringComparison.Ordinal
            )
            .Replace(
                "av.[site_code]",
                ColumnReference(columns, "site_code"),
                StringComparison.Ordinal
            );

    private static string BuildSelectList(IReadOnlySet<string> columns)
    {
        var verificationDate = BuildCoalesceExpression(
            columns,
            "verification_date",
            "date_last_verified",
            "datetime2"
        );
        var vmfCode = columns.Contains("vmf_code")
            ? "COALESCE(av.[vmf_code], v.[vmf_code])"
            : "v.[vmf_code]";
        var status = BuildStatusExpression(columns);

        return string.Join(
            ",\n                    ",
            [
                "av.[asset_verification_code] AS [asset_verification_code]",
                OptionalColumn(columns, "province", "province", "nvarchar(max)"),
                OptionalColumn(columns, "department_name", "department_name", "nvarchar(max)"),
                OptionalColumn(columns, "site_code", "site_code", "smallint"),
                OptionalColumn(columns, "site_name", "site_name", "nvarchar(max)"),
                OptionalColumn(
                    columns,
                    "responsible_manager",
                    "responsible_manager",
                    "nvarchar(max)"
                ),
                OptionalColumn(columns, "tel_no", "tel_no", "nvarchar(max)"),
                OptionalColumn(columns, "fax_no", "fax_no", "nvarchar(max)"),
                OptionalColumn(columns, "vehicle_reg_no", "vehicle_reg_no", "nvarchar(max)"),
                $"{vmfCode} AS [vmf_code]",
                $"{verificationDate} AS [verification_date]",
                OptionalColumn(columns, "verified_by", "verified_by", "int"),
                $"{status} AS [verification_status]",
                OptionalColumn(columns, "notes", "notes", "nvarchar(max)"),
                OptionalColumn(columns, "vehicle_make", "vehicle_make", "nvarchar(max)"),
                OptionalColumn(columns, "vehicle_model", "vehicle_model", "nvarchar(max)"),
                OptionalColumn(columns, "vehicle_colour", "vehicle_colour", "nvarchar(max)"),
                OptionalColumn(columns, "mobitrack_fitted", "mobitrack_fitted", "nvarchar(max)"),
                OptionalColumn(columns, "petrol_card", "petrol_card", "nvarchar(max)"),
                OptionalColumn(columns, "lamination", "lamination", "nvarchar(max)"),
                OptionalColumn(columns, "tyre_bands", "tyre_bands", "nvarchar(max)"),
                OptionalColumn(columns, "barcode", "barcode", "nvarchar(max)"),
                OptionalColumn(columns, "logbook", "logbook", "nvarchar(max)"),
                OptionalColumn(columns, "gearlock", "gearlock", "nvarchar(max)"),
                OptionalColumn(columns, "radio", "radio", "nvarchar(max)"),
                OptionalColumn(columns, "car_keys", "car_keys", "nvarchar(max)"),
                OptionalColumn(columns, "licence_expiry_date", "licence_expiry_date", "datetime2"),
                OptionalColumn(columns, "barcode_number", "barcode_number", "nvarchar(max)"),
                OptionalColumn(
                    columns,
                    "vehicle_engine_num",
                    "vehicle_engine_num",
                    "nvarchar(max)"
                ),
                OptionalColumn(
                    columns,
                    "vehicle_chassis_num",
                    "vehicle_chassis_num",
                    "nvarchar(max)"
                ),
                OptionalColumn(columns, "current_km", "current_km", "int"),
                OptionalColumn(columns, "date_last_verified", "date_last_verified", "datetime2"),
                OptionalColumn(columns, "comments", "comments", "nvarchar(max)"),
                OptionalColumn(columns, "is_deleted", "is_deleted", "bit"),
            ]
        );
    }

    private static string BuildVehicleMatchPredicate(IReadOnlySet<string> columns)
    {
        if (!columns.Contains("vehicle_reg_no"))
        {
            return "1 = 0";
        }

        return "v.[fleet_number] = av.[vehicle_reg_no] OR v.[registration_number] = av.[vehicle_reg_no]";
    }

    private static string BuildVmfExpressionPlaceholder() => "__ASSET_VERIFICATION_VMF__";

    private static string BuildVmfExpression(IReadOnlySet<string> columns) =>
        columns.Contains("vmf_code") ? "COALESCE(av.[vmf_code], v.[vmf_code])" : "v.[vmf_code]";

    private static string BuildStatusExpressionPlaceholder() => "__ASSET_VERIFICATION_STATUS__";

    private static string BuildStatusExpression(IReadOnlySet<string> columns)
    {
        var verificationDate = BuildCoalesceExpression(
            columns,
            "verification_date",
            "date_last_verified",
            "datetime2"
        );
        var fallback = $"CASE WHEN {verificationDate} IS NULL THEN 'Pending' ELSE 'Verified' END";
        return columns.Contains("verification_status")
            ? $"COALESCE(NULLIF(av.[verification_status], ''), {fallback})"
            : fallback;
    }

    private static string BuildCoalesceExpression(
        IReadOnlySet<string> columns,
        string first,
        string second,
        string sqlType
    )
    {
        var expressions = new[] { first, second }
            .Where(columns.Contains)
            .Select(column => $"av.[{column}]")
            .ToList();
        return expressions.Count switch
        {
            0 => $"CAST(NULL AS {sqlType})",
            1 => expressions[0],
            _ => $"COALESCE({string.Join(", ", expressions)})",
        };
    }

    private static string OptionalColumn(
        IReadOnlySet<string> columns,
        string column,
        string alias,
        string sqlType
    ) =>
        columns.Contains(column)
            ? $"av.[{column}] AS [{alias}]"
            : $"CAST(NULL AS {sqlType}) AS [{alias}]";

    private static string ColumnReference(IReadOnlySet<string> columns, string column) =>
        columns.Contains(column) ? $"av.[{column}]" : "NULL";

    private static List<WriteValue> BuildWriteValues(
        IReadOnlySet<string> columns,
        AssetVerification verification,
        int currentUserId,
        bool isCreate
    )
    {
        var values = new List<WriteValue>();
        AddValue(values, columns, "province", DbType.String, verification.province);
        AddValue(values, columns, "department_name", DbType.String, verification.department_name);
        AddValue(values, columns, "site_code", DbType.Int16, verification.site_code);
        AddValue(values, columns, "site_name", DbType.String, verification.site_name);
        AddValue(
            values,
            columns,
            "responsible_manager",
            DbType.String,
            verification.responsible_manager
        );
        AddValue(values, columns, "tel_no", DbType.String, verification.tel_no);
        AddValue(values, columns, "fax_no", DbType.String, verification.fax_no);
        AddValue(values, columns, "vehicle_reg_no", DbType.String, verification.vehicle_reg_no);
        AddValue(values, columns, "vmf_code", DbType.Int32, verification.vmf_code);
        AddValue(
            values,
            columns,
            "verification_date",
            DbType.DateTime2,
            verification.verification_date ?? verification.date_last_verified
        );
        AddValue(
            values,
            columns,
            "verified_by",
            DbType.Int32,
            verification.verified_by ?? (currentUserId > 0 ? currentUserId : null)
        );
        AddValue(
            values,
            columns,
            "verification_status",
            DbType.String,
            verification.verification_status
                ?? (
                    (verification.verification_date ?? verification.date_last_verified).HasValue
                        ? "Verified"
                        : "Pending"
                )
        );
        AddValue(
            values,
            columns,
            "notes",
            DbType.String,
            verification.notes ?? verification.comments
        );
        AddValue(values, columns, "vehicle_make", DbType.String, verification.vehicle_make);
        AddValue(values, columns, "vehicle_model", DbType.String, verification.vehicle_model);
        AddValue(values, columns, "vehicle_colour", DbType.String, verification.vehicle_colour);
        AddValue(values, columns, "mobitrack_fitted", DbType.String, verification.mobitrack_fitted);
        AddValue(values, columns, "petrol_card", DbType.String, verification.petrol_card);
        AddValue(values, columns, "lamination", DbType.String, verification.lamination);
        AddValue(values, columns, "tyre_bands", DbType.String, verification.tyre_bands);
        AddValue(values, columns, "barcode", DbType.String, verification.barcode);
        AddValue(values, columns, "logbook", DbType.String, verification.logbook);
        AddValue(values, columns, "gearlock", DbType.String, verification.gearlock);
        AddValue(values, columns, "radio", DbType.String, verification.radio);
        AddValue(values, columns, "car_keys", DbType.String, verification.car_keys);
        AddValue(
            values,
            columns,
            "licence_expiry_date",
            DbType.DateTime2,
            verification.licence_expiry_date
        );
        AddValue(values, columns, "barcode_number", DbType.String, verification.barcode_number);
        AddValue(
            values,
            columns,
            "vehicle_engine_num",
            DbType.String,
            verification.vehicle_engine_num
        );
        AddValue(
            values,
            columns,
            "vehicle_chassis_num",
            DbType.String,
            verification.vehicle_chassis_num
        );
        AddValue(values, columns, "current_km", DbType.Int32, verification.current_km);
        AddValue(
            values,
            columns,
            "date_last_verified",
            DbType.DateTime2,
            verification.date_last_verified ?? verification.verification_date
        );
        AddValue(
            values,
            columns,
            "comments",
            DbType.String,
            verification.comments ?? verification.notes
        );

        if (isCreate)
        {
            AddValue(values, columns, "date_created", DbType.DateTime2, DateTime.UtcNow);
            AddValue(
                values,
                columns,
                "created_by_user_code",
                DbType.Int32,
                currentUserId > 0 ? currentUserId : null
            );
            AddValue(values, columns, "is_deleted", DbType.Boolean, false);
        }
        else
        {
            AddValue(values, columns, "date_updated", DbType.DateTime2, DateTime.UtcNow);
            AddValue(
                values,
                columns,
                "modified_by_user_code",
                DbType.Int32,
                currentUserId > 0 ? currentUserId : null
            );
        }

        return values;
    }

    private static void AddValue(
        List<WriteValue> values,
        IReadOnlySet<string> columns,
        string column,
        DbType type,
        object? value
    )
    {
        if (!columns.Contains(column))
        {
            return;
        }

        var parameter = $"@value{values.Count}";
        values.Add(new WriteValue(column, parameter, type, value));
    }

    private static Task<HashSet<string>> GetTableColumnsAsync(DbConnection connection) =>
        GetTableColumnsAsync(connection, "Asset_Verification", CancellationToken.None);

    private static async Task<HashSet<string>> GetTableColumnsAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT [COLUMN_NAME]
            FROM [INFORMATION_SCHEMA].[COLUMNS]
            WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table;
            """;
        AddParameter(command, "@schema", DbType.String, "dbo");
        AddParameter(command, "@table", DbType.String, tableName);

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static AssetVerificationReportRow MapReportRow(DbDataReader reader) =>
        new(
            ReadInt(reader, "asset_verification_code"),
            ReadString(reader, "vehicle_reg_no"),
            ReadInt(reader, "vmf_code"),
            ReadString(reader, "fleet_number"),
            ReadString(reader, "registration_number"),
            ReadString(reader, "department_name"),
            ReadString(reader, "site_name"),
            ReadShort(reader, "site_code"),
            ReadString(reader, "province"),
            ReadString(reader, "vehicle_make"),
            ReadString(reader, "vehicle_model"),
            ReadDate(reader, "licence_expiry_date"),
            ReadDate(reader, "date_last_verified"),
            ReadString(reader, "status"),
            ReadString(reader, "responsible_manager"),
            ReadInt(reader, "current_km"),
            ReadString(reader, "barcode")
        );

    private static AssetVerification Map(DbDataReader reader) =>
        new()
        {
            asset_verification_code = ReadInt(reader, "asset_verification_code") ?? 0,
            province = ReadString(reader, "province"),
            department_name = ReadString(reader, "department_name"),
            site_code = ReadShort(reader, "site_code"),
            site_name = ReadString(reader, "site_name"),
            responsible_manager = ReadString(reader, "responsible_manager"),
            tel_no = ReadString(reader, "tel_no"),
            fax_no = ReadString(reader, "fax_no"),
            vehicle_reg_no = ReadString(reader, "vehicle_reg_no"),
            vmf_code = ReadInt(reader, "vmf_code"),
            verification_date = ReadDate(reader, "verification_date"),
            verified_by = ReadInt(reader, "verified_by"),
            verification_status = ReadString(reader, "verification_status"),
            notes = ReadString(reader, "notes"),
            vehicle_make = ReadString(reader, "vehicle_make"),
            vehicle_model = ReadString(reader, "vehicle_model"),
            vehicle_colour = ReadString(reader, "vehicle_colour"),
            mobitrack_fitted = ReadString(reader, "mobitrack_fitted"),
            petrol_card = ReadString(reader, "petrol_card"),
            lamination = ReadString(reader, "lamination"),
            tyre_bands = ReadString(reader, "tyre_bands"),
            barcode = ReadString(reader, "barcode"),
            logbook = ReadString(reader, "logbook"),
            gearlock = ReadString(reader, "gearlock"),
            radio = ReadString(reader, "radio"),
            car_keys = ReadString(reader, "car_keys"),
            licence_expiry_date = ReadDate(reader, "licence_expiry_date"),
            barcode_number = ReadString(reader, "barcode_number"),
            vehicle_engine_num = ReadString(reader, "vehicle_engine_num"),
            vehicle_chassis_num = ReadString(reader, "vehicle_chassis_num"),
            current_km = ReadInt(reader, "current_km"),
            date_last_verified = ReadDate(reader, "date_last_verified"),
            comments = ReadString(reader, "comments"),
            is_deleted = ReadBool(reader, "is_deleted") ?? false,
        };

    private static string? ReadString(DbDataReader reader, string column)
    {
        var value = reader[column];
        return value is DBNull ? null : Convert.ToString(value);
    }

    private static int? ReadInt(DbDataReader reader, string column)
    {
        var value = reader[column];
        return value is DBNull ? null : Convert.ToInt32(value);
    }

    private static short? ReadShort(DbDataReader reader, string column)
    {
        var value = reader[column];
        return value is DBNull ? null : Convert.ToInt16(value);
    }

    private static DateTime? ReadDate(DbDataReader reader, string column)
    {
        var value = reader[column];
        return value is DBNull ? null : Convert.ToDateTime(value);
    }

    private static bool? ReadBool(DbDataReader reader, string column)
    {
        var value = reader[column];
        return value is DBNull ? null : Convert.ToBoolean(value);
    }

    private static void AddParameters(DbCommand command, IEnumerable<WriteValue> values)
    {
        foreach (var value in values)
        {
            AddParameter(command, value.Parameter, value.Type, value.Value);
        }
    }

    private static void AddParameters(DbCommand command, IEnumerable<ParameterValue> values)
    {
        foreach (var value in values)
        {
            AddParameter(command, value.Name, value.Type, value.Value);
        }
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        DbType type,
        object? value,
        bool enabled = true
    )
    {
        if (!enabled)
        {
            return;
        }

        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private readonly record struct ParameterValue(string Name, DbType Type, object? Value);

    private sealed record AssetVerificationReportSql(
        string FromClause,
        string WhereClause,
        string SelectList,
        string OrderBy,
        IReadOnlyList<ParameterValue> Parameters
    );

    private readonly record struct WriteValue(
        string Column,
        string Parameter,
        DbType Type,
        object? Value
    );
}
