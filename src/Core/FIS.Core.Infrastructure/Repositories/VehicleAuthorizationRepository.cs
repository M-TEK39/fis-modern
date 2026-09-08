using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Vehicles;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Vehicle inception access negotiated against the connected database at
/// runtime. The client's pre_vehicle_master table predates the modern EF
/// model, so a static EF query would select columns that do not exist on the
/// client's database. Legacy columns are selected/written when present and
/// modern audit columns are optional.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Table, column, and stored procedure identifiers come from fixed compatibility allowlists; values are parameters."
)]
public sealed class VehicleAuthorizationRepository : IVehicleAuthorizationRepository
{
    private const string PreVehicleTableName = "pre_vehicle_master";
    private const string VehicleTableName = "vehicle_master";
    private const string ModelTableName = "model";
    private const string GgNumberTableName = "block_gg_numbers";

    private static readonly string[] SelectedColumns =
    [
        "temp_vmf_code",
        "fleet_number",
        "registration_number",
        "replaced_gg_number",
        "model_code",
        "colour",
        "year_manufactured",
        "chassis_number",
        "engine_number",
        "take_on_odo",
        "take_on_date",
        "location_code",
        "vehicle_status_code",
        "vehicle_status_date",
        "type_code",
        "vs_code",
        "comment",
        "purchase_amount",
        "purchase_from",
        "purchase_date",
        "captured_by_user_code",
        "action_user_access_code",
        "site_code",
        "Authority_Status",
        "captured_date",
        "printed",
        "invoice_number",
        "gp_number",
        "Fleet_Notes",
        "damage_status",
        "damages_comment",
        "authorized_by_user_code",
        "authorization_date",
        "rejection_reason",
        "authorization_comment",
        "vmf_code",
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private static readonly string[] StringColumns =
    [
        "fleet_number",
        "registration_number",
        "replaced_gg_number",
        "colour",
        "chassis_number",
        "engine_number",
        "comment",
        "purchase_from",
        "Authority_Status",
        "printed",
        "invoice_number",
        "gp_number",
        "Fleet_Notes",
        "damage_status",
        "damages_comment",
        "rejection_reason",
        "authorization_comment",
    ];

    private static readonly string[] DateColumns =
    [
        "take_on_date",
        "vehicle_status_date",
        "purchase_date",
        "captured_date",
        "authorization_date",
        "date_created",
        "date_updated",
    ];

    private static readonly string[] IntegerColumns =
    [
        "temp_vmf_code",
        "take_on_odo",
        "authorized_by_user_code",
        "vmf_code",
        "created_by_user_code",
        "modified_by_user_code",
    ];

    private static readonly string[] ShortColumns =
    [
        "model_code",
        "year_manufactured",
        "location_code",
        "vehicle_status_code",
        "type_code",
        "action_user_access_code",
        "site_code",
    ];

    private static readonly string[] ByteColumns = ["vs_code"];
    private static readonly string[] DecimalColumns = ["purchase_amount"];

    private readonly FisDbContext _context;

    public VehicleAuthorizationRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<PreVehicleMaster?> GetByIdAsync(int tempVmfCode) =>
        WithConnectionAsync(async connection =>
        {
            var schema = await GetSchemaAsync(connection, null);
            return (
                await QueryAsync(connection, null, schema, tempVmfCode: tempVmfCode)
            ).SingleOrDefault();
        });

    public Task<PreVehicleMaster?> GetByChassisNumberAsync(string chassisNumber) =>
        string.IsNullOrWhiteSpace(chassisNumber)
            ? Task.FromResult<PreVehicleMaster?>(null)
            : WithConnectionAsync(async connection =>
            {
                var schema = await GetSchemaAsync(connection, null);
                return (
                    await QueryAsync(connection, null, schema, chassisNumber: chassisNumber.Trim())
                ).SingleOrDefault();
            });

    public Task<IEnumerable<PreVehicleMaster>> GetPendingAuthorizationsAsync() =>
        GetByStatusAsync("Awaiting Authorization");

    public Task<IEnumerable<PreVehicleMaster>> GetAuthorizedVehiclesAsync() =>
        GetByStatusAsync("Authorized");

    public Task<IEnumerable<PreVehicleMaster>> GetRejectedVehiclesAsync() =>
        GetByStatusAsync("Rejected");

    public async Task<IEnumerable<PreVehicleMaster>> GetByStatusAsync(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return [];
        }

        return await WithConnectionAsync(async connection =>
        {
            var schema = await GetSchemaAsync(connection, null);
            return (IEnumerable<PreVehicleMaster>)
                await QueryAsync(connection, null, schema, status: status);
        });
    }

    public async Task<IEnumerable<PreVehicleMaster>> GetAuthorizationHistoryAsync(
        DateTime? startDate = null,
        DateTime? endDate = null
    ) =>
        await WithConnectionAsync(async connection =>
        {
            var schema = await GetSchemaAsync(connection, null);
            var historyStatuses = schema.Columns.Contains("Authority_Status")
                ? new[] { "Authorized", "Rejected" }
                : Array.Empty<string>();
            var rows = await QueryAsync(
                connection,
                null,
                schema,
                statuses: historyStatuses,
                startDate: startDate,
                endDate: endDate
            );
            return (IEnumerable<PreVehicleMaster>)rows;
        });

    public async Task<IReadOnlyList<VehicleMaintenanceTypeOption>> GetMaintenanceTypesAsync() =>
        await WithConnectionAsync(async connection =>
        {
            if (!await ProcedureExistsAsync(connection, null, "DEV_SEL_AllMaintenanceTypes"))
            {
                return (IReadOnlyList<VehicleMaintenanceTypeOption>)[];
            }

            await using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "dbo.DEV_SEL_AllMaintenanceTypes";

            var types = new List<VehicleMaintenanceTypeOption>();
            await using var reader = await command.ExecuteReaderAsync();
            var codeOrdinal = FindOrdinal(reader, "Maintenance_TypeId", "maintenance_type_id");
            var nameOrdinal = FindOrdinal(reader, "Name", "name");
            if (codeOrdinal is null || nameOrdinal is null)
            {
                return types;
            }

            while (await reader.ReadAsync())
            {
                if (reader.IsDBNull(codeOrdinal.Value) || reader.IsDBNull(nameOrdinal.Value))
                {
                    continue;
                }

                var name = reader.GetValue(nameOrdinal.Value)?.ToString()?.Trim();
                if (
                    !short.TryParse(reader.GetValue(codeOrdinal.Value)?.ToString(), out var code)
                    || string.IsNullOrWhiteSpace(name)
                )
                {
                    continue;
                }

                types.Add(new VehicleMaintenanceTypeOption(code, name));
            }

            return types;
        });

    public async Task<PreVehicleMaster> CreateAsync(PreVehicleMaster vehicleAuth, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(vehicleAuth);

        return await WithConnectionAsync(async connection =>
        {
            var schema = await GetSchemaAsync(connection, null);
            await using var transaction = await connection.BeginTransactionAsync(
                IsolationLevel.Serializable
            );
            var committed = false;

            try
            {
                await EnsureNoVehicleDuplicateAsync(connection, transaction, vehicleAuth);

                var existing = string.IsNullOrWhiteSpace(vehicleAuth.chassis_number)
                    ? null
                    : (
                        await QueryAsync(
                            connection,
                            transaction,
                            schema,
                            chassisNumber: vehicleAuth.chassis_number.Trim()
                        )
                    ).SingleOrDefault();

                int tempVmfCode;
                if (existing is not null)
                {
                    if (
                        string.Equals(
                            existing.Authority_Status,
                            "Authorized",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        throw new InvalidOperationException(
                            $"Vehicle authorization already exists for chassis number: {vehicleAuth.chassis_number}"
                        );
                    }

                    tempVmfCode = existing.temp_vmf_code;
                    await UpdateRowAsync(
                        connection,
                        transaction,
                        schema,
                        tempVmfCode,
                        vehicleAuth,
                        currentUserId
                    );
                }
                else
                {
                    tempVmfCode = await InsertRowAsync(
                        connection,
                        transaction,
                        schema,
                        vehicleAuth,
                        currentUserId
                    );
                }

                await WriteLegacyCaptureSideEffectsAsync(
                    connection,
                    transaction,
                    tempVmfCode,
                    vehicleAuth,
                    currentUserId
                );
                await transaction.CommitAsync();
                committed = true;

                return (
                    await QueryAsync(connection, null, schema, tempVmfCode: tempVmfCode)
                ).Single();
            }
            catch
            {
                if (!committed)
                {
                    await transaction.RollbackAsync();
                }

                throw;
            }
        });
    }

    public async Task UpdateAsync(PreVehicleMaster vehicleAuth, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(vehicleAuth);

        await WithConnectionAsync(async connection =>
        {
            var schema = await GetSchemaAsync(connection, null);
            await UpdateRowAsync(
                connection,
                null,
                schema,
                vehicleAuth.temp_vmf_code,
                vehicleAuth,
                currentUserId
            );
            return true;
        });
    }

    public async Task ApproveAsync(int tempVmfCode, int authorizedByUserId, string? comment = null)
    {
        await WithConnectionAsync(async connection =>
        {
            var schema = await GetSchemaAsync(connection, null);
            var vehicle = (
                await QueryAsync(connection, null, schema, tempVmfCode: tempVmfCode)
            ).SingleOrDefault();
            if (vehicle is null)
            {
                throw new KeyNotFoundException(
                    $"Vehicle authorization with ID {tempVmfCode} not found"
                );
            }

            if (
                string.Equals(
                    vehicle.Authority_Status,
                    "Authorized",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                throw new InvalidOperationException(
                    $"Vehicle authorization {tempVmfCode} is already approved"
                );
            }

            await using var transaction = await connection.BeginTransactionAsync(
                IsolationLevel.Serializable
            );
            var committed = false;
            try
            {
                if (
                    await ProcedureExistsAsync(
                        connection,
                        transaction,
                        "DEV_INS_VehicleFromPre_Vehicle_Master"
                    )
                )
                {
                    // This procedure owns the legacy GG allocation and the
                    // promotion into vehicle_master. Do not replace it on a
                    // client's legacy database.
                    await ExecuteProcedureAsync(
                        connection,
                        transaction,
                        "DEV_INS_VehicleFromPre_Vehicle_Master",
                        new ProcedureParameter("@ChassisNo", DbType.String, vehicle.chassis_number),
                        new ProcedureParameter("@comment", DbType.String, comment ?? string.Empty),
                        new ProcedureParameter(
                            "@captured_by_user_code",
                            DbType.Int16,
                            authorizedByUserId
                        )
                    );
                }
                else
                {
                    await PromoteWithoutLegacyProcedureAsync(
                        connection,
                        transaction,
                        vehicle,
                        authorizedByUserId
                    );
                }

                await UpdateAuthorizationStatusAsync(
                    connection,
                    transaction,
                    schema,
                    tempVmfCode,
                    "Authorized",
                    authorizedByUserId,
                    comment,
                    null,
                    vehicle.vmf_code
                );
                await transaction.CommitAsync();
                committed = true;
            }
            catch
            {
                if (!committed)
                {
                    await transaction.RollbackAsync();
                }

                throw;
            }

            return true;
        });
    }

    public async Task RejectAsync(
        int tempVmfCode,
        int rejectedByUserId,
        string rejectionReason,
        string? comment = null
    )
    {
        if (string.IsNullOrWhiteSpace(rejectionReason))
        {
            throw new ArgumentException("Rejection reason is required", nameof(rejectionReason));
        }

        await WithConnectionAsync(async connection =>
        {
            var schema = await GetSchemaAsync(connection, null);
            if (
                (
                    await QueryAsync(connection, null, schema, tempVmfCode: tempVmfCode)
                ).SingleOrDefault()
                is null
            )
            {
                throw new KeyNotFoundException(
                    $"Vehicle authorization with ID {tempVmfCode} not found"
                );
            }

            await UpdateAuthorizationStatusAsync(
                connection,
                null,
                schema,
                tempVmfCode,
                "Rejected",
                rejectedByUserId,
                comment,
                rejectionReason,
                null
            );
            return true;
        });
    }

    public async Task AddCommentAsync(int tempVmfCode, string comment, int modifiedByUserId)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            throw new ArgumentException("Comment cannot be empty", nameof(comment));
        }

        await WithConnectionAsync(async connection =>
        {
            var schema = await GetSchemaAsync(connection, null);
            var vehicle = (
                await QueryAsync(connection, null, schema, tempVmfCode: tempVmfCode)
            ).SingleOrDefault();
            if (vehicle is null)
            {
                throw new KeyNotFoundException(
                    $"Vehicle authorization with ID {tempVmfCode} not found"
                );
            }

            var timestampedComment = $"[{DateTime.Now:yyyy-MM-dd HH:mm}] {comment.Trim()}";
            var combinedComment = string.IsNullOrWhiteSpace(vehicle.authorization_comment)
                ? timestampedComment
                : $"{vehicle.authorization_comment}\n{timestampedComment}";
            var values = new List<WriteValue>();
            AddValue(
                values,
                schema.Columns,
                "authorization_comment",
                DbType.String,
                combinedComment
            );
            AddValue(values, schema.Columns, "date_updated", DbType.DateTime2, DateTime.Now);
            AddValue(
                values,
                schema.Columns,
                "modified_by_user_code",
                DbType.Int32,
                modifiedByUserId > 0 ? modifiedByUserId : null
            );
            await ExecuteUpdateAsync(connection, null, tempVmfCode, values);

            if (await ProcedureExistsAsync(connection, null, "DEV_INS_PreVehicle_master_Notes"))
            {
                await ExecuteProcedureAsync(
                    connection,
                    null,
                    "DEV_INS_PreVehicle_master_Notes",
                    new ProcedureParameter("@comment", DbType.String, comment.Trim()),
                    new ProcedureParameter(
                        "@chassis_number",
                        DbType.String,
                        vehicle.chassis_number
                    ),
                    new ProcedureParameter("@added_by_user_code", DbType.Int16, modifiedByUserId)
                );
            }

            return true;
        });
    }

    public async Task DeleteAsync(int tempVmfCode, int currentUserId)
    {
        await WithConnectionAsync(async connection =>
        {
            var schema = await GetSchemaAsync(connection, null);
            var values = new List<WriteValue>();
            if (schema.Columns.Contains("is_deleted"))
            {
                AddValue(values, schema.Columns, "is_deleted", DbType.Boolean, true);
            }
            else
            {
                AddValue(values, schema.Columns, "Authority_Status", DbType.String, "Deleted");
            }

            AddValue(values, schema.Columns, "date_updated", DbType.DateTime2, DateTime.Now);
            AddValue(
                values,
                schema.Columns,
                "modified_by_user_code",
                DbType.Int32,
                currentUserId > 0 ? currentUserId : null
            );
            if (
                values.Count == 0
                || await ExecuteUpdateAsync(connection, null, tempVmfCode, values) == 0
            )
            {
                throw new KeyNotFoundException(
                    $"Vehicle authorization with ID {tempVmfCode} not found"
                );
            }

            return true;
        });
    }

    private async Task<T> WithConnectionAsync<T>(Func<DbConnection, Task<T>> operation)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            return await operation(connection);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<VehicleAuthorizationSchema> GetSchemaAsync(
        DbConnection connection,
        DbTransaction? transaction
    )
    {
        var columns = await GetColumnsAsync(connection, transaction, "dbo", PreVehicleTableName);
        if (!columns.Contains("temp_vmf_code"))
        {
            throw new InvalidOperationException(
                "The pre_vehicle_master compatibility table is not available."
            );
        }

        var modelColumns = await GetColumnsAsync(connection, transaction, "dbo", ModelTableName);
        await using var identityCommand = connection.CreateCommand();
        identityCommand.Transaction = transaction;
        identityCommand.CommandText =
            $"SELECT COLUMNPROPERTY(OBJECT_ID(N'[dbo].[{PreVehicleTableName}]'), N'temp_vmf_code', 'IsIdentity')";
        var identityValue = await identityCommand.ExecuteScalarAsync();
        return new VehicleAuthorizationSchema(
            columns,
            modelColumns,
            Convert.ToInt32(identityValue) == 1
        );
    }

    private static async Task<HashSet<string>> GetColumnsAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string schemaName,
        string tableName
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT [COLUMN_NAME]
            FROM [INFORMATION_SCHEMA].[COLUMNS]
            WHERE [TABLE_SCHEMA] = @schema
              AND [TABLE_NAME] = @table
            """;
        AddParameter(command, "@schema", DbType.String, schemaName);
        AddParameter(command, "@table", DbType.String, tableName);

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static async Task<List<PreVehicleMaster>> QueryAsync(
        DbConnection connection,
        DbTransaction? transaction,
        VehicleAuthorizationSchema schema,
        int? tempVmfCode = null,
        string? chassisNumber = null,
        string? status = null,
        IReadOnlyCollection<string>? statuses = null,
        DateTime? startDate = null,
        DateTime? endDate = null
    )
    {
        if (
            status is not null
            && !schema.Columns.Contains("Authority_Status")
            && !string.Equals(status, "Awaiting Authorization", StringComparison.OrdinalIgnoreCase)
        )
        {
            return [];
        }

        if (!string.IsNullOrWhiteSpace(chassisNumber) && !schema.Columns.Contains("chassis_number"))
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var conditions = new List<string> { GetNotDeletedFilter("p", schema.Columns) };
        var joins = string.Empty;

        if (tempVmfCode.HasValue)
        {
            conditions.Add("[p].[temp_vmf_code] = @tempVmfCode");
            AddParameter(command, "@tempVmfCode", DbType.Int32, tempVmfCode.Value);
        }

        if (!string.IsNullOrWhiteSpace(chassisNumber))
        {
            conditions.Add("[p].[chassis_number] = @chassisNumber");
            AddParameter(command, "@chassisNumber", DbType.String, chassisNumber.Trim());
        }

        if (schema.Columns.Contains("Authority_Status"))
        {
            if (status is not null)
            {
                conditions.Add("[p].[Authority_Status] = @authorityStatus");
                AddParameter(command, "@authorityStatus", DbType.String, status);
            }
            else if (statuses is not null && statuses.Count > 0)
            {
                var statusParameters = statuses
                    .Select((_, index) => $"@historyStatus{index}")
                    .ToArray();
                conditions.Add(
                    $"[p].[Authority_Status] IN ({string.Join(", ", statusParameters)})"
                );
                for (var index = 0; index < statuses.Count; index++)
                {
                    AddParameter(
                        command,
                        statusParameters[index],
                        DbType.String,
                        statuses.ElementAt(index)
                    );
                }
            }
        }

        var dateColumn =
            schema.Columns.Contains("authorization_date") ? "authorization_date"
            : schema.Columns.Contains("date_created") ? "date_created"
            : null;
        if (startDate.HasValue && dateColumn is not null)
        {
            conditions.Add($"[p].[{dateColumn}] >= @startDate");
            AddParameter(command, "@startDate", DbType.DateTime2, startDate.Value);
        }

        if (endDate.HasValue && dateColumn is not null)
        {
            conditions.Add($"[p].[{dateColumn}] <= @endDate");
            AddParameter(command, "@endDate", DbType.DateTime2, endDate.Value);
        }

        var modelProjection =
            schema.ModelColumns.Contains("model_code")
            && schema.ModelColumns.Contains("model_description")
                ? "[m].[model_description] AS [model_description]"
                : "CAST(NULL AS nvarchar(255)) AS [model_description]";
        if (
            schema.ModelColumns.Contains("model_code")
            && schema.ModelColumns.Contains("model_description")
        )
        {
            joins = "LEFT JOIN [dbo].[model] AS [m] ON [m].[model_code] = [p].[model_code]";
        }

        var projection = string.Join(
            ", ",
            SelectedColumns
                .Select(column => GetProjection("p", schema.Columns, column))
                .Append(modelProjection)
        );
        var orderColumn =
            schema.Columns.Contains("authorization_date") ? "authorization_date"
            : schema.Columns.Contains("date_created") ? "date_created"
            : "temp_vmf_code";
        command.CommandText =
            $"SELECT {projection} FROM [dbo].[{PreVehicleTableName}] AS [p] {joins} WHERE {string.Join(" AND ", conditions)} ORDER BY [p].[{orderColumn}] DESC, [p].[temp_vmf_code] DESC";

        var rows = new List<PreVehicleMaster>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(Map(reader));
        }

        return rows;
    }

    private static PreVehicleMaster Map(DbDataReader reader) =>
        new()
        {
            temp_vmf_code = ReadInt32(reader, "temp_vmf_code") ?? 0,
            fleet_number = ReadString(reader, "fleet_number"),
            registration_number = ReadString(reader, "registration_number"),
            replaced_gg_number = ReadString(reader, "replaced_gg_number"),
            model_code = ReadInt16(reader, "model_code") ?? 0,
            Model = ReadModel(reader),
            colour = ReadString(reader, "colour"),
            year_manufactured = ReadInt16(reader, "year_manufactured"),
            chassis_number = ReadString(reader, "chassis_number"),
            engine_number = ReadString(reader, "engine_number"),
            take_on_odo = ReadInt32(reader, "take_on_odo"),
            take_on_date = ReadDateTime(reader, "take_on_date"),
            location_code = ReadInt16(reader, "location_code"),
            vehicle_status_code = ReadInt16(reader, "vehicle_status_code"),
            vehicle_status_date = ReadDateTime(reader, "vehicle_status_date"),
            type_code = ReadInt16(reader, "type_code"),
            vs_code = ReadByte(reader, "vs_code"),
            comment = ReadString(reader, "comment"),
            purchase_amount = ReadDecimal(reader, "purchase_amount"),
            purchase_from = ReadString(reader, "purchase_from"),
            purchase_date = ReadDateTime(reader, "purchase_date"),
            captured_by_user_code = ReadInt16(reader, "captured_by_user_code"),
            action_user_access_code = ReadInt16(reader, "action_user_access_code"),
            site_code = ReadInt16(reader, "site_code"),
            Authority_Status = ReadString(reader, "Authority_Status") ?? "Awaiting Authorization",
            captured_date = ReadDateTime(reader, "captured_date"),
            printed = ReadString(reader, "printed"),
            invoice_number = ReadString(reader, "invoice_number"),
            gp_number = ReadString(reader, "gp_number"),
            Fleet_Notes = ReadString(reader, "Fleet_Notes"),
            damage_status = ReadString(reader, "damage_status"),
            damages_comment = ReadString(reader, "damages_comment"),
            authorized_by_user_code = ReadInt32(reader, "authorized_by_user_code"),
            authorization_date = ReadDateTime(reader, "authorization_date"),
            rejection_reason = ReadString(reader, "rejection_reason"),
            authorization_comment = ReadString(reader, "authorization_comment"),
            vmf_code = ReadInt32(reader, "vmf_code"),
            date_created = ReadDateTime(reader, "date_created") ?? DateTime.MinValue,
            date_updated = ReadDateTime(reader, "date_updated"),
            created_by_user_code =
                ReadInt32(reader, "created_by_user_code")
                ?? ReadInt16(reader, "captured_by_user_code"),
            modified_by_user_code = ReadInt32(reader, "modified_by_user_code"),
            is_deleted = ReadBoolean(reader, "is_deleted") ?? false,
        };

    private static FIS.Core.Domain.Entities.Model? ReadModel(DbDataReader reader)
    {
        var description = ReadString(reader, "model_description");
        return string.IsNullOrWhiteSpace(description)
            ? null
            : new FIS.Core.Domain.Entities.Model { model_description = description };
    }

    private static async Task<int> InsertRowAsync(
        DbConnection connection,
        DbTransaction transaction,
        VehicleAuthorizationSchema schema,
        PreVehicleMaster vehicle,
        int currentUserId
    )
    {
        var values = BuildValues(vehicle, schema.Columns, currentUserId, includeCreateAudit: true);
        if (!schema.IsIdentity)
        {
            var code = await GetNextCodeAsync(connection, transaction);
            AddValue(values, schema.Columns, "temp_vmf_code", DbType.Int32, code);
            await ExecuteInsertAsync(connection, transaction, values, outputKey: false);
            return code;
        }

        return await ExecuteInsertAsync(connection, transaction, values, outputKey: true);
    }

    private static async Task EnsureNoVehicleDuplicateAsync(
        DbConnection connection,
        DbTransaction transaction,
        PreVehicleMaster vehicle
    )
    {
        var columns = await GetColumnsAsync(connection, transaction, "dbo", VehicleTableName);
        var checks = new List<(string Column, string Parameter, object? Value)>();

        if (
            !string.IsNullOrWhiteSpace(vehicle.chassis_number) && columns.Contains("chassis_number")
        )
        {
            checks.Add(("chassis_number", "@chassisNumber", vehicle.chassis_number.Trim()));
        }

        var engineColumn =
            columns.Contains("engine_number_1") ? "engine_number_1"
            : columns.Contains("engine_number") ? "engine_number"
            : null;
        if (!string.IsNullOrWhiteSpace(vehicle.engine_number) && engineColumn is not null)
        {
            checks.Add((engineColumn, "@engineNumber", vehicle.engine_number.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(vehicle.fleet_number) && columns.Contains("fleet_number"))
        {
            checks.Add(("fleet_number", "@fleetNumber", vehicle.fleet_number.Trim()));
        }

        if (
            !string.IsNullOrWhiteSpace(vehicle.gp_number) && columns.Contains("registration_number")
        )
        {
            checks.Add(("registration_number", "@registrationNumber", vehicle.gp_number.Trim()));
        }

        if (checks.Count == 0 || !columns.Contains("vmf_code"))
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"SELECT TOP (1) [v].[vmf_code] FROM [dbo].[{VehicleTableName}] AS [v] WHERE {GetNotDeletedFilter("v", columns)} AND ({string.Join(" OR ", checks.Select(check => $"[v].[{check.Column}] = {check.Parameter}"))})";
        foreach (var check in checks)
        {
            AddParameter(command, check.Parameter, DbType.String, check.Value);
        }

        var existingCode = await command.ExecuteScalarAsync();
        if (existingCode is not null and not DBNull)
        {
            throw new InvalidOperationException(
                $"A vehicle already exists with one of the supplied identifying values (VMF {existingCode})."
            );
        }
    }

    private static async Task UpdateRowAsync(
        DbConnection connection,
        DbTransaction? transaction,
        VehicleAuthorizationSchema schema,
        int tempVmfCode,
        PreVehicleMaster vehicle,
        int currentUserId
    )
    {
        var values = BuildValues(vehicle, schema.Columns, currentUserId, includeCreateAudit: false);
        if (
            values.Count == 0
            || await ExecuteUpdateAsync(connection, transaction, tempVmfCode, values) == 0
        )
        {
            throw new KeyNotFoundException(
                $"Vehicle authorization with ID {tempVmfCode} not found"
            );
        }
    }

    private static List<WriteValue> BuildValues(
        PreVehicleMaster vehicle,
        IReadOnlySet<string> columns,
        int currentUserId,
        bool includeCreateAudit
    )
    {
        var takeOnDate = vehicle.take_on_date ?? DateTime.Now;
        var purchaseDate = vehicle.purchase_date ?? takeOnDate;
        var values = new List<WriteValue>();
        AddValue(values, columns, "fleet_number", DbType.String, vehicle.fleet_number);
        AddValue(
            values,
            columns,
            "registration_number",
            DbType.String,
            vehicle.registration_number
        );
        AddValue(values, columns, "replaced_gg_number", DbType.String, vehicle.replaced_gg_number);
        AddValue(values, columns, "model_code", DbType.Int16, vehicle.model_code);
        AddValue(values, columns, "colour", DbType.String, vehicle.colour ?? "UNKNOWN");
        AddValue(values, columns, "year_manufactured", DbType.Int16, vehicle.year_manufactured);
        AddValue(values, columns, "chassis_number", DbType.String, vehicle.chassis_number);
        AddValue(values, columns, "engine_number", DbType.String, vehicle.engine_number);
        AddValue(values, columns, "take_on_odo", DbType.Int32, vehicle.take_on_odo ?? 0);
        AddValue(values, columns, "take_on_date", DbType.DateTime2, takeOnDate);
        AddValue(values, columns, "location_code", DbType.Int16, vehicle.location_code ?? 0);
        AddValue(
            values,
            columns,
            "vehicle_status_code",
            DbType.Int16,
            vehicle.vehicle_status_code ?? 0
        );
        AddValue(
            values,
            columns,
            "vehicle_status_date",
            DbType.DateTime2,
            vehicle.vehicle_status_date ?? takeOnDate
        );
        AddValue(values, columns, "type_code", DbType.Int16, vehicle.type_code ?? 1);
        AddValue(values, columns, "vs_code", DbType.Byte, vehicle.vs_code ?? 1);
        AddValue(values, columns, "comment", DbType.String, vehicle.comment ?? string.Empty);
        AddValue(values, columns, "purchase_amount", DbType.Decimal, vehicle.purchase_amount ?? 0m);
        AddValue(
            values,
            columns,
            "purchase_from",
            DbType.String,
            vehicle.purchase_from ?? string.Empty
        );
        AddValue(values, columns, "purchase_date", DbType.DateTime2, purchaseDate);
        AddValue(
            values,
            columns,
            "captured_by_user_code",
            DbType.Int16,
            vehicle.captured_by_user_code ?? ToShortUserCode(currentUserId)
        );
        AddValue(
            values,
            columns,
            "action_user_access_code",
            DbType.Int16,
            vehicle.action_user_access_code ?? ToShortUserCode(currentUserId)
        );
        AddValue(values, columns, "site_code", DbType.Int16, vehicle.site_code);
        AddValue(values, columns, "Authority_Status", DbType.String, "Awaiting Authorization");
        AddValue(
            values,
            columns,
            "captured_date",
            DbType.DateTime2,
            vehicle.captured_date ?? DateTime.Now
        );
        AddValue(values, columns, "printed", DbType.String, vehicle.printed ?? "N");
        AddValue(values, columns, "invoice_number", DbType.String, vehicle.invoice_number);
        AddValue(values, columns, "gp_number", DbType.String, vehicle.gp_number);
        AddValue(values, columns, "Fleet_Notes", DbType.String, vehicle.Fleet_Notes);
        AddValue(values, columns, "damage_status", DbType.String, vehicle.damage_status);
        AddValue(values, columns, "damages_comment", DbType.String, vehicle.damages_comment);

        if (includeCreateAudit)
        {
            AddValue(
                values,
                columns,
                "date_created",
                DbType.DateTime2,
                vehicle.date_created == default ? DateTime.Now : vehicle.date_created
            );
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
            AddValue(values, columns, "date_updated", DbType.DateTime2, DateTime.Now);
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

    private static async Task UpdateAuthorizationStatusAsync(
        DbConnection connection,
        DbTransaction? transaction,
        VehicleAuthorizationSchema schema,
        int tempVmfCode,
        string status,
        int actingUserId,
        string? comment,
        string? rejectionReason,
        int? vmfCode
    )
    {
        var values = new List<WriteValue>();
        AddValue(values, schema.Columns, "Authority_Status", DbType.String, status);
        AddValue(values, schema.Columns, "authorized_by_user_code", DbType.Int32, actingUserId);
        AddValue(values, schema.Columns, "authorization_date", DbType.DateTime2, DateTime.Now);
        AddValue(values, schema.Columns, "authorization_comment", DbType.String, comment);
        AddValue(values, schema.Columns, "rejection_reason", DbType.String, rejectionReason);
        if (vmfCode.HasValue)
        {
            // A legacy promotion procedure may allocate or populate the
            // vehicle identifier itself. Never overwrite that value with
            // NULL when the pre-vehicle row did not have it beforehand.
            AddValue(values, schema.Columns, "vmf_code", DbType.Int32, vmfCode);
        }
        AddValue(values, schema.Columns, "date_updated", DbType.DateTime2, DateTime.Now);
        AddValue(values, schema.Columns, "modified_by_user_code", DbType.Int32, actingUserId);
        if (
            values.Count == 0
            || await ExecuteUpdateAsync(connection, transaction, tempVmfCode, values) == 0
        )
        {
            throw new KeyNotFoundException(
                $"Vehicle authorization with ID {tempVmfCode} not found"
            );
        }
    }

    private static async Task WriteLegacyCaptureSideEffectsAsync(
        DbConnection connection,
        DbTransaction transaction,
        int tempVmfCode,
        PreVehicleMaster vehicle,
        int currentUserId
    )
    {
        if (
            await ProcedureExistsAsync(connection, transaction, "DEV_INS_PreVehicle_master_Notes")
            && !string.IsNullOrWhiteSpace(vehicle.comment)
        )
        {
            await ExecuteProcedureAsync(
                connection,
                transaction,
                "DEV_INS_PreVehicle_master_Notes",
                new ProcedureParameter("@comment", DbType.String, vehicle.comment),
                new ProcedureParameter("@chassis_number", DbType.String, vehicle.chassis_number),
                new ProcedureParameter("@added_by_user_code", DbType.Int16, currentUserId)
            );
        }

        if (
            await ProcedureExistsAsync(connection, transaction, "DEV_INS_Vehicle_Damages")
            && (
                !string.IsNullOrWhiteSpace(vehicle.damage_status)
                || !string.IsNullOrWhiteSpace(vehicle.damages_comment)
            )
        )
        {
            await ExecuteProcedureAsync(
                connection,
                transaction,
                "DEV_INS_Vehicle_Damages",
                new ProcedureParameter("@chassisno", DbType.String, vehicle.chassis_number),
                new ProcedureParameter(
                    "@comment",
                    DbType.String,
                    vehicle.damages_comment ?? string.Empty
                ),
                new ProcedureParameter("@userid", DbType.Int16, currentUserId),
                new ProcedureParameter("@status", DbType.String, vehicle.damage_status ?? "N")
            );
        }

        if (
            await ProcedureExistsAsync(connection, transaction, "DEV_INS_temp_fleet_notes")
            && !string.IsNullOrWhiteSpace(vehicle.Fleet_Notes)
        )
        {
            await ExecuteProcedureAsync(
                connection,
                transaction,
                "DEV_INS_temp_fleet_notes",
                new ProcedureParameter("@chassisno", DbType.String, vehicle.chassis_number),
                new ProcedureParameter("@fleet_notes", DbType.String, vehicle.Fleet_Notes)
            );
        }

        if (await ProcedureExistsAsync(connection, transaction, "DEV_INS_NewVehicle_Extras"))
        {
            foreach (var extraCode in vehicle.ExtraCodes.Distinct())
            {
                await ExecuteProcedureAsync(
                    connection,
                    transaction,
                    "DEV_INS_NewVehicle_Extras",
                    new ProcedureParameter("@chassis_no", DbType.String, vehicle.chassis_number),
                    new ProcedureParameter("@extra_code", DbType.Int16, extraCode)
                );
            }
        }

        if (
            vehicle.MaintenanceTypeCode is > 0
            && await ProcedureExistsAsync(
                connection,
                transaction,
                "DEV_UPD_VehicleMaintenanceOptions"
            )
        )
        {
            await ExecuteProcedureAsync(
                connection,
                transaction,
                "DEV_UPD_VehicleMaintenanceOptions",
                new ProcedureParameter("@vmfCode", DbType.Int32, 0),
                new ProcedureParameter("@tempVmfCode", DbType.Int32, tempVmfCode),
                new ProcedureParameter("@maintType", DbType.Int16, vehicle.MaintenanceTypeCode),
                new ProcedureParameter(
                    "@startDate",
                    DbType.String,
                    vehicle.MaintenanceStartDate?.ToString("yyyy/MM/dd")
                ),
                new ProcedureParameter(
                    "@period",
                    DbType.Int32,
                    vehicle.MaintenancePeriodMonths ?? 0
                ),
                new ProcedureParameter("@kilos", DbType.Int32, vehicle.MaintenanceKilos ?? 0),
                new ProcedureParameter("@maintValue", DbType.Decimal, vehicle.MaintenanceValue),
                new ProcedureParameter("@userCode", DbType.Int32, currentUserId)
            );
        }
    }

    private static async Task PromoteWithoutLegacyProcedureAsync(
        DbConnection connection,
        DbTransaction transaction,
        PreVehicleMaster preVehicle,
        int actingUserId
    )
    {
        var columns = await GetColumnsAsync(connection, transaction, "dbo", VehicleTableName);
        if (!columns.Contains("vmf_code"))
        {
            throw new InvalidOperationException(
                "The vehicle_master compatibility table is not available for authorization."
            );
        }

        var existingCode = await FindVehicleCodeAsync(
            connection,
            transaction,
            columns,
            preVehicle.chassis_number
        );
        if (existingCode.HasValue)
        {
            preVehicle.vmf_code = existingCode.Value;
            return;
        }

        var fleetNumber = preVehicle.fleet_number;
        if (string.IsNullOrWhiteSpace(fleetNumber))
        {
            fleetNumber = await AllocateGgNumberAsync(connection, transaction, columns);
        }

        var values = new List<WriteValue>();
        AddVehicleValue(values, columns, "model_code", DbType.Int16, preVehicle.model_code);
        AddVehicleValue(values, columns, "type_code", DbType.Int16, preVehicle.type_code ?? 1);
        AddVehicleValue(
            values,
            columns,
            "vehicle_status_code",
            DbType.Int16,
            preVehicle.vehicle_status_code ?? 0
        );
        AddVehicleValue(
            values,
            columns,
            "location_code",
            DbType.Int16,
            preVehicle.location_code ?? 0
        );
        AddVehicleValue(values, columns, "fleet_number", DbType.String, fleetNumber);
        AddVehicleValue(
            values,
            columns,
            "registration_number",
            DbType.String,
            preVehicle.registration_number ?? fleetNumber
        );
        AddVehicleValue(
            values,
            columns,
            "take_on_date",
            DbType.DateTime2,
            preVehicle.take_on_date ?? DateTime.Now
        );
        AddVehicleValue(values, columns, "take_on_odo", DbType.Int32, preVehicle.take_on_odo ?? 0);
        AddVehicleValue(values, columns, "current_odo", DbType.Int32, preVehicle.take_on_odo ?? 0);
        AddVehicleValue(
            values,
            columns,
            "engine_number_1",
            DbType.String,
            preVehicle.engine_number
        );
        AddVehicleValue(
            values,
            columns,
            "chassis_number",
            DbType.String,
            preVehicle.chassis_number
        );
        AddVehicleValue(
            values,
            columns,
            "year_manufactured",
            DbType.Int16,
            preVehicle.year_manufactured
        );
        AddVehicleValue(
            values,
            columns,
            "purchase_date",
            DbType.DateTime2,
            preVehicle.purchase_date
        );
        AddVehicleValue(
            values,
            columns,
            "purchase_amount",
            DbType.Decimal,
            preVehicle.purchase_amount
        );
        AddVehicleValue(values, columns, "purchased_from", DbType.String, preVehicle.purchase_from);
        AddVehicleValue(values, columns, "colour", DbType.String, preVehicle.colour);
        AddVehicleValue(
            values,
            columns,
            "previos_gg_number",
            DbType.String,
            preVehicle.replaced_gg_number
        );
        AddVehicleValue(
            values,
            columns,
            "vehicle_status_date",
            DbType.DateTime2,
            preVehicle.vehicle_status_date ?? preVehicle.take_on_date
        );
        AddVehicleValue(
            values,
            columns,
            "user_access_code",
            DbType.Int16,
            preVehicle.captured_by_user_code ?? ToShortUserCode(actingUserId)
        );
        AddVehicleValue(
            values,
            columns,
            "captured_date",
            DbType.DateTime2,
            preVehicle.captured_date ?? DateTime.Now
        );
        AddVehicleValue(values, columns, "vs_code", DbType.Byte, preVehicle.vs_code);
        AddVehicleValue(
            values,
            columns,
            "invoice_number",
            DbType.String,
            preVehicle.invoice_number
        );
        AddVehicleValue(values, columns, "veh_site_code", DbType.Int16, preVehicle.site_code);
        AddVehicleValue(values, columns, "Site_code", DbType.Int16, preVehicle.site_code);
        AddVehicleValue(values, columns, "initial_site_code", DbType.Int16, preVehicle.site_code);
        AddVehicleValue(values, columns, "temp_vmf_code", DbType.Int32, preVehicle.temp_vmf_code);
        AddVehicleValue(values, columns, "date_created", DbType.DateTime2, DateTime.Now);
        AddVehicleValue(values, columns, "created_by_user_code", DbType.Int32, actingUserId);
        AddVehicleValue(values, columns, "is_deleted", DbType.Boolean, false);

        var vmfCode = await ExecuteVehicleInsertAsync(connection, transaction, values);
        preVehicle.vmf_code = vmfCode;
        if (string.IsNullOrWhiteSpace(preVehicle.fleet_number))
        {
            preVehicle.fleet_number = fleetNumber;
        }
    }

    private static async Task<int?> FindVehicleCodeAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlySet<string> columns,
        string? chassisNumber
    )
    {
        if (!columns.Contains("chassis_number") || string.IsNullOrWhiteSpace(chassisNumber))
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"SELECT TOP (1) [vmf_code] FROM [dbo].[{VehicleTableName}] WHERE [chassis_number] = @chassisNumber";
        AddParameter(command, "@chassisNumber", DbType.String, chassisNumber);
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? null : Convert.ToInt32(result);
    }

    private static async Task<string?> AllocateGgNumberAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlySet<string> vehicleColumns
    )
    {
        if (!vehicleColumns.Contains("fleet_number"))
        {
            return null;
        }

        var numberColumns = await GetColumnsAsync(
            connection,
            transaction,
            "dbo",
            GgNumberTableName
        );
        if (!numberColumns.Contains("GG_Number"))
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var notDeleted = numberColumns.Contains("is_deleted")
            ? " AND ([n].[is_deleted] = 0 OR [n].[is_deleted] IS NULL)"
            : string.Empty;
        command.CommandText = $"""
            SELECT TOP (1) [n].[GG_Number]
            FROM [dbo].[{GgNumberTableName}] AS [n]
            WHERE [n].[GG_Number] IS NOT NULL{notDeleted}
              AND NOT EXISTS (
                  SELECT 1
                  FROM [dbo].[{VehicleTableName}] AS [v]
                  WHERE [v].[fleet_number] = [n].[GG_Number]
              )
            ORDER BY [n].[GG_Number]
            """;
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? null : result.ToString();
    }

    private static async Task<int> ExecuteVehicleInsertAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlyList<WriteValue> values
    )
    {
        var insertValues = values
            .Where(value =>
                !string.Equals(value.Column, "vmf_code", StringComparison.OrdinalIgnoreCase)
            )
            .ToArray();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"INSERT INTO [dbo].[{VehicleTableName}] ({string.Join(", ", insertValues.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[vmf_code] VALUES ({string.Join(", ", insertValues.Select(value => value.Parameter))})";
        AddParameters(command, insertValues);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> ExecuteInsertAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlyList<WriteValue> values,
        bool outputKey
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = outputKey
            ? $"INSERT INTO [dbo].[{PreVehicleTableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[temp_vmf_code] VALUES ({string.Join(", ", values.Select(value => value.Parameter))})"
            : $"INSERT INTO [dbo].[{PreVehicleTableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
        AddParameters(command, values);
        if (outputKey)
        {
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        await command.ExecuteNonQueryAsync();
        return 0;
    }

    private static async Task<int> ExecuteUpdateAsync(
        DbConnection connection,
        DbTransaction? transaction,
        int tempVmfCode,
        IReadOnlyList<WriteValue> values
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"UPDATE [dbo].[{PreVehicleTableName}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [temp_vmf_code] = @tempVmfCode";
        AddParameters(command, values);
        AddParameter(command, "@tempVmfCode", DbType.Int32, tempVmfCode);
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> GetNextCodeAsync(
        DbConnection connection,
        DbTransaction transaction
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"SELECT COALESCE(MAX([temp_vmf_code]), 0) + 1 FROM [dbo].[{PreVehicleTableName}] WITH (UPDLOCK, HOLDLOCK)";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<bool> ProcedureExistsAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string procedureName
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT COUNT(1)
            FROM [INFORMATION_SCHEMA].[ROUTINES]
            WHERE [ROUTINE_SCHEMA] = 'dbo'
              AND [ROUTINE_NAME] = @procedureName
              AND [ROUTINE_TYPE] = 'PROCEDURE'
            """;
        AddParameter(command, "@procedureName", DbType.String, procedureName);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    private static async Task ExecuteProcedureAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string procedureName,
        params ProcedureParameter[] parameters
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = $"dbo.{procedureName}";
        foreach (var parameter in parameters)
        {
            AddParameter(command, parameter.Name, parameter.Type, parameter.Value);
        }

        await command.ExecuteNonQueryAsync();
    }

    private static string GetProjection(string alias, IReadOnlySet<string> columns, string column)
    {
        if (columns.Contains(column))
        {
            return $"[{alias}].[{column}] AS [{column}]";
        }

        var sqlType =
            StringColumns.Contains(column, StringComparer.OrdinalIgnoreCase) ? "nvarchar(4000)"
            : DateColumns.Contains(column, StringComparer.OrdinalIgnoreCase) ? "datetime2"
            : DecimalColumns.Contains(column, StringComparer.OrdinalIgnoreCase) ? "decimal(18, 2)"
            : ByteColumns.Contains(column, StringComparer.OrdinalIgnoreCase) ? "tinyint"
            : ShortColumns.Contains(column, StringComparer.OrdinalIgnoreCase) ? "smallint"
            : IntegerColumns.Contains(column, StringComparer.OrdinalIgnoreCase) ? "int"
            : column.Equals("is_deleted", StringComparison.OrdinalIgnoreCase) ? "bit"
            : "nvarchar(4000)";
        return $"CAST(NULL AS {sqlType}) AS [{column}]";
    }

    private static string GetNotDeletedFilter(string alias, IReadOnlySet<string> columns)
    {
        var prefix = string.IsNullOrWhiteSpace(alias) ? string.Empty : $"[{alias}].";
        return columns.Contains("is_deleted")
            ? $"({prefix}[is_deleted] = 0 OR {prefix}[is_deleted] IS NULL)"
            : "1 = 1";
    }

    private static void AddValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> columns,
        string column,
        DbType type,
        object? value
    )
    {
        if (columns.Contains(column))
        {
            values.Add(new WriteValue(column, $"@{column}", type, value));
        }
    }

    private static void AddVehicleValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> columns,
        string column,
        DbType type,
        object? value
    ) => AddValue(values, columns, column, type, value);

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

    private static short? ToShortUserCode(int userId) =>
        userId is > 0 and <= short.MaxValue ? (short)userId : null;

    private static int? FindOrdinal(DbDataReader reader, params string[] columnNames)
    {
        foreach (var columnName in columnNames)
        {
            try
            {
                return reader.GetOrdinal(columnName);
            }
            catch (IndexOutOfRangeException)
            {
                // The legacy procedure's result-set casing/alias varies by
                // database version; continue with the next known alias.
            }
        }

        return null;
    }

    private static string? ReadString(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : reader[column]?.ToString();

    private static int? ReadInt32(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToInt32(reader[column]);

    private static short? ReadInt16(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToInt16(reader[column]);

    private static byte? ReadByte(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToByte(reader[column]);

    private static decimal? ReadDecimal(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToDecimal(reader[column]);

    private static DateTime? ReadDateTime(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToDateTime(reader[column]);

    private static bool? ReadBoolean(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToBoolean(reader[column]);

    private sealed record VehicleAuthorizationSchema(
        HashSet<string> Columns,
        HashSet<string> ModelColumns,
        bool IsIdentity
    );

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);

    private sealed record ProcedureParameter(string Name, DbType Type, object? Value);
}
