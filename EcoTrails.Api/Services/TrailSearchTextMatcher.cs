using System.Text.RegularExpressions;

namespace EcoTrails.Api.Services;

public static partial class TrailSearchTextMatcher
{
    [GeneratedRegex("[\\p{L}\\p{Nd}]+", RegexOptions.Compiled)]
    private static partial Regex SearchTokenRegex();

    public static List<string> ExtractPromptTokens(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return [];
        }

        return SearchTokenRegex()
            .Matches(prompt.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(value => value.Length >= 3)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public static bool ContainsExactToken(string text, string token)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var normalizedToken = token.Trim().ToLowerInvariant();
        if (normalizedToken.Length == 0)
        {
            return false;
        }

        return SearchTokenRegex()
            .Matches(text.ToLowerInvariant())
            .Any(match => string.Equals(match.Value, normalizedToken, StringComparison.Ordinal));
    }
}