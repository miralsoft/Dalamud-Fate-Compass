using FateHelper.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace FateHelper.Adapters;

/// <summary>
/// Reads whether the zone's riding maps have been bought.
/// </summary>
/// <remarks>
/// Each zone can have mount speed upgrades, sold as riding maps and shown as stars in the
/// game's own Mount Speed window. The upgrade is recorded as a completed quest, and the zone
/// names its quests in the territory's <c>MountSpeed</c> row, so the whole thing is readable
/// without touching the window at all.
/// <para>
/// This is information, not an input to the route estimate. The plugin measures travel speed
/// from actual movement, so a missing upgrade is already in that number; scaling the estimate
/// by a guessed factor on top would count the same slowdown twice.
/// </para>
/// </remarks>
internal static unsafe class MountSpeedProvider
{
    /// <summary>What a zone's riding maps look like: how many exist and how many are owned.</summary>
    internal readonly record struct Upgrades(int Owned, int Available)
    {
        /// <summary>True when the zone has upgrades to buy and at least one is missing.</summary>
        public bool HasMissing => Available > 0 && Owned < Available;

        /// <summary>Nothing to report for a zone that offers none.</summary>
        public bool IsKnown => Available > 0;

        internal static Upgrades None => new(0, 0);
    }

    private static readonly Dictionary<uint, uint[]> QuestsByTerritory = [];

    internal static Upgrades For(uint territoryId)
    {
        if (territoryId == 0)
        {
            return Upgrades.None;
        }

        var quests = QuestsFor(territoryId);
        if (quests.Length == 0)
        {
            return Upgrades.None;
        }

        try
        {
            var manager = QuestManager.Instance();
            if (manager is null)
            {
                return Upgrades.None;
            }

            var owned = quests.Count(QuestManager.IsQuestComplete);
            return new Upgrades(owned, quests.Length);
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex, "MountSpeedProvider: could not read the quest state");
            return Upgrades.None;
        }
    }

    internal static Upgrades Current() => For(DalamudServices.ClientState.TerritoryType);

    /// <summary>
    /// True when the player can fly here, which makes the riding maps beside the point.
    /// </summary>
    /// <remarks>
    /// A riding map raises ground speed. Once a zone's aether currents are attuned nobody
    /// travels it on the ground any more, so pointing at an unbought map there is advice for a
    /// situation that no longer arises.
    /// <para>
    /// The game keeps this as a single flag on the player state, so it needs no reasoning about
    /// currents or quests: it already knows the answer for the zone you are standing in.
    /// </para>
    /// </remarks>
    internal static bool CanFlyHere()
    {
        try
        {
            var state = PlayerState.Instance();
            return state is not null && state->CanFly;
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex, "MountSpeedProvider: could not read the flying state");

            // Assume flight. A wrong "yes" merely withholds a hint; a wrong "no" nags about a
            // purchase that would change nothing.
            return true;
        }
    }

    internal static void ClearCache() => QuestsByTerritory.Clear();

    /// <summary>
    /// The quests that stand for this zone's riding maps, cached because game data never changes
    /// while the client runs.
    /// </summary>
    private static uint[] QuestsFor(uint territoryId)
    {
        if (QuestsByTerritory.TryGetValue(territoryId, out var cached))
        {
            return cached;
        }

        var found = Array.Empty<uint>();

        try
        {
            var territories = DalamudServices.DataManager.GetExcelSheet<TerritoryType>();
            if (territories.TryGetRow(territoryId, out var territory)
                && territory.MountSpeed.ValueNullable is { } speed
                && speed.Quest.RowId != 0)
            {
                found = [speed.Quest.RowId];
            }
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(
                ex, "MountSpeedProvider: could not read the mount speed row for {Territory}", territoryId);
        }

        QuestsByTerritory[territoryId] = found;
        return found;
    }
}
