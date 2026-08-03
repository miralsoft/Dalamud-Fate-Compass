using FateCompass.Core.Configuration;
using FateCompass.Core.Engage;
using FateCompass.Core.Model;

namespace FateCompass.Core.Tests;

public sealed class EngagePlannerTests
{
    private static FateCompassSettings Settings() => new();

    [Fact]
    public void PlansAllThreeStepsInOrderWhenAllAreNeeded()
    {
        var player = TestData.Player(
            level: 90,
            isMounted: true,
            tankJob: TankJob.Warrior,
            isLevelSyncAvailable: true);

        var plan = EngagePlanner.PlanEngage(player, TestData.Fate(level: 50), Settings());

        Assert.Equal(
            [EngageStep.Dismount, EngageStep.LevelSync, EngageStep.TankStance],
            plan.Steps);
    }

    [Fact]
    public void SkipsDismountWhenThePlayerIsAlreadyOnFoot()
    {
        var player = TestData.Player(isMounted: false, isLevelSyncAvailable: true);

        var plan = EngagePlanner.PlanEngage(player, TestData.Fate(level: 50), Settings());

        Assert.DoesNotContain(EngageStep.Dismount, plan.Steps);
    }

    [Fact]
    public void SkipsStanceWhenItIsAlreadyUp()
    {
        var player = TestData.Player(tankJob: TankJob.Paladin, isTankStanceActive: true);

        var plan = EngagePlanner.PlanEngage(player, TestData.Fate(level: 90), Settings());

        Assert.DoesNotContain(EngageStep.TankStance, plan.Steps);
    }

    [Fact]
    public void PlansNoStanceForANonTankJob()
    {
        var player = TestData.Player(tankJob: null);

        var plan = EngagePlanner.PlanEngage(player, TestData.Fate(level: 90), Settings());

        Assert.DoesNotContain(EngageStep.TankStance, plan.Steps);
    }

    [Fact]
    public void SkipsLevelSyncWhenTheGameDoesNotOfferIt()
    {
        var player = TestData.Player(level: 90, isLevelSyncAvailable: false);

        var plan = EngagePlanner.PlanEngage(player, TestData.Fate(level: 20), Settings());

        Assert.DoesNotContain(EngageStep.LevelSync, plan.Steps);
    }

    [Fact]
    public void SkipsLevelSyncWhenTheFateIsNotBelowThePlayerLevel()
    {
        var player = TestData.Player(level: 50, isLevelSyncAvailable: true);

        var plan = EngagePlanner.PlanEngage(player, TestData.Fate(level: 50), Settings());

        Assert.DoesNotContain(EngageStep.LevelSync, plan.Steps);
    }

    [Fact]
    public void SkipsLevelSyncWhenAlreadySynced()
    {
        var player = TestData.Player(level: 90, isLevelSyncAvailable: true, isLevelSynced: true);

        var plan = EngagePlanner.PlanEngage(player, TestData.Fate(level: 20), Settings());

        Assert.DoesNotContain(EngageStep.LevelSync, plan.Steps);
    }

    [Fact]
    public void ProducesNothingWhenThePluginIsDisabled()
    {
        var settings = Settings();
        settings.Enabled = false;

        var player = TestData.Player(isMounted: true, tankJob: TankJob.DarkKnight);

        var plan = EngagePlanner.PlanEngage(player, TestData.Fate(), settings);

        Assert.False(plan.HasWork);
        Assert.Equal(PlanBlockedReason.Disabled, plan.BlockedReason);
    }

    [Fact]
    public void ReportsDisabledWhenEveryIndividualStepIsSwitchedOff()
    {
        var settings = Settings();
        settings.EngageDismount = false;
        settings.EngageLevelSync = false;
        settings.EngageTankStance = false;

        var player = TestData.Player(isMounted: true, tankJob: TankJob.Gunbreaker);

        var plan = EngagePlanner.PlanEngage(player, TestData.Fate(), settings);

        Assert.Equal(PlanBlockedReason.Disabled, plan.BlockedReason);
    }

    [Fact]
    public void ReportsAlreadyDoneWhenNothingNeedsChanging()
    {
        var player = TestData.Player(isMounted: false, tankJob: TankJob.Warrior, isTankStanceActive: true);

        var plan = EngagePlanner.PlanEngage(player, TestData.Fate(level: 90), Settings());

        Assert.False(plan.HasWork);
        Assert.Equal(PlanBlockedReason.AlreadyDone, plan.BlockedReason);
    }

    [Fact]
    public void RemountIsPlannedWhenTheCoastIsClear()
    {
        var plan = EngagePlanner.PlanRemount(TestData.Player(), Settings());

        Assert.Equal([EngageStep.Mount], plan.Steps);
    }

    [Fact]
    public void RemountIsBlockedInCombat()
    {
        var plan = EngagePlanner.PlanRemount(TestData.Player(isInCombat: true), Settings());

        Assert.False(plan.HasWork);
        Assert.Equal(PlanBlockedReason.InCombat, plan.BlockedReason);
    }

    [Fact]
    public void RemountIsBlockedWhileOccupied()
    {
        var plan = EngagePlanner.PlanRemount(TestData.Player(isOccupied: true), Settings());

        Assert.Equal(PlanBlockedReason.Occupied, plan.BlockedReason);
    }

    [Fact]
    public void RemountIsSkippedWhenAlreadyMounted()
    {
        var plan = EngagePlanner.PlanRemount(TestData.Player(isMounted: true), Settings());

        Assert.Equal(PlanBlockedReason.AlreadyDone, plan.BlockedReason);
    }

    [Fact]
    public void ManualAndAutomaticTriggersShareTheSamePlan()
    {
        // The trigger is not an input to the planner. This test exists to keep it that way:
        // if a trigger ever changed what gets sent, the automation boundary would move
        // invisibly (FH-01, FH-06).
        var player = TestData.Player(isMounted: true, tankJob: TankJob.Paladin, isLevelSyncAvailable: true);
        var fate = TestData.Fate(level: 30);
        var settings = Settings();

        var first = EngagePlanner.PlanEngage(player, fate, settings);
        var second = EngagePlanner.PlanEngage(player, fate, settings);

        Assert.Equal(first.Steps, second.Steps);
    }
}
