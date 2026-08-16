namespace FateCompass.Core.Routing;

/// <summary>
/// Turns a stream of "what kind of warp is happening" readings into the single moment that
/// matters: the player has just arrived somewhere.
/// </summary>
/// <remarks>
/// The game reports a warp kind while a warp is running and clears it once it is over, so the
/// arrival is the falling edge rather than a value one can look up. Measured in the Occult
/// Crescent: the value is set for under three seconds and is back to nothing by the time anybody
/// could ask for it. The details, including which value an aetheryte hop inside such a zone
/// actually produces, are in <c>docs/platform-notes.md</c>.
/// <para>
/// The codes arrive as plain numbers rather than as the platform's enum, because this project's
/// core may not reference a platform type (FH-04). That is also what makes this testable without
/// a running game, which for a state machine driven by a value nobody can produce on demand is
/// the difference between a tested rule and a hopeful one.
/// </para>
/// </remarks>
public sealed class WarpArrival
{
    /// <summary>Nothing is happening. Reported between warps.</summary>
    public const uint None = 0;

    /// <summary>An ordinary teleport to an aetheryte.</summary>
    public const uint Teleport = 4;

    /// <summary>The Return spell.</summary>
    public const uint Return = 7;

    /// <summary>Entering instanced content from outside it.</summary>
    public const uint EnterInstanceContent = 12;

    /// <summary>
    /// The short hop between aetherytes that does not leave the zone. Named for the aethernet in
    /// cities, and it is what an aetheryte inside an exploratory zone reports as well.
    /// </summary>
    public const uint TownTranslate = 15;

    /// <summary>
    /// The kinds that mean "the player travelled and has now landed somewhere".
    /// </summary>
    /// <remarks>
    /// Deliberately not every kind the game knows. Being resurrected also warps the character,
    /// and so does a chocobo taxi, and neither is a journey the player chose to end here.
    /// Widening this list is a product decision, not a completeness exercise.
    /// </remarks>
    private static readonly uint[] Travelling = [Teleport, Return, EnterInstanceContent, TownTranslate];

    private uint previous = None;

    /// <summary>The kind of warp that was running when the last arrival completed.</summary>
    public uint LastArrivalKind { get; private set; }

    /// <summary>True while the game reports a warp of a kind this treats as travel.</summary>
    public bool IsTravelling => IsTravel(previous);

    /// <summary>
    /// Feeds one reading.
    /// </summary>
    /// <returns>
    /// True exactly once per journey, on the reading where a travelling warp has just given way
    /// to nothing.
    /// </returns>
    public bool Observe(uint code)
    {
        var wasTravelling = IsTravel(previous);
        var arrived = wasTravelling && code == None;

        if (arrived)
        {
            LastArrivalKind = previous;
        }

        previous = code;
        return arrived;
    }

    /// <summary>Forgets the last reading, so a reload does not report a phantom arrival.</summary>
    public void Reset()
    {
        previous = None;
        LastArrivalKind = None;
    }

    /// <summary>Whether a code counts as travel that ends in an arrival.</summary>
    public static bool IsTravel(uint code) => Array.IndexOf(Travelling, code) >= 0;
}
