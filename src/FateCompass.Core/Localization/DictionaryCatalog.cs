namespace FateCompass.Core.Localization;

/// <summary>
/// A catalogue backed by a plain dictionary, which is what a loaded JSON language file becomes.
/// </summary>
public sealed class DictionaryCatalog : ITranslationCatalog
{
    private readonly Dictionary<string, string> entries;

    public DictionaryCatalog(string code, IReadOnlyDictionary<string, string> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(entries);

        Code = code.ToLowerInvariant();
        this.entries = new Dictionary<string, string>(entries, StringComparer.Ordinal);
    }

    public string Code { get; }

    public IReadOnlyCollection<string> Keys => entries.Keys;

    public bool TryGet(string key, out string value) => entries.TryGetValue(key, out value!);
}
