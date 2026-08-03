using FateHelper.Core.News;

namespace FateHelper.Core.Tests;

public sealed class VersionNumberTests
{
    [Theory]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData(" 0.1.0 ", 0, 1, 0)]
    [InlineData("0.2", 0, 2, 0)]
    [InlineData("2.0.0-beta.1", 2, 0, 0)]
    [InlineData("1.4.0+build7", 1, 4, 0)]
    public void ReadsTheVersionItsGiven(string text, int major, int minor, int patch)
    {
        Assert.Equal(new Version(major, minor, patch), VersionNumber.Parse(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nonsense")]
    [InlineData("1")]
    public void AnythingUnreadableSortsAsTheOldestVersion(string? text)
    {
        Assert.Equal(new Version(0, 0, 0), VersionNumber.Parse(text));
    }

    [Fact]
    public void MinorVersionsCompareNumericallyNotAlphabetically()
    {
        Assert.True(VersionNumber.IsNewer("0.10.0", "0.9.0"));
        Assert.False(VersionNumber.IsNewer("0.9.0", "0.10.0"));
    }

    [Fact]
    public void TheSameVersionIsNotNewerThanItself()
    {
        Assert.False(VersionNumber.IsNewer("1.0.0", "1.0.0"));
    }

    /// <summary>
    /// An unreadable stored value has to err towards showing the notes again rather than never
    /// showing them, which is why it parses as the oldest version rather than throwing.
    /// </summary>
    [Fact]
    public void AnUnreadableStoredValueMakesEverythingNewer()
    {
        Assert.True(VersionNumber.IsNewer("0.1.0", "corrupted"));
    }
}
