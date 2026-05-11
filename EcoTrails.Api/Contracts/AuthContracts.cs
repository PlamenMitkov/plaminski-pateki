using System.ComponentModel.DataAnnotations;

namespace EcoTrails.Api.Contracts;

public record RegisterRequest(
	[Required, EmailAddress, MaxLength(256)] string Email,
	[Required, MinLength(6), MaxLength(128)] string Password);

public record LoginRequest(
	[Required, EmailAddress, MaxLength(256)] string Email,
	[Required, MinLength(6), MaxLength(128)] string Password);

public record UpdateProfileRequest(
	[Required, EmailAddress, MaxLength(256)] string Email,
	[MaxLength(64)] string? UserName,
	[Phone, MaxLength(32)] string? PhoneNumber);

public record ChangePasswordRequest(
	[Required, MinLength(6), MaxLength(128)] string CurrentPassword,
	[Required, MinLength(6), MaxLength(128)] string NewPassword);

public record DeleteAccountRequest(
	[Required, MinLength(6), MaxLength(128)] string CurrentPassword,
	[Required, MaxLength(32)] string ConfirmationText);

public record AuthResponse(string Token, string UserId, string Email);
public record AuthMeResponse(
	string UserId,
	string Email,
	List<string> Roles,
	string UserName = "",
	string? PhoneNumber = null);