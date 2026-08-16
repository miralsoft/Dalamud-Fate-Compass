using FateCompass.Core.Routing;
using FateCompass.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace FateCompass.Adapters;

/// <summary>
/// Watches the client's own report of what kind of warp is happening, so arriving somewhere can
/// be noticed rather than guessed at from a jump in position.
/// </summary>
/// <remarks>
/// Inside Eureka, Bozja and the Occult Crescent an aetheryte hop does not change territory, so
/// watching for a territory change sees nothing at all. The client does distinguish the kinds of
/// warp, and the values it reports were measured rather than assumed: the details, including the
/// surprise that an aetheryte hop reports <c>TownTranslate</c> and not <c>Teleport</c>, are in
/// <c>docs/platform-notes.md</c>.
/// <para>
/// Sampled on every tick and everywhere, not only where the feature acts. A journey into one of
/// those zones begins while the player is still somewhere else, so watching only inside them
/// would miss the start and therefore the arrival. The decision about whether an arrival matters
/// belongs to the caller, and it is cheaper to read one pointer per frame than to lose a case.
/// </para>
/// </remarks>
internal static unsafe class WarpWatcher
{
    /// <summary>One reading, kept for the diagnostics window.</summary>
    internal readonly record struct Observation(
        DateTime AtUtc,
        uint Warp,
        uint TransitionState,
        uint LoadState,
        ushort TerritoryId);

    private const int HistoryLength = 24;

    private static readonly WarpArrival Arrival = new();
    private static readonly List<Observation> Recorded = [];
    private static readonly Lock Gate = new();

    private static Observation last;
    private static bool failed;

    /// <summary>What the watcher is doing, for the diagnostics window.</summary>
    internal static string Status { get; private set; } = "not started";

    /// <summary>Whether the pointers resolved and the watcher is reading.</summary>
    internal static bool IsAvailable => !failed;

    /// <summary>The kind of warp that ended in the most recent arrival.</summary>
    internal static uint LastArrivalKind => Arrival.LastArrivalKind;

    /// <summary>The most recent reading.</summary>
    internal static Observation Last
    {
        get { lock (Gate) { return last; } }
    }

    /// <summary>Recent changes, newest first. Only readings that differ from the one before.</summary>
    internal static IReadOnlyList<Observation> History
    {
        get { lock (Gate) { return [.. Recorded]; } }
    }

    /// <summary>
    /// Takes one reading and says whether a journey just ended.
    /// </summary>
    /// <remarks>
    /// Must be called from the framework thread (FH-08). Everything here follows a pointer into
    /// game memory, and the draw callback is a different thread; that mistake has already taken
    /// this client down once.
    /// </remarks>
    /// <returns>True on the single tick where a warp gave way to nothing.</returns>
    internal static bool Poll()
    {
        if (failed)
        {
            return false;
        }

        try
        {
            // Checked before it is followed (FH-09). These are static address pointers resolved
            // by signature, so a game patch leaves them null rather than wrong, and null is the
            // case that has to be survivable.
            var warp = WarpInfo.Instance();
            var main = GameMain.Instance();
            if (warp is null || main is null)
            {
                Status = warp is null ? "WarpInfo unavailable" : "GameMain unavailable";
                return false;
            }

            var sample = new Observation(
                DateTime.UtcNow,
                (uint)warp->WarpType,
                main->TerritoryTransitionState,
                main->TerritoryLoadState,
                (ushort)main->CurrentTerritoryTypeId);

            Status = "watching";
            Record(sample);

            return Arrival.Observe(sample.Warp);
        }
        catch (Exception ex)
        {
            // Off for the session rather than a warning every frame (FH-07, FH-12). What is lost
            // is one convenience; everything else carries on, which is why this is a warning and
            // not an error.
            failed = true;
            Status = "disabled after an error";
            DalamudServices.Log.Warning(
                ex,
                "WarpWatcher: reading the warp state failed. Mounting after a teleport is off for this session.");
            return false;
        }
    }

    /// <summary>
    /// Forgets what it saw. Called when the plugin starts and stops, so a reload that lands in
    /// the middle of a warp does not announce an arrival that already happened.
    /// </summary>
    internal static void Reset()
    {
        Arrival.Reset();

        lock (Gate)
        {
            Recorded.Clear();
            last = default;
        }
    }

    private static void Record(Observation sample)
    {
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
}
