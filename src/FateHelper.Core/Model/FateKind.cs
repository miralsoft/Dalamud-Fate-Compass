namespace FateHelper.Core.Model;

/// <summary>
/// The kind of a FATE, used for filtering and for the notification rules.
/// </summary>
/// <remarks>
/// The game does not expose a clean category for every FATE, so <see cref="Unknown"/> is a
/// normal value rather than an error. Filters must treat it as includable by default, or a
/// classification gap would silently hide FATEs from the player.
/// </remarks>
public enum FateKind
{
    Unknown = 0,

    /// <summary>Defeat a single notable enemy.</summary>
    Boss,

    /// <summary>Defeat a number of regular enemies.</summary>
    Slay,

    /// <summary>Hand in or gather a number of items.</summary>
    Collect,

    /// <summary>Escort an NPC to a destination.</summary>
    Escort,

    /// <summary>Defend an NPC or an object against waves.</summary>
    Defend,

    // The instanced content kinds. Kept in the same enum on purpose: the player filters by
    // "what sort of thing is this", and splitting that across two types would mean two filters
    // and two sets of settings for one question.

    /// <summary>A Bozja or Zadnor skirmish. Ongoing, low pressure to join at a set moment.</summary>
    Skirmish,

    /// <summary>
    /// A Bozja or Zadnor critical engagement. Has a registration window that can be missed,
    /// which is what makes it worth surfacing loudly.
    /// </summary>
    CriticalEngagement,

    /// <summary>An Occult Crescent critical encounter.</summary>
    CriticalEncounter,

    /// <summary>
    /// A large standing objective such as the Forked Tower: something a player goes to
    /// deliberately, not something that belongs in a farming rotation.
    /// </summary>
    /// <remarks>
    /// Recognised by how long it runs. A critical encounter lasts minutes and is over; these
    /// stand for an hour or more, because they are not a stop on a route but a plan for the
    /// evening. Keeping them out of the numbered order is the point: putting a raid at position
    /// two, between two FATEs, would misrepresent what it is.
    /// </remarks>
    SpecialObjective,
}
