using System.ComponentModel.DataAnnotations;

namespace EcoTrails.Api.Contracts;

public record RegisterRequest(
	[Required, EmailAddress, MaxLength(256)] string Email,
	[Required, MinLength(6), MaxLength(128)] string Password);

public record LoginRequest(
	[Required, EmailAddress, MaxLength(256)] string Email,
	[Required, MinLength(6), MaxLength(128)] string Password);

public record AuthResponse(string Token, string UserId, string Email);
public record AuthMeResponse(string UserId, string Email, List<string> Roles);