using System.Data;
using System.Data.Common;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
public class ProvinceController : BaseApiController
{
    private readonly FisDbContext _context;
    private readonly ILogger<ProvinceController> _logger;

    public ProvinceController(FisDbContext context, ILogger<ProvinceController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all provinces
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProvinceDto>>> GetAll()
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            var hasDeletedColumn = await HasColumnAsync(connection, "is_deleted");
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT [province_code], [province_name], [province_abbreviation] FROM dbo.[province] {(hasDeletedColumn ? "WHERE [is_deleted] = 0" : string.Empty)} ORDER BY [province_name]";

            var provinces = new List<ProvinceDto>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                provinces.Add(new ProvinceDto(
                    reader.GetByte(0).ToString(),
                    reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2)));
            }

            _logger.LogInformation("Retrieved {Count} provinces", provinces.Count);
            return Ok(provinces);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving provinces");
            return StatusCode(500, new { error = "Failed to retrieve provinces", message = ex.Message });
        }
    }

    private static async Task<bool> HasColumnAsync(DbConnection connection, string columnName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'province' AND COLUMN_NAME = @columnName";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@columnName";
        parameter.DbType = DbType.String;
        parameter.Value = columnName;
        command.Parameters.Add(parameter);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }
}

public sealed record ProvinceDto(string province_code, string province_name, string province_abbreviation);
