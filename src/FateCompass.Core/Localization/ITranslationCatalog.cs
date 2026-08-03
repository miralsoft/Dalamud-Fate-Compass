namespace FateCompass.Core.Localization;

/// <summary>
/// The translated strings for one language.
/// </summary>
public interface ITranslationCatalog
{
    /// <summary>Lowercase language tag, for example "de".</summary>
    string Code { get; }

    bool TryGet(string key, out string value);

    /// <summary>Every key this catalogue defines. Used to report gaps against the fallback.</summary>
    IReadOnlyCollection<string> Keys { get; }
}
