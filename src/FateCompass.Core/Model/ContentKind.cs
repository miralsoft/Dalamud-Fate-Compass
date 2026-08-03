namespace FateCompass.Core.Model;

/// <summary>
/// The kind of content the player is standing in, as far as this plugin needs to care.
/// </summary>
/// <remarks>
/// Only the distinctions that change what the plugin should show or do are named here. Every
/// other duty, dungeon, or town is <see cref="Other"/>, because nothing in this plugin behaves
/// differently between them.
/// </remarks>
public enum ContentKind
{
    /// <summary>Anywhere the plugin has no special behaviour: towns, duties, housing.</summary>
    Other = 0,

    /// <summary>An ordinary open-world zone, where FATEs and bicolour gemstones live.</summary>
    Overworld,

    /// <summary>Eureka. Its notorious monsters are ordinary FATEs, but its rewards are its own.</summary>
    Eureka,

    /// <summary>Bozja or Zadnor.</summary>
    Bozja,

    /// <summary>The Occult Crescent.</summary>
    OccultCrescent,
}

/// <summary>Helpers that answer questions about a <see cref="ContentKind"/>.</summary>
public static class ContentKindExtensions
{
    /// <summary>
    /// True where finishing an activity can award bicolour gemstones.
    /// </summary>
    /// <remarks>
    /// The exploratory zones pay in their own currencies instead, so the gemstone purse is
    /// simply not part of what the player is doing there. Showing a counter that cannot move is
    /// noise on a screen that has little room to spare.
    /// </remarks>
    public static bool EarnsBicolorGemstones(this ContentKind kind) => kind == ContentKind.Overworld;

    /// <summary>
    /// True for the exploratory zones, whose engagements come from the dynamic event system
    /// rather than from the FATE table.
    /// </summary>
    public static bool IsExploratory(this ContentKind kind) =>
        kind is ContentKind.Eureka or ContentKind.Bozja or ContentKind.OccultCrescent;
}
