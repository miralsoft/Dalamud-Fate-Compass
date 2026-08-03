using FateCompass.Core.Configuration;
using FateCompass.Core.Engage;
using FateCompass.Core.Model;
using FateCompass.Core.Ranking;
using FateCompass.Core.Routing;

namespace FateCompass.Core.Tests;

/// <summary>
/// The behaviour that only applies to the instanced engagement content of Bozja, Zadnor, and
/// the Occult Crescent. Open-world FATEs must be unaffected by all of it.
/// </summary>
public sealed class DynamicEventRankingTests
{
    private static FateCompassSettings Settings() => new();

    [Fact]
    public void OccupancyIsNullWhenParticipationIsNotReported()
    {
        var fate = TestData.Fate();

        Assert.Null(fate.Occupancy);
    }

    [Fact]
    public void OccupancyIsTheFilledFraction()
    {
        var engagement = TestData.Fate(participants: 12, maxParticipants: 48);

        Assert.Equal(0.25f, engagement.Occupancy!.Value, precision: 4);
    }

    [Fact]
    public void AnEmptyEngagementOutranksAFullOneAtTheSameDistance()
    {
        var settings = Settings();
        settings.Weights.Occupancy = 2f;

        var full = TestData.Fate(id: 1, x: 100f, participants: 48, maxParticipants: 48);
        var empty = TestData.Fate(id: 2, x: 100f, participants: 0, maxParticipants: 48);

        var result = FateRanker.Rank([full, empty], TestData.Player(), settings);

        Assert.Equal(empty.Id, result[0].Fate.Id);
    }

    [Fact]
    public void AFateWithoutParticipationIsNotPenalisedAgainstOneWithIt()
    {
        // A plain FATE reports no participation. It must not lose to an engagement purely
        // because it has nothing to contribute to that factor.
        var settings = Settings();
        settings.Weights.Occupancy = 2f;

        var plainFate = TestData.Fate(id: 1, x: 10f);
        var fullEngagement = TestData.Fate(id: 2, x: 10f, participants: 48, maxParticipants: 48);

        var result = FateRanker.Rank([fullEngagement, plainFate], TestData.Player(), settings);

        Assert.Equal(plainFate.Id, result[0].Fate.Id);
    }

    [Fact]
    public void AnOpenRegistrationWindowOutranksACloserOrdinaryActivity()
    {
        // Registration can be missed outright, unlike arriving late at a FATE, so it is meant
        // to win against mere proximity.
        var nearbyFate = TestData.Fate(id: 1, x: 5f);
        var distantRegistration = TestData.Fate(
            id: 2,
            x: 400f,
            source: ActivitySource.DynamicEvent,
            isRegistrationOpen: true);

        var result = FateRanker.Rank([nearbyFate, distantRegistration], TestData.Player(), Settings());

        Assert.Equal(distantRegistration.Id, result[0].Fate.Id);
    }

    [Fact]
    public void TheRegistrationBonusCanBeTurnedOff()
    {
        var settings = Settings();
        settings.Weights.RegistrationOpenBonus = 0f;

        var nearbyFate = TestData.Fate(id: 1, x: 5f);
        var distantRegistration = TestData.Fate(id: 2, x: 400f, isRegistrationOpen: true);

        var result = FateRanker.Rank([nearbyFate, distantRegistration], TestData.Player(), settings);

        Assert.Equal(nearbyFate.Id, result[0].Fate.Id);
    }

    [Theory]
    [InlineData(FateKind.Skirmish)]
    [InlineData(FateKind.CriticalEngagement)]
    [InlineData(FateKind.CriticalEncounter)]
    public void TheNewKindsCanBeFilteredLikeAnyOther(FateKind kind)
    {
        var settings = Settings();
        settings.ExcludedKinds.Add(kind);

        var result = FateRanker.Rank([TestData.Fate(kind: kind)], TestData.Player(), settings);

        Assert.Equal(FateExclusionReason.Filtered, result[0].ExclusionReason);
    }

    [Fact]
    public void RemountIsSkippedWhereTheZoneForbidsMounting()
    {
        var player = TestData.Player(canUseMount: false);

        var plan = EngagePlanner.PlanRemount(player, Settings());

        Assert.False(plan.HasWork);
        Assert.Equal(PlanBlockedReason.NotPermittedHere, plan.BlockedReason);
    }

    [Fact]
    public void RemountStillWorksWhereMountingIsAllowed()
    {
        var plan = EngagePlanner.PlanRemount(TestData.Player(canUseMount: true), Settings());

        Assert.Equal([EngageStep.Mount], plan.Steps);
    }

    [Fact]
    public void TheZoneRestrictionIsReportedAheadOfCombat()
    {
        // Both apply, and the zone restriction is the more useful answer: combat ends, the
        // restriction does not. Reporting "in combat" here would imply that waiting helps.
        var player = TestData.Player(isInCombat: true, canUseMount: false);

        var plan = EngagePlanner.PlanRemount(player, Settings());

        Assert.Equal(PlanBlockedReason.NotPermittedHere, plan.BlockedReason);
    }

    [Fact]
    public void OccupancyIsNullWhenTheMaximumIsUnknown()
    {
        var engagement = TestData.Fate(participants: 5, maxParticipants: null);

        Assert.Null(engagement.Occupancy);
    }

    [Fact]
    public void SourceDefaultsToFate()
    {
        Assert.Equal(ActivitySource.Fate, TestData.Fate().Source);
    }

    // --- The registration gate ---------------------------------------------------------

    [Fact]
    public void AGatedEncounterIsExcludedOnceItsWindowHasClosed()
    {
        // The fight is still running and still reports ten minutes left. That is exactly the
        // trap: the numbers look inviting and nobody can get in any more.
        var encounter = TestData.Fate(
            kind: FateKind.CriticalEncounter,
            hasRegistrationGate: true,
            isRegistrationOpen: false,
            secondsRemaining: 600);

        var result = FateRanker.Rank([encounter], TestData.Player(), Settings());

        Assert.Equal(FateExclusionReason.RegistrationClosed, result[0].ExclusionReason);
    }

    [Fact]
    public void AGatedEncounterIsRecommendedWhileItsWindowIsOpen()
    {
        var encounter = TestData.Fate(
            kind: FateKind.CriticalEncounter,
            hasRegistrationGate: true,
            isRegistrationOpen: true,
            secondsUntilRegistrationCloses: 170);

        var result = FateRanker.Rank([encounter], TestData.Player(), Settings());

        Assert.True(result[0].IsRecommended);
    }

    [Fact]
    public void AnEncounterWhoseWindowShutsBeforeArrivalIsExcluded()
    {
        var settings = Settings();
        settings.TravelSpeedYalmsPerSecond = 10f;

        // Six hundred yalms at ten yalms a second is a minute of travel, against a window that
        // closes in thirty seconds.
        var encounter = TestData.Fate(
            x: 600f,
            kind: FateKind.CriticalEncounter,
            hasRegistrationGate: true,
            isRegistrationOpen: true,
            secondsUntilRegistrationCloses: 30,
            secondsRemaining: 900);

        var result = FateRanker.Rank([encounter], TestData.Player(), settings);

        Assert.Equal(FateExclusionReason.RegistrationTooLate, result[0].ExclusionReason);
    }

    [Fact]
    public void TheSignUpDeadlineWinsOverTheFightsOwnCountdown()
    {
        // Nine hundred seconds of fight left says "plenty of time". Only the sign-up deadline
        // knows better, so the exclusion has to come from that one.
        var settings = Settings();
        settings.TravelSpeedYalmsPerSecond = 10f;
        settings.MinimumSecondsRemaining = 45;

        var encounter = TestData.Fate(
            x: 400f,
            hasRegistrationGate: true,
            isRegistrationOpen: true,
            secondsUntilRegistrationCloses: 20,
            secondsRemaining: 900);

        var result = FateRanker.Rank([encounter], TestData.Player(), settings);

        Assert.Equal(FateExclusionReason.RegistrationTooLate, result[0].ExclusionReason);
    }

    [Fact]
    public void AnUngatedSkirmishStaysJoinableWithoutARegistrationWindow()
    {
        var skirmish = TestData.Fate(
            kind: FateKind.Skirmish,
            hasRegistrationGate: false,
            isRegistrationOpen: false);

        var result = FateRanker.Rank([skirmish], TestData.Player(), Settings());

        Assert.True(result[0].IsRecommended);
    }

    [Fact]
    public void ReachableSignUpWindowsAreNotPenalisedForBeingShort()
    {
        // Both are reachable well inside their window. A three minute window must not score
        // worse than an eight minute FATE simply for being a shorter number.
        var settings = Settings();
        settings.Weights.RegistrationOpenBonus = 0f;

        var encounter = TestData.Fate(
            id: 1, x: 100f,
            hasRegistrationGate: true,
            isRegistrationOpen: true,
            secondsUntilRegistrationCloses: 170,
            secondsRemaining: 900);

        var fate = TestData.Fate(id: 2, x: 100f, secondsRemaining: 480);

        var result = FateRanker.Rank([encounter, fate], TestData.Player(), settings);

        Assert.Equal(
            result.Single(entry => entry.Fate.Id == 2).Score,
            result.Single(entry => entry.Fate.Id == 1).Score,
            precision: 4);
    }

    // --- Travel speed per content ------------------------------------------------------

    [Fact]
    public void ExploratoryZonesArePlannedAtTheirOwnTravelSpeed()
    {
        var settings = Settings();
        settings.TravelSpeedYalmsPerSecond = 20f;
        settings.ExploratoryTravelSpeedYalmsPerSecond = 10f;

        var fate = TestData.Fate(x: 200f);

        var overworld = FateRanker.Rank([fate], TestData.Player(), settings);
        var crescent = FateRanker.Rank(
            [fate], TestData.Player(content: ContentKind.OccultCrescent), settings);

        Assert.Equal(10f, overworld[0].EstimatedTravelSeconds, precision: 3);
        Assert.Equal(20f, crescent[0].EstimatedTravelSeconds, precision: 3);
    }

    [Fact]
    public void AReachableCriticalEncounterOutranksANearerFate()
    {
        // The FATE is closer and will still be there in two minutes. The encounter will not:
        // its sign-up window shuts and the whole fight is gone for that cycle.
        var settings = Settings();
        settings.TravelSpeedYalmsPerSecond = 20f;

        var fate = TestData.Fate(id: 1, x: 50f);
        var encounter = TestData.Fate(
            id: 2, x: 400f,
            kind: FateKind.CriticalEncounter,
            hasRegistrationGate: true,
            isRegistrationOpen: true,
            secondsUntilRegistrationCloses: 150);

        var result = FateRanker.Rank([fate, encounter], TestData.Player(), settings);

        Assert.Equal(encounter.Id, result[0].Fate.Id);
    }

    [Fact]
    public void AnUnreachableCriticalEncounterDoesNotJumpTheQueue()
    {
        // The priority bonus must never outweigh being too late: the deadline is checked before
        // any score is worked out, so an unreachable encounter is excluded outright.
        var settings = Settings();
        settings.TravelSpeedYalmsPerSecond = 10f;

        var fate = TestData.Fate(id: 1, x: 50f);
        var encounter = TestData.Fate(
            id: 2, x: 900f,
            kind: FateKind.CriticalEncounter,
            hasRegistrationGate: true,
            isRegistrationOpen: true,
            secondsUntilRegistrationCloses: 20);

        var result = FateRanker.Rank([fate, encounter], TestData.Player(), settings);

        Assert.Equal(fate.Id, result[0].Fate.Id);
        Assert.Equal(
            FateExclusionReason.RegistrationTooLate,
            result.Single(entry => entry.Fate.Id == encounter.Id).ExclusionReason);
    }

    [Fact]
    public void AClosedEncounterIsSidelinedRatherThanRanked()
    {
        var encounter = TestData.Fate(
            kind: FateKind.CriticalEncounter,
            hasRegistrationGate: true,
            isRegistrationOpen: false);

        var result = FateRanker.Rank([encounter], TestData.Player(), Settings());

        Assert.True(result[0].IsSidelined);
        Assert.False(result[0].IsRecommended);
    }

    [Fact]
    public void TheAnnouncementCarriesTheChannelTheFlagAndTheName()
    {
        var line = Core.Announce.ChatAnnouncement.Build("/p", "Auge um Auge", includeName: true);

        Assert.Equal("/p <flag> Auge um Auge", line);
    }

    [Fact]
    public void TheAnnouncementCanLeaveTheNameOut()
    {
        var line = Core.Announce.ChatAnnouncement.Build("/p", "Auge um Auge", includeName: false);

        Assert.Equal("/p <flag>", line);
    }

    [Fact]
    public void TheAnnouncementAlwaysCarriesTheFlag()
    {
        // Without the placeholder the message points at nothing, so it is not optional however
        // the other pieces are configured.
        var line = Core.Announce.ChatAnnouncement.Build(string.Empty, string.Empty, includeName: true);

        Assert.Equal("<flag>", line);
    }

    [Fact]
    public void AnUnknownChannelFallsBackToTheFirstEntry()
    {
        Assert.Equal(0, Core.Announce.ChatChannels.IndexOf("/nonsense"));
    }

    [Fact]
    public void AnExploratoryZoneWithoutItsRidingMapIsPlannedSlower()
    {
        var settings = Settings();
        settings.ExploratoryTravelSpeedYalmsPerSecond = 15f;
        settings.ExploratorySlowTravelSpeedYalmsPerSecond = 9f;

        Assert.Equal(15f, settings.SpeedFor(ContentKind.Bozja, 0, hasMountSpeedUpgrades: true));
        Assert.Equal(9f, settings.SpeedFor(ContentKind.Bozja, 0, hasMountSpeedUpgrades: false));
    }

    [Fact]
    public void AMeasuredZoneBeatsEveryGuess()
    {
        // Once a zone has been travelled, what it actually took is the answer, whatever the
        // riding map state would have suggested.
        var settings = Settings();
        settings.ExploratoryTravelSpeedYalmsPerSecond = 15f;
        settings.ExploratorySlowTravelSpeedYalmsPerSecond = 9f;
        settings.TravelSpeedByTerritory[920] = 11.5f;

        Assert.Equal(11.5f, settings.SpeedFor(ContentKind.Bozja, 920, hasMountSpeedUpgrades: false));
        Assert.Equal(11.5f, settings.SpeedFor(ContentKind.Bozja, 920, hasMountSpeedUpgrades: true));
    }

    [Fact]
    public void TheRidingMapNeverChangesAnOpenWorldEstimate()
    {
        var settings = Settings();
        settings.TravelSpeedYalmsPerSecond = 20f;

        Assert.Equal(20f, settings.SpeedFor(ContentKind.Overworld, 0, hasMountSpeedUpgrades: false));
    }

    [Fact]
    public void AnExploratoryZonePaysForBothHops()
    {
        // There is no teleporting to a destination from where you stand: it is the return spell
        // to camp and then the aetheryte out. Both waits count, and counting only the second one
        // made the trip look half as expensive as it is.
        var settings = Settings();
        settings.TeleportOverheadSeconds = 15f;
        settings.ReturnOverheadSeconds = 20f;

        Assert.Equal(15f, settings.TeleportOverheadFor(ContentKind.Overworld));
        Assert.Equal(35f, settings.TeleportOverheadFor(ContentKind.OccultCrescent));
        Assert.Equal(35f, settings.TeleportOverheadFor(ContentKind.Bozja));
    }

    [Fact]
    public void StandingAtTheAetheryteDropsTheReturnLeg()
    {
        var settings = Settings();
        settings.TeleportOverheadSeconds = 15f;
        settings.ReturnOverheadSeconds = 20f;
        settings.ExploratoryTravelSpeedYalmsPerSecond = 10f;

        var camp = TestData.Aetheryte(id: 0, x: 0f, z: 0f);
        var fate = TestData.Fate(x: 20f, z: 0f);

        // One map unit is fifty yalms, so half a unit away is standing beside the crystal.
        var atCamp = TestData.Player(x: 0.2f, z: 0f, content: ContentKind.OccultCrescent);
        var away = TestData.Player(x: 10f, z: 0f, content: ContentKind.OccultCrescent);

        var fromCamp = RouteHintCalculator.Calculate(fate, atCamp, [camp], settings, 50f);
        var fromAway = RouteHintCalculator.Calculate(fate, away, [camp], settings, 50f);

        Assert.Equal(
            fromAway!.EstimatedTeleportRouteSeconds - settings.ReturnOverheadSeconds,
            fromCamp!.EstimatedTeleportRouteSeconds,
            precision: 3);
    }

    [Fact]
    public void OnlyOpenWorldZonesPayInGemstones()
    {
        Assert.True(ContentKind.Overworld.EarnsBicolorGemstones());
        Assert.False(ContentKind.OccultCrescent.EarnsBicolorGemstones());
        Assert.False(ContentKind.Bozja.EarnsBicolorGemstones());
        Assert.False(ContentKind.Eureka.EarnsBicolorGemstones());
    }
}
