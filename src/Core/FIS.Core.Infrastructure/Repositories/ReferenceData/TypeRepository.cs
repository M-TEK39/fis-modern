using System.Data;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TypeEntity = FIS.Core.Domain.Entities.ReferenceData.VehicleType;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// dbo.type is type_code + type_description only.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Predicates are fixed compatibility strings; values are parameterized."
)]
public class TypeRepository : ITypeRepository
{
    private readonly FisDbContext _context;

    public TypeRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<TypeEntity?> GetByIdAsync(short typeCode)
    {
        var types = await QueryLegacyTypesAsync(
            "[type_code] = @typeCode",
            command => AddParameter(command, "@typeCode", DbType.Int16, typeCode)
        );
        return types.FirstOrDefault();
    }

    public async Task<TypeEntity?> GetByNameAsync(string typeName)
    {
        var types = await QueryLegacyTypesAsync(
            "LOWER([type_description]) = LOWER(@typeName)",
            command => AddParameter(command, "@typeName", DbType.String, typeName)
        );
        return types.FirstOrDefault();
    }

    public async Task<IEnumerable<TypeEntity>> GetAllTypesAsync()
    {
        var leftover = await QueryLegacyTypesAsync();
        var keys = await LegacySelectorProcedure.TryReadOrderedKeysAsync(
            _context,
            "DEV_SEL_Vehicle_Hire_Types",
            [],
            null,
            "type_code"
        );
        return keys is null
            ? leftover
            : LegacySelectorProcedure.OrderByKeys(leftover, keys, type => type.type_code);
    }

    public async Task<IEnumerable<TypeEntity>> SearchTypesAsync(string searchTerm)
    {
        return await QueryLegacyTypesAsync(
            "[type_description] LIKE @searchTerm",
            command => AddParameter(command, "@searchTerm", DbType.String, $"%{searchTerm}%")
        );
    }

    public async Task<TypePage> GetPageAsync(int page, int pageSize)
    {
        var resolvedPage = Math.Max(1, page);
        var resolvedPageSize = Math.Clamp(pageSize, 1, 100);
        var types = await QueryLegacyTypesAsync();
        var total = types.Count;
        var items = types
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .ToList();
        return new TypePage(items, resolvedPage, resolvedPageSize, total);
    }

    public async Task<TypeEntity> CreateAsync(TypeEntity type, int currentUserId)
    {
        _ = currentUserId;
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
                INSERT INTO [dbo].[type] ([type_description])
                VALUES (@description);
                SELECT CAST(SCOPE_IDENTITY() AS smallint);
                """;
            AddParameter(command, "@description", DbType.String, type.type_description);
            var result = await command.ExecuteScalarAsync();
            type.type_code = result is null || result is DBNull ? (short)0 : Convert.ToInt16(result);
            return type;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<TypeEntity> UpdateAsync(TypeEntity type, int currentUserId)
    {
        _ = currentUserId;
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        var existing = await GetByIdAsync(type.type_code);
        if (existing == null)
            throw new InvalidOperationException($"Type with type_code {type.type_code} not found");

        await ExecuteNonQueryAsync(
            """
            UPDATE [dbo].[type]
            SET [type_description] = @description
            WHERE [type_code] = @typeCode;
            """,
            command =>
            {
                AddParameter(command, "@typeCode", DbType.Int16, type.type_code);
                AddParameter(command, "@description", DbType.String, type.type_description);
            }
        );
        return type;
    }

    public async Task DeleteAsync(short typeCode, int currentUserId)
    {
        _ = currentUserId;
        await ExecuteNonQueryAsync(
            """
            DELETE FROM [dbo].[type]
            WHERE [type_code] = @typeCode;
            """,
            command => AddParameter(command, "@typeCode", DbType.Int16, typeCode)
        );
    }

    private async Task<List<TypeEntity>> QueryLegacyTypesAsync(
        string? predicate = null,
        Action<System.Data.Common.DbCommand>? bind = null
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
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = string.IsNullOrWhiteSpace(predicate)
                ? """
                    SELECT [type_code], [type_description]
                    FROM [dbo].[type]
                    ORDER BY [type_description], [type_code]
                    """
                : $"""
                    SELECT [type_code], [type_description]
                    FROM [dbo].[type]
                    WHERE {predicate}
                    ORDER BY [type_description], [type_code]
                    """;
            bind?.Invoke(command);

            var types = new List<TypeEntity>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var typeCode = reader.IsDBNull(0) ? (short)0 : Convert.ToInt16(reader.GetValue(0));
                var description = reader.IsDBNull(1) ? string.Empty : reader.GetString(1).Trim();
                if (typeCode <= 0 || string.IsNullOrWhiteSpace(description))
                {
                    continue;
                }

                types.Add(new TypeEntity { type_code = typeCode, type_description = description });
            }

            return types;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task ExecuteNonQueryAsync(
        string commandText,
        Action<System.Data.Common.DbCommand> bind
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
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = commandText;
            bind(command);
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

    private static void AddParameter(
        System.Data.Common.DbCommand command,
        string name,
        DbType type,
        object value
    )
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
