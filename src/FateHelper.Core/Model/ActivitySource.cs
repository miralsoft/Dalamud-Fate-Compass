namespace FateHelper.Core.Model;

/// <summary>
/// Which of the game's two systems an entry came from.
/// </summary>
/// <remarks>
/// Open-world FATEs and the instanced engagements of Bozja, Zadnor, and the Occult Crescent
/// are separate systems in the client, but from the player's point of view they are the same
/// job: something is happening nearby, is it worth going to. The ranking, filtering, history,
/// and route logic therefore treat both identically, and this only records where an entry came
/// from so the UI can label it and the engage sequence can adapt.
/// <para>
/// Eureka is deliberately absent: its notorious monsters are ordinary FATEs and arrive through
/// the same table as any other, so they need no separate source.
/// </para>
/// </remarks>
public enum ActivitySource
{
    /// <summary>An open-world FATE, including Eureka notorious monsters.</summary>
    Fate = 0,

    /// <summary>
    /// A dynamic event: a Bozja or Zadnor skirmish or critical engagement, or an Occult
    /// Crescent critical encounter.
    /// </summary>
    DynamicEvent,
}
