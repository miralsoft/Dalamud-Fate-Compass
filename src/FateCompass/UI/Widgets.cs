using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace FateCompass.UI;

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

    /// <summary>Fully on course. The same green the route advice uses for a worthwhile teleport.</summary>
    private static readonly Vector4 OnCourseColour = new(0.40f, 0.95f, 0.50f, 1f);

    /// <summary>
    /// Pointing anywhere else. Grey rather than red, because a wrong heading is not a warning.
    /// </summary>
    /// <remarks>
    /// Bright, though. The first version used a mid grey at four fifths opacity, which on this
    /// window's near-black background left a shape you had to look for rather than one you saw.
    /// Off course still has to be legible; it is the state the needle spends most of its time in.
    /// </remarks>
    private static readonly Vector4 OffCourseColour = new(0.78f, 0.78f, 0.80f, 1f);

    /// <summary>
    /// Drawn behind the needle so it separates from whatever is under it.
    /// </summary>
    /// <remarks>
    /// A dark outline around a light shape is what makes a small glyph readable on a background
    /// whose brightness cannot be relied on. Without it the needle disappeared into the window
    /// at a glance, which for something meant to be glanced at is the whole failure.
    /// </remarks>
    private static readonly Vector4 NeedleOutline = new(0.05f, 0.05f, 0.07f, 0.9f);

    /// <summary>
    /// A needle pointing at something, with up meaning straight ahead.
    /// </summary>
    /// <remarks>
    /// Screen space grows downward, so ahead is minus Y. The needle is a triangle with a notch
    /// cut out of its base rather than a plain one, because at this size a plain triangle reads
    /// as a blob and its point is the only part carrying the information.
    /// </remarks>
    /// <param name="radius">Half the space the needle occupies, in pixels.</param>
    /// <param name="relativeRadians">Turn needed, clockwise, with zero straight ahead.</param>
    /// <param name="onCourse">Zero for wide of the mark, one for dead on. Blends the colour.</param>
    internal static void CompassNeedle(float radius, float relativeRadians, float onCourse)
    {
        var size = new Vector2(radius * 2f, radius * 2f);
        var origin = ImGui.GetCursorScreenPos();
        ImGui.Dummy(size);

        var centre = origin + new Vector2(radius, radius);
        var colour = Vector4.Lerp(OffCourseColour, OnCourseColour, Math.Clamp(onCourse, 0f, 1f));
        var draw = ImGui.GetWindowDrawList();

        Vector2 At(float angle, float distance) => centre + new Vector2(
            MathF.Sin(angle) * distance,
            -MathF.Cos(angle) * distance);

        // An arrowhead: a point, two wings swept back, and a notch cut into the base between
        // them. The notch belongs *behind* the centre, opposite the point. Putting it in front,
        // between the centre and the tip, folds the shape in on itself and leaves two slivers
        // that read as a tick mark rather than an arrow, which is precisely how the first
        // attempt looked on screen.
        var tip = At(relativeRadians, radius);
        var wingLeft = At(relativeRadians + WingAngle, radius * 0.95f);
        var wingRight = At(relativeRadians - WingAngle, radius * 0.95f);
        var notch = At(relativeRadians + MathF.PI, radius * 0.25f);

        // Traced tip, wing, notch, wing, so the quad follows the outline rather than crossing
        // itself. The fill and the outline take the same four points, which is what keeps the
        // edge on the shape instead of near it.
        draw.AddQuadFilled(tip, wingLeft, notch, wingRight, ImGui.GetColorU32(colour));
        draw.AddQuad(
            tip, wingLeft, notch, wingRight,
            ImGui.GetColorU32(NeedleOutline),
            MathF.Max(radius * 0.12f, 1f));
    }

    /// <summary>
    /// How far back the wings sweep from the point, in radians.
    /// </summary>
    /// <remarks>
    /// About 140 degrees. Narrower gives a dart that is elegant at four times this size and
    /// unreadable here; wider gives a lozenge with no obvious front. What carries a direction at
    /// this scale is the silhouette, and a silhouette needs both width and an unmistakable point.
    /// </remarks>
    private const float WingAngle = 2.45f;

    /// <summary>
    /// Drawn instead of the needle once the target is underfoot.
    /// </summary>
    /// <remarks>
    /// A ring with a dot in it, the shape a compass uses for "this is the place". Hiding the
    /// needle would have done the job too, but an empty space says nothing, while this says
    /// arrived, and it keeps the row from changing height as it happens.
    /// </remarks>
    internal static void CompassArrived(float radius)
    {
        var size = new Vector2(radius * 2f, radius * 2f);
        var origin = ImGui.GetCursorScreenPos();
        ImGui.Dummy(size);

        var centre = origin + new Vector2(radius, radius);
        var colour = ImGui.GetColorU32(OnCourseColour);
        var draw = ImGui.GetWindowDrawList();

        draw.AddCircle(centre, radius * 0.85f, colour, 0, MathF.Max(radius * 0.16f, 1.5f));
        draw.AddCircleFilled(centre, radius * 0.3f, colour);
    }

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
