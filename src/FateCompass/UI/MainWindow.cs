using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using FateCompass.Adapters;
using FateCompass.Configuration;
using FateCompass.Core.Announce;
using FateCompass.Core.Gemstones;
using FateCompass.Core.History;
using FateCompass.Core.Localization;
using FateCompass.Core.Model;
using FateCompass.Core.Ranking;
using FateCompass.Core.Routing;
using FateCompass.Services;

namespace FateCompass.UI;

/// <summary>
/// The FATE list for the current zone.
/// </summary>
/// <remarks>
/// Excluded FATEs are shown greyed out with their reason rather than hidden, so the player can
/// see that something was considered and why it was passed over.
/// </remarks>
internal sealed class MainWindow : Window, IDisposable
{
    /// <summary>Edge length of a tile's FATE icon, which also sets the tile width.</summary>
    private const float TileWidth = 52f;

    private const float TileGap = 10f;

    /// <summary>
    /// Gap before a standing objective, wide enough to read as a separation rather than as the
    /// next item in the row.
    /// </summary>
    private const float SpecialTileGap = 34f;

    /// <summary>Breathing room at both ends of the tile row, so nothing touches the frame.</summary>
    private const float RowMargin = 8f;

    /// <summary>Air above and below the bottom bar's rule.</summary>
    private const float BarPadding = 10f;

    /// <summary>
    /// How much room the bottom bar needs, in one place.
    /// </summary>
    /// <remarks>
    /// Used both to pin the bar and to size the list above it. When those two disagreed, the
    /// list claimed room the bar was standing in and the whole window started scrolling.
    /// </remarks>
    private static float BottomBarHeight() =>
        ImGui.GetFrameHeight() + (ImGui.GetStyle().ItemSpacing.Y * 2f) + BarPadding;

    /// <summary>Stands in for the rank number on a standing objective, which has none.</summary>
    private const string SpecialObjectiveGlyph = "★";

    /// <summary>Stands in for the rank number on a fight that is running but can no longer be joined.</summary>
    private const string SidelinedGlyph = "•";

    private const float ActionIconSize = 22f;

    /// <summary>Breathing room between the controls in the bottom bar.</summary>
    private const float ControlGap = 12f;

    /// <summary>Stands in for a countdown that has not begun. Narrow on purpose.</summary>
    private const string NotStartedGlyph = "--:--";

    /// <summary>Gap between the gemstone count and the zone progress on the header line.</summary>
    private const float SectionGap = 20f;

    private const float ProgressBarWidth = 110f;

    /// <summary>Upper bound on drawn stars. The game does not report a maximum rank.</summary>
    private const int MaxRankStars = 4;

    /// <summary>Quest-style flag icon, matching the marker the flag button places.</summary>
    private const uint FlagIconId = 60561;

    /// <summary>Aetheryte icon, taken from the game's own map markers for aetherytes.</summary>
    private const uint AetheryteIconId = 60453;

    /// <summary>Colour of the release-notes button while there is something unread behind it.</summary>
    private static readonly Vector4 UnseenNewsColour = new(1f, 0.82f, 0.4f, 1f);

    private readonly FateCompassController controller;
    private readonly PluginConfiguration configuration;
    private readonly Localizer localizer;
    private readonly FateHistoryStore history;
    private readonly Action openConfig;

    private TitleBarButton? newsButton;
    private Func<bool> hasUnseenNews = () => false;
    private Func<string> latestNewsVersion = () => string.Empty;

    internal MainWindow(
        FateCompassController controller,
        PluginConfiguration configuration,
        Localizer localizer,
        FateHistoryStore history,
        Action openConfig)
        // The window itself never scrolls. Everything in it is either a single row of tiles or a
        // list that scrolls inside its own frame, so a window scrollbar could only ever mean the
        // layout has gone wrong, and its arrival shifted the whole view sideways and pushed the
        // controls out of reach. Denying it turns that class of mistake into a visible one.
        : base(
            // Three hashes, not two. With two, the identity of the window is derived from the
            // whole caption, so putting the zone name in the title made every zone a different
            // window as far as the interface was concerned: it forgot where it had been put and
            // how big it was on every zone change. Three hashes take the identity from the part
            // after them alone, which lets the caption say whatever it likes.
            "FateCompass###FateCompassMain",
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.controller = controller;
        this.configuration = configuration;
        this.localizer = localizer;
        this.history = history;
        this.openConfig = openConfig;

        SizeConstraints = new WindowSizeConstraints
        {
            // Wide enough that the bottom row never wraps onto a second line, which is what
            // sets the floor rather than the tiles.
            // Tall enough for the header, its rule, a row of tiles, and the bottom bar. The
            // window does not scroll, so the minimum has to be the height that actually fits
            // everything rather than a round number.
            MinimumSize = new Vector2(360, 230),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        // The gear belongs in the title bar next to the close button, where every other window
        // puts it, rather than taking up space in the content area.
        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Cog,
            IconOffset = new Vector2(2f, 1f),
            Click = _ => openConfig(),
            ShowTooltip = () => ImGui.SetTooltip(localizer.Get(StringKeys.ButtonSettings)),
        });
    }

    public void Dispose()
    {
        // Nothing unmanaged is held. Present so the owner can dispose uniformly.
    }

    /// <summary>
    /// Adds the release-notes button beside the gear.
    /// </summary>
    /// <remarks>
    /// The title bar rather than a line in the content area, for the same reason as the gear:
    /// this is a way out of the window, not part of what the window is for. It turns gold while
    /// there are notes that have not been opened, which is the whole reason anyone would think
    /// to press it after an update.
    /// </remarks>
    internal void AddNewsButton(Func<bool> unseen, Func<string> latestVersion, Action onClick)
    {
        hasUnseenNews = unseen;
        latestNewsVersion = latestVersion;

        newsButton = new TitleBarButton
        {
            Icon = FontAwesomeIcon.Scroll,
            IconOffset = new Vector2(2f, 1f),
            Click = _ => onClick(),
            ShowTooltip = () => ImGui.SetTooltip(hasUnseenNews()
                ? localizer.Format(StringKeys.ButtonNewsTooltipUnseen, latestNewsVersion())
                : localizer.Get(StringKeys.ButtonNewsTooltip)),
        };

        TitleBarButtons.Add(newsButton);
    }

    /// <summary>Adds the diagnostics button. Only called while the developer tools are compiled in.</summary>
    internal void AddDebugButton(Action onClick) =>
        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Bug,
            IconOffset = new Vector2(2f, 1f),
            Click = _ => onClick(),
            ShowTooltip = () => ImGui.SetTooltip("Diagnostics"),
        });

    /// <summary>
    /// Title bar: the plugin name, then the zone it is currently talking about.
    /// </summary>
    /// <remarks>
    /// The window shows one zone's FATEs and nothing said which. That is fine until two of the
    /// same zone exist, which is exactly when it matters, so the instance number goes in as well
    /// whenever the game reports one.
    /// </remarks>
    public override void PreDraw()
    {
        var title = localizer.Get(StringKeys.WindowMainTitle);
        var zone = controller.CurrentZoneName;

        if (!string.IsNullOrEmpty(zone))
        {
            var instance = GameSnapshotProvider.CurrentInstance();
            title = instance > 0
                ? $"{title} - {zone} ({localizer.Format(StringKeys.InstanceLabel, instance)})"
                : $"{title} - {zone}";
        }

        WindowName = $"{title}###FateCompassMain";

        // Set every frame rather than once: it goes out again the moment the notes are opened,
        // and the language can change underneath it.
        newsButton?.IconColor = hasUnseenNews() ? UnseenNewsColour : null;
    }

    public override void Draw()
    {
        if (!configuration.Settings.Enabled)
        {
            ImGui.TextUnformatted(localizer.Get(StringKeys.ListDisabled));
            DrawSettingsButton();
            return;
        }

        if (controller.Player is null)
        {
            ImGui.TextUnformatted(localizer.Get(StringKeys.ListNotInWorld));
            DrawSettingsButton();
            return;
        }

        var ranked = controller.Ranked;
        if (ranked.Count == 0)
        {
            ImGui.TextUnformatted(localizer.Get(StringKeys.ListEmpty));
            DrawSettingsButton();
            return;
        }

        var gemstonesShown = DrawGemstones();
        DrawSharedFate(gemstonesShown);
        DrawRidingMapHint();

        // A rule under the header, matching the one above the bottom bar. The two frame the
        // list between them, which is what makes the window read as three parts rather than as
        // a run of text that happens to have tiles in the middle.
        ImGui.Dummy(new Vector2(0f, BarPadding * 0.5f));
        ImGui.Separator();
        ImGui.Dummy(new Vector2(0f, BarPadding * 0.5f));

        if (configuration.Settings.CompactView)
        {
            DrawCompact(ranked);
        }
        else
        {
            DrawTable(ranked);
        }

        DrawSettingsButton();
    }

    /// <summary>
    /// Shared FATE standing for the zone the player is in.
    /// </summary>
    /// <remarks>
    /// Deliberately labelled as a snapshot with its age. These values can only be read while
    /// the game's own progress window is open, so they can be hours old, and showing a stale
    /// number as though it were current would be worse than showing none. The plugin refreshes
    /// it silently every time that window is opened.
    /// </remarks>
    /// <param name="shareRow">True when the gemstone line was drawn and this can sit beside it.</param>
    private void DrawSharedFate(bool shareRow)
    {
        // The shared FATE standing is an open-world reward track. In Eureka, Bozja, and the
        // Occult Crescent there is nothing for it to report, so the whole line stays away
        // rather than announcing that it has no data.
        if (!configuration.Settings.TrackSharedFateRank
            || !ContentKindProvider.Current().EarnsBicolorGemstones())
        {
            return;
        }

        var snapshot = controller.SharedFate;
        if (!snapshot.HasData)
        {
            ImGui.TextDisabled(localizer.Get(StringKeys.SharedFateNoData));
            return;
        }

        var zoneName = DalamudServices.ClientState.MapId == 0
            ? string.Empty
            : controller.CurrentZoneName;

        var zone = snapshot.ForZone(zoneName);
        if (zone is null)
        {
            return;
        }

        var heading = localizer.Format(StringKeys.SharedFateHeading, zone.ZoneName);

        // Share the line only when the whole block actually fits on it. Sitting beside the
        // gemstones unconditionally meant the progress bar ran off the edge and lost its digits
        // the moment the window was made narrow, which is when the number matters most.
        var needed = ImGui.CalcTextSize(heading).X
            + (ImGui.CalcTextSize(FontAwesomeIcon.Star.ToIconString()).X * MaxRankStars)
            + ProgressBarWidth
            + (SectionGap * 2f);

        // Where the block would begin if it shared the line: the right edge of the gemstone
        // line, in window coordinates, plus the gap between the two.
        var wouldStartAt = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X + SectionGap;

        if (shareRow && wouldStartAt + needed <= ImGui.GetContentRegionMax().X)
        {
            ImGui.SameLine(0f, SectionGap);
        }

        ImGui.TextUnformatted(heading);

        // Rank as filled stars, mirroring the medals the game's own window shows. No claim is
        // made about the maximum: the game does not report it and it differs by expansion, so
        // only earned ranks are drawn.
        ImGui.SameLine(0f, 6f);
        DrawRankStars(zone);

        ImGui.SameLine(0f, 6f);

        if (zone.IsComplete)
        {
            ImGui.TextColored(
                new Vector4(0.45f, 0.85f, 0.5f, 1f),
                localizer.Get(StringKeys.SharedFateDone));
        }
        else
        {
            // Show progress towards finishing the zone entirely, not just the current rank.
            // "How far to done" is the question actually being asked; the rank is a milestone
            // along the way. Falls back to the current rank when the rank scheme cannot be
            // told apart yet.
            var overall = zone.TotalFraction;
            var fraction = overall ?? zone.Fraction ?? 0f;
            var label = overall is not null && zone.TotalCompleted is { } done && zone.TotalNeeded is { } need
                ? $"{done}/{need}"
                : $"{zone.Completed}/{zone.Needed}";

            ImGui.SetNextItemWidth(ProgressBarWidth);
            ImGui.ProgressBar(
                fraction,
                new Vector2(ProgressBarWidth, ImGui.GetTextLineHeight() + 2f),
                label);
        }

        if (ImGui.IsItemHovered())
        {
            var age = DateTimeOffset.Now - snapshot.TakenAt;
            var detail = zone.IsComplete
                ? localizer.Format(StringKeys.SharedFateComplete, zone.ZoneName, RankLabel(zone))
                : localizer.Format(
                    StringKeys.SharedFateProgress,
                    zone.ZoneName, RankLabel(zone), zone.Completed, zone.Needed, zone.Remaining);

            ImGui.SetTooltip($"{detail}\n{localizer.Format(StringKeys.SharedFateAge, FormatAge(age))}");
        }
    }

    /// <summary>Draws one star per earned rank, in the game's gold.</summary>
    private static void DrawRankStars(Core.Progress.SharedFateZone zone)
    {
        var stars = Math.Clamp(zone.Rank, 0, MaxRankStars);

        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.82f, 0.35f, 1f));

        try
        {
            for (var i = 0; i < stars; i++)
            {
                if (i > 0)
                {
                    ImGui.SameLine(0f, 1f);
                }

                ImGui.TextUnformatted(FontAwesomeIcon.Star.ToIconString());
            }

            if (stars == 0)
            {
                ImGui.TextUnformatted(FontAwesomeIcon.Star.ToIconString());
            }
        }
        finally
        {
            ImGui.PopStyleColor();
            ImGui.PopFont();
        }
    }

    private static string RankLabel(Core.Progress.SharedFateZone zone) =>
        string.IsNullOrEmpty(zone.RankText)
            ? zone.Rank.ToString(CultureInfo.CurrentCulture)
            : zone.RankText;

    private string FormatAge(TimeSpan age) => age.TotalMinutes < 1
        ? $"<1 {localizer.Get(StringKeys.UnitMinutes)}"
        : age.TotalHours < 1
            ? $"{age.TotalMinutes:F0} {localizer.Get(StringKeys.UnitMinutes)}"
            : $"{age.TotalHours:F0} h";

    /// <summary>
    /// The compact view: one card per FATE, in running order, with the two actions underneath.
    /// </summary>
    /// <remarks>
    /// Only recommended entries appear here. The excluded ones and their reasons belong in the
    /// table, where there is room to explain; repeating them as greyed-out cards would defeat
    /// the point of the compact view.
    /// </remarks>
    private void DrawCompact(IReadOnlyList<RankedFate> ranked)
    {
        var candidates = ranked
            .Where(entry => entry.IsRecommended && !entry.IsSpecialObjective)
            .ToList();

        // Everything that is running but is not a target: engagements whose sign-up window has
        // closed, then the standing objectives. The standing objective is pinned furthest right
        // of all, because it is the one entry that is always there and always the same; a fixed
        // place makes it something the eye can skip rather than something to re-read.
        var special = ranked
            .Where(entry => entry.IsSidelined)
            .OrderBy(entry => entry.IsSpecialObjective ? 1 : 0)
            .ThenBy(entry => entry.DistanceYalms)
            .ToList();

        if (candidates.Count == 0 && special.Count == 0)
        {
            ImGui.TextUnformatted(localizer.Get(StringKeys.ListEmpty));
            return;
        }

        var rowTop = ImGui.GetCursorPosY();

        // With nothing in the running order there is no strip to draw. Building one anyway left
        // an empty box the height of a tile row sitting above the inactive area, which pushed
        // those tiles down the window and stretched the dividing rule the whole way with them.
        if (candidates.Count == 0)
        {
            DrawSidelined(special, hasTargets: false);
            DrawPendingSidelineRule(rowTop);
            return;
        }

        var tile = FixedTileWidth();

        // The inactive area keeps its place at the right edge whatever happens to its left, so
        // its width comes off the top before the scrolling strip is measured.
        var reserved = special.Count > 0
            ? SpecialTileGap + (special.Count * (tile + TileGap))
            : 0f;

        var stripWidth = MathF.Max(
            ImGui.GetContentRegionAvail().X - reserved - (RowMargin * 2f),
            tile + TileGap);

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + RowMargin);

        // A strip that scrolls sideways rather than a row that stops. Cutting the list off at
        // whatever happened to fit meant the window quietly claimed a zone had five FATEs when
        // it had seven, and the number that could be shown depended on how wide the window was
        // rather than on anything about the zone. Now every recommendation is in there and the
        // window's width decides only how many are in view.
        if (ImGui.BeginChild(
            "##compactTiles",
            new Vector2(stripWidth, TileRowHeight()),
            false,
            ImGuiWindowFlags.HorizontalScrollbar))
        {
            for (var index = 0; index < candidates.Count; index++)
            {
                if (index > 0)
                {
                    ImGui.SameLine(0f, TileGap);
                }

                DrawTile(candidates[index]);
            }
        }

        ImGui.EndChild();

        DrawSidelined(special, hasTargets: true);
        DrawPendingSidelineRule(rowTop);
    }

    /// <summary>
    /// Draws the dividing rule, once the row it divides has been laid out and its height is
    /// therefore known.
    /// </summary>
    private void DrawPendingSidelineRule(float rowTop)
    {
        if (sidelineRuleX <= 0f)
        {
            return;
        }

        DrawSidelineRule(sidelineRuleX, rowTop, ImGui.GetCursorPosY());
        sidelineRuleX = 0f;
    }

    /// <summary>
    /// How tall one row of tiles is, including room for the strip's scrollbar.
    /// </summary>
    /// <remarks>
    /// Added up from the pieces a tile is made of rather than measured, because the strip has to
    /// be given its height before anything has been drawn into it.
    /// </remarks>
    private static float TileRowHeight()
    {
        var line = ImGui.GetTextLineHeightWithSpacing();
        var spacing = ImGui.GetStyle().ItemSpacing.Y;

        return line               // the rank above the icon
            + TileWidth           // the icon, which is square
            + ActionIconSize      // the flag and teleport buttons
            + (line * 2f)         // progress with the countdown, then the distance
            + (spacing * 4f)
            + ImGui.GetStyle().ScrollbarSize
            + 8f;
    }

    /// <summary>Where the inactive area's rule goes, set while the row is laid out.</summary>
    private float sidelineRuleX;

    /// <summary>
    /// The inactive area: everything that is running but is not a target, pinned to the right
    /// edge of the window.
    /// </summary>
    /// <remarks>
    /// Pinned rather than merely spaced. Space after the last target reads as separation only
    /// while there is a target to be after; with nothing else in the zone these tiles slid to
    /// the left and looked exactly like the running order they are meant not to be part of.
    /// Against the right edge they are in the same place every time, whatever else is on screen.
    /// <para>
    /// A faint rule marks where the area begins, so the grouping survives a glance.
    /// </para>
    /// </remarks>
    private void DrawSidelined(List<RankedFate> sidelined, bool hasTargets)
    {
        if (sidelined.Count == 0)
        {
            return;
        }

        var width = ((sidelined.Count - 1) * TileGap) + (sidelined.Count * FixedTileWidth());

        if (hasTargets)
        {
            ImGui.SameLine(0f, 0f);
        }

        // Never overlaps the targets: the right edge is preferred, but a crowded row wins.
        var minimum = ImGui.GetCursorPosX() + (hasTargets ? SpecialTileGap : RowMargin);
        var start = Math.Max(ImGui.GetContentRegionMax().X - width - RowMargin, minimum);

        sidelineRuleX = start;
        ImGui.SetCursorPosX(start);

        for (var index = 0; index < sidelined.Count; index++)
        {
            if (index > 0)
            {
                ImGui.SameLine(0f, TileGap);
            }

            DrawTile(sidelined[index]);
        }
    }

    /// <summary>
    /// A faint vertical rule just left of the inactive area, running the full depth of the row.
    /// </summary>
    /// <remarks>
    /// Given the row's real top and bottom rather than a fixed length, so it reaches as far as
    /// the tiles do however tall they turn out. A rule that stops halfway down reads as an
    /// accident.
    /// </remarks>
    private static void DrawSidelineRule(float startX, float top, float bottom)
    {
        var origin = ImGui.GetWindowPos() - new Vector2(ImGui.GetScrollX(), ImGui.GetScrollY());
        var x = origin.X + startX - (SpecialTileGap * 0.5f);

        ImGui.GetWindowDrawList().AddLine(
            new Vector2(x, origin.Y + top),
            new Vector2(x, origin.Y + bottom),
            ImGui.GetColorU32(ImGuiCol.Separator));
    }

    /// <summary>
    /// One FATE as a narrow vertical tile: rank, the game's own icon, the two actions as icon
    /// buttons, and the numbers that matter underneath.
    /// </summary>
    /// <remarks>
    /// Tiles sit side by side rather than stacked, so a farming run reads left to right and the
    /// window stays short. Everything that needs a word rather than a picture lives in the
    /// table view instead.
    /// </remarks>
    private void DrawTile(RankedFate entry)
    {
        ImGui.PushID((int)entry.Fate.Id);

        try
        {
            ImGui.BeginGroup();

            // Rank, centred over the icon. Anything sidelined has no place in the running order,
            // so it gets a dimmed mark rather than a number that would claim it is the next
            // thing to do. A star for a standing objective, a dot for a fight that has locked.
            var rank = entry switch
            {
                { IsSpecialObjective: true } => SpecialObjectiveGlyph,
                { IsSidelined: true } => SidelinedGlyph,
                _ => entry.Rank.ToString(CultureInfo.CurrentCulture),
            };

            var tileWidth = FixedTileWidth();
            var rankWidth = ImGui.CalcTextSize(rank).X;

            // Centred over the icon, not over the tile. The tile is as wide as its widest line
            // of text, which is wider than the icon, so centring on the tile pushed every number
            // to the right of the thing it labels and left it ambiguous which was which.
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((TileWidth - rankWidth) * 0.5f));

            if (entry.IsSidelined)
            {
                ImGui.TextDisabled(rank);
            }
            else
            {
                ImGui.TextUnformatted(rank);
            }

            DrawTileIcon(entry);

            ImGui.Spacing();
            DrawTileActions(entry);

            ImGui.Spacing();
            DrawTileFooter(entry);

            // Two jobs: keeps the tiles clear of the separator above the bottom bar, and fixes
            // the group's width so every tile is the same size whatever its contents measure.
            ImGui.Dummy(new Vector2(tileWidth, 4f));

            ImGui.EndGroup();
        }
        finally
        {
            ImGui.PopID();
        }
    }

    private void DrawTileIcon(RankedFate entry)
    {
        var icon = entry.Fate.IconId != 0
            ? DalamudServices.TextureProvider.GetFromGameIcon(entry.Fate.IconId).GetWrapOrDefault()
            : null;

        if (icon is null)
        {
            ImGui.Dummy(new Vector2(TileWidth, TileWidth));
        }
        else
        {
            // Full colour even in the inactive area. A fight that is under way should look like
            // one: greying it said "over", when what is true is "running, just not for you".
            // The position and the mark above the tile carry that, and they carry it better.
            ImGui.Image(icon.Handle, new Vector2(TileWidth, TileWidth));
        }

        if (ImGui.IsItemHovered())
        {
            DrawNameTooltip(entry);
        }
    }

    private void DrawTileActions(RankedFate entry)
    {
        if (IconButton("##flag", FlagIconId, localizer.Get(StringKeys.ButtonFlagTooltip)))
        {
            var territory = DalamudServices.ClientState.TerritoryType;
            var map = DalamudServices.ClientState.MapId;
            var position = entry.Fate.Position;
            var name = entry.Fate.Name;

            DalamudServices.OnGameThread(
                () => MapService.SetFlagAndEcho(
                    territory, map, position, name, configuration.Settings.TypeFlagIntoChat),
                "set flag");
        }

        if (!controller.Routes.TryGetValue(entry.Fate.Id, out var route))
        {
            return;
        }

        ImGui.SameLine(0f, 2f);

        var tint = route.Verdict switch
        {
            TeleportVerdict.Worthwhile => new Vector4(0.45f, 1f, 0.55f, 1f),
            TeleportVerdict.TravelIsFaster => new Vector4(0.6f, 0.6f, 0.6f, 1f),
            _ => new Vector4(1f, 0.45f, 0.45f, 1f),
        };

        var pressed = IconButton("##teleport", AetheryteIconId, TeleportTooltip(route), tint);

        if (pressed)
        {
            TravelTo(route.NearestAetheryte);
        }
    }

    /// <summary>
    /// Starts the journey the route advice describes.
    /// </summary>
    /// <remarks>
    /// One button, two actions, because the zones differ. Everywhere with real aetherytes this
    /// teleports. Inside Eureka, Bozja and the Occult Crescent the travel points carry no
    /// identifier at all, because they cannot be teleported to from outside; there the journey
    /// begins by returning to camp, and Return is that zone's teleport.
    /// <para>
    /// This used to do nothing at all in those zones, which was defensible when it was written
    /// (better than handing the game a destination it would refuse) and was still wrong: the
    /// player presses a travel button and expects to travel.
    /// </para>
    /// <para>
    /// The second leg, from the camp out to the waypoint, stays with the player. It runs through
    /// the camp's own travel menu, and driving a menu is not something this plugin does.
    /// </para>
    /// </remarks>
    private static void TravelTo(Aetheryte aetheryte)
    {
        if (aetheryte.Id == 0)
        {
            DalamudServices.OnGameThread(() => Plugin.Actions.Return(), "return");
            return;
        }

        var aetheryteId = aetheryte.Id;
        DalamudServices.OnGameThread(() => Plugin.Actions.Teleport(aetheryteId), "teleport");
    }

    private void DrawTileFooter(RankedFate entry)
    {
        // While a sign-up window is open, its cut-off replaces the ordinary countdown and is
        // drawn in amber. It is the only clock here that closes a door: miss it and the fight
        // carries on without you, however much time it still shows.
        if (entry.Fate.IsRegistrationOpen && entry.Fate.SecondsUntilRegistrationCloses is { } closing)
        {
            ImGui.TextColored(
                new Vector4(0.95f, 0.78f, 0.35f, 1f),
                $"{entry.Fate.ProgressPercent}%  {FormatSeconds(closing)}");

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(
                    localizer.Format(StringKeys.StateRegistrationCloses, FormatSeconds(closing)));
            }

            ImGui.TextDisabled($"{entry.DistanceYalms:F0}{localizer.Get(StringKeys.UnitYalms)}");
            DrawCompass(entry, TileWidth);
            return;
        }

        // A FATE whose clock has not started gets a dash rather than the words. Spelling it out
        // here made that one tile far wider than the rest and threw the whole row out of
        // alignment, for information that is only ever glanced at. The wording lives in the
        // tooltip, where there is room for it.
        var remaining = entry.Fate.HasStarted
            ? FormatSeconds(entry.Fate.SecondsRemaining)
            : NotStartedGlyph;

        ImGui.TextDisabled($"{entry.Fate.ProgressPercent}%  {remaining}");

        if (!entry.Fate.HasStarted && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(localizer.Get(StringKeys.StateNotStarted));
        }

        ImGui.TextDisabled($"{entry.DistanceYalms:F0}{localizer.Get(StringKeys.UnitYalms)}");
        DrawCompass(entry, TileWidth);
    }

    /// <summary>Radius of the needle drawn under a tile.</summary>
    private const float CompassRadius = 8f;

    /// <summary>The same needle on one line, for a table row.</summary>
    private void DrawCompassInline(RankedFate entry)
    {
        if (!configuration.Settings.ShowCompassNeedle)
        {
            return;
        }

        var player = DalamudServices.ObjectTable.LocalPlayer;
        if (player is null)
        {
            return;
        }

        var radius = ImGui.GetFontSize() * 0.42f;
        var top = ImGui.GetCursorPosY();

        if (CompassBearing.IsAtTarget(entry.DistanceYalms))
        {
            Widgets.CompassArrived(radius);
        }
        else
        {
            var here = new WorldPosition(player.Position.X, player.Position.Y, player.Position.Z);
            var relative = CompassBearing.Relative(here, player.Rotation, entry.Fate.Position);
            Widgets.CompassNeedle(radius, relative, CompassBearing.OnCourse(relative));
        }

        ImGui.SameLine(0f, 6f);
        ImGui.SetCursorPosY(top);
        ImGui.AlignTextToFramePadding();
    }

    /// <summary>
    /// The needle that says which way to turn, drawn from the facing as it is this frame.
    /// </summary>
    /// <remarks>
    /// Read live rather than from the polled snapshot. The rest of this window is refreshed
    /// twice a second, which is plenty for a countdown and useless for something that follows
    /// the character's heading: at that rate the needle lags a visible fraction of a turn behind
    /// the player and looks broken. One property read per tile per frame is a price worth paying
    /// for that.
    /// <para>
    /// It points along the straight line, which is the whole idea and also its limit. It answers
    /// "am I heading at it", never "can I get there this way".
    /// </para>
    /// </remarks>
    private void DrawCompass(RankedFate entry, float width)
    {
        if (!configuration.Settings.ShowCompassNeedle)
        {
            return;
        }

        var player = DalamudServices.ObjectTable.LocalPlayer;
        if (player is null)
        {
            return;
        }

        // Centred under the tile, so the row of needles reads as a row.
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((width - (CompassRadius * 2f)) / 2f));

        if (CompassBearing.IsAtTarget(entry.DistanceYalms))
        {
            Widgets.CompassArrived(CompassRadius);
            return;
        }

        var here = new WorldPosition(player.Position.X, player.Position.Y, player.Position.Z);
        var relative = CompassBearing.Relative(here, player.Rotation, entry.Fate.Position);

        Widgets.CompassNeedle(CompassRadius, relative, CompassBearing.OnCourse(relative));
    }

    /// <summary>
    /// The width every tile gets, whatever is in it.
    /// </summary>
    /// <remarks>
    /// Measured once from the widest thing a tile can ever hold rather than from what it happens
    /// to hold right now: full completion beside a three-digit countdown, and a four-figure
    /// distance. Sizing each tile to its own contents meant the row shifted sideways every time
    /// a percentage gained a digit or a timer crossed a minute, which is movement for its own
    /// sake in a window that is glanced at.
    /// </remarks>
    private float FixedTileWidth()
    {
        var widestFooter = $"100%  {WidestTimeSample}";
        var widestDistance = $"9999{localizer.Get(StringKeys.UnitYalms)}";

        return MathF.Max(
            TileWidth,
            MathF.Max(ImGui.CalcTextSize(widestFooter).X, ImGui.CalcTextSize(widestDistance).X));
    }

    /// <summary>The longest countdown a tile has to make room for.</summary>
    private const string WidestTimeSample = "110:00";

    /// <summary>The time shown in a tile's footer: the sign-up cut-off, or the time left.</summary>
    private string FooterTime(RankedFate entry) =>
        entry.Fate.IsRegistrationOpen && entry.Fate.SecondsUntilRegistrationCloses is { } closing
            ? FormatSeconds(closing)
            : entry.Fate.HasStarted
                ? FormatSeconds(entry.Fate.SecondsRemaining)
                : NotStartedGlyph;

    /// <summary>A borderless icon button with a tooltip, sized for the tiles.</summary>
    private static bool IconButton(string id, uint iconId, string tooltip, Vector4? tint = null)
    {
        var texture = DalamudServices.TextureProvider.GetFromGameIcon(iconId).GetWrapOrDefault();
        var size = new Vector2(ActionIconSize, ActionIconSize);

        bool pressed;
        if (texture is null)
        {
            pressed = ImGui.Button(id, size);
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1f, 1f, 1f, 0.15f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1f, 1f, 1f, 0.25f));

            try
            {
                pressed = ImGui.ImageButton(
                    texture.Handle, size, Vector2.Zero, Vector2.One, 0,
                    Vector4.Zero, tint ?? Vector4.One);
            }
            finally
            {
                ImGui.PopStyleColor(3);
            }
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(tooltip);
        }

        return pressed;
    }


    /// <summary>
    /// Gemstone purse against its cap. Every FATE reward earned at the cap is lost, so this is
    /// worth a line of its own rather than being buried in the settings.
    /// </summary>
    /// <remarks>
    /// Hidden in the exploratory zones. Nothing there pays in gemstones, so the line could only
    /// ever repeat the same number, and this window has too little room to spend a whole line on
    /// something that cannot change. The player's own currencies in those zones are already on
    /// screen in the game's own panel, so there is nothing to replace it with either.
    /// </remarks>
    /// <summary>
    /// One dimmed line when the zone sells a riding map the player has not bought.
    /// </summary>
    /// <remarks>
    /// Worth saying because it is the cheapest thing there is to make every route in this zone
    /// shorter, and it is easy to forget in a zone you have not farmed before. Only shown while
    /// something is actually missing, so it disappears the moment it stops being useful.
    /// <para>
    /// Deliberately not fed into the travel estimate. The plugin measures actual speed, so a
    /// missing upgrade is already reflected there; applying a guessed penalty on top would count
    /// the same slowdown twice.
    /// </para>
    /// </remarks>
    private void DrawRidingMapHint()
    {
        // Nothing to say where the player flies. The map raises ground speed, and in a zone with
        // its aether currents attuned the ground is not how anyone crosses it.
        if (MountSpeedProvider.CanFlyHere())
        {
            return;
        }

        var upgrades = MountSpeedProvider.Current();
        if (!upgrades.HasMissing)
        {
            return;
        }

        // Wrapped. It is a whole sentence and it ran off the right edge, losing the half that
        // said what to do about it.
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.95f, 0.78f, 0.35f, 0.9f));
        ImGui.PushTextWrapPos(0f);

        try
        {
            ImGui.TextUnformatted(localizer.Format(
                StringKeys.HintRidingMapMissing, upgrades.Available - upgrades.Owned));
        }
        finally
        {
            ImGui.PopTextWrapPos();
            ImGui.PopStyleColor();
        }
    }

    /// <returns>True when the line was actually drawn, so the caller knows whether the next
    /// header piece can share its row.</returns>
    private bool DrawGemstones()
    {
        if (!configuration.Settings.TrackGemstones
            || !ContentKindProvider.Current().EarnsBicolorGemstones())
        {
            return false;
        }

        var current = CurrencyProvider.GemstoneCount();
        var cap = CurrencyProvider.GemstoneCap();
        if (cap <= 0)
        {
            return false;
        }

        var status = GemstoneTracker.Evaluate(current, cap, configuration.Settings);
        var headroom = GemstoneTracker.Headroom(current, cap);

        var (text, colour) = status switch
        {
            GemstoneStatus.Full => (
                localizer.Get(StringKeys.GemstonesFull),
                new Vector4(0.95f, 0.35f, 0.35f, 1f)),
            GemstoneStatus.Warning => (
                localizer.Format(StringKeys.GemstonesWarning, headroom),
                new Vector4(0.95f, 0.78f, 0.35f, 1f)),
            _ => (
                localizer.Format(StringKeys.GemstonesLabel, current, cap),
                ImGui.GetStyle().Colors[(int)ImGuiCol.Text]),
        };

        ImGui.TextColored(colour, text);
        return true;
    }

    private void DrawTable(IReadOnlyList<RankedFate> ranked)
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg
            | ImGuiTableFlags.Borders
            | ImGuiTableFlags.SizingStretchProp
            | ImGuiTableFlags.ScrollY;

        // The table reserves exactly the room the bottom bar needs, no more and no less. It was
        // reserving a round thirty-two pixels, which stopped being enough when the bar gained
        // its padding: the table then overflowed by the difference, pushed the bar off the
        // bottom, and the window grew a scrollbar to reach controls that should never need
        // scrolling to. The table has its own scrollbar for the rows, which is where scrolling
        // belongs.
        var outerSize = new Vector2(0f, -BottomBarHeight());

        if (!ImGui.BeginTable("##fateList", 11, flags, outerSize))
        {
            return;
        }

        try
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn(localizer.Get(StringKeys.ListRank), ImGuiTableColumnFlags.WidthFixed, 28);
            ImGui.TableSetupColumn(localizer.Get(StringKeys.ListName));
            ImGui.TableSetupColumn(localizer.Get(StringKeys.ListLevel), ImGuiTableColumnFlags.WidthFixed, 40);
            ImGui.TableSetupColumn(localizer.Get(StringKeys.ListSync), ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn(localizer.Get(StringKeys.ListKind), ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn(localizer.Get(StringKeys.ListProgress), ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableSetupColumn(localizer.Get(StringKeys.ListRemaining), ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableSetupColumn(localizer.Get(StringKeys.ListDistance), ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableSetupColumn(localizer.Get(StringKeys.ListParticipants), ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn(localizer.Get(StringKeys.ListAetheryte));
            ImGui.TableSetupColumn(localizer.Get(StringKeys.ListActions), ImGuiTableColumnFlags.WidthFixed, 150);
            ImGui.TableHeadersRow();

            foreach (var entry in ranked)
            {
                DrawRow(entry);
            }
        }
        finally
        {
            ImGui.EndTable();
        }
    }

    private void DrawRow(RankedFate entry)
    {
        ImGui.TableNextRow();
        ImGui.PushID((int)entry.Fate.Id);

        // Tracked separately from "should this row be dim", because the finally block must only
        // pop what was actually pushed. An unbalanced ImGui style stack does not corrupt this
        // row, it corrupts every window drawn afterwards.
        var pushedColour = false;

        try
        {
            if (!entry.IsRecommended)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.55f, 0.55f, 0.55f, 1f));
                pushedColour = true;
            }

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(entry switch
            {
                { IsSpecialObjective: true } => SpecialObjectiveGlyph,
                { IsRecommended: true } => entry.Rank.ToString(CultureInfo.CurrentCulture),
                _ => "-",
            });

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(entry.Fate.Name);
            if (ImGui.IsItemHovered())
            {
                DrawNameTooltip(entry);
            }

            // An open registration window is the one thing here that can be permanently missed,
            // so it gets called out on the row rather than left to the tooltip.
            if (entry.Fate.IsRegistrationOpen)
            {
                ImGui.SameLine();
                ImGui.TextColored(
                    new Vector4(0.4f, 0.9f, 0.5f, 1f),
                    $"[{localizer.Get(StringKeys.StateRegistrationOpen)}]");
            }

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(entry.Fate.Level.ToString(CultureInfo.CurrentCulture));

            // Whether entering this one will sync you down, and to what.
            ImGui.TableNextColumn();
            var playerLevel = controller.Player?.Level ?? 0;
            ImGui.TextUnformatted(playerLevel > entry.Fate.SyncLevel
                ? localizer.Format(StringKeys.SyncYes, entry.Fate.SyncLevel)
                : localizer.Get(StringKeys.SyncNo));

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(KindText(entry.Fate.Kind));

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{entry.Fate.ProgressPercent}%");

            // The sign-up cut-off takes this column while the window is open, because it is the
            // deadline the player has to act on. The fight's own countdown keeps running past it
            // and would only tell them they still have time when they no longer do.
            ImGui.TableNextColumn();
            if (entry.Fate.IsRegistrationOpen && entry.Fate.SecondsUntilRegistrationCloses is { } closes)
            {
                ImGui.TextColored(new Vector4(0.95f, 0.78f, 0.35f, 1f), FormatSeconds(closes));
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(
                        localizer.Format(StringKeys.StateRegistrationCloses, FormatSeconds(closes)));
                }
            }
            else
            {
                ImGui.TextUnformatted(entry.Fate.HasStarted
                    ? FormatSeconds(entry.Fate.SecondsRemaining)
                    : localizer.Get(StringKeys.StateNotStarted));
            }

            ImGui.TableNextColumn();

            // Beside the distance rather than under it: a table row is one line, and how far
            // away something is and which way it lies are the same question asked twice.
            DrawCompassInline(entry);
            ImGui.TextUnformatted($"{entry.DistanceYalms:F0}{localizer.Get(StringKeys.UnitYalms)}");

            // Participation is only reported by the instanced engagement content. Open-world
            // FATEs leave this blank rather than showing a misleading zero.
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(entry.Fate.Participants is { } participants
                ? entry.Fate.MaxParticipants is { } max
                    ? $"{participants}/{max}"
                    : participants.ToString(CultureInfo.CurrentCulture)
                : string.Empty);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(controller.Routes.TryGetValue(entry.Fate.Id, out var route)
                ? route.NearestAetheryte.Name
                : localizer.Get(StringKeys.RouteNoAetheryte));

            ImGui.TableNextColumn();

            // Buttons keep their normal colour even on a dimmed row, so they still read as
            // usable rather than looking disabled.
            if (pushedColour)
            {
                ImGui.PopStyleColor();
                pushedColour = false;
            }

            DrawRowActions(entry);
        }
        finally
        {
            if (pushedColour)
            {
                ImGui.PopStyleColor();
            }

            ImGui.PopID();
        }
    }

    /// <summary>
    /// Why a FATE was passed over, and when it is expected back. The estimate only appears
    /// once the history holds enough sightings to mean something.
    /// </summary>
    /// <summary>
    /// What a tile is, on hover: its name first, then its kind, then anything worth adding.
    /// </summary>
    /// <remarks>
    /// The name was missing, which left the tooltip answering questions nobody had while the
    /// obvious one went unanswered: a tile is an icon, and four icons of the same kind are not
    /// telling anyone which FATE is which.
    /// <para>
    /// Only what the tile cannot already show. The progress, the countdown and the distance are
    /// printed underneath it, so repeating them here would be filling a tooltip to look thorough.
    /// The interval estimate is gone entirely: it was a guess from a handful of sightings, and a
    /// number nobody should act on is worse than no number.
    /// </para>
    /// </remarks>
    private void DrawNameTooltip(RankedFate entry)
    {
        var lines = new List<string> { entry.Fate.Name, KindText(entry.Fate.Kind) };

        // Only the instanced content reports this, and there it is the one thing that decides
        // whether a fight is worth going to at all.
        if (entry.Fate.Participants is { } participants && entry.Fate.MaxParticipants is { } max)
        {
            lines.Add($"{localizer.Get(StringKeys.ListParticipants)}: {participants}/{max}");
        }

        if (!entry.IsRecommended)
        {
            lines.Add(ReasonText(entry.ExclusionReason));
        }

        ImGui.SetTooltip(string.Join('\n', lines));
    }

    private string FormatInterval(TimeSpan interval) => interval.TotalMinutes >= 1
        ? $"{interval.TotalMinutes:F0} {localizer.Get(StringKeys.UnitMinutes)}"
        : $"{interval.TotalSeconds:F0}{localizer.Get(StringKeys.UnitSeconds)}";

    private void DrawRowActions(RankedFate entry)
    {
        if (ImGui.SmallButton(localizer.Get(StringKeys.ButtonFlag)))
        {
            // On the framework thread, like every other game call. This one was reaching the
            // map agent straight from the draw callback, which is the mistake that took the
            // client down once already (FH-08).
            var territory = DalamudServices.ClientState.TerritoryType;
            var map = DalamudServices.ClientState.MapId;
            var position = entry.Fate.Position;
            var name = entry.Fate.Name;
            var typeIntoChat = configuration.Settings.TypeFlagIntoChat;

            DalamudServices.OnGameThread(
                () => MapService.SetFlagAndEcho(territory, map, position, name, typeIntoChat),
                "set flag");
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(localizer.Get(StringKeys.ButtonFlagTooltip));
        }

        if (!controller.Routes.TryGetValue(entry.Fate.Id, out var route))
        {
            return;
        }

        ImGui.SameLine();

        // The button is coloured by whether the teleport is actually worth it, so the answer is
        // visible without hovering. It stays clickable in every case: the recommendation is
        // advice, not a lock.
        var colour = route.Verdict switch
        {
            TeleportVerdict.Worthwhile => new Vector4(0.35f, 0.75f, 0.4f, 1f),
            TeleportVerdict.TravelIsFaster => new Vector4(0.5f, 0.5f, 0.5f, 1f),
            _ => new Vector4(0.7f, 0.35f, 0.35f, 1f),
        };

        ImGui.PushStyleColor(ImGuiCol.Button, colour);
        try
        {
            if (ImGui.SmallButton(TravelButtonLabel(route.NearestAetheryte)))
            {
                TravelTo(route.NearestAetheryte);
            }
        }
        finally
        {
            ImGui.PopStyleColor();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(TeleportTooltip(route));
        }
    }

    /// <summary>
    /// The route advice: where to go on the first line, what it is worth on the second.
    /// </summary>
    /// <remarks>
    /// Two lines, and the destination is the first thing on the first one. This used to run to
    /// four lines of prose that repeated the destination once and the timings twice, and the one
    /// thing being looked for — which waypoint — was buried in the middle of a sentence.
    /// </remarks>
    /// <summary>
    /// What the travel button says. Inside an exploratory zone it does something different, so
    /// it has to be called something different.
    /// </summary>
    private string TravelButtonLabel(Aetheryte aetheryte) => localizer.Get(
        aetheryte.Id == 0 ? StringKeys.ButtonReturn : StringKeys.ButtonTeleport);

    private string TeleportTooltip(RouteHint route)
    {
        var seconds = MathF.Abs(route.SecondsSaved).ToString("F0", CultureInfo.CurrentCulture);

        // In an exploratory zone the button casts Return, and the leg from the camp out to the
        // waypoint is the player's to walk through the travel menu. Saying so is the difference
        // between a button that looks broken and one that is understood.
        var leg = route.NearestAetheryte.Id == 0
            ? "\n" + localizer.Get(StringKeys.RouteReturnFirst)
            : string.Empty;

        return TeleportVerdictText(route, seconds) + leg;
    }

    private string TeleportVerdictText(RouteHint route, string seconds)
    {
        return route.Verdict switch
        {
            TeleportVerdict.Worthwhile =>
                $"→ {route.NearestAetheryte.Name}\n"
                + localizer.Format(StringKeys.RouteSaves, seconds),

            TeleportVerdict.TravelIsFaster =>
                localizer.Get(StringKeys.RouteWalkFaster) + "\n"
                + localizer.Format(StringKeys.RouteCosts, seconds),

            _ => localizer.Get(StringKeys.RouteTooLate),
        };
    }

    /// <summary>
    /// The bottom bar: controls on the left, view switch pinned to the right edge so the two
    /// groups never run into each other.
    /// </summary>
    private void DrawSettingsButton()
    {
        // Pin the bar to the bottom edge. Left where it fell, it floated directly under the
        // tiles with a growing gap beneath it whenever the window was made taller.
        var bottom = ImGui.GetWindowContentRegionMax().Y - BottomBarHeight();
        if (bottom > ImGui.GetCursorPosY())
        {
            ImGui.SetCursorPosY(bottom);
        }

        ImGui.Separator();

        // Air between the rule and the controls. Sitting straight on the line they read as
        // hanging off it rather than as a bar of their own.
        ImGui.Dummy(new Vector2(0f, BarPadding * 0.5f));

        DrawAutomationToggle();

        DrawAnnounceControls();

        // The right-hand group: ready up, then the view switch. Both are icons, so the bar reads
        // as controls rather than as a sentence with a button in it.
        // Rule, gap, ready-up, gap, then the two halves of the view switch.
        var icon = IconButtonSize();
        var groupWidth = 1f + ControlGap + icon + ControlGap + (icon * 2f) + 2f;
        var rightEdge = ImGui.GetWindowContentRegionMax().X - groupWidth;

        ImGui.SameLine();
        ImGui.SetCursorPosX(MathF.Max(rightEdge, ImGui.GetCursorPosX() + ControlGap));

        DrawVerticalRule();
        ImGui.SameLine(0f, ControlGap);

        DrawReadyUpButton();

        ImGui.SameLine(0f, ControlGap);
        DrawViewToggle();
    }

    /// <summary>
    /// Ready up, as an icon on the right rather than a labelled button in the middle.
    /// </summary>
    /// <remarks>
    /// A lightning bolt: this is the one control here that does something the instant it is
    /// pressed, and the everything-at-once quality is the point. As a word in the middle of the
    /// bar it read as a heading for the switches around it rather than as a thing to press.
    /// </remarks>
    private void DrawReadyUpButton()
    {
        var size = IconButtonSize();

        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign, new Vector2(0.5f, 0.5f));

        bool pressed;
        try
        {
            pressed = ImGui.Button(FontAwesomeIcon.Bolt.ToIconString(), new Vector2(size, size));
        }
        finally
        {
            ImGui.PopStyleVar();
            ImGui.PopFont();
        }

        if (pressed)
        {
            DalamudServices.OnGameThread(
                () => controller.RunEngage(ActionTrigger.Manual), "manual engage");
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                $"{localizer.Get(StringKeys.ButtonRunNow)}\n{localizer.Get(StringKeys.ButtonEngageTooltip)}");
        }
    }

    /// <summary>
    /// The two views as a pair of buttons with the active one highlighted, rather than one
    /// button whose label you have to interpret. A single button saying "Table" cannot tell you
    /// whether that is the current view or the one you would switch to.
    /// </summary>
    private void DrawViewToggle()
    {
        DrawSegment(FontAwesomeIcon.ThLarge, StringKeys.ButtonViewCompact,
            configuration.Settings.CompactView, true);

        ImGui.SameLine(0f, 2f);

        DrawSegment(FontAwesomeIcon.List, StringKeys.ButtonViewTable,
            !configuration.Settings.CompactView, false);
    }

    private void DrawSegment(FontAwesomeIcon icon, string tooltipKey, bool active, bool compact)
    {
        var background = active
            ? new Vector4(0.24f, 0.42f, 0.62f, 1f)
            : new Vector4(0f, 0f, 0f, 0f);

        ImGui.PushStyleColor(ImGuiCol.Button, background);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, active
            ? background + new Vector4(0.08f, 0.08f, 0.08f, 0f)
            : new Vector4(1f, 1f, 1f, 0.12f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, background);
        ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign, new Vector2(0.5f, 0.5f));
        ImGui.PushFont(UiBuilder.IconFont);

        try
        {
            var size = IconButtonSize();
            if (ImGui.Button(icon.ToIconString(), new Vector2(size, size)) && !active)
            {
                configuration.Settings.CompactView = compact;
                configuration.Save();
            }
        }
        finally
        {
            ImGui.PopFont();
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(3);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(localizer.Get(tooltipKey));
        }
    }

    /// <summary>
    /// A toggle that shows its state rather than a button you have to press to find out.
    /// </summary>
    /// <remarks>
    /// The automation is the one setting worth flipping mid-session, so it belongs in the main
    /// window and not only in the settings. Colour carries the state: green for on, muted for
    /// off, matching the minimap icon.
    /// </remarks>
    /// <summary>
    /// The announcement switch and its channel, beside the automation switch.
    /// </summary>
    /// <remarks>
    /// Here rather than in the settings because both are decisions of the moment: whether to
    /// tell the party at all, and which party. A setting you change several times an evening
    /// does not belong two clicks away behind a gear.
    /// <para>
    /// Absent entirely unless the feature has been allowed in the settings. A switch for
    /// something that cannot happen is worse than no switch.
    /// </para>
    /// </remarks>
    private void DrawAnnounceControls()
    {
        var settings = configuration.Settings;
        if (!settings.AllowChatAnnounce)
        {
            return;
        }

        // Space alone was not enough, and it could not be: the automation switch carries a label
        // and this one does not, so a gap between them still reads as one control with two
        // halves. A rule says "different thing" outright, and the speech bubble says which thing,
        // which is the part a gap can never carry.
        ImGui.SameLine(0f, ControlGap);
        DrawVerticalRule();

        ImGui.SameLine(0f, ControlGap);
        ImGui.PushFont(UiBuilder.IconFont);

        try
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled(FontAwesomeIcon.CommentDots.ToIconString());
        }
        finally
        {
            ImGui.PopFont();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(localizer.Get(StringKeys.ButtonAnnounceTooltip));
        }

        ImGui.SameLine(0f, 6f);

        var on = settings.AnnounceNextFate;
        if (Widgets.Toggle("##announceNextFate", ref on))
        {
            settings.AnnounceNextFate = on;
            configuration.Save();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(localizer.Get(StringKeys.ButtonAnnounceTooltip));
        }

        ImGui.SameLine(0f, 6f);

        var channels = ChatChannels.All;
        var index = ChatChannels.IndexOf(settings.AnnounceChannel);
        var labels = ChatChannels.LabelsFor(localizer);

        ImGui.SetNextItemWidth(AnnounceChannelWidth);

        if (ImGui.Combo("##announceChannel", ref index, labels, labels.Length))
        {
            settings.AnnounceChannel = channels[index].Command;
            configuration.Save();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(localizer.Format(
                StringKeys.ButtonAnnounceChannelTooltip, channels[index].Command));
        }
    }

    /// <summary>Width of the channel picker in the bottom bar.</summary>
    private const float AnnounceChannelWidth = 130f;

    /// <summary>
    /// Edge length for the square icon buttons in the bottom bar.
    /// </summary>
    /// <remarks>
    /// Measured from the widest glyph that has to go in one, not taken from the row height. The
    /// icon font's glyphs are wider than a line of text, so a button sized to the text height
    /// clipped them and left what remained looking off-centre.
    /// <para>
    /// One size for all of them, so the bar reads as a row of equals rather than as buttons that
    /// happen to be near each other.
    /// </para>
    /// </remarks>
    private static float IconButtonSize()
    {
        ImGui.PushFont(UiBuilder.IconFont);

        float widest;
        try
        {
            widest = MathF.Max(
                ImGui.CalcTextSize(FontAwesomeIcon.Bolt.ToIconString()).X,
                MathF.Max(
                    ImGui.CalcTextSize(FontAwesomeIcon.ThLarge.ToIconString()).X,
                    ImGui.CalcTextSize(FontAwesomeIcon.List.ToIconString()).X));
        }
        finally
        {
            ImGui.PopFont();
        }

        return MathF.Max(
            ImGui.GetFrameHeight(),
            widest + (ImGui.GetStyle().FramePadding.X * 2f));
    }

    /// <summary>
    /// A short upright rule on the current line, dividing one group of controls from the next.
    /// </summary>
    private static void DrawVerticalRule()
    {
        var height = ImGui.GetFrameHeight();
        var top = ImGui.GetCursorScreenPos();

        ImGui.GetWindowDrawList().AddLine(
            top with { Y = top.Y + 2f },
            new Vector2(top.X, top.Y + height - 2f),
            ImGui.GetColorU32(ImGuiCol.Separator));

        // Claims the width the line occupies, so the next control does not sit on top of it.
        ImGui.Dummy(new Vector2(1f, height));
    }

    private void DrawAutomationToggle()
    {
        var on = configuration.Settings.AutoEngageOnFateEnter;

        if (Widgets.Toggle("##fateAutomation", ref on))
        {
            configuration.Settings.AutoEngageOnFateEnter = on;
            configuration.Save();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(localizer.Get(StringKeys.ButtonAutomationTooltip));
        }

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(localizer.Get(
            on ? StringKeys.ButtonAutomationOn : StringKeys.ButtonAutomationOff));
    }

    private string KindText(FateKind kind) => localizer.Get(kind switch
    {
        FateKind.Boss => StringKeys.KindBoss,
        FateKind.Slay => StringKeys.KindSlay,
        FateKind.Collect => StringKeys.KindCollect,
        FateKind.Escort => StringKeys.KindEscort,
        FateKind.Defend => StringKeys.KindDefend,
        FateKind.Skirmish => StringKeys.KindSkirmish,
        FateKind.CriticalEngagement => StringKeys.KindCriticalEngagement,
        FateKind.CriticalEncounter => StringKeys.KindCriticalEncounter,
        FateKind.SpecialObjective => StringKeys.KindSpecialObjective,
        _ => StringKeys.KindUnknown,
    });

    private string ReasonText(FateExclusionReason reason) => localizer.Get(reason switch
    {
        FateExclusionReason.NotJoinable => StringKeys.ExcludedNotJoinable,
        FateExclusionReason.RegistrationClosed => StringKeys.ExcludedRegistrationClosed,
        FateExclusionReason.RegistrationTooLate => StringKeys.ExcludedRegistrationTooLate,
        FateExclusionReason.Filtered => StringKeys.ExcludedFiltered,
        FateExclusionReason.NearlyComplete => StringKeys.ExcludedNearlyComplete,
        FateExclusionReason.ExpiringSoon => StringKeys.ExcludedExpiringSoon,
        FateExclusionReason.Unreachable => StringKeys.ExcludedUnreachable,
        _ => StringKeys.ListEmpty,
    });

    private string FormatSeconds(int seconds) => seconds >= 60
        ? $"{seconds / 60}:{seconds % 60:D2}"
        : $"{seconds}{localizer.Get(StringKeys.UnitSeconds)}";
}
