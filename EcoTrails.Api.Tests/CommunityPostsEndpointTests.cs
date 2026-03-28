using System.Net;
using System.Net.Http.Json;
using EcoTrails.Api.Contracts;
using EcoTrails.Api.Data;
using EcoTrails.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EcoTrails.Api.Tests;

public class CommunityPostsEndpointTests : IClassFixture<TrailsSummaryApiFactory>
{
    private readonly TrailsSummaryApiFactory _factory;

    public CommunityPostsEndpointTests(TrailsSummaryApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UpdateMyPost_WhenOwned_UpdatesPostAndReturnsPayload()
    {
        const string userId = "community-owner-1";
        await EnsureUserAsync(userId, "community-owner-1@ecotrails.test", "Start123");

        var postId = await CreatePostAsync(userId, "Original title", "Original content for editing.");

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);

        var updateRequest = new CommunityPostUpdateRequest
        {
            Title = "Updated title",
            Content = "Updated content that is definitely longer than 12 chars.",
            PostType = "TrailFeedback"
        };

        var response = await client.PutAsJsonAsync($"/api/communityposts/mine/{postId}", updateRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CommunityPostResponse>();
        Assert.NotNull(payload);
        Assert.Equal("Updated title", payload!.Title);
        Assert.Equal("TrailFeedback", payload.PostType);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await dbContext.CommunityTrailPosts.FirstAsync(item => item.Id == postId);

        Assert.Equal("Updated title", saved.Title);
        Assert.Equal("Updated content that is definitely longer than 12 chars.", saved.Content);
        Assert.Equal(CommunityPostType.TrailFeedback, saved.PostType);
    }

    [Fact]
    public async Task UpdateMyPost_WhenNotOwned_ReturnsNotFound()
    {
        const string ownerUserId = "community-owner-2";
        const string otherUserId = "community-other-2";

        await EnsureUserAsync(ownerUserId, "community-owner-2@ecotrails.test", "Start123");
        await EnsureUserAsync(otherUserId, "community-other-2@ecotrails.test", "Start123");

        var postId = await CreatePostAsync(ownerUserId, "Original title", "Original content for editing.");

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", otherUserId);

        var response = await client.PutAsJsonAsync($"/api/communityposts/mine/{postId}", new CommunityPostUpdateRequest
        {
            Title = "Not allowed",
            Content = "This update should fail because ownership differs.",
            PostType = "General"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMyPost_WhenOwned_DeletesPost()
    {
        const string userId = "community-owner-3";
        await EnsureUserAsync(userId, "community-owner-3@ecotrails.test", "Start123");

        var postId = await CreatePostAsync(userId, "Post to delete", "This post will be deleted by owner.");

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);

        var response = await client.DeleteAsync($"/api/communityposts/mine/{postId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var exists = await dbContext.CommunityTrailPosts.AnyAsync(item => item.Id == postId);

        Assert.False(exists);
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

    private async Task<int> CreatePostAsync(string userId, string title, string content)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var trailId = await dbContext.Trails.Select(item => item.Id).FirstAsync();

        var post = new CommunityTrailPost
        {
            AppUserId = userId,
            TrailId = trailId,
            Title = title,
            Content = content,
            PostType = CommunityPostType.General,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.CommunityTrailPosts.Add(post);
        await dbContext.SaveChangesAsync();
        return post.Id;
    }
}
