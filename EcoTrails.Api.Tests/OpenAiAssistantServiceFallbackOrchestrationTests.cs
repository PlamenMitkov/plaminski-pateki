using EcoTrails.Api.Contracts;
using EcoTrails.Api.Models;
using EcoTrails.Api.Repositories;
using EcoTrails.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EcoTrails.Api.Tests;

public class OpenAiAssistantServiceFallbackOrchestrationTests
{
    [Fact]
    public async Task GenerateReplyAsync_WhenGeminiFails_AndPolicyAllows_FallsBackToOpenAi()
    {
        await using var dbContext = TestDbContextFactory.CreateContext();
        var aiClient = new RecordingAiProviderClient(
            onGemini: (_, _) => throw new AiProviderException(StatusCodes.Status503ServiceUnavailable, "gemini unavailable"),
            onOpenAi: (_, _) => "openai-fallback-ok");

        var fallbackPolicy = new StubFallbackPolicy
        {
            ShouldFallbackToOpenAiFromGeminiResult = true,
            ShouldFallbackToSecondaryOpenAiResult = false,
            ResolvedOpenAiFallbackModel = "gpt-4o"
        };

        var service = CreateService(
            dbContext,
            aiClient,
            fallbackPolicy,
            options =>
            {
                options.Provider = "gemini";
                options.GeminiApiKey = "gemini-key";
                options.OpenAiModel = "gpt-4o";
            });

        var response = await service.GenerateReplyAsync(
            new AssistantChatRequest { Prompt = "Искам маршрут около София" },
            currentUserId: "user-1",
            CancellationToken.None);

        Assert.Equal("openai-fallback-ok", response.Reply);
        Assert.Single(aiClient.GeminiModels);
        Assert.Single(aiClient.OpenAiModels);
        Assert.Equal("gpt-4o", aiClient.OpenAiModels[0]);
    }

    [Fact]
    public async Task GenerateReplyAsync_WhenPrimaryOpenAiModelFails_AndPolicyAllows_UsesSecondaryModel()
    {
        await using var dbContext = TestDbContextFactory.CreateContext();
        var aiClient = new RecordingAiProviderClient(
            onGemini: (_, _) => "unused",
            onOpenAi: (model, _) => model == "gpt-4o-mini"
                ? throw new AiProviderException(StatusCodes.Status404NotFound, "model not found")
                : "secondary-openai-ok");

        var fallbackPolicy = new StubFallbackPolicy
        {
            ShouldFallbackToOpenAiFromGeminiResult = false,
            ShouldFallbackToSecondaryOpenAiResult = true,
            ResolvedOpenAiFallbackModel = "gpt-4o"
        };

        var service = CreateService(
            dbContext,
            aiClient,
            fallbackPolicy,
            options =>
            {
                options.Provider = "openai";
                options.ApiKey = "openai-key";
                options.OpenAiModel = "gpt-4o-mini";
                options.OpenAiFallbackModel = "gpt-4o";
            });

        var response = await service.GenerateReplyAsync(
            new AssistantChatRequest { Prompt = "Препоръчай ми умерен маршрут" },
            currentUserId: "user-1",
            CancellationToken.None);

        Assert.Equal("secondary-openai-ok", response.Reply);
        Assert.Equal(2, aiClient.OpenAiModels.Count);
        Assert.Equal("gpt-4o-mini", aiClient.OpenAiModels[0]);
        Assert.Equal("gpt-4o", aiClient.OpenAiModels[1]);
    }

    [Fact]
    public async Task GenerateReplyAsync_WhenContextPromptThrows_AndFailOpenEnabled_FallsBackToCurrentPrompt()
    {
        await using var dbContext = TestDbContextFactory.CreateContext();
        var aiClient = new RecordingAiProviderClient(
            onGemini: (_, _) => "unused",
            onOpenAi: (_, userPrompt) => userPrompt.Contains("mode:context_prompt", StringComparison.Ordinal)
                ? throw new InvalidOperationException("context prompt path failed")
                : "current-mode-ok");

        var fallbackPolicy = new StubFallbackPolicy
        {
            ShouldFallbackToOpenAiFromGeminiResult = false,
            ShouldFallbackToSecondaryOpenAiResult = false,
            ResolvedOpenAiFallbackModel = null
        };

        var service = CreateService(
            dbContext,
            aiClient,
            fallbackPolicy,
            options =>
            {
                options.Provider = "openai";
                options.ApiKey = "openai-key";
                options.AssistantMode = "context_prompt";
                options.PromptTemplateShadowMode = false;
                options.PromptTemplateFailOpen = true;
            },
            new StubAssistantPromptAssemblyService { ResolvedMode = "context_prompt" });

        var response = await service.GenerateReplyAsync(
            new AssistantChatRequest { Prompt = "Искам маршрут с детайлен контекст" },
            currentUserId: "user-1",
            CancellationToken.None);

        Assert.Equal("current-mode-ok", response.Reply);
        Assert.Equal(2, aiClient.OpenAiUserPrompts.Count);
        Assert.Contains("mode:context_prompt", aiClient.OpenAiUserPrompts[0], StringComparison.Ordinal);
        Assert.Contains("mode:current", aiClient.OpenAiUserPrompts[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateReplyAsync_WhenContextPromptReturnsEmpty_AndFailOpenEnabled_FallsBackToCurrentPrompt()
    {
        await using var dbContext = TestDbContextFactory.CreateContext();
        var aiClient = new RecordingAiProviderClient(
            onGemini: (_, _) => "unused",
            onOpenAi: (_, userPrompt) => userPrompt.Contains("mode:context_prompt", StringComparison.Ordinal)
                ? string.Empty
                : "current-after-empty-ok");

        var fallbackPolicy = new StubFallbackPolicy
        {
            ShouldFallbackToOpenAiFromGeminiResult = false,
            ShouldFallbackToSecondaryOpenAiResult = false,
            ResolvedOpenAiFallbackModel = null
        };

        var service = CreateService(
            dbContext,
            aiClient,
            fallbackPolicy,
            options =>
            {
                options.Provider = "openai";
                options.ApiKey = "openai-key";
                options.AssistantMode = "context_prompt";
                options.PromptTemplateShadowMode = false;
                options.PromptTemplateFailOpen = true;
            },
            new StubAssistantPromptAssemblyService { ResolvedMode = "context_prompt" });

        var response = await service.GenerateReplyAsync(
            new AssistantChatRequest { Prompt = "Искам маршрут с контекст и fallback" },
            currentUserId: "user-1",
            CancellationToken.None);

        Assert.Equal("current-after-empty-ok", response.Reply);
        Assert.Equal(2, aiClient.OpenAiUserPrompts.Count);
        Assert.Contains("mode:context_prompt", aiClient.OpenAiUserPrompts[0], StringComparison.Ordinal);
        Assert.Contains("mode:current", aiClient.OpenAiUserPrompts[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateReplyAsync_WhenShadowModeEnabled_UsesCurrentPromptServingPath()
    {
        await using var dbContext = TestDbContextFactory.CreateContext();
        var aiClient = new RecordingAiProviderClient(
            onGemini: (_, _) => "unused",
            onOpenAi: (_, userPrompt) => userPrompt.Contains("mode:context_prompt", StringComparison.Ordinal)
                ? "unexpected-context-mode"
                : "shadow-current-ok");

        var fallbackPolicy = new StubFallbackPolicy
        {
            ShouldFallbackToOpenAiFromGeminiResult = false,
            ShouldFallbackToSecondaryOpenAiResult = false,
            ResolvedOpenAiFallbackModel = null
        };

        var service = CreateService(
            dbContext,
            aiClient,
            fallbackPolicy,
            options =>
            {
                options.Provider = "openai";
                options.ApiKey = "openai-key";
                options.AssistantMode = "context_prompt";
                options.PromptTemplateShadowMode = true;
                options.PromptTemplateFailOpen = true;
            },
            new StubAssistantPromptAssemblyService { ResolvedMode = "context_prompt" });

        var response = await service.GenerateReplyAsync(
            new AssistantChatRequest { Prompt = "Провери shadow режим" },
            currentUserId: "user-1",
            CancellationToken.None);

        Assert.Equal("shadow-current-ok", response.Reply);
        Assert.Single(aiClient.OpenAiUserPrompts);
        Assert.DoesNotContain("mode:context_prompt", aiClient.OpenAiUserPrompts[0], StringComparison.Ordinal);
        Assert.Contains("mode:current", aiClient.OpenAiUserPrompts[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateReplyAsync_WhenFailOpenDisabled_PropagatesContextPromptException()
    {
        await using var dbContext = TestDbContextFactory.CreateContext();
        var aiClient = new RecordingAiProviderClient(
            onGemini: (_, _) => "unused",
            onOpenAi: (_, userPrompt) => userPrompt.Contains("mode:context_prompt", StringComparison.Ordinal)
                ? throw new InvalidOperationException("strict context failure")
                : "unexpected-current-path");

        var fallbackPolicy = new StubFallbackPolicy
        {
            ShouldFallbackToOpenAiFromGeminiResult = false,
            ShouldFallbackToSecondaryOpenAiResult = false,
            ResolvedOpenAiFallbackModel = null
        };

        var service = CreateService(
            dbContext,
            aiClient,
            fallbackPolicy,
            options =>
            {
                options.Provider = "openai";
                options.ApiKey = "openai-key";
                options.AssistantMode = "context_prompt";
                options.PromptTemplateShadowMode = false;
                options.PromptTemplateFailOpen = false;
            },
            new StubAssistantPromptAssemblyService { ResolvedMode = "context_prompt" });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.GenerateReplyAsync(
                new AssistantChatRequest { Prompt = "Strict mode without fallback" },
                currentUserId: "user-1",
                CancellationToken.None));

        Assert.Equal("strict context failure", exception.Message);
        Assert.Single(aiClient.OpenAiUserPrompts);
        Assert.Contains("mode:context_prompt", aiClient.OpenAiUserPrompts[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateReplyAsync_WhenFailOpenDisabled_PropagatesContextPromptError()
    {
        await using var dbContext = TestDbContextFactory.CreateContext();
        var aiClient = new RecordingAiProviderClient(
            onGemini: (_, _) => "unused",
            onOpenAi: (_, userPrompt) => userPrompt.Contains("mode:context_prompt", StringComparison.Ordinal)
                ? throw new InvalidOperationException("context mode hard failure")
                : "unexpected-current-fallback");

        var fallbackPolicy = new StubFallbackPolicy
        {
            ShouldFallbackToOpenAiFromGeminiResult = false,
            ShouldFallbackToSecondaryOpenAiResult = false,
            ResolvedOpenAiFallbackModel = null
        };

        var service = CreateService(
            dbContext,
            aiClient,
            fallbackPolicy,
            options =>
            {
                options.Provider = "openai";
                options.ApiKey = "openai-key";
                options.AssistantMode = "context_prompt";
                options.PromptTemplateShadowMode = false;
                options.PromptTemplateFailOpen = false;
            },
            new StubAssistantPromptAssemblyService { ResolvedMode = "context_prompt" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateReplyAsync(
            new AssistantChatRequest { Prompt = "Strict context mode" },
            currentUserId: "user-1",
            CancellationToken.None));

        Assert.Single(aiClient.OpenAiUserPrompts);
        Assert.Contains("mode:context_prompt", aiClient.OpenAiUserPrompts[0], StringComparison.Ordinal);
    }

    private static OpenAiAssistantService CreateService(
        EcoTrails.Api.Data.AppDbContext dbContext,
        RecordingAiProviderClient aiClient,
        StubFallbackPolicy fallbackPolicy,
        Action<OpenAiOptions>? configure,
        IAssistantPromptAssemblyService? promptAssemblyService = null)
    {
        var options = new OpenAiOptions();
        configure?.Invoke(options);

        return new OpenAiAssistantService(
            new HttpClient(),
            Options.Create(options),
            dbContext,
            new StubAssistantMessageRepository(),
            new StubAssistantSessionOrchestrationService(),
            new StubVectorService(),
            new AssistantPromptSafetyService(),
            promptAssemblyService ?? new StubAssistantPromptAssemblyService(),
            new StubAssistantProvenancePolicyService(),
            new StubAssistantRetrievalService(),
            new StubAssistantEnrichmentWorkflowService(),
            new StubAssistantResponseCompositionService(),
            aiClient,
            fallbackPolicy,
            new StubAssistantWeatherContextService(),
            NullLogger<OpenAiAssistantService>.Instance);
    }

    private sealed class RecordingAiProviderClient : IAiProviderClient
    {
        private readonly Func<string, string, string> _onOpenAi;
        private readonly Func<string, string, string> _onGemini;

        public RecordingAiProviderClient(Func<string, string, string> onGemini, Func<string, string, string> onOpenAi)
        {
            _onGemini = onGemini;
            _onOpenAi = onOpenAi;
        }

        public List<string> OpenAiModels { get; } = [];
        public List<string> OpenAiUserPrompts { get; } = [];
        public List<string> GeminiModels { get; } = [];

        public Task<string> SendOpenAiRequestAsync(string model, string systemInstruction, List<AssistantChatMessage> history, string userPrompt, double temperature, int maxTokens, CancellationToken cancellationToken, bool forceJsonResponse)
        {
            OpenAiModels.Add(model);
            OpenAiUserPrompts.Add(userPrompt);
            return Task.FromResult(_onOpenAi(model, userPrompt));
        }

        public Task<string> SendGeminiRequestAsync(string model, string systemInstruction, List<AssistantChatMessage> history, string userPrompt, double temperature, int maxTokens, CancellationToken cancellationToken, bool forceJsonResponse)
        {
            GeminiModels.Add(model);
            return Task.FromResult(_onGemini(model, userPrompt));
        }
    }

    private sealed class StubFallbackPolicy : IAiProviderFallbackPolicy
    {
        public bool ShouldFallbackToSecondaryOpenAiResult { get; set; }
        public bool ShouldFallbackToOpenAiFromGeminiResult { get; set; }
        public string? ResolvedOpenAiFallbackModel { get; set; }

        public string? ResolveOpenAiFallbackModel(string primaryModel) => ResolvedOpenAiFallbackModel;

        public bool ShouldFallbackToSecondaryOpenAiModel(string primaryModel, AiProviderException exception)
            => ShouldFallbackToSecondaryOpenAiResult;

        public bool ShouldFallbackToOpenAiFromGemini(AiProviderException exception)
            => ShouldFallbackToOpenAiFromGeminiResult;
    }

    private sealed class StubAssistantMessageRepository : IAssistantMessageRepository
    {
        public Task<List<AssistantChatMessage>> GetRecentMessagesAsync(int sessionInternalId, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<AssistantChatMessage>());

        public Task SaveConversationTurnAsync(AssistantChatSession session, string userPrompt, string assistantReply, string? updatedTitle, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubAssistantSessionOrchestrationService : IAssistantSessionOrchestrationService
    {
        public Task<AssistantChatSession> GetOrCreateSessionAsync(string? sessionId, string prompt, string? currentUserId, CancellationToken cancellationToken)
            => Task.FromResult(new AssistantChatSession
            {
                Id = 1,
                SessionId = string.IsNullOrWhiteSpace(sessionId) ? Guid.NewGuid().ToString("N") : sessionId!,
                AppUserId = currentUserId,
                Title = "Нова сесия",
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
            });

        public string BuildSessionTitle(string prompt)
            => string.IsNullOrWhiteSpace(prompt) ? "Нова сесия" : prompt.Trim();

        public Task<AssistantSessionResponse> CreateSessionAsync(AssistantSessionCreateRequest request, string? currentUserId, CancellationToken cancellationToken)
            => Task.FromResult(new AssistantSessionResponse());

        public Task<List<AssistantSessionResponse>> GetUserSessionsAsync(string userId, int limit, CancellationToken cancellationToken)
            => Task.FromResult(new List<AssistantSessionResponse>());

        public Task<List<AssistantSessionMessageResponse>> GetSessionMessagesAsync(string sessionId, string? currentUserId, int limit, CancellationToken cancellationToken)
            => Task.FromResult(new List<AssistantSessionMessageResponse>());

        public Task<bool> DeleteSessionAsync(string sessionId, string? currentUserId, CancellationToken cancellationToken)
            => Task.FromResult(true);
    }

    private sealed class StubVectorService : IVectorService
    {
        public Task<VectorEmbeddingResult> CreateEmbeddingAsync(string input, CancellationToken cancellationToken)
            => Task.FromResult(new VectorEmbeddingResult { Model = "stub", Values = [0.1f, 0.2f] });

        public Task<VectorEmbeddingsBatchResult> CreateEmbeddingsAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken)
            => Task.FromResult(new VectorEmbeddingsBatchResult { Model = "stub", Values = inputs.Select(_ => new List<float> { 0.1f, 0.2f }).ToList() });
    }

    private sealed class StubAssistantPromptAssemblyService : IAssistantPromptAssemblyService
    {
        public string ResolvedMode { get; set; } = "current";

        public string ResolveAssistantMode() => ResolvedMode;

        public string BuildSystemInstruction(string mode, bool hasReliabilityWarning, bool isPotentialInjection)
            => $"system:{mode}";

        public string BuildUserPromptByMode(string mode, AssistantChatRequest request, string safePrompt, List<AssistantTrailContext> trails, List<AssistantTrailContext> alternatives, string? weatherContext, string? reliabilityNote, bool isPotentialInjection)
            => $"mode:{mode}|prompt:{safePrompt}";
    }

    private sealed class StubAssistantProvenancePolicyService : IAssistantProvenancePolicyService
    {
        public Task<AssistantProvenanceContextResult> BuildContextAsync(List<AssistantTrailContext> trails, List<AssistantTrailContext> alternatives, CancellationToken cancellationToken)
            => Task.FromResult(new AssistantProvenanceContextResult(trails, alternatives, false, null));
    }

    private sealed class StubAssistantRetrievalService : IAssistantRetrievalService
    {
        public Task<List<AssistantTrailContext>> FindRelevantTrailsAsync(string prompt, AssistantChatRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new List<AssistantTrailContext>
            {
                new()
                {
                    Id = 1,
                    Name = "Тест пътека",
                    Location = "София",
                    Region = "София",
                    Difficulty = 2,
                    DifficultyLevel = "moderate",
                    WaterSources = true,
                    SuitableForKids = true,
                    RequiredGear = ["обувки"]
                }
            });

        public Task<List<AssistantTrailContext>> GetAlternativeTrailsAsync(string prompt, List<AssistantTrailContext> contextTrails, AssistantChatRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new List<AssistantTrailContext>());
    }

    private sealed class StubAssistantEnrichmentWorkflowService : IAssistantEnrichmentWorkflowService
    {
        public Task<AssistantEnrichResponse> ExecuteAsync(AssistantEnrichRequest request, Func<Trail, CancellationToken, Task<AssistantTrailSemanticData>> extractSemanticDataAsync, CancellationToken cancellationToken)
            => Task.FromResult(new AssistantEnrichResponse());
    }

    private sealed class StubAssistantResponseCompositionService : IAssistantResponseCompositionService
    {
        public List<AssistantKnowledgeChip> BuildKnowledgeChips(List<AssistantTrailContext> trails, List<AssistantTrailContext> alternatives, bool hasReliabilityWarning, bool isPotentialInjection)
            => [];

        public List<AssistantQuickAction> BuildQuickActions(List<AssistantTrailContext> trails, List<AssistantTrailContext> alternatives, AssistantChatRequest request, string prompt)
            => [];
    }

    private sealed class StubAssistantWeatherContextService : IAssistantWeatherContextService
    {
        public bool IsWeatherPrompt(string prompt) => false;

        public Task<string?> BuildWeatherContextAsync(string prompt, List<AssistantTrailContext> trails, CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);
    }
}
