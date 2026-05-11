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
    public async Task CreateMyPost_WithBulgarianTrailGuideContent_PersistsAndReturnsPayload()
    {
        const string userId = "community-owner-bg-1";
        await EnsureUserAsync(userId, "community-owner-bg-1@ecotrails.test", "Start123");

        const string title = "Екопътека „Чернелка“: По стъпките на каньона";
        const string content = """
Екопътека „Чернелка“ е един от най-впечатляващите маршрути в Централна Северна България. Тя е разположена в сърцето на едноименния карстов каньон и предлага перфектна комбинация от природа, история и лек физически преход.

Ето структурирано съдържание, което можеш да използваш за описание или пътеводител:

🌿 Екопътека „Чернелка“: По стъпките на каньона
Екопътеката е изградена в пролома на река Чернелка и се простира на около 7 км дължина. Каньонът е обявен за природна забележителност заради уникалните си вертикални скали, които достигат до 30 метра височина.

📍 Ключова информация
Местоположение: Между селата Горталово и Къртожабене (на около 12 км от Плевен).

Дължина: Около 7 км (в едната посока).

Време за преход: 2.5 – 3 часа спокоен ход.

Трудност: Ниска. Пътеката е почти равна, което я прави идеална за продължително ходене или леко планинско бягане.

Инфраструктура: Изградени са 18 моста над реката, които позволяват лесно пресичане от бряг на бряг.

🗺️ Какво ще видите по маршрута?
Пътеката е наситена с природни и исторически обекти, които правят прехода динамичен:

Скални образувания: Каньонът е изпълнен с причудливи форми, пещери и ниши във варовиковите скали.

Пещера „Царева дупка": Една от най-известните пещери в района, обвита в легенди за цар Иван Шишман.

Средновековна крепост „Градината": Останки от укрепление, което е пазило прохода в миналото.

Местност „Провъртеника": Уникална скала с естествен отвор (дупка) в горната си част, през която преминава слънчевата светлина.

Мъртвата долина: Район с драматичен ландшафт и интересна растителност.
""";

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);

        using var form = new MultipartFormDataContent
        {
            { new StringContent(title), "title" },
            { new StringContent(content), "content" },
            { new StringContent("General"), "postType" }
        };

        var response = await client.PostAsync("/api/communityposts/mine", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CommunityPostResponse>();
        Assert.NotNull(payload);
        Assert.Equal(title, payload!.Title);
        Assert.Equal(content, payload.Content);
        Assert.Equal("General", payload.PostType);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await dbContext.CommunityTrailPosts.FirstAsync(item => item.Id == payload.Id);

        Assert.Equal(title, saved.Title);
        Assert.Equal(content, saved.Content);
        Assert.Equal(CommunityPostType.General, saved.PostType);
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
