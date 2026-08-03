using FateHelper.Services;
using Lumina.Excel.Sheets;

namespace FateHelper.Adapters;

/// <summary>
/// Resolves text commands out of the game's own data instead of hard-coding their spelling.
/// </summary>
/// <remarks>
/// Checked against the game files on 2026-08-01: row 270 is the level sync command, and its
/// <c>Command</c> column reads <c>/levelsync</c> in English, German, French, and Japanese
/// alike. The localised forms (<c>/stufenanpassung</c>, <c>/synchroniveau</c>) are additional
/// aliases, not replacements, so a single English literal would in fact have worked.
/// <para>
/// Reading the row anyway costs nothing and removes the assumption: if a future patch ever
/// changes the wording, this follows it. The literal remains only as a fallback for when the
/// sheet cannot be read.
/// </para>
/// </remarks>
internal static class TextCommandResolver
{
    /// <summary>Row id of the level sync command in the TextCommand sheet.</summary>
    private const uint LevelSyncRowId = 270;

    private const string LevelSyncFallback = "/levelsync";

    /// <summary>
    /// The level sync command in the client's language, with the <c>on</c> subcommand appended.
    /// </summary>
    internal static string LevelSyncOn() => $"{Resolve(LevelSyncRowId, LevelSyncFallback)} on";

    private static string Resolve(uint rowId, string fallback)
    {
        try
        {
            var sheet = DalamudServices.DataManager.GetExcelSheet<TextCommand>();
            if (sheet.TryGetRow(rowId, out var row))
            {
                var command = row.Command.ExtractText();
                if (!string.IsNullOrWhiteSpace(command))
                {
                    return command;
                }
            }
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex, "TextCommandResolver: row {Row} unreadable", rowId);
        }

        return fallback;
    }
}
