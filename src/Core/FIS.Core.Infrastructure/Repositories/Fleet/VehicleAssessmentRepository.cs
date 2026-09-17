using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

public class VehicleAssessmentRepository : IVehicleAssessmentRepository
{
    private const string TableName = "vehicle_assessment";

    // These are the columns present in the legacy assessment table. They are
    // selected dynamically because the modern EF entity intentionally leaves
    // the inspection checklist fields unmapped; a static EF query would lose
    // those values (or fail when a later optional column is absent).
    private static readonly string[] LegacyColumns =
    [
        "vehicle_assessment_code",
        "vmf_code",
        "spare_wheel",
        "jack",
        "wheel_spanner",
        "wheel_lock_key",
        "fuel_card",
        "license_disc",
        "cof_disc",
        "radio",
        "gear_lock",
        "logbook",
        "logbook_start_number",
        "logbook_end_number",
        "fire_extinguisher",
        "smash_and_grab",
        "tracker",
        "sets_of_keys",
        "damages",
        "assessment_notes",
        "capture_date",
        "modified_date",
        "user_access_code",
        "user_access_name",
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private static readonly string[] RequiredColumns =
    [
        "vehicle_assessment_code",
        "vmf_code",
    ];

    private readonly FisDbContext _context;

    public VehicleAssessmentRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<VehicleAssessment?> GetByIdAsync(int assessmentCode)
    {
        var procedure = await ResolveProcedureAsync("DEV_SEL_Vehicle_Assessment");
        if (procedure is not null)
        {
            EnsureProcedureContract(
                "DEV_SEL_Vehicle_Assessment",
                procedure,
                SelectProcedureParameters
            );
            return await ExecuteSelectProcedureAsync(assessmentCode);
        }

        return (
            await QueryAsync(
                "[va].[vehicle_assessment_code] = @assessmentCode",
                command => AddParameter(command, "@assessmentCode", DbType.Int32, assessmentCode)
            )
        ).SingleOrDefault();
    }

    public async Task<IEnumerable<VehicleAssessment>> GetAllAsync()
    {
        return await QueryAsync(orderBy: "[va].[vehicle_assessment_code] ASC");
    }

    public async Task<VehicleAssessment?> GetByVehicleAsync(int vmfCode)
    {
        return (
            await QueryAsync(
                "[va].[vmf_code] = @vmfCode",
                command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode),
                orderBy: "[va].[capture_date] DESC, [va].[vehicle_assessment_code] DESC"
            )
        ).FirstOrDefault();
    }

    public async Task<IEnumerable<VehicleAssessment>> GetRecentAssessmentsAsync(int days)
    {
        var cutoffDate = DateTime.Now.AddDays(-Math.Max(0, days));
        return await QueryAsync(
            "[va].[capture_date] >= @cutoffDate",
            command => AddParameter(command, "@cutoffDate", DbType.DateTime, cutoffDate),
            orderBy: "[va].[capture_date] DESC, [va].[vehicle_assessment_code] DESC"
        );
    }

    public async Task<VehicleAssessment> CreateAsync(
        VehicleAssessment assessment,
        int currentUserId
    )
    {
        ArgumentNullException.ThrowIfNull(assessment);
        ValidateAssessment(assessment);

        var procedure = await ResolveProcedureAsync("DEV_INS_Vehicle_Assessment");
        if (procedure is not null)
        {
            EnsureProcedureContract(
                "DEV_INS_Vehicle_Assessment",
                procedure,
                InsertProcedureParameters
            );
            assessment.vehicle_assessment_code = await ExecuteInsertProcedureAsync(
                assessment,
                currentUserId
            );
        }
        else
        {
            throw new NotSupportedException(
                "The legacy vehicle-assessment insert procedure is unavailable; no partial EF fallback was run."
            );
        }

        return await GetByIdAsync(assessment.vehicle_assessment_code)
            ?? throw new InvalidOperationException(
                "The vehicle assessment was written but could not be reloaded."
            );
    }

    public async Task<VehicleAssessment> UpdateAsync(
        VehicleAssessment assessment,
        int currentUserId
    )
    {
        if (assessment == null)
            throw new ArgumentNullException(nameof(assessment));

        var existing = await GetByIdAsync(assessment.vehicle_assessment_code)
            ?? throw new KeyNotFoundException(
                $"Vehicle assessment with code {assessment.vehicle_assessment_code} was not found."
            );
        if (assessment.vmf_code is > 0 && assessment.vmf_code != existing.vmf_code)
            throw new InvalidOperationException(
                "The vehicle for an existing assessment cannot be changed."
            );
        assessment.vmf_code = existing.vmf_code;
        ValidateAssessment(assessment, requireVehicleCode: false);
        var procedure = await ResolveProcedureAsync("DEV_UPD_Vehicle_Assessment");
        if (procedure is not null)
        {
            EnsureProcedureContract(
                "DEV_UPD_Vehicle_Assessment",
                procedure,
                UpdateProcedureParameters
            );
            await ExecuteUpdateProcedureAsync(assessment, currentUserId);
        }
        else
        {
            throw new NotSupportedException(
                "The legacy vehicle-assessment update procedure is unavailable; no partial EF fallback was run."
            );
        }

        return await GetByIdAsync(assessment.vehicle_assessment_code)
            ?? throw new InvalidOperationException(
                "The vehicle assessment was updated but could not be reloaded."
            );
    }

    public Task DeleteAsync(int assessmentCode, int currentUserId) =>
        Task.FromException(
            new NotSupportedException(
                "Legacy vehicle assessments do not expose a delete procedure. Keep the inspection record for audit history."
            )
        );

    private static readonly string[] InsertProcedureParameters =
    [
        "@vehicle_assessment_code", "@vmf_code", "@spare_wheel", "@jack",
        "@wheel_spanner", "@wheel_lock_key", "@fuel_card", "@license_disc",
        "@cof_disc", "@radio", "@gear_lock", "@logbook", "@logbook_start_number",
        "@logbook_end_number", "@fire_extinguisher", "@smash_and_grab", "@tracker",
        "@sets_of_keys", "@damages", "@assessment_notes", "@user_access_code",
    ];

    private static readonly string[] UpdateProcedureParameters =
    [
        "@vehicle_assessment_code", "@spare_wheel", "@jack", "@wheel_spanner",
        "@wheel_lock_key", "@fuel_card", "@license_disc", "@cof_disc", "@radio",
        "@gear_lock", "@logbook", "@logbook_start_number", "@logbook_end_number",
        "@fire_extinguisher", "@smash_and_grab", "@tracker", "@sets_of_keys", "@damages",
        "@assessment_notes", "@user_access_code",
    ];

    private static readonly string[] SelectProcedureParameters = ["@vehicle_assessment_code"];

    private async Task<VehicleAssessment?> ExecuteSelectProcedureAsync(int assessmentCode)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "[dbo].[DEV_SEL_Vehicle_Assessment]";
            AddParameter(command, "@vehicle_assessment_code", DbType.Int32, assessmentCode);
            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapAssessment(reader) : null;
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
        Justification = "The table and projection identifiers come only from fixed legacy allowlists; predicates and values are parameterized."
    )]
    private async Task<List<VehicleAssessment>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null,
        string? orderBy = null
    )
    {
        var columns = await GetTableColumnsAsync();
        if (columns.Count == 0)
        {
            return [];
        }

        var missing = RequiredColumns.Where(column => !columns.Contains(column)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"The vehicle_assessment compatibility table is missing required columns: {string.Join(", ", missing)}"
            );
        }

        var projection = LegacyColumns
            .Where(columns.Contains)
            .Select(column => $"[va].[{column}] AS [{column}]")
            .ToArray();
        var conditions = new List<string>();
        if (columns.Contains("is_deleted"))
        {
            conditions.Add("([va].[is_deleted] = 0 OR [va].[is_deleted] IS NULL)");
        }
        if (!string.IsNullOrWhiteSpace(predicate))
        {
            conditions.Add(predicate);
        }

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"SELECT {string.Join(", ", projection)} FROM [dbo].[{TableName}] AS [va]"
                + (conditions.Count > 0 ? $" WHERE {string.Join(" AND ", conditions)}" : string.Empty)
                + $" ORDER BY {orderBy ?? "[va].[vehicle_assessment_code] ASC"}";
            configure?.Invoke(command);

            var results = new List<VehicleAssessment>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapAssessment(reader));
            }

            return results;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<HashSet<string>> GetTableColumnsAsync()
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
                SELECT [COLUMN_NAME]
                FROM [INFORMATION_SCHEMA].[COLUMNS]
                WHERE [TABLE_SCHEMA] = @schema
                  AND [TABLE_NAME] = @table
                """;
            AddParameter(command, "@schema", DbType.String, "dbo");
            AddParameter(command, "@table", DbType.String, TableName);

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(0));
            }

            return columns;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static VehicleAssessment MapAssessment(DbDataReader reader)
    {
        var captureDate = ReadDateTime(reader, "capture_date");
        var modifiedDate = ReadDateTime(reader, "modified_date");
        var notes = ReadString(reader, "assessment_notes");
        var userAccessCode = ReadInt16(reader, "user_access_code");

        return new VehicleAssessment
        {
            vehicle_assessment_code = ReadInt32(reader, "vehicle_assessment_code") ?? 0,
            vmf_code = ReadInt32(reader, "vmf_code") ?? 0,
            spare_wheel = ReadBoolean(reader, "spare_wheel") ?? false,
            jack = ReadBoolean(reader, "jack") ?? false,
            wheel_spanner = ReadBoolean(reader, "wheel_spanner") ?? false,
            wheel_lock_key = ReadBoolean(reader, "wheel_lock_key") ?? false,
            fuel_card = ReadBoolean(reader, "fuel_card") ?? false,
            license_disc = ReadBoolean(reader, "license_disc") ?? false,
            cof_disc = ReadBoolean(reader, "cof_disc") ?? false,
            radio = ReadBoolean(reader, "radio") ?? false,
            gear_lock = ReadBoolean(reader, "gear_lock") ?? false,
            logbook = ReadBoolean(reader, "logbook") ?? false,
            logbook_start_number = ReadString(reader, "logbook_start_number"),
            logbook_end_number = ReadString(reader, "logbook_end_number"),
            fire_extinguisher = ReadBoolean(reader, "fire_extinguisher"),
            smash_and_grab = ReadBoolean(reader, "smash_and_grab") ?? false,
            tracker = ReadBoolean(reader, "tracker") ?? false,
            sets_of_keys = ReadInt16(reader, "sets_of_keys"),
            damages = ReadBoolean(reader, "damages") ?? false,
            assessment_notes = notes,
            notes = notes,
            capture_date = captureDate,
            assessment_date = captureDate,
            modified_date = modifiedDate,
            user_access_code = userAccessCode,
            user_access_name = ReadString(reader, "user_access_name"),
            date_created = ReadDateTime(reader, "date_created") ?? captureDate ?? DateTime.MinValue,
            date_updated = ReadDateTime(reader, "date_updated") ?? modifiedDate,
            created_by_user_code = ReadInt32(reader, "created_by_user_code") ?? userAccessCode,
            modified_by_user_code = ReadInt32(reader, "modified_by_user_code") ?? userAccessCode,
            is_deleted = ReadBoolean(reader, "is_deleted") ?? false,
        };
    }

    private static object? ReadValue(DbDataReader reader, string column)
    {
        try
        {
            var value = reader[column];
            return value is DBNull ? null : value;
        }
        catch (IndexOutOfRangeException)
        {
            return null;
        }
    }

    private static string? ReadString(DbDataReader reader, string column) =>
        ReadValue(reader, column)?.ToString()?.Trim();

    private static int? ReadInt32(DbDataReader reader, string column)
    {
        var value = ReadValue(reader, column);
        return value is null ? null : Convert.ToInt32(value);
    }

    private static short? ReadInt16(DbDataReader reader, string column)
    {
        var value = ReadValue(reader, column);
        return value is null ? null : Convert.ToInt16(value);
    }

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
    {
        var value = ReadValue(reader, column);
        return value is null ? null : Convert.ToDateTime(value);
    }

    private static bool? ReadBoolean(DbDataReader reader, string column)
    {
        var value = ReadValue(reader, column);
        return value is null ? null : Convert.ToBoolean(value);
    }

    private async Task<IReadOnlyList<string>?> ResolveProcedureAsync(string procedureName)
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
                SELECT [parameterObject].[name]
                FROM [sys].[procedures] AS [procedureObject]
                INNER JOIN [sys].[schemas] AS [schemaObject]
                    ON [schemaObject].[schema_id] = [procedureObject].[schema_id]
                LEFT JOIN [sys].[parameters] AS [parameterObject]
                    ON [parameterObject].[object_id] = [procedureObject].[object_id]
                   AND [parameterObject].[parameter_id] > 0
                WHERE [schemaObject].[name] = N'dbo'
                  AND [procedureObject].[name] = @procedureName
                ORDER BY [parameterObject].[parameter_id];
                """;
            AddParameter(command, "@procedureName", DbType.String, procedureName);
            var values = new List<string>();
            var found = false;
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    found = true;
                    if (!reader.IsDBNull(0))
                        values.Add(reader.GetString(0));
                }
            }
            if (!found)
            {
            await using var existsCommand = connection.CreateCommand();
                existsCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
                existsCommand.CommandText = "SELECT OBJECT_ID(@procedureName, 'P');";
                AddParameter(existsCommand, "@procedureName", DbType.String, $"dbo.{procedureName}");
                if ((await existsCommand.ExecuteScalarAsync()) is not null and not DBNull)
                    throw new LegacyVehicleAssessmentWorkflowUnavailableException(
                        $"The deployed legacy procedure {procedureName} exposes no parameters; no direct-DML fallback was run."
                    );
                return null;
            }
            return values;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static void EnsureProcedureContract(
        string procedureName,
        IReadOnlyList<string> actual,
        IReadOnlyList<string> expected
    )
    {
        if (!actual.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase))
            throw new LegacyVehicleAssessmentWorkflowUnavailableException(
                $"The deployed legacy procedure {procedureName} does not match its archived parameter contract; no direct-DML fallback was run."
            );
    }

    private async Task<int> ExecuteInsertProcedureAsync(
        VehicleAssessment assessment,
        int currentUserId
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
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "[dbo].[DEV_INS_Vehicle_Assessment]";
            var output = command.CreateParameter();
            output.ParameterName = "@vehicle_assessment_code";
            output.DbType = DbType.Int32;
            output.Direction = ParameterDirection.Output;
            command.Parameters.Add(output);
            AddAssessmentParameters(
                command,
                assessment,
                currentUserId,
                includeCode: false,
                includeVehicleCode: true
            );
            await command.ExecuteNonQueryAsync();
            if (output.Value is null or DBNull || !int.TryParse(output.Value.ToString(), out var code) || code <= 0)
                throw new LegacyVehicleAssessmentWorkflowUnavailableException(
                    "The legacy vehicle-assessment insert did not return a code."
                );
            return code;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task ExecuteUpdateProcedureAsync(VehicleAssessment assessment, int currentUserId)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "[dbo].[DEV_UPD_Vehicle_Assessment]";
            AddParameter(command, "@vehicle_assessment_code", DbType.Int32, assessment.vehicle_assessment_code);
            AddAssessmentParameters(
                command,
                assessment,
                currentUserId,
                includeCode: false,
                includeVehicleCode: false
            );
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static void AddAssessmentParameters(
        DbCommand command,
        VehicleAssessment assessment,
        int currentUserId,
        bool includeCode,
        bool includeVehicleCode = true
    )
    {
        if (includeCode)
            AddParameter(command, "@vehicle_assessment_code", DbType.Int32, assessment.vehicle_assessment_code);
        if (includeVehicleCode)
            AddParameter(command, "@vmf_code", DbType.Int32, assessment.vmf_code);
        AddParameter(command, "@spare_wheel", DbType.Boolean, assessment.spare_wheel);
        AddParameter(command, "@jack", DbType.Boolean, assessment.jack);
        AddParameter(command, "@wheel_spanner", DbType.Boolean, assessment.wheel_spanner);
        AddParameter(command, "@wheel_lock_key", DbType.Boolean, assessment.wheel_lock_key);
        AddParameter(command, "@fuel_card", DbType.Boolean, assessment.fuel_card);
        AddParameter(command, "@license_disc", DbType.Boolean, assessment.license_disc);
        AddParameter(command, "@cof_disc", DbType.Boolean, assessment.cof_disc);
        AddParameter(command, "@radio", DbType.Boolean, assessment.radio);
        AddParameter(command, "@gear_lock", DbType.Boolean, assessment.gear_lock);
        AddParameter(command, "@logbook", DbType.Boolean, assessment.logbook);
        // The client-era table and current procedures expose these as
        // varchar/nvarchar values. Older releases used smallint; SQL Server
        // performs the compatible numeric conversion when needed, while the
        // modern contract retains the string form used by the restored schema.
        AddParameter(command, "@logbook_start_number", DbType.String, assessment.logbook_start_number);
        AddParameter(command, "@logbook_end_number", DbType.String, assessment.logbook_end_number);
        AddParameter(command, "@fire_extinguisher", DbType.Boolean, assessment.fire_extinguisher ?? false);
        AddParameter(command, "@smash_and_grab", DbType.Boolean, assessment.smash_and_grab);
        AddParameter(command, "@tracker", DbType.Boolean, assessment.tracker);
        AddParameter(command, "@sets_of_keys", DbType.Byte, assessment.sets_of_keys ?? 0);
        AddParameter(command, "@damages", DbType.Boolean, assessment.damages);
        AddParameter(command, "@assessment_notes", DbType.String, assessment.assessment_notes ?? assessment.notes);
        AddParameter(command, "@user_access_code", DbType.Int16, ToLegacyUserCode(currentUserId));
    }

    private static void ValidateAssessment(VehicleAssessment assessment, bool requireVehicleCode = true)
    {
        if (requireVehicleCode && assessment.vmf_code <= 0)
            throw new ArgumentException("A valid vehicle code is required.", nameof(assessment));
        var startNumber = ParseLogbookNumber(assessment.logbook_start_number, "start");
        var endNumber = ParseLogbookNumber(assessment.logbook_end_number, "end");
        if (startNumber.HasValue && endNumber.HasValue && endNumber < startNumber && endNumber != 0)
            throw new ArgumentException("Logbook end number cannot precede the start number.", nameof(assessment));
        if (assessment.assessment_notes is { Length: > 2000 } || assessment.notes is { Length: > 2000 })
            throw new ArgumentException("Assessment notes cannot exceed 2000 characters.", nameof(assessment));
    }

    private static long? ParseLogbookNumber(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        if (!long.TryParse(
                normalized,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var number
            ) || number < 0)
        {
            throw new ArgumentException(
                $"Logbook {label} number must be a non-negative number.",
                nameof(value)
            );
        }

        return number;
    }

    private static short? ToLegacyUserCode(int userId) =>
        userId is > 0 and <= short.MaxValue ? (short)userId : null;

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}

public sealed class LegacyVehicleAssessmentWorkflowUnavailableException : InvalidOperationException
{
    public LegacyVehicleAssessmentWorkflowUnavailableException(string message)
        : base(message) { }
}
