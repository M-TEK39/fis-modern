using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

public class CreateUserDto
{
    public string? TelephoneNumber { get; set; }
    public string? Email { get; set; }
}

public class UpdateUserDto : CreateUserDto { }

public class UserDto
{
    public int UserAccessCode { get; set; }
    public string? TelephoneNumber { get; set; }
    public string? Email { get; set; }
}

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UserController> _logger;

    public UserController(IUserRepository userRepository, ILogger<UserController> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers()
    {
        try
        {
            var users = await _userRepository.GetAllUsersAsync();
            var userDtos = users.Select(u => new UserDto
            {
                UserAccessCode = u.user_access_code,
                TelephoneNumber = u.tel_no,
                Email = u.email
            });
            return Ok(userDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUser(int id)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var userDto = new UserDto
            {
                UserAccessCode = user.user_access_code,
                TelephoneNumber = user.tel_no,
                Email = user.email
            };

            return Ok(userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user with id {UserId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("email/{email}")]
    public async Task<ActionResult<UserDto>> GetUserByEmail(string email)
    {
        try
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null)
            {
                return NotFound();
            }

            var userDto = new UserDto
            {
                UserAccessCode = user.user_access_code,
                TelephoneNumber = user.tel_no,
                Email = user.email
            };

            return Ok(userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user with email {Email}", email);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("telephone/{telephone}")]
    public async Task<ActionResult<UserDto>> GetUserByTelephone(string telephone)
    {
        try
        {
            var user = await _userRepository.GetByTelephoneAsync(telephone);
            if (user == null)
            {
                return NotFound();
            }

            var userDto = new UserDto
            {
                UserAccessCode = user.user_access_code,
                TelephoneNumber = user.tel_no,
                Email = user.email
            };

            return Ok(userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user with telephone {Telephone}", telephone);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserDto createUserDto)
    {
        try
        {
            var user = new User
            {
                tel_no = createUserDto.TelephoneNumber,
                email = createUserDto.Email
            };

            var createdUser = await _userRepository.CreateAsync(user);

            var userDto = new UserDto
            {
                UserAccessCode = createdUser.user_access_code,
                TelephoneNumber = createdUser.tel_no,
                Email = createdUser.email
            };

            return CreatedAtAction(nameof(GetUser), new { id = createdUser.user_access_code }, userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<UserDto>> UpdateUser(int id, [FromBody] UpdateUserDto updateUserDto)
    {
        try
        {
            var existingUser = await _userRepository.GetByIdAsync(id);
            if (existingUser == null)
            {
                return NotFound();
            }

            existingUser.tel_no = updateUserDto.TelephoneNumber;
            existingUser.email = updateUserDto.Email;

            await _userRepository.UpdateAsync(existingUser);

            var userDto = new UserDto
            {
                UserAccessCode = existingUser.user_access_code,
                TelephoneNumber = existingUser.tel_no,
                Email = existingUser.email
            };

            return Ok(userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user with id {UserId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteUser(int id)
    {
        try
        {
            var existingUser = await _userRepository.GetByIdAsync(id);
            if (existingUser == null)
            {
                return NotFound();
            }

            await _userRepository.DeleteAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user with id {UserId}", id);
            return StatusCode(500, "Internal server error");
        }
    }
}