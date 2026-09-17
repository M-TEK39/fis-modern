using System.Data;
using System.Data.Common;
using System.Globalization;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Services.Finance;

/// <summary>
/// Executes the BAS importer and segment-list update procedures from the
/// legacy Finance application. These operations deliberately do not write to
/// the modern <c>bassegment</c> preview table: the legacy <c>segment</c> table,
/// triggers, and downstream SCOA/default-segment work are the business source
/// of truth.
/// </summary>
public sealed class LegacyBasCompatibilityService
{
    private const string ValidateImportProcedure = "DEV_SEL_ValidateBasImport";
    private const string ImportProcedure = "DEV_INS_SegmentFromXml";
    private const string ActivateProcedure = "DEV_UPD_SegmentActiveFromXml";

    private readonly FisDbContext _context;

    public LegacyBasCompatibilityService(FisDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Validates a BAS document against the selected department using the
    /// archived DEV_SEL_ValidateBasImport contract.
    /// </summary>
    /// <returns>
    /// <see langword="null"/> when the archived procedure is absent. A
    /// present procedure with a different contract throws
    /// <see cref="LegacyBasProcedureContractException"/>.
    /// </returns>
    public async Task<LegacyBasImportValidation?> TryValidateImportAsync(
        int selectedDepartmentCode,
        string basInstallationCode,
        CancellationToken cancellationToken = default
    )
    {
        await using var connectionScope = await OpenConnectionAsync(cancellationToken);
        var connection = connectionScope.Connection;
        var contract = await GetProcedureContractAsync(
            connection,
            ValidateImportProcedure,
            cancellationToken
        );
        if (contract is null)
        {
            return null;
        }

        EnsureContract(
            ValidateImportProcedure,
            contract,
            new Dictionary<string, ParameterExpectation>(StringComparer.OrdinalIgnoreCase)
            {
                ["@selected_departmentId"] = new(false),
                ["@bas_installation_code"] = new(false),
                ["@selected_department"] = new(true),
                ["@document_department"] = new(true),
                ["@department_code"] = new(true),
                ["@errorcode"] = new(true),
            }
        );

        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = $"[dbo].[{ValidateImportProcedure}]";
        command.CommandTimeout = 180;
        AddParameter(command, "@selected_departmentId", DbType.Int32, selectedDepartmentCode);
        AddParameter(
            command,
            "@bas_installation_code",
            DbType.AnsiStringFixedLength,
            basInstallationCode,
            size: 3
        );
        AddOutputParameter(command, "@selected_department", DbType.AnsiString, 255);
        AddOutputParameter(command, "@document_department", DbType.AnsiString, 255);
        AddOutputParameter(command, "@department_code", DbType.Int32);
        AddOutputParameter(command, "@errorcode", DbType.Int32);

        await command.ExecuteNonQueryAsync(cancellationToken);
        return new LegacyBasImportValidation(
            ReadString(command.Parameters["@selected_department"]!),
            ReadString(command.Parameters["@document_department"]!),
            ReadInt32(command.Parameters["@department_code"]!),
            ReadInt32(command.Parameters["@errorcode"]!)
        );
    }

    /// <summary>
    /// Imports validated BAS XML through DEV_INS_SegmentFromXml. The
    /// procedure owns segment expiry, inserts/updates, department installation
    /// behavior, and the legacy SCOA/default-segment chain.
    /// </summary>
    public async Task<LegacyBasImportResult?> TryImportAsync(
        string documentXml,
        int departmentCode,
        string basInstallationCode,
        int actionCode,
        int segmentGroupCode,
        CancellationToken cancellationToken = default
    )
    {
        await using var connectionScope = await OpenConnectionAsync(cancellationToken);
        var connection = connectionScope.Connection;
        var contract = await GetProcedureContractAsync(
            connection,
            ImportProcedure,
            cancellationToken
        );
        if (contract is null)
        {
            return null;
        }

        EnsureContract(
            ImportProcedure,
            contract,
            new Dictionary<string, ParameterExpectation>(StringComparer.OrdinalIgnoreCase)
            {
                ["@Doc"] = new(false),
                ["@department_code"] = new(false),
                ["@bas_installation_code"] = new(false),
                ["@actionCode"] = new(false),
                ["@segment_group_code"] = new(false),
                ["@delete"] = new(true),
                ["@update"] = new(true),
                ["@insert"] = new(true),
            }
        );

        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = $"[dbo].[{ImportProcedure}]";
        command.CommandTimeout = 180;
        AddParameter(command, "@Doc", DbType.AnsiString, documentXml, size: -1);
        AddParameter(command, "@department_code", DbType.Int16, checked((short)departmentCode));
        AddParameter(
            command,
            "@bas_installation_code",
            DbType.AnsiStringFixedLength,
            basInstallationCode,
            size: 3
        );
        AddParameter(command, "@actionCode", DbType.Int16, checked((short)actionCode));
        AddParameter(command, "@segment_group_code", DbType.Int32, segmentGroupCode);
        AddOutputParameter(command, "@delete", DbType.Int32);
        AddOutputParameter(command, "@update", DbType.Int32);
        AddOutputParameter(command, "@insert", DbType.Int32);

        await command.ExecuteNonQueryAsync(cancellationToken);
        return new LegacyBasImportResult(
            ReadInt32(command.Parameters["@delete"]!),
            ReadInt32(command.Parameters["@update"]!),
            ReadInt32(command.Parameters["@insert"]!)
        );
    }

    /// <summary>
    /// Updates legacy segment_show_in_list flags through
    /// DEV_UPD_SegmentActiveFromXml. The archived page explicitly starts and
    /// commits a transaction around this procedure; preserve that boundary.
    /// </summary>
    public async Task<LegacyBasActivationResult?> TryActivateAsync(
        string documentXml,
        int departmentCode,
        CancellationToken cancellationToken = default
    )
    {
        await using var connectionScope = await OpenConnectionAsync(cancellationToken);
        var connection = connectionScope.Connection;
        var contract = await GetProcedureContractAsync(
            connection,
            ActivateProcedure,
            cancellationToken
        );
        if (contract is null)
        {
            return null;
        }

        EnsureContract(
            ActivateProcedure,
            contract,
            new Dictionary<string, ParameterExpectation>(StringComparer.OrdinalIgnoreCase)
            {
                ["@Doc"] = new(false),
                ["@department_code"] = new(false),
                ["@update"] = new(true),
            }
        );

        var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = $"[dbo].[{ActivateProcedure}]";
            command.CommandTimeout = 180;
            AddParameter(command, "@Doc", DbType.AnsiString, documentXml, size: -1);
            AddParameter(command, "@department_code", DbType.Int32, departmentCode);
            AddOutputParameter(command, "@update", DbType.Int32);

            await command.ExecuteNonQueryAsync(cancellationToken);
            var updated = ReadInt32(command.Parameters["@update"]!);
            await transaction.CommitAsync(cancellationToken);
            return new LegacyBasActivationResult(updated, departmentCode);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            await transaction.DisposeAsync();
        }
    }

    private async Task<ProcedureContract?> GetProcedureContractAsync(
        DbConnection connection,
        string procedureName,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT [name], [is_output], [has_default_value], [parameter_id]
            FROM [sys].[parameters]
            WHERE [object_id] = OBJECT_ID(@procedureName, 'P')
              AND [parameter_id] > 0
            ORDER BY [parameter_id]
            """;
        AddParameter(command, "@procedureName", DbType.String, $"dbo.{procedureName}");

        var parameters = new List<ProcedureParameter>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                parameters.Add(
                    new ProcedureParameter(
                        reader.GetString(0),
                        reader.GetBoolean(1),
                        !reader.IsDBNull(2) && reader.GetBoolean(2),
                        reader.GetInt32(3)
                    )
                );
            }
        }

        await using var existsCommand = connection.CreateCommand();
        existsCommand.CommandText = "SELECT OBJECT_ID(@procedureName, 'P');";
        AddParameter(existsCommand, "@procedureName", DbType.String, $"dbo.{procedureName}");
        var objectId = await existsCommand.ExecuteScalarAsync(cancellationToken);
        return objectId is null or DBNull ? null : new ProcedureContract(parameters);
    }

    private static void EnsureContract(
        string procedureName,
        ProcedureContract actual,
        IReadOnlyDictionary<string, ParameterExpectation> expected
    )
    {
        var actualByName = actual.Parameters.ToDictionary(
            parameter => parameter.Name,
            StringComparer.OrdinalIgnoreCase
        );
        var missing = expected.Keys.Where(name => !actualByName.ContainsKey(name)).ToArray();
        var unexpected = actual.Parameters
            .Where(parameter => !expected.ContainsKey(parameter.Name))
            .Select(parameter => parameter.Name)
            .ToArray();
        var directionMismatch = expected
            .Where(pair =>
                actualByName.TryGetValue(pair.Key, out var parameter)
                && parameter.IsOutput != pair.Value.IsOutput
            )
            .Select(pair => pair.Key)
            .ToArray();

        if (missing.Length > 0 || unexpected.Length > 0 || directionMismatch.Length > 0)
        {
            throw new LegacyBasProcedureContractException(
                procedureName,
                missing,
                unexpected,
                directionMismatch
            );
        }
    }

    private async Task<ConnectionScope> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        return new ConnectionScope(connection, shouldClose);
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        DbType type,
        object? value,
        int? size = null
    )
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        if (size.HasValue)
        {
            parameter.Size = size.Value;
        }

        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static void AddOutputParameter(
        DbCommand command,
        string name,
        DbType type,
        int? size = null
    )
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Direction = ParameterDirection.Output;
        if (size.HasValue)
        {
            parameter.Size = size.Value;
        }

        command.Parameters.Add(parameter);
    }

    private static string? ReadString(DbParameter parameter) =>
        parameter.Value is null or DBNull
            ? null
            : Convert.ToString(parameter.Value, CultureInfo.InvariantCulture)?.Trim();

    private static int ReadInt32(DbParameter parameter) =>
        parameter.Value is null or DBNull
            ? 0
            : Convert.ToInt32(parameter.Value, CultureInfo.InvariantCulture);

    private sealed record ConnectionScope(DbConnection Connection, bool ShouldClose) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            if (ShouldClose)
            {
                await Connection.CloseAsync();
            }
        }
    }

    private sealed record ProcedureContract(IReadOnlyList<ProcedureParameter> Parameters);

    private sealed record ProcedureParameter(
        string Name,
        bool IsOutput,
        bool HasDefaultValue,
        int ParameterId
    );

    private sealed record ParameterExpectation(bool IsOutput);
}

public sealed record LegacyBasImportValidation(
    string? SelectedDepartmentName,
    string? DocumentDepartmentName,
    int DocumentDepartmentCode,
    int ActionCode
);

public sealed record LegacyBasImportResult(int Deleted, int Updated, int Inserted);

public sealed record LegacyBasActivationResult(int Updated, int DepartmentCode);

public sealed class LegacyBasProcedureContractException : InvalidOperationException
{
    public LegacyBasProcedureContractException(
        string procedureName,
        IReadOnlyCollection<string> missing,
        IReadOnlyCollection<string> unexpected,
        IReadOnlyCollection<string> directionMismatch
    )
        : base(
            $"Legacy BAS procedure {procedureName} has an incompatible parameter contract."
        )
    {
        ProcedureName = procedureName;
        Missing = missing;
        Unexpected = unexpected;
        DirectionMismatch = directionMismatch;
    }

    public string ProcedureName { get; }
    public IReadOnlyCollection<string> Missing { get; }
    public IReadOnlyCollection<string> Unexpected { get; }
    public IReadOnlyCollection<string> DirectionMismatch { get; }
}
