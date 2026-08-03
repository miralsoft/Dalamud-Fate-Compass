namespace FateCompass.Core.Gemstones;

/// <summary>
/// How close the bicolor gemstone purse is to its cap.
/// </summary>
public enum GemstoneStatus
{
    /// <summary>Room to spare.</summary>
    Ok = 0,

    /// <summary>Close enough to the cap that it is worth spending soon.</summary>
    Warning,

    /// <summary>At the cap. Further FATE rewards are lost.</summary>
    Full,
}
