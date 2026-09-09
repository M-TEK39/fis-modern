using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace FIS.Tools.DatabaseAuditTool;

/// <summary>
/// Compares the connected database with the authoritative legacy table DDL.
/// The comparison is metadata-only; it never executes a DDL script against the
/// target database.
/// </summary>
internal static class LegacySchemaAudit
{
    private const string DefaultRelativeRoot =
        "backup/sources/GGMT.Database/SQLScripts/v2.0.0";

    private static readonly Regex CreateTablePattern = new(
        "CREATE\\s+TABLE\\s+\\[(?<schema>[^\\]]+)\\]\\s*\\.\\s*\\[(?<table>[^\\]]+)\\]\\s*\\(",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    private static readonly Regex ColumnPattern = new(
        "^\\s*\\[(?<column>[^\\]]+)\\]\\s+",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant
    );

    private static readonly Regex ColumnBlockEndPattern = new(
        "^\\s*(?:CONSTRAINT\\b|\\)\\s*(?:ON\\b|;))",
        RegexOptions.Compiled
            | RegexOptions.Multiline
            | RegexOptions.IgnoreCase
            | RegexOptions.CultureInvariant
    );

    public static async Task<LegacySchemaAuditResult> BuildAsync(
        SqlConnection connection,
        string? configuredRoot
    )
    {
        var root = ResolveRoot(configuredRoot);
        if (root is null)
        {
            return new LegacySchemaAuditResult(
                null,
                [
                    new LegacySchemaAuditIssue(
                        "SCHEMA_REFERENCE_UNAVAILABLE",
                        "warning",
                        "legacy-ddl",
                        null,
                        1,
                        "The authoritative legacy table DDL could not be found, so the full schema comparison was not run.",
                        $"Set FIS_LEGACY_DDL_ROOT to the checked-in {DefaultRelativeRoot} directory before relying on this audit."
                    ),
                ]
            );
        }

        var expectedTables = LoadTableSpecs(root);
        if (expectedTables.Count == 0)
        {
            return new LegacySchemaAuditResult(
                root,
                [
                    new LegacySchemaAuditIssue(
                        "SCHEMA_REFERENCE_EMPTY",
                        "warning",
                        root,
                        null,
                        1,
                        "The configured legacy DDL directory contains no parseable *.Table.sql declarations.",
                        "Point FIS_LEGACY_DDL_ROOT at the v2.0.0 table-script directory."
                    ),
                ]
            );
        }

        var actualColumns = await LoadActualColumnsAsync(connection);
        var issues = new List<LegacySchemaAuditIssue>();

        foreach (var expected in expectedTables.Values.OrderBy(table => table.Key))
        {
            if (!actualColumns.TryGetValue(expected.Key, out var actualTableColumns))
            {
                issues.Add(
                    new LegacySchemaAuditIssue(
                        "SCHEMA_LEGACY_DDL_TABLE_MISSING",
                        IsHistoricalSupportTable(expected) ? "info" : "warning",
                        expected.Key,
                        null,
                        1,
                        $"The table is declared by the authoritative legacy DDL but is absent from the connected database ({expected.SourceFile}).",
                        "Confirm whether a route still depends on this legacy object. Restore the approved client object or keep that route on an explicit compatibility fallback."
                    )
                );
                continue;
            }

            var missingColumns = expected.Columns
                .Where(column => !actualTableColumns.Contains(column))
                .OrderBy(column => column, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (missingColumns.Length == 0)
                continue;

            issues.Add(
                new LegacySchemaAuditIssue(
                    "SCHEMA_LEGACY_DDL_COLUMNS_MISSING",
                    "warning",
                    expected.Key,
                    string.Join(',', missingColumns),
                    missingColumns.Length,
                    $"The table exists but is missing {missingColumns.Length} column(s) declared by the authoritative legacy DDL ({expected.SourceFile}).",
                    "Do not add guessed columns to a client database. Confirm the live client schema, then either add an approved additive migration or make the owning repository negotiate these fields at runtime."
                )
            );
        }

        return new LegacySchemaAuditResult(root, issues);
    }

    private static string? ResolveRoot(string? configuredRoot)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuredRoot))
            candidates.Add(configuredRoot);

        var environmentRoot = Environment.GetEnvironmentVariable("FIS_LEGACY_DDL_ROOT");
        if (!string.IsNullOrWhiteSpace(environmentRoot))
            candidates.Add(environmentRoot);

        for (
            var directory = new DirectoryInfo(Environment.CurrentDirectory);
            directory is not null;
            directory = directory.Parent
        )
        {
            candidates.Add(Path.Combine(directory.FullName, DefaultRelativeRoot));
        }

        candidates.Add(Path.Combine("/src", DefaultRelativeRoot));

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (Directory.Exists(fullPath))
                return fullPath;
        }

        return null;
    }

    private static Dictionary<string, LegacyTableSpec> LoadTableSpecs(string root)
    {
        var tables = new Dictionary<string, LegacyTableSpec>(StringComparer.OrdinalIgnoreCase);
        foreach (var sourceFile in Directory.EnumerateFiles(root, "*.Table.sql"))
        {
            var sql = File.ReadAllText(sourceFile);
            foreach (Match tableMatch in CreateTablePattern.Matches(sql))
            {
                var schema = tableMatch.Groups["schema"].Value;
                var table = tableMatch.Groups["table"].Value;
                var key = schema + "." + table;
                var blockStart = tableMatch.Index + tableMatch.Length;
                var remaining = sql[blockStart..];
                var blockEndMatch = ColumnBlockEndPattern.Match(remaining);
                var columnBlock = blockEndMatch.Success
                    ? remaining[..blockEndMatch.Index]
                    : remaining;
                var columns = ColumnPattern.Matches(columnBlock)
                    .Select(match => match.Groups["column"].Value)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (columns.Count == 0)
                    continue;

                if (tables.TryGetValue(key, out var existing))
                {
                    existing.Columns.UnionWith(columns);
                    continue;
                }

                tables[key] = new LegacyTableSpec(
                    schema,
                    table,
                    columns,
                    Path.GetRelativePath(root, sourceFile)
                );
            }
        }

        return tables;
    }

    private static async Task<Dictionary<string, HashSet<string>>> LoadActualColumnsAsync(
        SqlConnection connection
    )
    {
        const string sql = """
            SELECT [TABLE_SCHEMA], [TABLE_NAME], [COLUMN_NAME]
            FROM [INFORMATION_SCHEMA].[COLUMNS]
            ORDER BY [TABLE_SCHEMA], [TABLE_NAME], [ORDINAL_POSITION];
            """;

        var tables = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var schema = reader.GetString(0);
            var table = reader.GetString(1);
            var key = schema + "." + table;
            if (!tables.TryGetValue(key, out var columns))
            {
                columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                tables[key] = columns;
            }

            columns.Add(reader.GetString(2));
        }

        return tables;
    }

    private static bool IsHistoricalSupportTable(LegacyTableSpec table) =>
        table.Schema.Equals("Audit", StringComparison.OrdinalIgnoreCase)
        || table.Table.StartsWith("aspnet_", StringComparison.OrdinalIgnoreCase)
        || table.Table.Contains("_Backup", StringComparison.OrdinalIgnoreCase)
        || table.Table.StartsWith("tmp", StringComparison.OrdinalIgnoreCase);

    internal sealed record LegacySchemaAuditResult(
        string? ReferenceRoot,
        IReadOnlyList<LegacySchemaAuditIssue> Issues
    );

    internal sealed record LegacySchemaAuditIssue(
        string IssueCode,
        string Severity,
        string TableName,
        string? FieldName,
        long Occurrences,
        string Description,
        string RecommendedAction
    );

    private sealed record LegacyTableSpec(
        string Schema,
        string Table,
        HashSet<string> Columns,
        string SourceFile
    )
    {
        public string Key => Schema + "." + Table;
    }
}
