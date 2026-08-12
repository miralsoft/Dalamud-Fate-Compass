using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using FateCompass.Configuration;
using FateCompass.Core.Localization;
using FateCompass.Core.News;

namespace FateCompass.UI;

/// <summary>
/// What changed, version by version.
/// </summary>
/// <remarks>
/// A plugin that keeps growing is a plugin whose players keep finding things that were not
/// there yesterday and assuming they broke something. This window is the answer to "was that
/// always like that", which is otherwise unanswerable from inside the game.
/// <para>
/// Each line carries a badge saying whether it is new, changed, repaired, or gone, because the
/// three questions people actually arrive with (what can I do now, what moved, and was my bug
/// fixed) are answered by sorting, not by reading.
/// </para>
/// </remarks>
internal sealed class ReleaseNotesWindow : Window, IDisposable
{
    /// <summary>Same enlargement as the settings: this is a window that gets read, not glanced at.</summary>
    private const float FontScale = 1.1f;

    /// <summary>Colour of a version heading, matching the gold used for section headings elsewhere.</summary>
    private static readonly Vector4 HeadingColour = new(1f, 0.82f, 0.4f, 1f);

    private static readonly Vector4 AddedColour = new(0.24f, 0.62f, 0.36f, 1f);
    private static readonly Vector4 ChangedColour = new(0.26f, 0.48f, 0.76f, 1f);
    private static readonly Vector4 FixedColour = new(0.74f, 0.52f, 0.18f, 1f);
    private static readonly Vector4 RemovedColour = new(0.58f, 0.33f, 0.33f, 1f);

    private readonly PluginConfiguration configuration;
    private readonly Localizer localizer;

    private ReleaseNotes notes = ReleaseNotes.Empty;
    private string loadedFor = string.Empty;

    internal ReleaseNotesWindow(PluginConfiguration configuration, Localizer localizer)
        // Three hashes: the caption is translated and changes with the language, and the window
        // has to keep its place across that.
        : base("FateCompass###FateCompassNews")
    {
        this.configuration = configuration;
        this.localizer = localizer;

        SizeConstraints = new WindowSizeConstraints
        {
            // Wide enough that a note is a line or two rather than a column of single words.
            MinimumSize = new Vector2(460, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        Size = new Vector2(620, 560);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    /// <summary>
    /// True while the newest notes are newer than what has been shown. Drives both the offer to
    /// open the window after an update and the colour of the button in the other window's title
    /// bar.
    /// </summary>
    internal bool HasUnseen
    {
        get
        {
            EnsureLoaded();
            return notes.HasUnseen(configuration.Settings.LastSeenReleaseNotes);
        }
    }

    /// <summary>The newest version that has notes, for the button's tooltip.</summary>
    internal string LatestVersion
    {
        get
        {
            EnsureLoaded();
            return notes.LatestVersion ?? string.Empty;
        }
    }

    public void Dispose()
    {
        // Nothing unmanaged is held.
    }

    /// <summary>
    /// The caption names the plugin as well as the window.
    /// </summary>
    /// <remarks>
    /// This window can open on its own after an update, so it has to say whose notes these are.
    /// A bare "What's new" appearing over the game says nothing about where it came from.
    /// </remarks>
    public override void PreDraw() =>
        WindowName = $"{localizer.Get(StringKeys.WindowMainTitle)} - "
            + $"{localizer.Get(StringKeys.WindowNewsTitle)}###FateCompassNews";

    /// <summary>
    /// Opening counts as having been shown.
    /// </summary>
    /// <remarks>
    /// On opening rather than on closing, deliberately. Marking on close would leave the button
    /// lit for as long as the window stays open, which reads as though it had not registered,
    /// and would keep the notes unseen forever if the window is simply left open.
    /// </remarks>
    public override void OnOpen() => MarkSeen();

    /// <summary>
    /// Records the newest notes as shown without opening anything.
    /// </summary>
    /// <remarks>
    /// Used on a first installation, where the notes are deliberately not shown. Without this
    /// the very next start would count as an update and open them, which is precisely the moment
    /// they are least wanted.
    /// </remarks>
    internal void MarkSeen()
    {
        EnsureLoaded();

        if (notes.LatestVersion is null
            || string.Equals(
                configuration.Settings.LastSeenReleaseNotes,
                notes.LatestVersion,
                StringComparison.Ordinal))
        {
            return;
        }

        configuration.Settings.LastSeenReleaseNotes = notes.LatestVersion;
        configuration.Save();
    }

    public override void Draw()
    {
        EnsureLoaded();

        ImGui.SetWindowFontScale(FontScale);

        try
        {
            if (notes.Versions.Count == 0)
            {
                ImGui.TextDisabled(localizer.Get(StringKeys.NewsEmpty));
                return;
            }

            for (var index = 0; index < notes.Versions.Count; index++)
            {
                DrawVersion(notes.Versions[index], expanded: index == 0);
            }
        }
        finally
        {
            // Window state, so it has to come back off.
            ImGui.SetWindowFontScale(1f);
        }
    }

    private void DrawVersion(ReleaseVersion version, bool expanded)
    {
        var flags = expanded ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
        var heading = localizer.Format(StringKeys.NewsVersion, version.Version);

        if (FormatDate(version.Date) is { } date)
        {
            heading = $"{heading}   ·   {date}";
        }

        if (IsRunningVersion(version.Version))
        {
            heading = $"{heading}   ·   {localizer.Get(StringKeys.NewsInstalled)}";
        }

        // The identity is the version alone, so a translated heading or a newly dated release
        // does not reset which sections the reader had open.
        ImGui.PushStyleColor(ImGuiCol.Text, HeadingColour);
        var open = ImGui.CollapsingHeader($"{heading}###news{version.Version}", flags);
        ImGui.PopStyleColor();

        if (!open)
        {
            return;
        }

        ImGui.Indent();

        try
        {
            if (!string.IsNullOrWhiteSpace(version.Summary))
            {
                ImGui.Spacing();
                Wrapped(version.Summary, dimmed: true);
                ImGui.Spacing();
            }

            DrawNotes(version);
        }
        finally
        {
            ImGui.Unindent();
            ImGui.Dummy(new Vector2(0f, 6f));
        }
    }

    private void DrawNotes(ReleaseVersion version)
    {
        if (version.Notes.Count == 0)
        {
            return;
        }

        // One shared width for every badge, so the text beside them starts on one line rather
        // than stepping in and out as the labels change length.
        var badgeWidth = MathF.Max(
            MathF.Max(Widgets.BadgeWidth(Label(ReleaseNoteKind.Added)), Widgets.BadgeWidth(Label(ReleaseNoteKind.Changed))),
            MathF.Max(Widgets.BadgeWidth(Label(ReleaseNoteKind.Fixed)), Widgets.BadgeWidth(Label(ReleaseNoteKind.Removed))));

        if (!ImGui.BeginTable($"##notes{version.Version}", 2, ImGuiTableFlags.SizingFixedFit))
        {
            return;
        }

        try
        {
            ImGui.TableSetupColumn("##kind", ImGuiTableColumnFlags.WidthFixed, badgeWidth);
            ImGui.TableSetupColumn("##text", ImGuiTableColumnFlags.WidthStretch);

            foreach (var note in version.Notes)
            {
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                Widgets.Badge(Label(note.Kind), Colour(note.Kind), badgeWidth);

                ImGui.TableNextColumn();
                Wrapped(note.Text, dimmed: false);

                ImGui.Dummy(new Vector2(0f, 3f));
            }
        }
        finally
        {
            ImGui.EndTable();
        }
    }

    /// <summary>
    /// Wrapped text without format handling, because these strings come from a data file and a
    /// stray percent sign in one of them must not be read as a placeholder.
    /// </summary>
    private static void Wrapped(string text, bool dimmed)
    {
        if (dimmed)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
        }

        ImGui.PushTextWrapPos(0f);

        try
        {
            ImGui.TextUnformatted(text);
        }
        finally
        {
            ImGui.PopTextWrapPos();

            if (dimmed)
            {
                ImGui.PopStyleColor();
            }
        }
    }

    private void EnsureLoaded()
    {
        var code = localizer.ActiveCode;

        if (string.Equals(loadedFor, code, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        notes = EmbeddedReleaseNotes.Load(code);
        loadedFor = code;
    }

    private string Label(ReleaseNoteKind kind) => localizer.Get(kind switch
    {
        ReleaseNoteKind.Added => StringKeys.NewsKindAdded,
        ReleaseNoteKind.Changed => StringKeys.NewsKindChanged,
        ReleaseNoteKind.Fixed => StringKeys.NewsKindFixed,
        _ => StringKeys.NewsKindRemoved,
    });

    private static Vector4 Colour(ReleaseNoteKind kind) => kind switch
    {
        ReleaseNoteKind.Added => AddedColour,
        ReleaseNoteKind.Changed => ChangedColour,
        ReleaseNoteKind.Fixed => FixedColour,
        _ => RemovedColour,
    };

    /// <summary>
    /// Marks whichever version is actually installed, which is the one thing a list of versions
    /// cannot say on its own.
    /// </summary>
    private static bool IsRunningVersion(string version) =>
        VersionNumber.Parse(version) == VersionNumber.Parse(
            typeof(ReleaseNotesWindow).Assembly.GetName().Version?.ToString());

    /// <summary>
    /// Turns the notes file's ISO day into whatever the player's system writes dates as. An
    /// unreadable or absent date simply does not appear.
    /// </summary>
    private static string? FormatDate(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso))
        {
            return null;
        }

        return DateTime.TryParse(
            iso,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed.ToString("d", CultureInfo.CurrentCulture)
            : iso;
    }
}
