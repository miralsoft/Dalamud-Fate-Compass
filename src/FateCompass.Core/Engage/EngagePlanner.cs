using FateCompass.Core.Configuration;
using FateCompass.Core.Model;

namespace FateCompass.Core.Engage;

/// <summary>
/// Decides which preparation steps are actually needed. It plans only, it never executes.
/// </summary>
/// <remarks>
/// This class is the reason the automation boundary is testable at all. Both triggers, the
/// manual command and the optional automatic one, run the exact same plan produced here. The
/// trigger is the only difference between them, and it is recorded by the executor rather than
/// changing what gets done.
/// <para>
/// Steps that are not needed are never planned, so the plugin does not send an action the
/// player does not require. A stance that is already up produces no action.
/// </para>
/// </remarks>
public static class EngagePlanner
{
    /// <summary>
    /// Plans the sequence for entering a FATE: get on the ground, sync down, put the stance up.
    /// The order matters, because dismounting first is what lets the rest happen while moving
    /// into the fight.
    /// </summary>
    public static EngagePlan PlanEngage(
        PlayerSnapshot player,
        FateSnapshot fate,
        FateCompassSettings settings)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(fate);
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.Enabled)
        {
            return EngagePlan.Empty(PlanBlockedReason.Disabled);
        }

        var steps = new List<EngageStep>(3);

        // Dismounting in mid-air drops the character out of the sky. Wait for the ground
        // rather than sending it and hoping.
        if (settings.EngageDismount && player.IsMounted && player.IsOnGround)
        {
            steps.Add(EngageStep.Dismount);
        }

        if (settings.EngageLevelSync && NeedsLevelSync(player, fate))
        {
            steps.Add(EngageStep.LevelSync);
        }

        if (settings.EngageTankStance && player.IsTank && !player.IsTankStanceActive)
        {
            steps.Add(EngageStep.TankStance);
        }

        if (steps.Count == 0)
        {
            var reason = AllStepsDisabled(settings)
                ? PlanBlockedReason.Disabled
                : PlanBlockedReason.AlreadyDone;
            return EngagePlan.Empty(reason);
        }

        return new EngagePlan { Steps = steps, BlockedReason = PlanBlockedReason.None };
    }

    /// <summary>
    /// Plans the step after a FATE ends. Mounting fails in combat and its cast can be
    /// interrupted, so the blocked cases are reported rather than attempted (FH-07).
    /// </summary>
    public static EngagePlan PlanRemount(PlayerSnapshot player, FateCompassSettings settings)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.Enabled)
        {
            return EngagePlan.Empty(PlanBlockedReason.Disabled);
        }

        if (player.IsMounted)
        {
            return EngagePlan.Empty(PlanBlockedReason.AlreadyDone);
        }

        // Mounting is not allowed everywhere. Reporting that plainly beats sending an action
        // the zone will refuse.
        if (!player.CanUseMount)
        {
            return EngagePlan.Empty(PlanBlockedReason.NotPermittedHere);
        }

        if (player.IsInCombat)
        {
            return EngagePlan.Empty(PlanBlockedReason.InCombat);
        }

        if (player.IsOccupied)
        {
            return EngagePlan.Empty(PlanBlockedReason.Occupied);
        }

        return EngagePlan.Of(EngageStep.Mount);
    }

    /// <summary>
    /// Level sync is only worth requesting when the game offers it, the player is not already
    /// synced, and the player actually outlevels the FATE.
    /// </summary>
    private static bool NeedsLevelSync(PlayerSnapshot player, FateSnapshot fate) =>
        player.IsLevelSyncAvailable
        && !player.IsLevelSynced
        && player.Level > fate.Level;

    private static bool AllStepsDisabled(FateCompassSettings settings) =>
        !settings.EngageDismount && !settings.EngageLevelSync && !settings.EngageTankStance;
}
