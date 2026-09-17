using FateCompass.Core.Configuration;
using FateCompass.Core.Engage;

namespace FateCompass.Core.Tests;

/// <summary>
/// The FATE level band, and the two defects that came out of reading what it really holds.
/// </summary>
/// <remarks>
/// Every value here was read out of the game's Fate sheet on 2026-09-16 with Lumina against the
/// installed client, not written from memory (T-07). ClassJobLevelMax is the top of the FATE's
/// own level band, not the level somebody is synced to, and 292 of the 1712 named FATEs carry
/// 255 in it as a stand-in for "no cap".
/// </remarks>
public sealed class SyncLevelTests
{
    [Theory]
    [InlineData(30, 35, 35)]     // Sprig Cleaning, the ordinary case
    [InlineData(100, 104, 104)]  // The Serpentlord Seethes, the modern shape
    [InlineData(60, 60, 60)]     // Excitable Boys, a FATE whose band is a single level
    public void TheBandTopIsWhatGetsSyncedTo(ushort level, ushort maxLevel, ushort expected)
    {
        Assert.Equal(expected, TestData.Fate(level: level, maxLevel: maxLevel).SyncLevel);
    }

    [Fact]
    public void AnUnboundedBandFallsBackToTheFatesOwnLevel()
    {
        // 255 is a one-byte stand-in for "no cap", not a level. Passed through untouched it
        // produced a sync column reading "to 255", and Eureka is full of these.
        var fate = TestData.Fate(level: 1, maxLevel: 255);

        Assert.Equal(1, fate.SyncLevel);
    }

    [Fact]
    public void AMissingBandTopFallsBackToTheFatesOwnLevel()
    {
        // The dynamic event path reports no band at all, so zero has to mean "not stated".
        Assert.Equal(50, TestData.Fate(level: 50, maxLevel: 0).SyncLevel);
    }

    [Theory]
    [InlineData(104, false)]  // at the band top, the game offers nothing
    [InlineData(102, false)]  // inside the band, and this is the case that used to disagree
    [InlineData(100, false)]  // at the FATE's own level
    [InlineData(105, true)]   // one above the band, which is where sync starts
    public void SyncIsPlannedAgainstTheBandTopAndNotTheDisplayedLevel(
        ushort playerLevel,
        bool expected)
    {
        // The defect this locks down: the planner compared against Fate.Level while the window
        // compared against the band top, so on a level 100 FATE running to 104 a level 102
        // player was told both that a sync was needed and that none was.
        var fate = TestData.Fate(level: 100, maxLevel: 104);
        var player = TestData.Player(
            level: playerLevel,
            isLevelSyncAvailable: true,
            isLevelSynced: false);

        var plan = EngagePlanner.PlanEngage(player, fate, new FateCompassSettings());

        Assert.Equal(expected, plan.Steps.Contains(EngageStep.LevelSync));
    }
}
