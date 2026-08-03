using System.Globalization;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using FateCompass.Configuration;
using FateCompass.Core.Localization;
using FateCompass.Services;

namespace FateCompass.UI;

/// <summary>
/// A clickable entry in the server info bar, next to the minimap.
/// </summary>
/// <remarks>
/// This answers the wish for a button that does not need a typed command. Dalamud offers the
/// server info bar for exactly this, and it sits where the player is already looking.
/// <para>
/// Left click opens the window, right click toggles the automatic preparation. The entry shows
/// how many FATEs are worth going to right now, so it is useful at a glance even without being
/// clicked.
/// </para>
/// </remarks>
internal sealed class StatusBarEntry : IDisposable
{
    private const string EntryTitle = "Fate Compass";

    private readonly IDtrBarEntry entry;
    private readonly PluginConfiguration configuration;
    private readonly Localizer localizer;
    private string lastText = string.Empty;

    internal StatusBarEntry(
        PluginConfiguration configuration,
        Localizer localizer,
        Action onLeftClick,
        Action onRightClick)
    {
        this.configuration = configuration;
        this.localizer = localizer;

        entry = DalamudServices.DtrBar.Get(EntryTitle);
        entry.OnClick = args =>
        {
            if (args.ClickType == MouseClickType.Left)
            {
                onLeftClick();
            }
            else
            {
                onRightClick();
            }
        };
    }

    /// <summary>Refreshes the label. Only writes when it actually changed.</summary>
    /// <remarks>
    /// The entry leads with the game's own FATE glyph rather than the word "FATE", so it reads
    /// as an icon next to the minimap instead of as a line of text. The count still follows,
    /// because an icon alone would not tell you whether anything is worth going to.
    /// </remarks>
    internal void Update(int recommendedCount)
    {
        var label = configuration.Settings.Enabled
            ? recommendedCount.ToString(CultureInfo.CurrentCulture)
            : "--";

        if (label != lastText)
        {
            entry.Text = new SeStringBuilder()
                .AddIcon(BitmapFontIcon.FateUnknownGold)
                .AddText(label)
                .Build();

            lastText = label;
        }

        entry.Tooltip = configuration.Settings.AutoEngageOnFateEnter
            ? $"{localizer.Get(StringKeys.StatusBarTooltip)}\n{localizer.Get(StringKeys.CommandAutoOn)}"
            : $"{localizer.Get(StringKeys.StatusBarTooltip)}\n{localizer.Get(StringKeys.CommandAutoOff)}";

        entry.Shown = configuration.Settings.ShowStatusBarEntry;
    }

    public void Dispose() => entry.Remove();
}
