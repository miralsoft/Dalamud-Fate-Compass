using System.Reflection;
using System.Text.Json;

namespace FateCompass.Core.Localization;

/// <summary>
/// Loads the language files that ship inside this assembly.
/// </summary>
/// <remarks>
/// Adding a language means adding a JSON file under <c>Localization/Catalogs</c> and nothing
/// else. The file name is the language code. Nothing here needs changing, and no other code
/// switches on the set of languages.
/// <para>
/// Kept in the core rather than the plugin so the completeness test can check every catalogue
/// against the fallback without needing a game installation.
/// </para>
/// </remarks>
public static class EmbeddedCatalogs
{
    private const string ResourcePrefix = "FateCompass.Core.Localization.Catalogs.";
    private const string ResourceSuffix = ".json";

    /// <summary>
    /// Loads every embedded catalogue. A file that fails to parse is skipped rather than
    /// taking the plugin down, because a broken translation must not cost the player the tool
    /// (S-09).
    /// </summary>
    public static IReadOnlyList<ITranslationCatalog> LoadAll()
    {
        var assembly = typeof(EmbeddedCatalogs).Assembly;
        var catalogues = new List<ITranslationCatalog>();

        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                || !resource.EndsWith(ResourceSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            var code = resource[ResourcePrefix.Length..^ResourceSuffix.Length];
            var catalog = Load(assembly, resource, code);
            if (catalog is not null)
            {
                catalogues.Add(catalog);
            }
        }

        return catalogues;
    }

    private static DictionaryCatalog? Load(Assembly assembly, string resource, string code)
    {
        try
        {
            using var stream = assembly.GetManifestResourceStream(resource);
            if (stream is null)
            {
                return null;
            }

            var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
            return entries is null ? null : new DictionaryCatalog(code, entries);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
