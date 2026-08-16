#if FATECOMPASS_DEVTOOLS

using FateCompass.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace FateCompass.Adapters;

/// <summary>
/// Watches what kind of warp the client thinks is happening, so a later feature can be built on
/// a fact instead of on a guess.
/// </summary>
/// <remarks>
/// The question this answers: when a player uses an aetheryte inside Eureka, Bozja or the Occult
/// Crescent, they stay in the same territory, so a territory change sees nothing. The client does
/// distinguish the kinds of warp (<see cref="WarpType"/>), and reading that is better than
/// inferring a teleport from a position jump.
/// <para>
/// Two things are unknown until somebody looks, which is what this exists for: which value an
/// aetheryte hop inside one of those zones produces, and whether the value stays set after the
/// warp or only during it. That second one is why this samples every tick and keeps a short
/// Recorded rather than reading on demand. A warp lasts a second or two; nobody presses a button
/// in that window.
/// </para>
/// <para>
/// Developer builds only. Nothing here is compiled into a release, and nothing acts on what it
/// sees. It reads and remembers.
/// </para>
/// </remarks>
internal static unsafe class WarpProbe
{
    /// <summary>One observation, with when it was seen.</summary>
    /// <remarks>
    /// The two state fields are widened to <c>uint</c> on purpose. Their exact width in the game
    /// structs is not the point being investigated, and a probe that fails to compile over an
    /// integer type teaches nothing.
    /// </remarks>
    internal readonly record struct Observation(
        DateTime AtUtc,
        WarpType Warp,
        uint TransitionState,
        uint LoadState,
        ushort TerritoryId);

    private const int HistoryLength = 24;

    private static readonly List<Observation> Recorded = [];
    private static readonly Lock Gate = new();

    private static Observation last;
    private static bool failed;

    /// <summary>What the probe is doing, for the diagnostics window.</summary>
    internal static string Status { get; private set; } = "not started";

    /// <summary>The most recent sample, whatever it was.</summary>
    internal static Observation Last
    {
        get { lock (Gate) { return last; } }
    }

    /// <summary>Recent changes, newest first. Only samples that differ from the one before.</summary>
    internal static IReadOnlyList<Observation> History
    {
        get { lock (Gate) { return [.. Recorded]; } }
    }

    /// <summary>
    /// Takes one sample. Must be called from the framework thread (FH-08): everything below
    /// follows a pointer into game memory, and the draw callback is a different thread.
    /// </summary>
    internal static void Poll()
    {
        if (failed)
        {
            return;
        }

        try
        {
            // Every pointer is checked before it is followed (FH-09). A static address pointer is
            // resolved by signature, so a game patch can leave it null rather than wrong, and
            // null is the case that has to be survivable.
            var warp = WarpInfo.Instance();
            var main = GameMain.Instance();
            if (warp is null || main is null)
            {
                Status = warp is null ? "WarpInfo unavailable" : "GameMain unavailable";
                return;
            }

            var sample = new Observation(
                DateTime.UtcNow,
                warp->WarpType,
                main->TerritoryTransitionState,
                main->TerritoryLoadState,
                (ushort)main->CurrentTerritoryTypeId);

            Status = "sampling";

            lock (Gate)
            {
                var changed =
                    sample.Warp != last.Warp ||
                    sample.TransitionState != last.TransitionState ||
                    sample.LoadState != last.LoadState ||
                    sample.TerritoryId != last.TerritoryId;

                last = sample;

                if (!changed)
                {
                    return;
                }

                Recorded.Insert(0, sample);
                if (Recorded.Count > HistoryLength)
                {
                    Recorded.RemoveAt(Recorded.Count - 1);
                }
            }
        }
        catch (Exception ex)
        {
            // Off for the session rather than a warning every frame (FH-07, FH-12). Nothing
            // depends on this yet, so failing here costs an observation and nothing else.
            failed = true;
            Status = "disabled after an error";
            DalamudServices.Log.Warning(ex, "WarpProbe: reading the warp state failed, stopping for this session");
        }
    }

    /// <summary>Forgets what it saw, so a test can start from a clean slate.</summary>
    internal static void Clear()
    {
        lock (Gate)
        {
            Recorded.Clear();
            last = default;
        }
    }
}

#endif
