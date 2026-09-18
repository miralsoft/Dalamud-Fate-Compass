using FateCompass.Core.Configuration;
using FateCompass.Core.Model;

namespace FateCompass.Core.Ranking;

/// <summary>
/// Works out how a FATE's level sits against the player's, as a <see cref="LevelFit"/>.
/// </summary>
/// <remarks>
/// A pure function of its inputs, in the core, so the boundaries can be tested without a game.
/// </remarks>
public static class LevelFitEvaluator
{
    /// <summary>
    /// Judges one FATE against the player. Returns <see cref="LevelFit.NotApplicable"/> wherever
    /// a comparison would mean nothing, which is the honest answer far more often than it looks.
    /// </summary>
    public static LevelFit Evaluate(
        FateSnapshot fate,
        PlayerSnapshot player,
        FateCompassSettings settings)
    {
        ArgumentNullException.ThrowIfNull(fate);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.LevelFitEnabled)
        {
            return LevelFit.NotApplicable;
        }

        // The zone decides this, not the activity source, and the difference is the whole trap.
        // Eureka's notorious monsters arrive through the FATE table like any other FATE, so a
        // check on ActivitySource would have let them through and compared a job level against a
        // fight that answers to elemental level instead. Bozja and the Crescent are the same
        // story with resistance ranks. Outside the open world this question has no answer worth
        // giving, so none is given (S-12).
        if (player.Content != ContentKind.Overworld || fate.Source != ActivitySource.Fate)
        {
            return LevelFit.NotApplicable;
        }

        // Either level missing means the snapshot was taken before the game had an answer.
        if (player.Level == 0 || fate.Level == 0)
        {
            return LevelFit.NotApplicable;
        }

        var deficit = fate.Level - player.Level;
        if (deficit <= 0)
        {
            // The other side of the scale, and it is off unless asked for. A FATE far under you
            // is never a problem, so this says "probably not what you are levelling on" and
            // nothing stronger. It is deliberately not an exclusion: at maximum level in a
            // starter zone every FATE lands here, and those are exactly the ones somebody
            // farming gemstones is going to.
            if (!settings.LevelFitShowFarBelow)
            {
                return LevelFit.Comfortable;
            }

            var farBelow = Math.Max(settings.LevelFitFarBelowBy, 1);
            return -deficit >= farBelow ? LevelFit.FarBelow : LevelFit.Comfortable;
        }

        // Both bounds come from the settings, so a player who disagrees can move them. Reading
        // them in the wrong order would silently swallow one band, so the upper is held at or
        // above the lower rather than trusted.
        var marginal = Math.Max(settings.LevelFitMarginalBelow, 1);
        var tight = Math.Max(settings.LevelFitTightBelow, marginal);

        if (deficit <= marginal)
        {
            return LevelFit.Marginal;
        }

        return deficit <= tight ? LevelFit.Tight : LevelFit.OutOfReach;
    }

    /// <summary>
    /// How many levels the player is under this FATE, or zero when they are not under it.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Evaluate"/> because the window shows the number itself. A band
    /// name tells you which colour to use; the number is what answers "how far off am I".
    /// </remarks>
    public static int LevelsBelow(FateSnapshot fate, PlayerSnapshot player)
    {
        ArgumentNullException.ThrowIfNull(fate);
        ArgumentNullException.ThrowIfNull(player);

        return Math.Max(fate.Level - player.Level, 0);
    }

    /// <summary>
    /// How many levels this FATE is under the player, or zero when it is not under them.
    /// </summary>
    /// <remarks>
    /// The mirror of <see cref="LevelsBelow"/>. Two methods rather than one signed number,
    /// because a caller that wants "how far under me is this" should not have to remember which
    /// direction the sign runs, and a tooltip that got it backwards would read perfectly well
    /// while saying the opposite of the truth.
    /// </remarks>
    public static int LevelsAbove(FateSnapshot fate, PlayerSnapshot player)
    {
        ArgumentNullException.ThrowIfNull(fate);
        ArgumentNullException.ThrowIfNull(player);

        return Math.Max(player.Level - fate.Level, 0);
    }
}
