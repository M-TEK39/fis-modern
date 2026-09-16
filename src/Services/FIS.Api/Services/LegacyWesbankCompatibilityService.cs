using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Services.Finance;

/// <summary>
/// Imports the full Standard Bank/Wesbank transaction file through the
/// archived legacy procedure. The legacy table has many required source
/// columns which are intentionally not represented by the modern preview
/// entity, so this path never falls back to EF inserts.
/// </summary>
public sealed class LegacyWesbankCompatibilityService
{
    private const string ImportProcedure = "DEV_INS_FuelFileFromXML";
    private const string ParameterProcedure = "DEV_SEL_ParameterValue";
    private const string ExistingTransactionsProcedure = "DEV_SEL_FuleTransactionsExistForMonth";
    private const string BatchParameter = "BatchIsRunning";

    // Exact OPENXML contract in DEV_INS_FuelFileFromXML. The first source
    // CSV column is mapped to CLO_CODE by the legacy uploader.
    private static readonly string[] RequiredColumns =
    [
        "CLO_CODE", "CLNT_CODE", "CC_CODE", "RNUMB", "CARD_SEQ_N", "CHCK_DIGIT",
        "TRANS_DATE", "ODO", "MICROFILM", "REF_NO", "STAT_NARR", "POST_AREA",
        "PURCH_CAT", "TRANS_AMNT", "LITRES", "CHG_NO_CHG", "PERS_BUS", "VAR_01",
        "VAR_02", "VAR_03", "VAR_04", "VAR_05", "VAR_06", "VAR_07", "VAR_08",
        "VAR_09", "VAR_10", "VAR_11", "VAR_12", "VAR_13", "VAR_14", "VAR_15",
        "VAR_16", "VAR_17", "VAR_18", "VAR_19", "VAR_20", "KM_SPAN", "CONSUMP",
        "VAT", "TRANS_CODE", "OLD_REP_DT",
    ];

    private static readonly HashSet<string> NullableSourceColumns = new(
        ["POST_AREA"],
        StringComparer.OrdinalIgnoreCase
    );

    private readonly FisDbContext _context;

    public LegacyWesbankCompatibilityService(FisDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Imports a converted DBF/CSV file. A null result means that the legacy
    /// import procedure is not deployed; callers must not perform a modern
    /// EF fallback in that case.
    /// </summary>
    public async Task<LegacyWesbankImportResult?> TryImportAsync(
        Stream csvStream,
        CancellationToken cancellationToken = default
    )
    {
        var document = await ReadCsvAsync(csvStream, cancellationToken);
        await using var connectionScope = await OpenConnectionAsync(cancellationToken);
        var connection = connectionScope.Connection;

        var parameterContract = await GetProcedureContractAsync(
            connection,
            ParameterProcedure,
            cancellationToken
        );
        if (parameterContract is null)
        {
            throw new LegacyWesbankDependencyException(
                $"The required legacy procedure {ParameterProcedure} is not deployed. No direct-DML fallback was run."
            );
        }

        EnsureContract(ParameterProcedure, parameterContract, "@receivedParameterName");
        var batchValue = await ReadLegacyParameterAsync(connection, BatchParameter, cancellationToken);
        if (IsLegacyTrue(batchValue))
        {
            throw new LegacyWesbankBatchRunningException(
                "The legacy Finance batch is running. Standard Bank transactions cannot be imported until it finishes."
            );
        }

        // The legacy upload page invokes this parameterless read before its
        // bulk copy and ignores the scalar result. Preserve that observable
        // dependency when the procedure is present, while allowing restored
        // databases that never shipped this unused helper to proceed through
        // the authoritative import procedure.
        var existingTransactionsContract = await GetProcedureContractAsync(
            connection,
            ExistingTransactionsProcedure,
            cancellationToken
        );
        if (existingTransactionsContract is not null)
        {
            EnsureNoParameters(ExistingTransactionsProcedure, existingTransactionsContract);
            await ExecuteLegacyScalarAsync(
                connection,
                ExistingTransactionsProcedure,
                cancellationToken
            );
        }

        var importContract = await GetProcedureContractAsync(
            connection,
            ImportProcedure,
            cancellationToken
        );
        if (importContract is null)
        {
            return null;
        }

        EnsureContract(ImportProcedure, importContract, "@XMLDoc");

        var before = await ReadTransactionCountAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = $"[dbo].[{ImportProcedure}]";
        command.CommandTimeout = 180;
        AddParameter(command, "@XMLDoc", DbType.AnsiString, document.Xml, size: -1);
        await command.ExecuteNonQueryAsync(cancellationToken);
        var after = await ReadTransactionCountAsync(connection, cancellationToken);
        var added = checked((int)Math.Max(0, after - before));

        return new LegacyWesbankImportResult(document.RowCount, added);
    }

    private static async Task<CsvDocument> ReadCsvAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        var raw = DecodeCsv(buffer.ToArray());
        var rows = ParseCsv(raw);
        if (rows.Count == 0 || rows[0].Count == 0)
        {
            throw new LegacyWesbankImportFormatException("The uploaded CSV file has no header row.");
        }

        var headers = rows[0].Select(value => value.Trim()).ToArray();
        headers[0] = headers[0].TrimStart('\uFEFF');
        var mapping = BuildColumnMapping(headers);
        var xml = new XDocument(
            new XElement(
                "NewDataSet",
                rows.Skip(1).Select((row, index) =>
                {
                    if (row.All(string.IsNullOrWhiteSpace))
                    {
                        return null;
                    }

                    if (row.Count != headers.Length)
                    {
                        throw new LegacyWesbankImportFormatException(
                            $"CSV row {index + 2} has {row.Count} columns; expected {headers.Length}."
                        );
                    }

                    return CreateTransactionElement(row, mapping, index + 2);
                }).Where(element => element is not null)
            )
        );

        var transactionRows = xml.Root?.Elements().ToArray() ?? Array.Empty<XElement>();
        if (transactionRows.Length == 0)
        {
            throw new LegacyWesbankImportFormatException("The uploaded CSV file contains no transaction rows.");
        }

        return new CsvDocument(xml.ToString(SaveOptions.DisableFormatting), transactionRows.Length);
    }

    private static string DecodeCsv(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        try
        {
            // Prefer UTF-8 when the file is valid UTF-8; browsers and modern
            // spreadsheet tools commonly omit the BOM for that encoding.
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            // The legacy page explicitly loaded ANSI CSV through Jet. Keep a
            // Windows-1252 fallback for those files on the Linux API host.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252).GetString(bytes);
        }
    }

    private static IReadOnlyDictionary<string, int> BuildColumnMapping(IReadOnlyList<string> headers)
    {
        if (headers.Count < RequiredColumns.Length)
        {
            throw new LegacyWesbankImportFormatException(
                $"The Standard Bank CSV must contain the {RequiredColumns.Length} legacy transaction columns."
            );
        }

        var mapping = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [RequiredColumns[0]] = 0,
        };
        for (var index = 1; index < headers.Count; index++)
        {
            var normalized = NormalizeColumn(headers[index]);
            if (string.Equals(normalized, "NONAME", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!RequiredColumns.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                throw new LegacyWesbankImportFormatException(
                    $"The Standard Bank CSV contains unsupported column '{headers[index]}'."
                );
            }

            if (!mapping.TryAdd(normalized, index))
            {
                throw new LegacyWesbankImportFormatException(
                    $"The Standard Bank CSV contains duplicate column '{headers[index]}'."
                );
            }
        }

        var missing = RequiredColumns.Where(column => !mapping.ContainsKey(column)).ToArray();
        if (missing.Length > 0)
        {
            throw new LegacyWesbankImportFormatException(
                $"The Standard Bank CSV is missing legacy column(s): {string.Join(", ", missing)}."
            );
        }

        return mapping;
    }

    private static XElement CreateTransactionElement(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> mapping,
        int rowNumber
    )
    {
        var transaction = new XElement("_x005B__x0027_44737_0103_DBF_x0024__x0027__x005D_");
        foreach (var column in RequiredColumns)
        {
            var value = row[mapping[column]].Trim();
            if (value.Length == 0 && !NullableSourceColumns.Contains(column))
            {
                throw new LegacyWesbankImportFormatException(
                    $"CSV row {rowNumber} is missing required value '{column}'."
                );
            }

            if (string.Equals(column, "TRANS_DATE", StringComparison.OrdinalIgnoreCase))
            {
                value = NormalizeTransactionDate(value, rowNumber);
            }

            transaction.Add(new XElement(column, value));
        }

        return transaction;
    }

    private static string NormalizeTransactionDate(string value, int rowNumber)
    {
        if (DateTime.TryParseExact(
                value,
                ["yyyyMMdd", "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "dd/MM/yyyy", "d/M/yyyy", "MM/dd/yyyy"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsed
            )
            || DateTime.TryParse(value, CultureInfo.GetCultureInfo("en-ZA"), DateTimeStyles.AllowWhiteSpaces, out parsed)
            || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
        {
            return parsed.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        throw new LegacyWesbankImportFormatException($"CSV row {rowNumber} contains an invalid TRANS_DATE value.");
    }

    private static string NormalizeColumn(string value) =>
        value.Trim().Replace(" ", "_", StringComparison.Ordinal).ToUpperInvariant();

    private static List<List<string>> ParseCsv(string value)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character == '"')
            {
                if (quoted && index + 1 < value.Length && value[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
                continue;
            }

            if (character == ',' && !quoted)
            {
                row.Add(field.ToString());
                field.Clear();
                continue;
            }

            if ((character == '\r' || character == '\n') && !quoted)
            {
                if (character == '\r' && index + 1 < value.Length && value[index + 1] == '\n')
                {
                    index++;
                }
                row.Add(field.ToString());
                field.Clear();
                if (row.Any(item => !string.IsNullOrWhiteSpace(item)))
                {
                    rows.Add(row);
                }
                row = new List<string>();
                continue;
            }

            field.Append(character);
        }

        if (quoted)
        {
            throw new LegacyWesbankImportFormatException("The Standard Bank CSV contains an unterminated quoted field.");
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            if (row.Any(item => !string.IsNullOrWhiteSpace(item)))
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    private static async Task<ProcedureContract?> GetProcedureContractAsync(
        DbConnection connection,
        string procedureName,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT [name], [is_output], [parameter_id]
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
                parameters.Add(new ProcedureParameter(reader.GetString(0), reader.GetBoolean(1), reader.GetInt32(2)));
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
        ProcedureContract contract,
        string expectedParameter
    )
    {
        if (
            contract.Parameters.Count != 1
            || !string.Equals(contract.Parameters[0].Name, expectedParameter, StringComparison.OrdinalIgnoreCase)
            || contract.Parameters[0].IsOutput
        )
        {
            throw new LegacyWesbankProcedureContractException(procedureName, expectedParameter);
        }
    }

    private static void EnsureNoParameters(string procedureName, ProcedureContract contract)
    {
        if (contract.Parameters.Count != 0)
        {
            throw new LegacyWesbankProcedureContractException(
                procedureName,
                "no parameters"
            );
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Procedure names come only from fixed archived legacy procedure constants."
    )]
    private static async Task<object?> ExecuteLegacyScalarAsync(
        DbConnection connection,
        string procedureName,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = $"[dbo].[{procedureName}]";
        command.CommandTimeout = 60;
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task<string?> ReadLegacyParameterAsync(
        DbConnection connection,
        string parameterName,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = $"[dbo].[{ParameterProcedure}]";
        command.CommandTimeout = 60;
        AddParameter(command, "@receivedParameterName", DbType.String, parameterName);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
    }

    private static async Task<long> ReadTransactionCountAsync(
        DbConnection connection,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT_BIG(*) FROM [dbo].[wesbank_transaction];";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
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

    private static void AddParameter(DbCommand command, string name, DbType type, object? value, int? size = null)
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

    private static bool IsLegacyTrue(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    private sealed record CsvDocument(string Xml, int RowCount);
    private sealed record ProcedureContract(IReadOnlyList<ProcedureParameter> Parameters);
    private sealed record ProcedureParameter(string Name, bool IsOutput, int ParameterId);

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
}

public sealed record LegacyWesbankImportResult(int RecordsSubmitted, int RecordsAdded);

public sealed class LegacyWesbankImportFormatException(string message) : InvalidOperationException(message);

public sealed class LegacyWesbankDependencyException(string message) : InvalidOperationException(message);

public sealed class LegacyWesbankProcedureContractException(string procedureName, string expectedParameter)
    : InvalidOperationException(
        $"Legacy Standard Bank procedure {procedureName} has an incompatible parameter contract; expected {expectedParameter}."
    );

public sealed class LegacyWesbankBatchRunningException(string message) : InvalidOperationException(message);
