namespace FateCompass.Core.Localization;

/// <summary>
/// Turns a configured language choice into the catalogue that should actually be used.
/// </summary>
/// <remarks>
/// Pure on purpose. The plugin layer passes in what Dalamud reports as its interface language
/// and which catalogues were loaded, and gets back a code. That keeps the decision testable and
/// stops the fallback rules from being scattered across the UI.
/// </remarks>
public static class LanguageResolver
{
    /// <summary>
    /// Resolves the language to use.
    /// </summary>
    /// <param name="configured">What the player chose: a code, or "auto".</param>
    /// <param name="clientLanguage">
    /// What Dalamud reports as its interface language. Accepts both a bare tag ("de") and a
    /// culture name ("de-DE"), because the exact shape is not guaranteed.
    /// </param>
    /// <param name="available">The catalogues that were actually loaded.</param>
    public static string Resolve(
        string? configured,
        string? clientLanguage,
        IReadOnlyCollection<string> available)
    {
        ArgumentNullException.ThrowIfNull(available);

        if (available.Count == 0)
        {
            return LanguageInfo.FallbackCode;
        }

        var wanted = string.IsNullOrWhiteSpace(configured) || IsAutomatic(configured)
            ? Normalise(clientLanguage)
            : Normalise(configured);

        var match = available.FirstOrDefault(
            code => string.Equals(code, wanted, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            return match;
        }

        // Nothing matched, so use English if it is there, otherwise whatever exists. Returning
        // an unavailable code would leave the window empty.
        return available.FirstOrDefault(
            code => string.Equals(code, LanguageInfo.FallbackCode, StringComparison.OrdinalIgnoreCase))
            ?? available.First();
    }

    public static bool IsAutomatic(string? code) =>
        string.Equals(code, LanguageInfo.AutomaticCode, StringComparison.OrdinalIgnoreCase);

    /// <summary>Reduces "de-DE" or "DE" to "de". An empty input becomes the fallback.</summary>
    private static string Normalise(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return LanguageInfo.FallbackCode;
        }

        var trimmed = language.Trim();
        var separator = trimmed.IndexOfAny(['-', '_']);
        if (separator > 0)
        {
            trimmed = trimmed[..separator];
        }

        return trimmed.ToLowerInvariant();
    }
}
