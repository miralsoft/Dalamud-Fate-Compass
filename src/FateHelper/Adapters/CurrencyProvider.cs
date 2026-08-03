using FateHelper.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace FateHelper.Adapters;

/// <summary>
/// Reads the bicolor gemstone purse.
/// </summary>
/// <remarks>
/// Both numbers come from the game rather than from constants in this file. The item id was
/// confirmed against the game data on 2026-08-01 (item 26807, "Bicolor Gemstone"), and the cap
/// is that item's stack size, which is currently 1500 but is read each time rather than
/// assumed, so a future increase needs no code change.
/// </remarks>
internal static unsafe class CurrencyProvider
{
    private const uint BicolorGemstoneItemId = 26807;

    /// <summary>Current gemstone count, or zero when the inventory cannot be read.</summary>
    internal static int GemstoneCount()
    {
        try
        {
            var manager = InventoryManager.Instance();
            return manager is null ? 0 : manager->GetInventoryItemCount(BicolorGemstoneItemId);
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex, "CurrencyProvider: could not read the gemstone count");
            return 0;
        }
    }

    /// <summary>
    /// The cap, taken from the item's stack size. Zero when unreadable, which the core treats
    /// as "no warning" rather than as a full purse.
    /// </summary>
    internal static int GemstoneCap()
    {
        try
        {
            var sheet = DalamudServices.DataManager.GetExcelSheet<Item>();
            return sheet.TryGetRow(BicolorGemstoneItemId, out var item) ? (int)item.StackSize : 0;
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex, "CurrencyProvider: could not read the gemstone cap");
            return 0;
        }
    }
}
