using EcoTrails.Api.Models;
using EcoTrails.Api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EcoTrails.Api.Tests;

public class AssistantMessageRepositoryTests
{
    [Fact]
    public async Task SaveConversationTurnAsync_SavesMessagesAndUpdatesSessionMetadata()
    {
        await using var context = TestDbContextFactory.CreateContext();

        var session = new AssistantChatSession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            AppUserId = "user-1",
            Title = "Нова сесия",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            LastActivityAt = DateTime.UtcNow.AddMinutes(-5)
        };

        await context.AssistantChatSessions.AddAsync(session);
        await context.SaveChangesAsync();

        var beforeUpdate = session.LastActivityAt;
        var repository = new AssistantMessageRepository(context);

        await repository.SaveConversationTurnAsync(
            session,
            userPrompt: "Препоръчай маршрут",
            assistantReply: "Ето 2 маршрута.",
            updatedTitle: "Препоръчай маршрут");

        var savedEntries = await context.AssistantChatEntries
            .Where(item => item.SessionInternalId == session.Id)
            .OrderBy(item => item.Id)
            .ToListAsync();

        Assert.Equal(2, savedEntries.Count);
        Assert.Equal("user", savedEntries[0].Role);
        Assert.Equal("assistant", savedEntries[1].Role);
        Assert.Equal("Препоръчай маршрут", session.Title);
        Assert.True(session.LastActivityAt > beforeUpdate);
    }

    [Fact]
    public async Task GetRecentMessagesAsync_ReturnsOldestFirstLimitedSet()
    {
        await using var context = TestDbContextFactory.CreateContext();

        var session = new AssistantChatSession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            AppUserId = "user-2",
            Title = "Тест",
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            LastActivityAt = DateTime.UtcNow.AddHours(-1)
        };

        await context.AssistantChatSessions.AddAsync(session);
        await context.SaveChangesAsync();

        var baseTime = DateTime.UtcNow.AddMinutes(-10);
        await context.AssistantChatEntries.AddRangeAsync(
            new AssistantChatEntry { SessionInternalId = session.Id, Role = "user", Content = "Първо", CreatedAt = baseTime },
            new AssistantChatEntry { SessionInternalId = session.Id, Role = "assistant", Content = "Второ", CreatedAt = baseTime.AddMinutes(1) },
            new AssistantChatEntry { SessionInternalId = session.Id, Role = "user", Content = "Трето", CreatedAt = baseTime.AddMinutes(2) });
        await context.SaveChangesAsync();

        var repository = new AssistantMessageRepository(context);
        var messages = await repository.GetRecentMessagesAsync(session.Id, limit: 2);

        Assert.Equal(2, messages.Count);
        Assert.Equal(new[] { "Първо", "Второ" }, messages.Select(item => item.Content).ToArray());
    }
}
