using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using FateCompass.Configuration;
using FateCompass.Core.Announce;
using FateCompass.Core.Localization;
using FateCompass.Core.Model;

namespace FateCompass.UI;

/// <summary>
/// Every setting, grouped into tabs and sections.
/// </summary>
/// <remarks>
/// The grouping is the whole point of this window. An earlier version put display options,
/// automation, and notifications into one tab as a flat run of checkboxes, and it read as a
/// wall: nothing said which switch belonged to which, and a setting's explanation ran off the
/// right edge because the window was narrower than its own text.
/// <para>
/// Three things fix that and are worth keeping. Each tab covers one subject. Within a tab,
/// headed sections say what a group of switches is for. A setting that only matters while
/// another is on sits indented beneath it and is greyed out when it does nothing, rather than
/// vanishing, so the window does not change shape as it is used.
/// </para>
/// <para>
/// Labels sit above their slider rather than beside it. German labels are long, and a label
/// beside the control leaves the control a sliver of the width.
/// </para>
/// </remarks>
internal sealed class ConfigWindow : Window, IDisposable
{
    private static readonly FateKind[] FilterableKinds =
    [
        FateKind.Boss, FateKind.Slay, FateKind.Collect, FateKind.Escort, FateKind.Defend,
        FateKind.Skirmish, FateKind.CriticalEngagement, FateKind.CriticalEncounter,
    ];

    /// <summary>
    /// Text scale for this window. Slightly larger than the game's, because this is a window
    /// that gets read rather than glanced at.
    /// </summary>
    private const float FontScale = 1.1f;

    /// <summary>Colour of a section heading, matching the game's own gold.</summary>
    private static readonly Vector4 HeadingColour = new(1f, 0.82f, 0.4f, 1f);

    private readonly PluginConfiguration configuration;
    private readonly Localizer localizer;
    private readonly Action<string> onLanguageChanged;
    private readonly Action<bool> onRepositionToggled;
    private bool isRepositioning;

    internal ConfigWindow(
        PluginConfiguration configuration,
        Localizer localizer,
        Action<string> onLanguageChanged,
        Action openMainWindow,
        Action<bool> onRepositionToggled)
        // Three hashes: the caption is translated, so it changes when the language does, and the
        // window must keep its place across that.
        : base("FateCompass###FateCompassConfig")
    {
        this.configuration = configuration;
        this.localizer = localizer;
        this.onLanguageChanged = onLanguageChanged;
        this.onRepositionToggled = onRepositionToggled;

        // Wide enough that a line of help text fits on one line. The previous minimum cut its
        // own explanations off mid-word, which is worse than having no explanation.
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(620, 480),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        Size = new Vector2(660, 640);
        SizeCondition = ImGuiCond.FirstUseEver;

        // Same treatment as the gear in the other window: the way back to the list belongs in
        // the title bar, not as a button eating a row of the content area.
        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.ListUl,
            IconOffset = new Vector2(2f, 1f),
            Click = _ => openMainWindow(),
            ShowTooltip = () => ImGui.SetTooltip(localizer.Get(StringKeys.ButtonOpenMain)),
        });
    }

    public void Dispose()
    {
        // Nothing unmanaged is held.
    }

    public override void PreDraw() => WindowName = $"{localizer.Get(StringKeys.WindowConfigTitle)}###FateCompassConfig";

    public override void OnClose()
    {
        // Leaving the settings open in reposition mode would strand the icon in drag state.
        if (isRepositioning)
        {
            isRepositioning = false;
            onRepositionToggled(false);
        }

        configuration.Save();
    }

    public override void Draw()
    {
        ImGui.SetWindowFontScale(FontScale);

        try
        {
            if (!ImGui.BeginTabBar("##fateCompassSettings"))
            {
                return;
            }

            try
            {
                DrawTab(StringKeys.TabBasics, DrawBasics);
                DrawTab(StringKeys.TabAutomation, DrawAutomation);
                DrawTab(StringKeys.TabMap, DrawMapAndChat);
                DrawTab(StringKeys.TabFilter, DrawFilter);
                DrawTab(StringKeys.TabAdvanced, DrawAdvanced);
            }
            finally
            {
                ImGui.EndTabBar();
            }
        }
        finally
        {
            // The scale is window state, so it has to come back off or it would apply to
            // anything else drawn into this window later.
            ImGui.SetWindowFontScale(1f);
        }
    }

    private void DrawTab(string labelKey, Action body)
    {
        if (!ImGui.BeginTabItem(localizer.Get(labelKey)))
        {
            return;
        }

        try
        {
            // A child region per tab, so a long tab scrolls on its own and the tab bar stays put.
            if (ImGui.BeginChild($"##body{labelKey}", new Vector2(0, 0), false))
            {
                ImGui.Spacing();
                body();
                ImGui.Dummy(new Vector2(0f, 8f));
            }

            ImGui.EndChild();
        }
        finally
        {
            ImGui.EndTabItem();
        }
    }

    // --- Tabs ------------------------------------------------------------------------------

    private void DrawBasics()
    {
        var settings = configuration.Settings;
        var changed = false;

        changed |= Checkbox(StringKeys.SettingEnabled, StringKeys.SettingEnabledHelp,
            settings.Enabled, value => settings.Enabled = value);

        Section(StringKeys.SettingsDisplay);

        changed |= Checkbox(StringKeys.SettingShowMinimapButton, StringKeys.SettingShowMinimapButtonHelp,
            settings.ShowMinimapButton, value => settings.ShowMinimapButton = value);

        changed |= Dependent(settings.ShowMinimapButton, () =>
        {
            var inner = false;

            // Dragging beats two sliders: the target is a spot on screen, and pointing at it is
            // more direct than describing it with numbers.
            var label = isRepositioning
                ? localizer.Get(StringKeys.ButtonPlacementDone)
                : localizer.Get(StringKeys.ButtonPlacementMove);

            if (ImGui.Button(label))
            {
                isRepositioning = !isRepositioning;
                onRepositionToggled(isRepositioning);

                if (!isRepositioning)
                {
                    inner = true;
                }
            }

            if (isRepositioning)
            {
                Help(StringKeys.PlacementHint);
            }

            inner |= FloatSlider(StringKeys.SettingMinimapSize, settings.MinimapButtonSize, 16f, 64f,
                value => settings.MinimapButtonSize = value);

            return inner;
        });

        changed |= Checkbox(StringKeys.SettingShowStatusBar, StringKeys.SettingShowStatusBarHelp,
            settings.ShowStatusBarEntry, value => settings.ShowStatusBarEntry = value);

        changed |= Checkbox(StringKeys.SettingShowNewsOnUpdate, StringKeys.SettingShowNewsOnUpdateHelp,
            settings.ShowReleaseNotesOnUpdate, value => settings.ShowReleaseNotesOnUpdate = value);

        Section(StringKeys.SettingsCounters);

        changed |= Checkbox(StringKeys.SettingTrackGemstones, null,
            settings.TrackGemstones, value => settings.TrackGemstones = value);
        changed |= Checkbox(StringKeys.SettingTrackSharedFate, StringKeys.SettingTrackSharedFateHelp,
            settings.TrackSharedFateRank, value => settings.TrackSharedFateRank = value);

        Section(StringKeys.SettingsLanguage);
        DrawLanguage();

        Save(changed);
    }

    private void DrawAutomation()
    {
        var settings = configuration.Settings;
        var changed = false;

        changed |= Checkbox(StringKeys.SettingAutoEngage, StringKeys.SettingAutoEngageHelp,
            settings.AutoEngageOnFateEnter, value => settings.AutoEngageOnFateEnter = value);

        Section(StringKeys.SettingsSteps, StringKeys.SettingsStepsHint);

        // The three steps are greyed out rather than hidden while the automation is off. They
        // still apply to the manual button, and a section that disappears makes the window
        // change shape as it is used, which is its own kind of confusing.
        changed |= Checkbox(StringKeys.SettingStepDismount, null,
            settings.EngageDismount, value => settings.EngageDismount = value);
        changed |= Checkbox(StringKeys.SettingStepLevelSync, null,
            settings.EngageLevelSync, value => settings.EngageLevelSync = value);
        changed |= Checkbox(StringKeys.SettingStepTankStance, null,
            settings.EngageTankStance, value => settings.EngageTankStance = value);

        Section(StringKeys.SettingsAfterFate);

        changed |= Checkbox(StringKeys.SettingAutoRemount, StringKeys.SettingAutoRemountHelp,
            settings.AutoRemountAfterFate, value => settings.AutoRemountAfterFate = value);

        changed |= Dependent(settings.AutoRemountAfterFate, () =>
            IntSlider(StringKeys.SettingRemountDelay, settings.RemountDelaySeconds, 0, 15,
                value => settings.RemountDelaySeconds = value));

        Section(StringKeys.SettingsTravel);

        changed |= Checkbox(StringKeys.SettingConfirmReturnPrompt, StringKeys.SettingConfirmReturnPromptHelp,
            settings.ConfirmReturnPrompt, value => settings.ConfirmReturnPrompt = value);

        Save(changed);
    }

    private void DrawMapAndChat()
    {
        var settings = configuration.Settings;
        var changed = false;

        Section(StringKeys.SettingsMapMarkers, StringKeys.SettingsMapMarkersHint);

        changed |= Checkbox(StringKeys.SettingShowRankMarkers, StringKeys.SettingShowRankMarkersHelp,
            settings.ShowRankMarkersOnMap, value => settings.ShowRankMarkersOnMap = value);

        changed |= Checkbox(StringKeys.SettingShowCompass, StringKeys.SettingShowCompassHelp,
            settings.ShowCompassNeedle, value => settings.ShowCompassNeedle = value);

        Section(StringKeys.SettingsChat);

        changed |= Checkbox(StringKeys.SettingTypeFlagIntoChat, StringKeys.SettingTypeFlagIntoChatHelp,
            settings.TypeFlagIntoChat, value => settings.TypeFlagIntoChat = value);

        changed |= Checkbox(StringKeys.SettingAllowAnnounce, StringKeys.SettingAllowAnnounceHelp,
            settings.AllowChatAnnounce, value => settings.AllowChatAnnounce = value);

        changed |= Dependent(settings.AllowChatAnnounce, () =>
        {
            var inner = Checkbox(StringKeys.SettingAnnounceIncludeName, null,
                settings.AnnounceIncludeName, value => settings.AnnounceIncludeName = value);

            ImGui.TextUnformatted(localizer.Get(StringKeys.SettingAnnounceChannel));

            var channels = ChatChannels.All;
            var index = ChatChannels.IndexOf(settings.AnnounceChannel);
            var labels = ChatChannels.LabelsFor(localizer);

            ImGui.SetNextItemWidth(-1);
            if (ImGui.Combo("##announceChannelSetting", ref index, labels, labels.Length))
            {
                settings.AnnounceChannel = channels[index].Command;
                inner = true;
            }

            ImGui.Spacing();

            // Shows the exact line that will appear, because the two switches above describe it
            // and a sample settles it.
            ImGui.TextDisabled(localizer.Format(
                StringKeys.SettingAnnouncePreview,
                ChatAnnouncement.Build(
                    settings.AnnounceChannel,
                    localizer.Get(StringKeys.SettingAnnounceSampleName),
                    settings.AnnounceIncludeName) ?? string.Empty));

            return inner;
        });

        Section(StringKeys.SettingsNotifications);

        changed |= Checkbox(StringKeys.SettingShowNotification, StringKeys.SettingShowNotificationHelp,
            settings.ShowNewFateNotification, value => settings.ShowNewFateNotification = value);
        changed |= Checkbox(StringKeys.SettingSoundOnNewFate, null,
            settings.PlaySoundOnNewFate, value => settings.PlaySoundOnNewFate = value);
        changed |= Checkbox(StringKeys.SettingNotifyOnlyIncluded, null,
            settings.NotifyOnlyIncludedKinds, value => settings.NotifyOnlyIncludedKinds = value);
        changed |= FloatSlider(StringKeys.SettingNotifyDistance, settings.MaximumNotificationDistance,
            0f, 2000f, value => settings.MaximumNotificationDistance = value, "%.0f");

        Save(changed);
    }

    private void DrawFilter()
    {
        var settings = configuration.Settings;
        var changed = false;

        Section(StringKeys.SettingLevelRange);

        // Zero stands for "no limit", which is friendlier than a nullable in a slider.
        var min = (int)(settings.MinimumLevel ?? 0);
        if (IntSliderRaw(StringKeys.SettingLevelMinimum, ref min, 0, 100))
        {
            settings.MinimumLevel = min <= 0 ? null : (ushort)min;
            changed = true;
        }

        var max = (int)(settings.MaximumLevel ?? 0);
        if (IntSliderRaw(StringKeys.SettingLevelMaximum, ref max, 0, 100))
        {
            settings.MaximumLevel = max <= 0 ? null : (ushort)max;
            changed = true;
        }

        Section(StringKeys.SettingsWhichFates, StringKeys.SettingExcludedKinds);

        // Two columns. Eight checkboxes stacked in one column pushed the rest of the tab off
        // the bottom for no reason: each label is short.
        if (ImGui.BeginTable("##kinds", 2, ImGuiTableFlags.SizingStretchSame))
        {
            foreach (var kind in FilterableKinds)
            {
                ImGui.TableNextColumn();

                var excluded = settings.ExcludedKinds.Contains(kind);
                if (ImGui.Checkbox($"{KindLabel(kind)}##kind{kind}", ref excluded))
                {
                    if (excluded)
                    {
                        settings.ExcludedKinds.Add(kind);
                    }
                    else
                    {
                        settings.ExcludedKinds.Remove(kind);
                    }

                    changed = true;
                }
            }

            ImGui.EndTable();
        }

        Save(changed);
    }

    private void DrawAdvanced()
    {
        var settings = configuration.Settings;
        var changed = false;


        Section(StringKeys.SettingsRanking);

        changed |= FloatSlider(StringKeys.SettingTravelSpeed, settings.TravelSpeedYalmsPerSecond,
            1f, 60f, value => settings.TravelSpeedYalmsPerSecond = value);
        changed |= FloatSlider(
            StringKeys.SettingTravelSpeedExploratory,
            settings.ExploratoryTravelSpeedYalmsPerSecond, 1f, 60f,
            value => settings.ExploratoryTravelSpeedYalmsPerSecond = value);
        changed |= FloatSlider(
            StringKeys.SettingTravelSpeedExploratorySlow,
            settings.ExploratorySlowTravelSpeedYalmsPerSecond, 1f, 60f,
            value => settings.ExploratorySlowTravelSpeedYalmsPerSecond = value);
        changed |= FloatSlider(StringKeys.SettingTeleportOverhead, settings.TeleportOverheadSeconds,
            0f, 60f, value => settings.TeleportOverheadSeconds = value);
        changed |= FloatSlider(
            StringKeys.SettingReturnOverhead, settings.ExploratoryTravelOverheadSeconds,
            0f, 60f, value => settings.ExploratoryTravelOverheadSeconds = value);
        changed |= IntSlider(StringKeys.SettingNearlyDone, settings.NearlyDoneThresholdPercent,
            50, 100, value => settings.NearlyDoneThresholdPercent = value);
        changed |= IntSlider(StringKeys.SettingMinimumRemaining, settings.MinimumSecondsRemaining,
            0, 300, value => settings.MinimumSecondsRemaining = value);

        Section(StringKeys.SettingsWeights, StringKeys.SettingsWeightsHint);

        changed |= FloatSlider(StringKeys.SettingWeightDistance, settings.Weights.Distance, 0f, 3f,
            value => settings.Weights.Distance = value);
        changed |= FloatSlider(StringKeys.SettingWeightTime, settings.Weights.TimeRemaining, 0f, 3f,
            value => settings.Weights.TimeRemaining = value);
        changed |= FloatSlider(StringKeys.SettingWeightProgress, settings.Weights.Progress, 0f, 3f,
            value => settings.Weights.Progress = value);
        changed |= FloatSlider(StringKeys.SettingWeightOccupancy, settings.Weights.Occupancy, 0f, 3f,
            value => settings.Weights.Occupancy = value);
        changed |= FloatSlider(StringKeys.SettingWeightRegistration, settings.Weights.RegistrationOpenBonus,
            0f, 5f, value => settings.Weights.RegistrationOpenBonus = value);

        Section(StringKeys.SettingsGeneral);

        changed |= IntSlider(StringKeys.SettingGemstoneHeadroom, settings.GemstoneWarningHeadroom,
            0, 500, value => settings.GemstoneWarningHeadroom = value);
        changed |= Checkbox(StringKeys.SettingTrackHistory, null,
            settings.TrackHistory, value => settings.TrackHistory = value);

        Save(changed);
    }

    private void DrawLanguage()
    {
        var available = new List<string> { LanguageInfo.AutomaticCode };
        available.AddRange(localizer.AvailableCodes.Order(StringComparer.Ordinal));

        var current = configuration.Settings.Language;
        var currentIndex = Math.Max(available.FindIndex(code =>
            string.Equals(code, current, StringComparison.OrdinalIgnoreCase)), 0);

        var labels = available
            .Select(code => code == LanguageInfo.AutomaticCode
                ? localizer.Get(StringKeys.SettingLanguageAuto)
                : code.ToUpperInvariant())
            .ToArray();

        ImGui.TextUnformatted(localizer.Get(StringKeys.SettingLanguageChoice));

        var index = currentIndex;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.Combo("##language", ref index, labels, labels.Length) && index != currentIndex)
        {
            configuration.Settings.Language = available[index];
            configuration.Save();
            onLanguageChanged(configuration.Settings.Language);
        }
    }

    // --- Building blocks -------------------------------------------------------------------

    /// <summary>
    /// A headed section: space above, a coloured heading with its explanation behind a marker,
    /// and a rule.
    /// </summary>
    private void Section(string titleKey, string? hintKey = null)
    {
        ImGui.Dummy(new Vector2(0f, SectionSpacing));
        ImGui.TextColored(HeadingColour, localizer.Get(titleKey));

        if (hintKey is not null)
        {
            HelpMarker(hintKey);
        }

        ImGui.Separator();
        ImGui.Spacing();
    }

    /// <summary>Air above a section heading, so the groups read as groups.</summary>
    private const float SectionSpacing = 10f;

    /// <summary>
    /// Runs a block indented and greyed out while its parent setting is off.
    /// </summary>
    /// <remarks>
    /// Greyed rather than hidden. A control that disappears takes its own explanation with it,
    /// and the window jumping in height as switches are flipped is disorienting. Greyed says
    /// the same thing and stays put.
    /// </remarks>
    private static bool Dependent(bool enabled, Func<bool> body)
    {
        ImGui.Indent();

        if (!enabled)
        {
            ImGui.BeginDisabled();
        }

        try
        {
            return body();
        }
        finally
        {
            if (!enabled)
            {
                ImGui.EndDisabled();
            }

            ImGui.Unindent();
        }
    }

    /// <summary>
    /// A question mark after a control, holding its explanation until it is asked for.
    /// </summary>
    /// <remarks>
    /// Every explanation used to sit permanently under its setting. Each one was justified on
    /// its own and together they doubled the height of the window and turned it into a wall of
    /// grey text, where the settings themselves, the part anyone came for, were the minority
    /// of what was on screen.
    /// <para>
    /// The text has not gone anywhere. It is one hover away, which is where an explanation
    /// belongs: you read it once, when you first wonder, and never again.
    /// </para>
    /// </remarks>
    private void HelpMarker(string key)
    {
        ImGui.SameLine(0f, 6f);
        ImGui.TextDisabled("(?)");

        if (!ImGui.IsItemHovered())
        {
            return;
        }

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 26f);

        try
        {
            ImGui.TextUnformatted(localizer.Get(key));
        }
        finally
        {
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }

    /// <summary>
    /// One dimmed, wrapped line, for text that describes a passing state rather than a setting.
    /// </summary>
    private void Help(string key)
    {
        ImGui.Indent();
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
        ImGui.PushTextWrapPos(0f);

        try
        {
            ImGui.TextUnformatted(localizer.Get(key));
        }
        finally
        {
            ImGui.PopTextWrapPos();
            ImGui.PopStyleColor();
            ImGui.Unindent();
        }
    }

    private void Save(bool changed)
    {
        if (changed)
        {
            configuration.Save();
        }
    }

    private string KindLabel(FateKind kind) => localizer.Get(kind switch
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

    private bool Checkbox(string labelKey, string? helpKey, bool value, Action<bool> apply)
    {
        var local = value;
        var changed = ImGui.Checkbox(localizer.Get(labelKey), ref local);
        if (changed)
        {
            apply(local);
        }

        if (helpKey is not null)
        {
            HelpMarker(helpKey);
        }

        return changed;
    }

    /// <summary>
    /// A slider with its label on the line above and the control across the full width.
    /// </summary>
    private bool IntSlider(string labelKey, int value, int min, int max, Action<int> apply)
    {
        var local = value;
        if (!IntSliderRaw(labelKey, ref local, min, max))
        {
            return false;
        }

        apply(local);
        return true;
    }

    private bool IntSliderRaw(string labelKey, ref int value, int min, int max)
    {
        ImGui.TextUnformatted(localizer.Get(labelKey));
        ImGui.SetNextItemWidth(-1);

        var changed = ImGui.SliderInt($"##{labelKey}", ref value, min, max);
        ImGui.Spacing();

        return changed;
    }

    private bool FloatSlider(
        string labelKey, float value, float min, float max, Action<float> apply, string format = "%.1f")
    {
        var local = value;

        ImGui.TextUnformatted(localizer.Get(labelKey));
        ImGui.SetNextItemWidth(-1);

        var changed = ImGui.SliderFloat($"##{labelKey}", ref local, min, max, format);
        ImGui.Spacing();

        if (changed)
        {
            apply(local);
        }

        return changed;
    }
}
