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
        return Ok(new AuthMeResponse(user.Id, user.Email ?? string.Empty, roles.ToList()));
    }
}