using System.Data;
using System.Data.Common;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Services;

/// <summary>
/// Executes the legacy contract billing scheduler on the restored FIS
/// database. The database procedure owns its child procedures, transactions,
/// triggers, journal details, Charged_Until updates, contract recreation, and
/// overcharge repairs.
/// </summary>
public sealed class LegacyContractBillingSchedulerService
{
    private const string ProcedureName = "ADM_Contract_JobScheduler";

    private readonly FisDbContext _context;
    private readonly ILogger<LegacyContractBillingSchedulerService> _logger;

    public LegacyContractBillingSchedulerService(
        FisDbContext context,
        ILogger<LegacyContractBillingSchedulerService> logger
    )
    {
        _context = context;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var parameters = await ReadProcedureParametersAsync(connection);
            if (parameters is null)
            {
                if (await ProcedureExistsAsync(connection, "ADM_Contract_JobScheduler_Off"))
                {
                    throw new LegacyBillingSchedulerUnavailableException(
                        "The legacy contract billing scheduler is disabled on this database (ADM_Contract_JobScheduler was renamed to ADM_Contract_JobScheduler_Off). No modern billing fallback was run."
                    );
                }

                throw new LegacyBillingSchedulerUnavailableException(
                    $"The legacy contract billing scheduler {ProcedureName} is unavailable. No modern billing fallback was run."
                );
            }

            if (parameters.Count != 0)
            {
                throw new LegacyBillingSchedulerContractException(
                    $"The deployed legacy procedure {ProcedureName} has an unexpected parameter contract. No modern billing fallback was run."
                );
            }

            await using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = $"[dbo].[{ProcedureName}]";
            command.CommandTimeout = 1800;
            await command.ExecuteNonQueryAsync();

            _logger.LogInformation(
                "Legacy contract billing scheduler {ProcedureName} completed successfully.",
                ProcedureName
            );
        }
        catch (LegacyBillingSchedulerContractException)
        {
            throw;
        }
        catch (LegacyBillingSchedulerUnavailableException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Legacy contract billing scheduler {ProcedureName} failed. No modern billing fallback was run.",
                ProcedureName
            );
            throw;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<IReadOnlyList<string>?> ReadProcedureParametersAsync(
        DbConnection connection
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT [p].[name]
            FROM [sys].[procedures] AS [sp]
            INNER JOIN [sys].[schemas] AS [s]
                ON [s].[schema_id] = [sp].[schema_id]
            LEFT JOIN [sys].[parameters] AS [p]
                ON [p].[object_id] = [sp].[object_id]
            WHERE [s].[name] = N'dbo'
              AND [sp].[name] = @procedureName
              AND [p].[parameter_id] > 0
            ORDER BY [p].[parameter_id]
            """;
        AddParameter(command, "@procedureName", ProcedureName);

        var parameters = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                parameters.Add(reader.GetString(0));
            }
        }

        if (parameters.Count > 0)
        {
            return parameters;
        }

        await using var existsCommand = connection.CreateCommand();
        existsCommand.CommandText = "SELECT OBJECT_ID(@procedureName, 'P');";
        AddParameter(existsCommand, "@procedureName", $"dbo.{ProcedureName}");
        var objectId = await existsCommand.ExecuteScalarAsync();
        return objectId is null or DBNull ? null : parameters;
    }

    private static async Task<bool> ProcedureExistsAsync(
        DbConnection connection,
        string procedureName
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT OBJECT_ID(@procedureName, 'P');";
        AddParameter(command, "@procedureName", $"dbo.{procedureName}");
        var objectId = await command.ExecuteScalarAsync();
        return objectId is not null and not DBNull;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = DbType.String;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

public sealed class LegacyBillingSchedulerContractException : InvalidOperationException
{
    public LegacyBillingSchedulerContractException(string message)
        : base(message) { }
}

public sealed class LegacyBillingSchedulerUnavailableException : InvalidOperationException
{
    public LegacyBillingSchedulerUnavailableException(string message)
        : base(message) { }
}
