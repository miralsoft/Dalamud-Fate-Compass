namespace FateHelper.Core.Model;

/// <summary>
/// Lifecycle state of a FATE.
/// </summary>
public enum FateProgressState
{
    Unknown = 0,

    /// <summary>Announced but not yet accepting participation.</summary>
    Preparation,

    /// <summary>Active and joinable.</summary>
    Running,

    /// <summary>Objective met, rewards being handed out.</summary>
    Ending,

    /// <summary>Over, either completed or failed.</summary>
    Finished,
}
