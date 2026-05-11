using EcoTrails.Api.Contracts;
using EcoTrails.Api.Models;
using EcoTrails.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace EcoTrails.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly JwtTokenService _jwtTokenService;

    public AuthController(UserManager<AppUser> userManager, JwtTokenService jwtTokenService)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            return Conflict("Потребител с този имейл вече съществува.");
        }

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors.Select(error => error.Description));
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtTokenService.CreateToken(user, roles);
        return Ok(new AuthResponse(token, user.Id, user.Email ?? string.Empty));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Unauthorized("Невалидни данни за вход.");
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            return Unauthorized("Невалидни данни за вход.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtTokenService.CreateToken(user, roles);
        return Ok(new AuthResponse(token, user.Id, user.Email ?? string.Empty));
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult<AuthMeResponse>> UpdateProfile(UpdateProfileRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Unauthorized();
        }

        var normalizedEmail = request.Email.Trim();
        var normalizedUserName = string.IsNullOrWhiteSpace(request.UserName)
            ? (user.UserName ?? normalizedEmail)
            : request.UserName.Trim();
        var normalizedPhone = string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? null
            : request.PhoneNumber.Trim();

        if (normalizedUserName.Length < 3 || normalizedUserName.Length > 64)
        {
            return BadRequest("Потребителското име трябва да е между 3 и 64 символа.");
        }

        var existingByEmail = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existingByEmail is not null && !string.Equals(existingByEmail.Id, user.Id, StringComparison.Ordinal))
        {
            return Conflict("Потребител с този имейл вече съществува.");
        }

        var existingByUserName = await _userManager.FindByNameAsync(normalizedUserName);
        if (existingByUserName is not null && !string.Equals(existingByUserName.Id, user.Id, StringComparison.Ordinal))
        {
            return Conflict("Потребителското име вече е заето.");
        }

        user.Email = normalizedEmail;
        user.UserName = normalizedUserName;
        user.PhoneNumber = normalizedPhone;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return BadRequest(updateResult.Errors.Select(error => error.Description));
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new AuthMeResponse(
            user.Id,
            user.Email ?? string.Empty,
            roles.ToList(),
            user.UserName ?? string.Empty,
            user.PhoneNumber));
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<ActionResult<AuthResponse>> ChangePassword(ChangePasswordRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Unauthorized();
        }

        if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
        {
            return BadRequest("Новата парола трябва да е различна от текущата.");
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors.Select(error => error.Description));
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtTokenService.CreateToken(user, roles);
        return Ok(new AuthResponse(token, user.Id, user.Email ?? string.Empty));
    }

    [Authorize]
    [HttpPost("delete-account")]
    public async Task<IActionResult> DeleteAccount(DeleteAccountRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Unauthorized();
        }

        if (!string.Equals(request.ConfirmationText?.Trim(), "DELETE", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("За потвърждение въведи DELETE.");
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.CurrentPassword);
        if (!passwordValid)
        {
            return BadRequest("Невалидна текуща парола.");
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors.Select(error => error.Description));
        }

        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthMeResponse>> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Unauthorized();
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new AuthMeResponse(
            user.Id,
            user.Email ?? string.Empty,
            roles.ToList(),
            user.UserName ?? string.Empty,
            user.PhoneNumber));
    }
}