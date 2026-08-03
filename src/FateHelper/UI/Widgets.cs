using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace FateHelper.UI;

/// <summary>
/// Small drawing helpers that ImGui does not provide.
/// </summary>
internal static class Widgets
{
    private static readonly Vector4 OnTrack = new(0.30f, 0.78f, 0.42f, 1f);
    private static readonly Vector4 OffTrack = new(0.86f, 0.33f, 0.33f, 1f);
    private static readonly Vector4 Knob = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Mark = new(1f, 1f, 1f, 0.95f);

    /// <summary>
    /// A sliding on/off switch, drawn rather than assembled from buttons.
    /// </summary>
    /// <remarks>
    /// A coloured button can say what it is, but a switch shows which side it sits on, which is
    /// the whole point of a boolean control: the state should be readable without reading.
    /// </remarks>
    /// <returns>True when the value was changed by this call.</returns>
    internal static bool Toggle(string id, ref bool value, float height = 0f)
    {
        var trackHeight = height > 0f ? height : ImGui.GetFrameHeight();
        var trackWidth = trackHeight * 1.85f;
        var origin = ImGui.GetCursorScreenPos();

        ImGui.InvisibleButton(id, new Vector2(trackWidth, trackHeight));
        var changed = ImGui.IsItemClicked();
        if (changed)
        {
            value = !value;
        }

        var draw = ImGui.GetWindowDrawList();
        var radius = trackHeight * 0.5f;
        var hovered = ImGui.IsItemHovered();

        var track = value ? OnTrack : OffTrack;
        if (hovered)
        {
            track += new Vector4(0.06f, 0.06f, 0.06f, 0f);
        }

        draw.AddRectFilled(
            origin,
            origin + new Vector2(trackWidth, trackHeight),
            ImGui.GetColorU32(track),
            radius);

        // The knob sits against whichever end matches the state.
        var padding = trackHeight * 0.1f;
        var knobRadius = radius - padding;
        var knobX = value
            ? origin.X + trackWidth - radius
            : origin.X + radius;
        var knobCentre = new Vector2(knobX, origin.Y + radius);

        draw.AddCircleFilled(knobCentre, knobRadius, ImGui.GetColorU32(Knob));

        // A tick or a cross on the empty side, so the state survives being read in greyscale
        // or by someone who does not separate red from green.
        var markCentre = value
            ? new Vector2(origin.X + radius, origin.Y + radius)
            : new Vector2(origin.X + trackWidth - radius, origin.Y + radius);

        var arm = knobRadius * 0.45f;
        var colour = ImGui.GetColorU32(Mark);
        var thickness = MathF.Max(trackHeight * 0.09f, 1.5f);

        if (value)
        {
            draw.AddLine(
                markCentre + new Vector2(-arm, 0f),
                markCentre + new Vector2(-arm * 0.2f, arm * 0.7f),
                colour,
                thickness);
            draw.AddLine(
                markCentre + new Vector2(-arm * 0.2f, arm * 0.7f),
                markCentre + new Vector2(arm, -arm * 0.7f),
                colour,
                thickness);
        }
        else
        {
            draw.AddLine(markCentre - new Vector2(arm, arm), markCentre + new Vector2(arm, arm), colour, thickness);
            draw.AddLine(markCentre + new Vector2(arm, -arm), markCentre + new Vector2(-arm, arm), colour, thickness);
        }

        return changed;
    }

    /// <summary>Text drawn on a badge. Light on purpose: every badge colour is a mid tone.</summary>
    private static readonly Vector4 BadgeText = new(1f, 1f, 1f, 0.95f);

    /// <summary>
    /// A small filled label, used to say what kind of thing a line is.
    /// </summary>
    /// <remarks>
    /// A coloured word alone would be readable but not scannable, and colour alone would not
    /// survive being read by someone who does not separate green from red. A filled shape with
    /// the word inside says it twice.
    /// </remarks>
    /// <param name="text">The word inside the badge.</param>
    /// <param name="colour">Fill colour, which is what makes a kind recognisable at a glance.</param>
    /// <param name="minWidth">
    /// Widen every badge to the same width so a column of them lines up. The text stays centred.
    /// </param>
    internal static void Badge(string text, Vector4 colour, float minWidth = 0f)
    {
        var padding = new Vector2(8f, 2f);
        var textSize = ImGui.CalcTextSize(text);
        var width = MathF.Max(textSize.X + (padding.X * 2f), minWidth);
        var size = new Vector2(width, textSize.Y + (padding.Y * 2f));
        var origin = ImGui.GetCursorScreenPos();

        ImGui.Dummy(size);

        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(origin, origin + size, ImGui.GetColorU32(colour), size.Y * 0.35f);
        draw.AddText(
            origin + new Vector2((width - textSize.X) * 0.5f, padding.Y),
            ImGui.GetColorU32(BadgeText),
            text);
    }

    /// <summary>How wide a badge would be for this text, so a set of them can share one width.</summary>
    internal static float BadgeWidth(string text) => ImGui.CalcTextSize(text).X + 16f;

    /// <summary>A switch with a caption beside it, aligned to the switch's centre line.</summary>
    internal static bool ToggleWithLabel(string id, string label, ref bool value)
    {
        var changed = Toggle(id, ref value);

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);

        return changed;
    }
}
