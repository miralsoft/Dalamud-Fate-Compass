using FateHelper.Core.Model;
using FateHelper.Services;
using Lumina.Excel.Sheets;

namespace FateHelper.Adapters;

/// <summary>
/// Maps the player's current job onto its tank stance.
/// </summary>
/// <remarks>
/// Whether a job is a tank is read from the game's own class job data (<c>Role == 1</c>)
/// rather than from a hard-coded list of job ids, so a future job lands in the right bucket
/// without a code change.
/// <para>
/// The four stance action ids below are the one part that cannot be derived: the game data has
/// no "this is the tank stance" flag. They are listed here as named constants precisely so
/// that if a stance ever stops being applied, there is a single obvious place to check. They
/// are marked in <c>docs/open-points.md</c> as needing confirmation against the live client.
/// </para>
/// </remarks>
internal static class TankStanceResolver
{
    private const byte TankRole = 1;

    // Stance action ids. Verify these in game before trusting them (I-10).
    private const uint IronWill = 28;      // Paladin
    private const uint Defiance = 48;      // Warrior
    private const uint Grit = 3629;        // Dark Knight
    private const uint RoyalGuard = 16142; // Gunbreaker

    /// <summary>The tank job the player is currently on, or null when they are not a tank.</summary>
    internal static TankJob? CurrentTankJob()
    {
        var player = DalamudServices.ObjectTable.LocalPlayer;
        if (player is null)
        {
            return null;
        }

        var classJobId = player.ClassJob.RowId;
        var sheet = DalamudServices.DataManager.GetExcelSheet<ClassJob>();
        if (!sheet.TryGetRow(classJobId, out var classJob) || classJob.Role != TankRole)
        {
            return null;
        }

        // Match on the row id, never on the abbreviation. Abbreviations are localised: on a
        // German client Warrior reads KRG, Dark Knight DKR, and Gunbreaker REV, so comparing
        // against the English text silently failed for three of the four tanks and the stance
        // step was never even planned. The row ids are the same in every language.
        return classJobId switch
        {
            1 or 19 => TankJob.Paladin,     // Gladiator, Paladin
            3 or 21 => TankJob.Warrior,     // Marauder, Warrior
            32 => TankJob.DarkKnight,
            37 => TankJob.Gunbreaker,
            _ => null,
        };
    }

    /// <summary>The stance action for the current job, or null when there is none.</summary>
    internal static uint? CurrentStanceActionId() => CurrentTankJob() switch
    {
        TankJob.Paladin => IronWill,
        TankJob.Warrior => Defiance,
        TankJob.DarkKnight => Grit,
        TankJob.Gunbreaker => RoyalGuard,
        _ => null,
    };

    /// <summary>
    /// True when the stance is already up. Detected through the status list rather than by
    /// tracking what we sent, so an externally applied stance is recognised too.
    /// </summary>
    internal static bool IsStanceActive()
    {
        var player = DalamudServices.ObjectTable.LocalPlayer;
        if (player is null || CurrentTankJob() is null)
        {
            return false;
        }

        foreach (var status in player.StatusList)
        {
            if (StanceStatusIds.Contains(status.StatusId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Status ids for the four stances, read out of the game's Status sheet on 2026-08-01.
    /// </summary>
    /// <remarks>
    /// Royal Guard was wrong here until it was checked against the sheet: it is 392, not the
    /// value that had been written from memory. That mistake meant the plugin could never tell
    /// that a Gunbreaker already had the stance up.
    /// </remarks>
    private static readonly HashSet<uint> StanceStatusIds =
    [
        79,  // Iron Will
        91,  // Defiance
        392, // Royal Guard
        743, // Grit
    ];
}
