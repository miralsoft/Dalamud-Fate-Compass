namespace FateCompass.Adapters;

/// <summary>
/// The handful of game values the windows need while drawing, sampled once per framework tick
/// so that drawing never has to ask the game anything.
/// </summary>
/// <remarks>
/// FH-08 allows the draw callback to read addon geometry, because an overlay cannot know where
/// the game's window is without it, and nothing else. The three values here are not addon
/// geometry: two of them were function calls into the game and one read a field out of a game
/// struct, all from inside <c>Draw</c> or <c>PreDraw</c>.
/// <para>
/// A wrapper around the marshalling helper cannot fix that, because these are wanted while a
/// frame is being built and the helper is asynchronous: there is nothing to await mid-draw. So
/// the direction is reversed. The tick, which already runs on the framework thread, takes the
/// readings; the windows read what it left behind. That costs one frame of staleness on values
/// that change on the scale of zoning, mounting and picking up gemstones, which is to say none
/// that anybody can see.
/// </para>
/// <para>
/// Found by redoing the crash-safety pass by reachability rather than by grep (R-21). The
/// earlier passes searched for <c>unsafe</c>, <c>-&gt;</c> and <c>Instance()</c>, found these
/// adapters, checked that they guard their pointers, and never asked which thread called them.
/// </para>
/// </remarks>
internal static class GameFacts
{
    /// <summary>
    /// The starting values are deliberately the ones each reader falls back to when the game
    /// cannot be read at all, so the first frame after a load behaves like a failed reading
    /// rather than like a confident zero.
    /// </summary>
    private const bool AssumeFlight = true;

    private static int? gemstoneCap;

    /// <summary>Which copy of a public zone the player is in, or zero outside one.</summary>
    internal static int PublicInstance { get; private set; }

    /// <summary>Bicolour gemstones carried, or zero when the purse cannot be read.</summary>
    internal static int GemstoneCount { get; private set; }

    /// <summary>The cap, from the item's own stack size. Zero means "do not warn".</summary>
    internal static int GemstoneCap { get; private set; }

    /// <summary>
    /// Whether flying is unlocked here. Starts true: a wrong yes withholds a hint, a wrong no
    /// nags about a riding map in a zone nobody walks.
    /// </summary>
    internal static bool CanFly { get; private set; } = AssumeFlight;

    /// <summary>How many of this zone's riding maps are owned.</summary>
    internal static MountSpeedProvider.Upgrades MountUpgrades { get; private set; } =
        MountSpeedProvider.Upgrades.None;

    /// <summary>
    /// Takes all of the readings. Framework thread only (FH-08); this is the whole point of the
    /// type.
    /// </summary>
    internal static void Refresh()
    {
        PublicInstance = GameSnapshotProvider.CurrentInstance();
        GemstoneCount = CurrencyProvider.GemstoneCount();
        CanFly = MountSpeedProvider.CanFlyHere();
        MountUpgrades = MountSpeedProvider.Current();

        // The cap is the item's stack size out of the game's own tables, so it is file data
        // rather than live memory and it cannot change while the game runs. Read once.
        gemstoneCap ??= CurrencyProvider.GemstoneCap();
        GemstoneCap = gemstoneCap.Value;
    }

    /// <summary>Back to the starting values, on load and on unload.</summary>
    internal static void Reset()
    {
        PublicInstance = 0;
        GemstoneCount = 0;
        GemstoneCap = 0;
        gemstoneCap = null;
        CanFly = AssumeFlight;
        MountUpgrades = MountSpeedProvider.Upgrades.None;
    }
}
