using EcoTrails.Api.Services;

namespace EcoTrails.Api.Tests;

public class TrailSearchTextMatcherTests
{
    [Fact]
    public void ContainsExactToken_DoesNotMatchSubstringInsideAnotherWord()
    {
        var result = TrailSearchTextMatcher.ContainsExactToken("Този текст е открила нов маршрут.", "рила");

        Assert.False(result);
    }

    [Fact]
    public void ContainsExactToken_MatchesWholeWord()
    {
        var result = TrailSearchTextMatcher.ContainsExactToken("Маршрут в регион Рила с панорамни гледки.", "рила");

        Assert.True(result);
    }

    [Fact]
    public void ExtractPromptTokens_NormalizesAndDeduplicatesWords()
    {
        var tokens = TrailSearchTextMatcher.ExtractPromptTokens("Искам маршрут в Рила, рила, с вода и деца");

        Assert.Contains("рила", tokens);
        Assert.Equal(1, tokens.Count(item => item == "рила"));
        Assert.DoesNotContain(tokens, item => item.Length < 3);
    }
}