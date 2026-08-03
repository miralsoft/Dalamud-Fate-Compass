using FateHelper.Core.Progress;
using FateHelper.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FateHelper.Adapters;

/// <summary>
/// Reads the shared FATE standing out of the game's own FATE progress window.
/// </summary>
/// <remarks>
/// There is no data source for this. Nothing in the client structs holds the rank, the
/// per-zone counter, or the completed flag: they exist only inside the window's own value
/// array. So this reads the window, which means it only works while the window is open and
/// could break on a patch that changes the layout.
/// <para>
/// The layout was read from a live client on 2026-08-01 rather than guessed. Header first,
/// then a repeating block of eight values per zone starting at index 6:
/// </para>
/// <code>
/// [1] tab count      [2] current tab      [4] "1.294/1.500" gemstones
///
/// per zone, from index 6, stride 8:
///   +0 int    ordering index
///   +1 string zone name
///   +2 uint   rank
///   +3 string rank as text
///   +4 uint   FATEs completed
///   +5 uint   FATEs needed
///   +6 string "15/60", or "--/--" when finished
///   +7 bool   zone complete
/// </code>
/// </remarks>
internal static unsafe class SharedFateProvider
{
    private const string AddonName = "FateProgress";

    /// <summary>Where the per-zone blocks begin.</summary>
    private const int FirstZoneIndex = 6;

    /// <summary>Values per zone block.</summary>
    private const int ZoneStride = 8;

    private const int CurrentTabIndex = 2;

    // Offsets within a zone block.
    private const int OffsetZoneIndex = 0;
    private const int OffsetName = 1;
    private const int OffsetRank = 2;
    private const int OffsetRankText = 3;
    private const int OffsetCompleted = 4;
    private const int OffsetNeeded = 5;
    private const int OffsetIsComplete = 7;

    /// <summary>
    /// Reads whatever the window is currently showing, or an empty snapshot when it is closed.
    /// </summary>
    internal static SharedFateSnapshot Read()
    {
        try
        {
            var handle = DalamudServices.GameGui.GetAddonByName(AddonName);
            var addon = (AtkUnitBase*)handle.Address;

            if (addon is null || !addon->IsVisible || addon->AtkValues is null)
            {
                return SharedFateSnapshot.Empty;
            }

            var count = addon->AtkValuesCount;
            var zones = new List<SharedFateZone>();

            for (var start = FirstZoneIndex; start + ZoneStride <= count; start += ZoneStride)
            {
                var zone = ReadZone(addon, start);
                if (zone is not null)
                {
                    zones.Add(zone);
                }
            }

            return new SharedFateSnapshot
            {
                Zones = zones,
                TakenAt = DateTimeOffset.Now,
                Tab = count > CurrentTabIndex ? (int)addon->AtkValues[CurrentTabIndex].UInt : 0,
            };
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex, "SharedFateProvider: could not read the progress window");
            return SharedFateSnapshot.Empty;
        }
    }

    private static SharedFateZone? ReadZone(AtkUnitBase* addon, int start)
    {
        var name = ReadString(addon, start + OffsetName);

        // A blank name means the slot holds no zone, which is different from a zone at zero.
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return new SharedFateZone
        {
            ZoneName = name,
            ZoneIndex = (int)ReadUInt(addon, start + OffsetZoneIndex),
            Rank = (int)ReadUInt(addon, start + OffsetRank),
            RankText = ReadString(addon, start + OffsetRankText),
            Completed = (int)ReadUInt(addon, start + OffsetCompleted),
            Needed = (int)ReadUInt(addon, start + OffsetNeeded),
            IsComplete = ReadBool(addon, start + OffsetIsComplete),
        };
    }

    private static uint ReadUInt(AtkUnitBase* addon, int index)
    {
        var value = addon->AtkValues[index];
        return value.Type switch
        {
            AtkValueType.UInt => value.UInt,
            AtkValueType.Int => value.Int < 0 ? 0u : (uint)value.Int,
            _ => 0u,
        };
    }

    private static bool ReadBool(AtkUnitBase* addon, int index)
    {
        var value = addon->AtkValues[index];
        return value.Type == AtkValueType.Bool && value.Bool;
    }

    private static string ReadString(AtkUnitBase* addon, int index)
    {
        var value = addon->AtkValues[index];

        var isText = value.Type is AtkValueType.String
            or AtkValueType.ManagedString
            or AtkValueType.ConstString;

        if (!isText || value.String.Value is null)
        {
            return string.Empty;
        }

        // The value carries a raw C string. Read it to the terminator, bounded so a corrupt
        // pointer cannot send this walking through memory.
        var pointer = value.String.Value;
        var length = 0;
        while (length < MaxStringLength && pointer[length] != 0)
        {
            length++;
        }

        return length == 0 ? string.Empty : System.Text.Encoding.UTF8.GetString(pointer, length);
    }

    /// <summary>Upper bound on a read string, so a bad pointer cannot run away.</summary>
    private const int MaxStringLength = 256;
}
