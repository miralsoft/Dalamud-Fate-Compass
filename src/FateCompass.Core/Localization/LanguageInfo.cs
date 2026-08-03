namespace FateCompass.Core.Localization;

/// <summary>
/// One supported interface language.
/// </summary>
/// <remarks>
/// The code is a plain lowercase two-letter tag ("en", "de"). It is deliberately a string
/// rather than an enum, because adding a language should mean adding a catalogue file, not
/// changing a type and recompiling everything that switches on it.
/// </remarks>
public sealed record LanguageInfo(string Code, string NativeName)
{
    /// <summary>The fallback language. Every key is expected to exist here.</summary>
    public const string FallbackCode = "en";

    /// <summary>Follow the language Dalamud is set to, rather than choosing explicitly.</summary>
    public const string AutomaticCode = "auto";

    public static LanguageInfo Automatic { get; } = new(AutomaticCode, "Automatic");
}
