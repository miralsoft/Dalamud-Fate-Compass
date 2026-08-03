using System.Globalization;

namespace FateCompass.Core.Localization;

/// <summary>
/// Resolves a string key into text for the active language.
/// </summary>
/// <remarks>
/// The fallback chain is deliberate and never throws: active language, then English, then the
/// key itself. A missing translation therefore shows an ugly key in the window rather than
/// crashing the plugin or blanking the row, which makes the gap obvious during use instead of
/// dangerous (S-09).
/// </remarks>
public sealed class Localizer
{
    private readonly Dictionary<string, ITranslationCatalog> catalogues = new(StringComparer.OrdinalIgnoreCase);
    private ITranslationCatalog? active;
    private ITranslationCatalog? fallback;

    /// <summary>The language currently in use, resolved from the request and what is available.</summary>
    public string ActiveCode => active?.Code ?? LanguageInfo.FallbackCode;

    public IReadOnlyCollection<string> AvailableCodes => catalogues.Keys;

    public void Register(ITranslationCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        catalogues[catalog.Code] = catalog;

        if (string.Equals(catalog.Code, LanguageInfo.FallbackCode, StringComparison.OrdinalIgnoreCase))
        {
            fallback = catalog;
        }

        active ??= catalog;
    }

    /// <summary>
    /// Switches language. An unknown code falls back rather than failing, so a stale setting or
    /// a removed catalogue file cannot leave the plugin unusable.
    /// </summary>
    public void Use(string code)
    {
        if (!string.IsNullOrWhiteSpace(code) && catalogues.TryGetValue(code, out var catalog))
        {
            active = catalog;
            return;
        }

        active = fallback ?? catalogues.Values.FirstOrDefault();
    }

    public string Get(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        if (active is not null && active.TryGet(key, out var value))
        {
            return value;
        }

        if (fallback is not null && fallback.TryGet(key, out var fallbackValue))
        {
            return fallbackValue;
        }

        return key;
    }

    /// <summary>
    /// Formats a translated string. A broken format placeholder in a translation returns the
    /// unformatted text instead of throwing, because a bad translation must not take the window
    /// down with it.
    /// </summary>
    public string Format(string key, params object[] args)
    {
        var template = Get(key);

        if (args is null || args.Length == 0)
        {
            return template;
        }

        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    /// <summary>
    /// Keys present in the fallback but missing from the given language. Used by the tests to
    /// keep the catalogues in step, so a new string cannot ship English-only unnoticed.
    /// </summary>
    public IReadOnlyList<string> MissingKeys(string code)
    {
        if (fallback is null || !catalogues.TryGetValue(code, out var catalog))
        {
            return [];
        }

        return [.. fallback.Keys.Where(key => !catalog.TryGet(key, out _)).Order(StringComparer.Ordinal)];
    }
}
