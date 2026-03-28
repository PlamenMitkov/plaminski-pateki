using System.Net;
using System.Net.Http.Json;
using EcoTrails.Api.Contracts;
using EcoTrails.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace EcoTrails.Api.Tests;

public class AuthProfileAndPasswordTests : IClassFixture<TrailsSummaryApiFactory>
{
    private readonly TrailsSummaryApiFactory _factory;

    public AuthProfileAndPasswordTests(TrailsSummaryApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UpdateProfile_WithAuthenticatedUser_UpdatesProfileFields()
    {
        const string userId = "auth-profile-user-1";
        await EnsureUserAsync(userId, "auth-profile-user-1@ecotrails.test", "Start123");

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);

        var request = new UpdateProfileRequest(
            "profile-updated@ecotrails.test",
            "profile_user_1",
            "+359888123456");

        var response = await client.PutAsJsonAsync("/api/auth/profile", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AuthMeResponse>();
        Assert.NotNull(payload);
        Assert.Equal("profile-updated@ecotrails.test", payload!.Email);
        Assert.Equal("profile_user_1", payload.UserName);
        Assert.Equal("+359888123456", payload.PhoneNumber);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var savedUser = await userManager.FindByIdAsync(userId);

        Assert.NotNull(savedUser);
        Assert.Equal("profile-updated@ecotrails.test", savedUser!.Email);
        Assert.Equal("profile_user_1", savedUser.UserName);
        Assert.Equal("+359888123456", savedUser.PhoneNumber);
    }

    [Fact]
    public async Task ChangePassword_WithValidCurrentPassword_ChangesPasswordAndReturnsToken()
    {
        const string userId = "auth-password-user-1";
        await EnsureUserAsync(userId, "auth-password-user-1@ecotrails.test", "Start123");

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);

        var response = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new ChangePasswordRequest("Start123", "Updated123"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.Token));

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByIdAsync(userId);

        Assert.NotNull(user);
        Assert.True(await userManager.CheckPasswordAsync(user!, "Updated123"));
        Assert.False(await userManager.CheckPasswordAsync(user!, "Start123"));
    }

    [Fact]
    public async Task ChangePassword_WithInvalidCurrentPassword_ReturnsBadRequest()
    {
        const string userId = "auth-password-user-2";
        await EnsureUserAsync(userId, "auth-password-user-2@ecotrails.test", "Start123");

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);

        var response = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new ChangePasswordRequest("Wrong123", "Updated123"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task EnsureUserAsync(string userId, string email, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var existing = await userManager.FindByIdAsync(userId);
        if (existing is not null)
        {
            return;
        }

        var user = new AppUser
        {
            Id = userId,
            UserName = email,
            Email = email,
        };

        var result = await userManager.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(item => item.Description)));
    }
}
