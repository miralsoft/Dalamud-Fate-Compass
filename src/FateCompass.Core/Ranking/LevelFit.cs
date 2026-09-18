namespace FateCompass.Core.Ranking;

/// <summary>
/// How a FATE's level sits against the player's own, for somebody levelling who wants to know
/// what is worth walking to.
/// </summary>
/// <remarks>
/// <para>
/// These are an estimate and the code says so rather than pretending otherwise. The game data
/// carries no minimum level for a FATE: it was searched for on 2026-09-16 across every RowRef
/// on the Fate sheet, every sheet type whose name contains "Fate", and the whole FateRuleEx
/// sheet, and there is none. The client's own accessor is called GetRecommendedLevel, and the
/// game lets anybody join anything. What being under-levelled actually costs is a contribution
/// that counts for less, not a door that is shut.
/// </para>
/// <para>
/// So the boundaries between these values are a judgement rather than a measurement, they are
/// the player's to set, and the plugin never claims a FATE is impossible. It says how far under
/// it you are and leaves the walking to you.
/// </para>
/// </remarks>
public enum LevelFit
{
    /// <summary>
    /// No honest comparison is possible, so nothing is shown. Covers the exploratory zones,
    /// where an elemental or resistance level decides the fight rather than the job level, and
    /// the case where either level is simply unknown.
    /// </summary>
    NotApplicable = 0,

    /// <summary>At or above the FATE's level. Level sync handles the rest, and it already does.</summary>
    Comfortable,

    /// <summary>
    /// Well under the player's own level. Shown greyed out where it is shown at all, and never
    /// set aside: a FATE far below you is not a problem, it is just probably not the one you are
    /// levelling on. For gemstones or a clearing run it may be exactly the one you want.
    /// </summary>
    FarBelow,

    /// <summary>A little under. Doable, and the usual case while levelling.</summary>
    Marginal,

    /// <summary>Well under. Possible with other people around, hard alone.</summary>
    Tight,

    /// <summary>So far under that walking there is probably wasted time.</summary>
    OutOfReach,
}
