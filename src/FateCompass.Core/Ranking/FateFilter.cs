using FateCompass.Core.Configuration;
using FateCompass.Core.Model;

namespace FateCompass.Core.Ranking;

/// <summary>
/// Decides whether a FATE passes the player's kind and level filter.
/// </summary>
public static class FateFilter
{
    /// <summary>
    /// True when the FATE should be shown. An unclassified FATE always passes the kind check,
    /// because a gap in classification must not hide content from the player.
    /// </summary>
    public static bool IsIncluded(FateSnapshot fate, FateCompassSettings settings)
    {
        ArgumentNullException.ThrowIfNull(fate);
        ArgumentNullException.ThrowIfNull(settings);

        if (fate.Kind != FateKind.Unknown && settings.ExcludedKinds.Contains(fate.Kind))
        {
            return false;
        }

        if (settings.MinimumLevel is { } min && fate.Level < min)
        {
            return false;
        }

        if (settings.MaximumLevel is { } max && fate.Level > max)
        {
            return false;
        }

        return true;
    }
}
