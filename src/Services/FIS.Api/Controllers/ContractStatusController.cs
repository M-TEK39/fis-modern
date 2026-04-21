using FIS.Api.DTOs;
using FIS.Core.Domain.Entities;
using FIS.Core.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// API Controller for ContractStatus operations
/// Provides endpoints for managing contract status types
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContractStatusController : BaseApiController
{
    private readonly IContractStatusRepository _contractStatusRepository;
    private readonly ILogger<ContractStatusController> _logger;

    public ContractStatusController(IContractStatusRepository contractStatusRepository, ILogger<ContractStatusController> logger)
    {
        _contractStatusRepository = contractStatusRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get all contract statuses
    /// </summary>
    /// <returns>List of all contract status entities</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ContractStatus>>> GetAllStatuses()
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var statuses = await _contractStatusRepository.GetAllStatusesAsync();
            return Ok(statuses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all contract statuses");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get contract status by status code
    /// </summary>
    /// <param name="statusCode">The status code to retrieve</param>
    /// <returns>ContractStatus entity if found</returns>
    [HttpGet("{statusCode}")]
    public async Task<ActionResult<ContractStatus>> GetStatus(short statusCode)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var status = await _contractStatusRepository.GetByIdAsync(statusCode);
            if (status == null)
                return NotFound($"Contract status with code {statusCode} not found");

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving contract status with code {StatusCode}", statusCode);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get contract status by description
    /// </summary>
    /// <param name="description">The status description to search for</param>
    /// <returns>ContractStatus entity if found</returns>
    [HttpGet("description/{description}")]
    public async Task<ActionResult<ContractStatus>> GetStatusByDescription(string description)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var status = await _contractStatusRepository.GetByDescriptionAsync(description);
            if (status == null)
                return NotFound($"Contract status with description '{description}' not found");

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving contract status with description {Description}", description);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get contract status by abbreviation
    /// </summary>
    /// <param name="abbreviation">The status abbreviation to search for</param>
    /// <returns>ContractStatus entity if found</returns>
    [HttpGet("abbreviation/{abbreviation}")]
    public async Task<ActionResult<ContractStatus>> GetStatusByAbbreviation(string abbreviation)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var status = await _contractStatusRepository.GetByAbbreviationAsync(abbreviation);
            if (status == null)
                return NotFound($"Contract status with abbreviation '{abbreviation}' not found");

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving contract status with abbreviation {Abbreviation}", abbreviation);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get active contract statuses only
    /// </summary>
    /// <returns>List of active contract status entities</returns>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<ContractStatus>>> GetActiveStatuses()
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var statuses = await _contractStatusRepository.GetActiveStatusesAsync();
            return Ok(statuses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active contract statuses");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get final contract statuses only
    /// </summary>
    /// <returns>List of final contract status entities</returns>
    [HttpGet("final")]
    public async Task<ActionResult<IEnumerable<ContractStatus>>> GetFinalStatuses()
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var statuses = await _contractStatusRepository.GetFinalStatusesAsync();
            return Ok(statuses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving final contract statuses");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Search contract statuses by partial description match
    /// </summary>
    /// <param name="searchTerm">The search term to match against descriptions</param>
    /// <returns>List of matching contract status entities</returns>
    [HttpGet("search/{searchTerm}")]
    public async Task<ActionResult<IEnumerable<ContractStatus>>> SearchStatuses(string searchTerm)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var statuses = await _contractStatusRepository.SearchStatusesAsync(searchTerm);
            return Ok(statuses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching contract statuses with term {SearchTerm}", searchTerm);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Create a new contract status
    /// </summary>
    /// <param name="createStatusDto">The contract status data to create</param>
    /// <returns>The created contract status entity</returns>
    [HttpPost]
    public async Task<ActionResult<ContractStatus>> CreateStatus([FromBody] CreateContractStatusDto createStatusDto)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var status = new ContractStatus
            {
                status_description = createStatusDto.status_description,
                status_abbreviation = createStatusDto.status_abbreviation,
                is_active = createStatusDto.is_active,
                is_final = createStatusDto.is_final
            };

            var createdStatus = await _contractStatusRepository.CreateAsync(status, currentUserId);
            return CreatedAtAction(nameof(GetStatus), new { statusCode = createdStatus.contract_status_code }, createdStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating contract status");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Update an existing contract status
    /// </summary>
    /// <param name="statusCode">The status code to update</param>
    /// <param name="status">The updated contract status data</param>
    /// <returns>The updated contract status entity</returns>
    [HttpPut("{statusCode}")]
    public async Task<ActionResult<ContractStatus>> UpdateStatus(short statusCode, [FromBody] ContractStatus status)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            if (statusCode != status.contract_status_code)
                return BadRequest("Status code in URL does not match status code in body");

            var existingStatus = await _contractStatusRepository.GetByIdAsync(statusCode);
            if (existingStatus == null)
                return NotFound($"Contract status with code {statusCode} not found");

            var updatedStatus = await _contractStatusRepository.UpdateAsync(status, currentUserId);
            return Ok(updatedStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating contract status with code {StatusCode}", statusCode);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Delete a contract status
    /// </summary>
    /// <param name="statusCode">The status code to delete</param>
    /// <returns>No content if successful</returns>
    [HttpDelete("{statusCode}")]
    public async Task<ActionResult> DeleteStatus(short statusCode)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var deleted = await _contractStatusRepository.DeleteAsync(statusCode, currentUserId);
            if (!deleted)
                return NotFound($"Contract status with code {statusCode} not found");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting contract status with code {StatusCode}", statusCode);
            return StatusCode(500, "Internal server error");
        }
    }
}
