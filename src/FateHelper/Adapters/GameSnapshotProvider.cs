using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Fates;
using FateHelper.Core.Model;
using FateHelper.Services;
using Lumina.Excel.Sheets;

namespace FateHelper.Adapters;

/// <summary>
/// Reads FATEs and player state out of the game and maps them onto the core's own types.
/// </summary>
/// <remarks>
/// A pass-through by design. Anything with a decision in it belongs in the core where it can
/// be tested, so this file only translates shapes (FH-04).
/// </remarks>
internal static class GameSnapshotProvider
{
    /// <summary>Every FATE the client currently knows about, which is the current zone only.</summary>
    internal static IReadOnlyList<FateSnapshot> Fates()
    {
        var snapshots = new List<FateSnapshot>();

        foreach (var fate in DalamudServices.FateTable)
        {
            try
            {
                snapshots.Add(Map(fate));
            }
            catch (Exception ex)
            {
                // One unreadable FATE must not cost the player the whole list (S-09).
                DalamudServices.Log.Warning(ex, "GameSnapshotProvider: skipped a FATE");
            }
        }

        return snapshots;
    }

    /// <summary>
    /// The zone's display name, matching what the FATE progress window shows, so the two can be
    /// lined up.
    /// </summary>
    internal static string ZoneName(uint territoryId)
    {
        try
        {
            var sheet = DalamudServices.DataManager.GetExcelSheet<TerritoryType>();
            return sheet.TryGetRow(territoryId, out var row)
                ? row.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty
                : string.Empty;
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex, "GameSnapshotProvider: could not read the zone name");
            return string.Empty;
        }
    }

    /// <summary>
    /// Which copy of the zone the player is in, or zero where the zone is not instanced.
    /// </summary>
    /// <remarks>
    /// Zones that exist several times over report a number here, and it belongs next to the zone
    /// name: two players in the same named zone can be looking at entirely different FATEs.
    /// </remarks>
    internal static unsafe int CurrentInstance()
    {
        try
        {
            var state = FFXIVClientStructs.FFXIV.Client.Game.UI.UIState.Instance();
            if (state is null || !state->PublicInstance.IsInstancedArea())
            {
                return 0;
            }

            return (int)state->PublicInstance.InstanceId;
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex, "GameSnapshotProvider: could not read the instance");
            return 0;
        }
    }

    /// <summary>Player state, or null while there is no local player, for example on a loading screen.</summary>
    internal static PlayerSnapshot? Player()
    {
        var player = DalamudServices.ObjectTable.LocalPlayer;
        if (player is null)
        {
            return null;
        }

        var condition = DalamudServices.Condition;

        return new PlayerSnapshot
        {
            Position = new WorldPosition(player.Position.X, player.Position.Y, player.Position.Z),
            Level = player.Level,
            Content = ContentKindProvider.Current(),
            TerritoryId = DalamudServices.ClientState.TerritoryType,
            HasMountSpeedUpgrades = !MountSpeedProvider.Current().HasMissing,
            IsMounted = condition[ConditionFlag.Mounted] || condition[ConditionFlag.RidingPillion],
            IsInCombat = condition[ConditionFlag.InCombat],
            TankJob = TankStanceResolver.CurrentTankJob(),
            IsTankStanceActive = TankStanceResolver.IsStanceActive(),

            // Level sync is offered while standing inside a FATE.
            IsLevelSyncAvailable = GameActions.CurrentFateId() != 0,
            IsLevelSynced = GameActions.IsSyncedToCurrentFate(),

            // Taken from the territory's own mount flag. The previous test, "not bound by duty",
            // was wrong in exactly the places this plugin cares about: the Occult Crescent, Bozja,
            // and Eureka all count as duties and all allow mounts, so the remount was reported as
            // not permitted and never ran there.
            CanUseMount = TerritoryAllowsMount(DalamudServices.ClientState.TerritoryType),

            IsOnGround = !condition[ConditionFlag.InFlight]
                && !condition[ConditionFlag.Jumping]
                && !condition[ConditionFlag.Jumping61]
                && !condition[ConditionFlag.Diving],

            IsOccupied = condition[ConditionFlag.Casting]
                || condition[ConditionFlag.Occupied]
                || condition[ConditionFlag.OccupiedInEvent]
                || condition[ConditionFlag.BetweenAreas]
                || condition[ConditionFlag.Mounting],
        };
    }

    /// <summary>
    /// Whether the zone allows mounts at all, from the game's own territory data.
    /// </summary>
    /// <remarks>
    /// Falls back to true when the row cannot be read. A wrong "yes" costs one refused action
    /// and a line in the log; a wrong "no" silently disables the remount everywhere, which is
    /// the failure that actually reached the player.
    /// </remarks>
    private static bool TerritoryAllowsMount(uint territoryId)
    {
        if (territoryId == 0)
        {
            return false;
        }

        try
        {
            var sheet = DalamudServices.DataManager.GetExcelSheet<TerritoryType>();
            return !sheet.TryGetRow(territoryId, out var row) || row.Mount;
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(
                ex, "GameSnapshotProvider: could not read the mount flag for {Territory}", territoryId);
            return true;
        }
    }

    private static FateSnapshot Map(IFate fate) => new()
    {
        Id = fate.FateId,
        DefinitionId = fate.GameData.RowId,
        Name = fate.Name.TextValue,
        Level = fate.Level,

        // The sync target is the FATE's maximum level, not its nominal level. Showing the
        // nominal one told the player they would be synced to exactly the level the FATE
        // already displays, which is never what happens.
        MaxLevel = fate.MaxLevel,
        Kind = ClassifyKind(fate),
        State = MapState(fate.State),
        Position = new WorldPosition(fate.Position.X, fate.Position.Y, fate.Position.Z),
        ProgressPercent = Math.Clamp((int)fate.Progress, 0, 100),
        SecondsRemaining = Math.Max((int)fate.TimeRemaining, 0),
        HasStarted = HasClockStarted(fate),
        IconId = fate.MapIconId,
    };

    /// <summary>
    /// Whether the FATE's countdown has actually begun.
    /// </summary>
    /// <remarks>
    /// Many FATEs sit visible on the map waiting for a player to start them, and some have to
    /// be started deliberately by talking to an NPC. In that state the client reports zero
    /// remaining time, because there is no countdown yet, not because it has run out.
    /// <para>
    /// Distinguishing the two is what stops the plugin from writing off exactly the FATEs that
    /// are freshest and most worth travelling to.
    /// </para>
    /// </remarks>
    private static bool HasClockStarted(IFate fate) =>
        fate.StartTimeEpoch != 0 && fate.TimeRemaining > 0;

    private static FateProgressState MapState(FateState state) => state switch
    {
        FateState.Preparing => FateProgressState.Preparation,
        FateState.Running => FateProgressState.Running,
        FateState.Ending => FateProgressState.Ending,
        FateState.Ended or FateState.Failed => FateProgressState.Finished,
        _ => FateProgressState.Unknown,
    };

    /// <summary>
    /// Classifies a FATE by its rule row. The game has no clean category column, so an
    /// unrecognised rule falls to <see cref="FateKind.Unknown"/>, which the filter treats as
    /// includable rather than hiding it.
    /// </summary>
    private static FateKind ClassifyKind(IFate fate)
    {
        // A hand-in count above zero is the reliable marker for a collection FATE.
        if (fate.HandInCount > 0)
        {
            return FateKind.Collect;
        }

        try
        {
            var sheet = DalamudServices.DataManager.GetExcelSheet<Fate>();
            if (sheet.TryGetRow(fate.GameData.RowId, out var row))
            {
                return row.Rule switch
                {
                    1 => FateKind.Slay,
                    2 => FateKind.Boss,
                    3 => FateKind.Escort,
                    4 => FateKind.Collect,
                    5 => FateKind.Defend,
                    _ => FateKind.Unknown,
                };
            }
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex, "GameSnapshotProvider: could not classify FATE {Id}", fate.FateId);
        }

        return FateKind.Unknown;
    }
}
