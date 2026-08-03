namespace FateHelper.Core.Model;

/// <summary>
/// Measures how fast the player actually travels, so the ranking does not have to guess.
/// </summary>
/// <remarks>
/// The travel speed drives whether a FATE is reachable in time and whether a teleport is worth
/// it, and until now it was a configured estimate. Measuring it removes the guess: mount speed
/// varies with flight, with the zone, and with what the player is riding.
/// <para>
/// Samples below a small floor are ignored, because standing still would otherwise drag the
/// average towards zero and make everything look unreachable.
/// </para>
/// </remarks>
public sealed class SpeedSampler
{
    /// <summary>Ignore movement slower than this, in yalms per second. Standing still, turning.</summary>
    private const float MinimumMeaningfulSpeed = 1.0f;

    /// <summary>Reject implausible jumps, which is what a teleport or a zone change looks like.</summary>
    private const float ImplausibleSpeed = 500f;

    private double totalDistance;
    private double totalSeconds;

    /// <summary>Fastest sustained speed seen, in yalms per second.</summary>
    public float PeakYalmsPerSecond { get; private set; }

    /// <summary>Number of samples that counted towards the average.</summary>
    public int SampleCount { get; private set; }

    /// <summary>Average speed while actually moving, or null before there is enough data.</summary>
    public float? AverageYalmsPerSecond =>
        totalSeconds > 0.5d ? (float)(totalDistance / totalSeconds) : null;

    /// <summary>Feeds one observation. Distance and elapsed time are supplied by the caller.</summary>
    public void Add(float distance, float seconds)
    {
        if (seconds <= 0f || distance <= 0f)
        {
            return;
        }

        var speed = distance / seconds;
        if (speed < MinimumMeaningfulSpeed || speed > ImplausibleSpeed)
        {
            return;
        }

        totalDistance += distance;
        totalSeconds += seconds;
        SampleCount++;

        if (speed > PeakYalmsPerSecond)
        {
            PeakYalmsPerSecond = speed;
        }
    }

    public void Reset()
    {
        totalDistance = 0d;
        totalSeconds = 0d;
        PeakYalmsPerSecond = 0f;
        SampleCount = 0;
    }
}
