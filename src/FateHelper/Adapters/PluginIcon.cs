using Dalamud.Interface.Textures.TextureWraps;
using FateHelper.Services;

namespace FateHelper.Adapters;

/// <summary>
/// The plugin's own icon, for the places inside the game where the plugin has to look like
/// itself.
/// </summary>
/// <remarks>
/// The artwork lives in <c>src/FateHelper/images</c> in two versions, because the places that
/// show it want different things. <c>icon.png</c> keeps its frame and goes to Dalamud's plugin
/// list, both as a file beside the packaged plugin and through the manifest's <c>IconUrl</c>.
/// <c>minimap.png</c> is the same emblem cut out, without frame or backdrop, for drawing over the
/// game's own interface, and that is the one compiled in here.
/// <para>
/// Embedded rather than loaded from disk. The packaged plugin does not carry the images inside
/// its zip, so a path-based load would work for a development build and quietly fall back to the
/// game icon for everyone who installed it normally. That is exactly the kind of split that is
/// invisible to whoever built it.
/// </para>
/// </remarks>
internal static class PluginIcon
{
    /// <summary>
    /// Assembly resource name of the icon: root namespace, then the path with dots.
    /// </summary>
    private const string ResourceName = "FateHelper.images.minimap.png";

    /// <summary>
    /// Stands in until the plugin has an icon of its own, and whenever loading one fails. A
    /// game icon rather than nothing, so the control never disappears (FH-07).
    /// </summary>
    private const uint FallbackGameIconId = 60093;

    /// <summary>
    /// Whether the icon is compiled in, worked out once. Asking the texture provider for a
    /// resource that does not exist would repeat the same failure every frame.
    /// </summary>
    private static bool? isEmbedded;

    /// <summary>
    /// The icon to draw, or null while neither it nor the fallback could be loaded.
    /// </summary>
    /// <remarks>
    /// The returned wrap belongs to Dalamud's shared texture cache and must not be disposed by
    /// the caller, which is why this hands back the same kind of thing the game icon lookup does.
    /// </remarks>
    internal static IDalamudTextureWrap? Texture()
    {
        try
        {
            if (IsEmbedded())
            {
                var own = DalamudServices.TextureProvider
                    .GetFromManifestResource(typeof(PluginIcon).Assembly, ResourceName)
                    .GetWrapOrDefault();

                if (own is not null)
                {
                    return own;
                }
            }

            return DalamudServices.TextureProvider.GetFromGameIcon(FallbackGameIconId).GetWrapOrDefault();
        }
        catch
        {
            // An icon is decoration. Nothing about it is worth risking the frame for (FH-09).
            return null;
        }
    }

    private static bool IsEmbedded()
    {
        isEmbedded ??= Array.Exists(
            typeof(PluginIcon).Assembly.GetManifestResourceNames(),
            name => string.Equals(name, ResourceName, StringComparison.Ordinal));

        return isEmbedded.Value;
    }
}
