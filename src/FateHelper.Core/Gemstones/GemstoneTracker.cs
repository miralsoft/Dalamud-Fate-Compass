using FateHelper.Core.Configuration;

namespace FateHelper.Core.Gemstones;

/// <summary>
/// Judges the bicolor gemstone purse against its cap.
/// </summary>
/// <remarks>
/// Worth having because gemstones are usually the point of farming FATEs, and every reward
/// earned at the cap is simply lost. Both the count and the cap are read from the game by the
/// adapter, so no cap value is hard-coded here.
/// </remarks>
public static class GemstoneTracker
{
    public static GemstoneStatus Evaluate(int current, int cap, FateHelperSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (cap <= 0)
        {
            return GemstoneStatus.Ok;
        }

        var clamped = Math.Clamp(current, 0, cap);
        if (clamped >= cap)
        {
            return GemstoneStatus.Full;
        }

        var headroom = cap - clamped;
        return headroom <= Math.Max(settings.GemstoneWarningHeadroom, 0)
            ? GemstoneStatus.Warning
            : GemstoneStatus.Ok;
    }

    /// <summary>Capacity left before rewards start being wasted. Never negative.</summary>
    public static int Headroom(int current, int cap) =>
        cap <= 0 ? 0 : Math.Max(cap - Math.Clamp(current, 0, cap), 0);
}
