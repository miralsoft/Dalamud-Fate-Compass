using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using FateHelper.Adapters;
using FateHelper.Configuration;
using FateHelper.Core.Localization;
using FateHelper.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FateHelper.UI;

/// <summary>
/// A small clickable icon stuck to the edge of the minimap, in the manner of the weather glyph.
/// </summary>
/// <remarks>
/// The server info bar sits above the minimap and reads as a line of text. This instead
/// positions a borderless overlay against the minimap addon itself, so the icon appears to
/// belong to it.
/// <para>
/// The position is read from the game's own <c>_NaviMap</c> addon every frame rather than
/// configured, so it follows the player's HUD layout, scale, and any repositioning without
/// needing to be told. When the minimap is hidden, so is this.
/// </para>
/// </remarks>
internal sealed unsafe class MinimapButton : Window, IDisposable
{
    private const ImGuiWindowFlags BaseFlags =
        ImGuiWindowFlags.NoDecoration
        | ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoResize
        | ImGuiWindowFlags.NoScrollbar
        | ImGuiWindowFlags.NoSavedSettings
        | ImGuiWindowFlags.NoFocusOnAppearing
        | ImGuiWindowFlags.NoNav;

    private const string MinimapAddonName = "_NaviMap";

    private readonly PluginConfiguration configuration;
    private readonly Localizer localizer;
    private readonly Action onLeftClick;
    private readonly Action onRightClick;

    internal MinimapButton(
        PluginConfiguration configuration,
        Localizer localizer,
        Action onLeftClick,
        Action onRightClick)
        : base("##FateHelperMinimapButton", BaseFlags)
    {
        this.configuration = configuration;
        this.localizer = localizer;
        this.onLeftClick = onLeftClick;
        this.onRightClick = onRightClick;

        IsOpen = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
    }

    internal int RecommendedCount { get; set; }

    public void Dispose()
    {
        // Nothing unmanaged is held.
    }

    /// <summary>Only draws while the minimap is on screen and nothing is covering the icon.</summary>
    public override bool DrawConditions()
    {
        if (!configuration.Settings.ShowMinimapButton || !TryGetMinimapBounds(out var origin, out var scale))
        {
            return false;
        }

        var size = configuration.Settings.MinimapButtonSize * scale;
        var iconX = origin.X - size + (configuration.Settings.MinimapButtonOffsetX * scale);
        var iconY = origin.Y + (configuration.Settings.MinimapButtonOffsetY * scale);

        return !IsCoveredByGameWindow(iconX, iconY, size);
    }

    /// <summary>
    /// True when a visible game window actually overlaps the icon's rectangle.
    /// </summary>
    /// <remarks>
    /// A Dalamud overlay always draws above the game's interface and there is no way to put it
    /// behind. The only honest alternative is to get out of the way when something is genuinely
    /// on top of it.
    /// <para>
    /// This replaces an earlier check on whether any window merely had focus, which was wrong in
    /// both directions: a small menu nowhere near the minimap hid the icon, and closing a menu
    /// did not reliably bring it back. Testing the rectangles asks the question that actually
    /// matters.
    /// </para>
    /// <para>
    /// The permanent HUD pieces are skipped by name. They are always visible, so treating them
    /// as cover would hide the icon forever.
    /// </para>
    /// </remarks>
    private static bool IsCoveredByGameWindow(float x, float y, float size)
    {
        try
        {
            var stage = AtkStage.Instance();
            if (stage is null || stage->RaptureAtkUnitManager is null)
            {
                return false;
            }

            // Only windows the player actually has open count. Walking every loaded unit and
            // testing rectangles hid the icon permanently, because the HUD is full of always
            // loaded units whose bounds happen to cover that corner. The focused list is what
            // the player would call "a window that is open".
            var units = &stage->RaptureAtkUnitManager->AtkUnitManager.FocusedUnitsList;
            var right = x + size;
            var bottom = y + size;

            for (var i = 0; i < units->Count; i++)
            {
                var unit = units->Entries[i].Value;
                if (unit is null || !unit->IsVisible || unit->RootNode is null)
                {
                    continue;
                }

                var name = unit->NameString;
                if (string.IsNullOrEmpty(name) || PermanentHudElements.Contains(name))
                {
                    continue;
                }

                var width = unit->RootNode->Width * unit->Scale;
                var height = unit->RootNode->Height * unit->Scale;

                // A zero-sized unit covers nothing, and there are plenty of those.
                if (width <= 1f || height <= 1f)
                {
                    continue;
                }

                if (unit->X < right && unit->X + width > x &&
                    unit->Y < bottom && unit->Y + height > y)
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            // Never let a display decision take the client down (FH-09).
            return false;
        }
    }

    /// <summary>
    /// HUD elements that are always on screen, so they must never count as covering the icon.
    /// </summary>
    private static readonly HashSet<string> PermanentHudElements = new(StringComparer.Ordinal)
    {
        MinimapAddonName,
        "_NaviMapContainer",
        "_ActionBar", "_ActionBar01", "_ActionBar02", "_ActionBar03", "_ActionBar04",
        "_ActionBar05", "_ActionBar06", "_ActionBar07", "_ActionBar08", "_ActionBar09",
        "_ActionCross", "_ActionDoubleCrossL", "_ActionDoubleCrossR",
        "_ParameterWidget", "_Status", "_StatusCustom0", "_StatusCustom1", "_StatusCustom2",
        "_StatusCustom3", "_TargetInfo", "_TargetInfoMainTarget", "_TargetInfoCastBar",
        "_TargetInfoBuffDebuff", "_FocusTargetInfo", "_PartyList", "_LimitBreak",
        "_ScreenText", "_ScreenFrame", "_Notification", "_ChatLog", "_ChatLogPanel_0",
        "_ChatLogPanel_1", "_ChatLogPanel_2", "_ChatLogPanel_3", "_DTR", "_MainCommand",
        "_Money", "_Exp", "_BagWidget", "_Journal", "_ToDoList", "_NaviMapPassage",
        "_LocationTitle", "_LocationTitleShort", "_AreaText", "_WideText", "_ScreenInfo",
    };

    /// <summary>
    /// While true the icon can be dragged with the mouse, in the manner of the game's own HUD
    /// layout mode. Dragging beats sliders here: the target is a spot on screen, and pointing
    /// at it is more direct than describing it with two numbers.
    /// </summary>
    internal bool IsRepositioning { get; set; }

    public override void PreDraw()
    {
        if (!TryGetMinimapBounds(out var position, out var scale))
        {
            return;
        }

        var size = configuration.Settings.MinimapButtonSize * scale;
        Size = new Vector2(size, size);
        SizeCondition = ImGuiCond.Always;

        Flags = IsRepositioning
            ? BaseFlags & ~ImGuiWindowFlags.NoMove
            : BaseFlags;

        // Anchored to the minimap, so it follows the HUD. The stored offset is relative to the
        // minimap rather than to the screen, which is what keeps it in place when the HUD moves
        // or its scale changes.
        var anchored = new Vector2(
            position.X - size + (configuration.Settings.MinimapButtonOffsetX * scale),
            position.Y + (configuration.Settings.MinimapButtonOffsetY * scale));

        if (!IsRepositioning)
        {
            Position = anchored;
            PositionCondition = ImGuiCond.Always;
            return;
        }

        // While dragging, place it once and then let ImGui own the position, so the anchor does
        // not fight the mouse.
        Position = anchored;
        PositionCondition = ImGuiCond.Appearing;
    }

    /// <summary>
    /// Stores wherever the icon was dragged to, converted back into an offset from the minimap.
    /// </summary>
    internal void CommitPosition(Vector2 screenPosition)
    {
        if (!TryGetMinimapBounds(out var minimap, out var scale) || scale <= 0f)
        {
            return;
        }

        var size = configuration.Settings.MinimapButtonSize * scale;
        configuration.Settings.MinimapButtonOffsetX = (screenPosition.X - minimap.X + size) / scale;
        configuration.Settings.MinimapButtonOffsetY = (screenPosition.Y - minimap.Y) / scale;
    }

    public override void Draw()
    {
        if (!TryGetMinimapBounds(out _, out var scale))
        {
            return;
        }

        var size = configuration.Settings.MinimapButtonSize * scale;
        // The plugin's own icon, the same picture the plugin list shows. A control stuck to the
        // HUD should be recognisable as this plugin rather than as a borrowed game glyph.
        var texture = PluginIcon.Texture();

        // While repositioning, draw a plain image rather than a button. A button would swallow
        // the click and force the player to grab an invisible strip above the icon, which is
        // exactly the thing being fixed: you should be able to drag the icon by the icon.
        if (IsRepositioning)
        {
            if (texture is not null)
            {
                ImGui.Image(texture.Handle, new Vector2(size, size));
            }
            else
            {
                ImGui.Dummy(new Vector2(size, size));
            }

            var min = ImGui.GetWindowPos();
            ImGui.GetWindowDrawList().AddRect(
                min,
                min + new Vector2(size, size),
                ImGui.GetColorU32(new Vector4(1f, 0.8f, 0.2f, 1f)),
                4f,
                ImDrawFlags.None,
                2f);

            CommitPosition(min);
            return;
        }

        if (texture is null)
        {
            // Fall back to a plain button so the control never simply disappears.
            if (ImGui.Button("F", new Vector2(size, size)))
            {
                onLeftClick();
            }
        }
        else
        {
            // No button chrome at all. The frame that ImGui draws behind an image button read
            // as a red box against the HUD, which is exactly what this control should not do:
            // it is meant to look like part of the minimap, not like a plugin window.
            ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);

            try
            {
                // Full colour when the automation is on, washed out and faded when it is off.
                // Greying it reads as "inactive" at a glance, where dimming alone can just look
                // like a dark icon.
                var tint = configuration.Settings.AutoEngageOnFateEnter
                    ? new Vector4(1f, 1f, 1f, 1f)
                    : new Vector4(0.45f, 0.45f, 0.45f, 0.65f);

                if (ImGui.ImageButton(texture.Handle, new Vector2(size, size),
                    Vector2.Zero, Vector2.One, 0, Vector4.Zero, tint))
                {
                    onLeftClick();
                }
            }
            finally
            {
                ImGui.PopStyleVar();
                ImGui.PopStyleColor(3);
            }
        }

        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            onRightClick();
        }

        if (ImGui.IsItemHovered())
        {
            var automation = configuration.Settings.AutoEngageOnFateEnter
                ? localizer.Get(StringKeys.CommandAutoOn)
                : localizer.Get(StringKeys.CommandAutoOff);

            ImGui.SetTooltip(
                $"{localizer.Get(StringKeys.WindowMainTitle)} ({RecommendedCount})\n" +
                $"{localizer.Get(StringKeys.StatusBarTooltip)}\n{automation}");
        }
    }

    /// <summary>Screen position and scale of the minimap, or false when it is not visible.</summary>
    private static bool TryGetMinimapBounds(out Vector2 position, out float scale)
    {
        position = Vector2.Zero;
        scale = 1f;

        try
        {
            // Not safe to follow addon pointers while the framework is unloading, which is
            // exactly what a hot reload does underneath a window that is still drawing.
            if (DalamudServices.Framework.IsFrameworkUnloading)
            {
                return false;
            }

            var handle = DalamudServices.GameGui.GetAddonByName(MinimapAddonName);
            var addon = (AtkUnitBase*)handle.Address;
            if (addon is null || !addon->IsVisible || addon->RootNode is null)
            {
                return false;
            }

            position = new Vector2(addon->X, addon->Y);
            scale = addon->Scale;
            return true;
        }
        catch
        {
            // A missing or mid-rebuild addon is normal during zone changes.
            return false;
        }
    }
}
