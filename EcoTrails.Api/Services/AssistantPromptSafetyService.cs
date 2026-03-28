using System.Text.RegularExpressions;

namespace EcoTrails.Api.Services;

public sealed class AssistantPromptSafetyService : IAssistantPromptSafetyService
{
    private static readonly Regex PromptInjectionRegex = new(
        "(ignore\\s+(all|any|previous|above)\\s+instructions|system\\s*prompt|developer\\s*message|role\\s*:\\s*system|jailbreak|reveal\\s+.*(secret|key|token)|<\\s*/?\\s*system\\s*>)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public bool IsPotentialPromptInjection(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return false;
        }

        return PromptInjectionRegex.IsMatch(prompt);
    }

    public string SanitizePrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return string.Empty;
        }

        var sanitized = PromptInjectionRegex.Replace(prompt, "[REDACTED]");
        return Regex.Replace(sanitized, "\\s+", " ").Trim();
    }
}
