using FateHelper.Core.Model;
using FateHelper.Services;
using Lumina.Excel.Sheets;

// Both Lumina and the client structs define a type of this name. The client structs one is the
// enumeration with the values; Lumina's is the sheet row it indexes into.
using IntendedUse = FFXIVClientStructs.FFXIV.Client.Enums.TerritoryIntendedUse;

namespace FateHelper.Adapters;

/// <summary>
/// Works out what kind of content a territory is.
/// </summary>
/// <remarks>
/// Read from the territory's own intended use rather than from a list of zone ids. A list would
/// need extending with every new zone and would quietly be wrong until someone noticed; the
/// game already classifies its own territories, so the classification is taken from there.
/// </remarks>
internal static class ContentKindProvider
{
    /// <summary>Cached per territory, because the answer cannot change while one is loaded.</summary>
    private static readonly Dictionary<uint, ContentKind> Cache = [];

    internal static ContentKind For(uint territoryId)
    {
        if (territoryId == 0)
        {
            return ContentKind.Other;
        }

        if (Cache.TryGetValue(territoryId, out var cached))
        {
            return cached;
        }

        var kind = Resolve(territoryId);
        Cache[territoryId] = kind;
        return kind;
    }

    /// <summary>The kind of content the player is in right now.</summary>
    internal static ContentKind Current() => For(DalamudServices.ClientState.TerritoryType);

    internal static void ClearCache() => Cache.Clear();

    private static ContentKind Resolve(uint territoryId)
    {
        try
        {
            var sheet = DalamudServices.DataManager.GetExcelSheet<TerritoryType>();
            if (!sheet.TryGetRow(territoryId, out var row))
            {
                return ContentKind.Other;
            }

            return (IntendedUse)row.TerritoryIntendedUse.RowId switch
            {
                IntendedUse.Overworld => ContentKind.Overworld,
                IntendedUse.Eureka => ContentKind.Eureka,
                IntendedUse.Bozja => ContentKind.Bozja,
                IntendedUse.OccultCrescent => ContentKind.OccultCrescent,
                _ => ContentKind.Other,
            };
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(
                ex, "ContentKindProvider: could not classify territory {Territory}", territoryId);
            return ContentKind.Other;
        }
    }
}
