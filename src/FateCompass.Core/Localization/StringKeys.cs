namespace FateCompass.Core.Localization;

/// <summary>
/// Every user-facing string key, in one place (C-05).
/// </summary>
/// <remarks>
/// Constants rather than loose strings, so a typo is a build error and so the test that
/// compares the catalogues against each other has something authoritative to compare with.
/// Keys are grouped by a dotted prefix that matches where they appear.
/// </remarks>
public static class StringKeys
{
    // Window titles and shell
    public const string WindowMainTitle = "window.main.title";
    public const string WindowConfigTitle = "window.config.title";

    /// <summary>Instance marker for the title bar, taking the instance number.</summary>
    public const string InstanceLabel = "window.instance";

    // Main window, list header
    public const string ListRank = "list.rank";
    public const string ListName = "list.name";
    public const string ListLevel = "list.level";
    public const string ListKind = "list.kind";
    public const string ListProgress = "list.progress";
    public const string ListRemaining = "list.remaining";
    public const string ListDistance = "list.distance";
    public const string ListAetheryte = "list.aetheryte";
    public const string ListActions = "list.actions";
    public const string ListSync = "list.sync";
    public const string SyncYes = "sync.yes";
    public const string SyncNo = "sync.no";

    // Main window, states
    public const string ListEmpty = "list.empty";
    public const string ListNotInWorld = "list.notInWorld";
    public const string ListDisabled = "list.disabled";

    // Buttons and tooltips
    public const string ButtonFlag = "button.flag";
    public const string ButtonFlagTooltip = "button.flag.tooltip";
    public const string ButtonTeleport = "button.teleport";

    /// <summary>Label for the travel button in a zone whose points cannot be teleported to.</summary>
    public const string ButtonReturn = "button.return";
    public const string ButtonTeleportTooltip = "button.teleport.tooltip";
    public const string ButtonEngage = "button.engage";
    public const string ButtonEngageTooltip = "button.engage.tooltip";
    public const string ButtonSettings = "button.settings";
    public const string ButtonOpenMain = "button.openMain";
    public const string ButtonViewTable = "button.view.table";
    public const string ButtonViewCompact = "button.view.compact";
    public const string ButtonAutomationOn = "button.automation.on";
    public const string ButtonAutomationOff = "button.automation.off";
    public const string ButtonAutomationTooltip = "button.automation.tooltip";
    public const string ButtonRunNow = "button.runNow";
    public const string ButtonAnnounceTooltip = "button.announce.tooltip";
    public const string ButtonAnnounceChannelTooltip = "button.announce.channel.tooltip";

    // Announcing the leading FATE
    public const string SettingAllowAnnounce = "setting.allowAnnounce";
    public const string SettingAllowAnnounceHelp = "setting.allowAnnounce.help";
    public const string SettingAnnounceIncludeName = "setting.announce.includeName";
    public const string SettingAnnounceChannel = "setting.announce.channel";
    public const string SettingAnnouncePreview = "setting.announce.preview";
    public const string SettingAnnounceSampleName = "setting.announce.sampleName";

    // Chat channels, named the way the game names them
    public const string ChannelEcho = "channel.echo";
    public const string ChannelSay = "channel.say";
    public const string ChannelParty = "channel.party";
    public const string ChannelAlliance = "channel.alliance";
    public const string ChannelFreeCompany = "channel.freeCompany";
    public const string ChannelShout = "channel.shout";
    public const string ChannelYell = "channel.yell";

    /// <summary>Linkshell, taking the linkshell number.</summary>
    public const string ChannelLinkshell = "channel.linkshell";

    /// <summary>Cross-world linkshell, taking its number.</summary>
    public const string ChannelCrossWorld = "channel.crossWorld";

    // Settings tabs
    public const string TabBasics = "tab.basics";
    public const string TabAutomation = "tab.automation";
    public const string TabMap = "tab.map";
    public const string TabFilter = "tab.filter";
    public const string TabAdvanced = "tab.advanced";

    // Settings, section headings within a tab
    public const string SettingsDisplay = "settings.display";
    public const string SettingsCounters = "settings.counters";
    public const string SettingsSteps = "settings.steps";
    public const string SettingsAfterFate = "settings.afterFate";
    public const string SettingsTravel = "settings.travel";
    public const string SettingsMapMarkers = "settings.mapMarkers";
    public const string SettingsChat = "settings.chat";
    public const string SettingsWeights = "settings.weights";
    public const string SettingsWhichFates = "settings.whichFates";

    // Section explanations, one line under a heading
    public const string SettingsStepsHint = "settings.steps.hint";
    public const string SettingsMapMarkersHint = "settings.mapMarkers.hint";
    public const string SettingsWeightsHint = "settings.weights.hint";

    // Server info bar
    public const string StatusBarTooltip = "statusBar.tooltip";
    public const string SettingShowStatusBar = "setting.showStatusBar";
    public const string SettingShowStatusBarHelp = "setting.showStatusBar.help";
    public const string SettingShowMinimapButton = "setting.showMinimapButton";
    public const string SettingShowMinimapButtonHelp = "setting.showMinimapButton.help";
    public const string SettingMinimapSize = "setting.minimap.size";
    public const string SettingMinimapOffsetX = "setting.minimap.offsetX";
    public const string SettingMinimapOffsetY = "setting.minimap.offsetY";
    public const string ButtonPlacementMove = "button.placement.move";
    public const string ButtonPlacementDone = "button.placement.done";
    public const string PlacementHint = "placement.hint";
    public const string SettingShowCompass = "setting.showCompass";
    public const string SettingShowCompassHelp = "setting.showCompass.help";
    public const string SettingShowRankMarkers = "setting.showRankMarkers";
    public const string SettingShowRankMarkersHelp = "setting.showRankMarkers.help";

    // FATE kinds
    public const string KindUnknown = "kind.unknown";
    public const string KindBoss = "kind.boss";
    public const string KindSlay = "kind.slay";
    public const string KindCollect = "kind.collect";
    public const string KindEscort = "kind.escort";
    public const string KindDefend = "kind.defend";
    public const string KindSkirmish = "kind.skirmish";
    public const string KindCriticalEngagement = "kind.criticalEngagement";
    public const string KindCriticalEncounter = "kind.criticalEncounter";
    public const string KindSpecialObjective = "kind.specialObjective";

    // Instanced engagement content
    public const string ListParticipants = "list.participants";
    public const string StateRegistrationOpen = "state.registrationOpen";
    public const string StateNotStarted = "state.notStarted";
    public const string NotificationRegistrationOpen = "notification.registrationOpen";
    public const string ExcludedNotPermittedHere = "excluded.notPermittedHere";
    public const string SettingWeightOccupancy = "setting.weight.occupancy";
    public const string SettingWeightRegistration = "setting.weight.registration";

    // Exclusion reasons
    public const string ExcludedNotJoinable = "excluded.notJoinable";
    public const string ExcludedFiltered = "excluded.filtered";
    public const string ExcludedNearlyComplete = "excluded.nearlyComplete";
    public const string ExcludedExpiringSoon = "excluded.expiringSoon";
    public const string ExcludedUnreachable = "excluded.unreachable";
    public const string ExcludedRegistrationClosed = "excluded.registrationClosed";
    public const string ExcludedRegistrationTooLate = "excluded.registrationTooLate";

    /// <summary>Countdown to a registration cut-off, taking one argument: the formatted time.</summary>
    public const string StateRegistrationCloses = "state.registrationCloses";

    // Route hints
    public const string RouteWalkFaster = "route.walkFaster";
    public const string RouteNoAetheryte = "route.noAetheryte";
    public const string RouteReturnFirst = "route.returnFirst";
    public const string RouteComparison = "route.comparison";
    public const string RouteTooLate = "route.tooLate";
    public const string RouteSaves = "route.saves";
    public const string RouteCosts = "route.costs";

    // Gemstones
    public const string GemstonesLabel = "gemstones.label";
    public const string GemstonesWarning = "gemstones.warning";
    public const string GemstonesFull = "gemstones.full";

    // Shared FATE standing
    public const string SharedFateProgress = "sharedFate.progress";
    public const string SharedFateComplete = "sharedFate.complete";
    public const string SharedFateNoData = "sharedFate.noData";
    public const string SharedFateAge = "sharedFate.age";
    public const string SharedFateHeading = "sharedFate.heading";
    public const string SharedFateDone = "sharedFate.done";
    public const string SettingTrackSharedFate = "setting.trackSharedFate";
    public const string SettingTrackSharedFateHelp = "setting.trackSharedFate.help";

    /// <summary>Riding map hint, taking one argument: how many are still missing.</summary>
    public const string HintRidingMapMissing = "hint.ridingMapMissing";

    // History
    public const string HistoryNextExpected = "history.nextExpected";
    public const string HistoryNotEnoughData = "history.notEnoughData";

    // Settings, groups
    public const string SettingsGeneral = "settings.general";
    public const string SettingsEngage = "settings.engage";
    public const string SettingsRanking = "settings.ranking";
    public const string SettingsFilter = "settings.filter";
    public const string SettingsNotifications = "settings.notifications";
    public const string SettingsLanguage = "settings.language";

    // Settings, individual entries
    public const string SettingEnabled = "setting.enabled";
    public const string SettingEnabledHelp = "setting.enabled.help";
    public const string SettingAutoEngage = "setting.autoEngage";
    public const string SettingAutoEngageHelp = "setting.autoEngage.help";
    public const string SettingAutoRemount = "setting.autoRemount";
    public const string SettingAutoRemountHelp = "setting.autoRemount.help";
    public const string SettingStepDismount = "setting.step.dismount";
    public const string SettingStepLevelSync = "setting.step.levelSync";
    public const string SettingStepTankStance = "setting.step.tankStance";
    public const string SettingRemountDelay = "setting.remountDelay";
    public const string SettingTravelSpeed = "setting.travelSpeed";
    public const string SettingTravelSpeedExploratory = "setting.travelSpeed.exploratory";
    public const string SettingTravelSpeedExploratorySlow = "setting.travelSpeed.exploratorySlow";
    public const string SettingTeleportOverhead = "setting.teleportOverhead";
    public const string SettingReturnOverhead = "setting.returnOverhead";
    public const string SettingNearlyDone = "setting.nearlyDone";
    public const string SettingMinimumRemaining = "setting.minimumRemaining";
    public const string SettingWeightDistance = "setting.weight.distance";
    public const string SettingWeightTime = "setting.weight.time";
    public const string SettingWeightProgress = "setting.weight.progress";
    public const string SettingExcludedKinds = "setting.excludedKinds";
    public const string SettingLevelRange = "setting.levelRange";
    public const string SettingSoundOnNewFate = "setting.soundOnNewFate";
    public const string SettingShowNotification = "setting.showNotification";
    public const string SettingShowNotificationHelp = "setting.showNotification.help";
    public const string SettingNotifyOnlyIncluded = "setting.notifyOnlyIncluded";
    public const string SettingNotifyDistance = "setting.notifyDistance";
    public const string SettingTrackGemstones = "setting.trackGemstones";
    public const string SettingGemstoneHeadroom = "setting.gemstoneHeadroom";
    public const string SettingTrackHistory = "setting.trackHistory";
    public const string SettingLanguageChoice = "setting.language.choice";
    public const string SettingLanguageAuto = "setting.language.auto";
    public const string SettingTypeFlagIntoChat = "setting.typeFlagIntoChat";
    public const string SettingTypeFlagIntoChatHelp = "setting.typeFlagIntoChat.help";
    public const string SettingLevelMinimum = "setting.level.minimum";
    public const string SettingLevelMaximum = "setting.level.maximum";

    // Command feedback
    public const string CommandAutoOn = "command.auto.on";
    public const string CommandAutoOff = "command.auto.off";
    public const string CommandEngageNothing = "command.engage.nothing";
    public const string CommandEngageBlockedCombat = "command.engage.blocked.combat";
    public const string CommandEngageBlockedOccupied = "command.engage.blocked.occupied";
    public const string CommandUnknown = "command.unknown";
    public const string CommandHelp = "command.help";

    // Notifications
    public const string NotificationNewFate = "notification.newFate";

    // Release notes
    public const string WindowNewsTitle = "window.news.title";
    public const string ButtonNewsTooltip = "button.news.tooltip";

    /// <summary>Tooltip while a version has notes the player has not opened yet, taking the version.</summary>
    public const string ButtonNewsTooltipUnseen = "button.news.tooltip.unseen";

    public const string NewsEmpty = "news.empty";
    public const string NewsVersion = "news.version";
    public const string NewsInstalled = "news.installed";
    public const string NewsKindAdded = "news.kind.added";
    public const string NewsKindChanged = "news.kind.changed";
    public const string NewsKindFixed = "news.kind.fixed";
    public const string NewsKindRemoved = "news.kind.removed";
    public const string SettingConfirmReturnPrompt = "setting.confirmReturnPrompt";
    public const string SettingConfirmReturnPromptHelp = "setting.confirmReturnPrompt.help";
    public const string SettingShowNewsOnUpdate = "setting.showNewsOnUpdate";
    public const string SettingShowNewsOnUpdateHelp = "setting.showNewsOnUpdate.help";

    // Units
    public const string UnitYalms = "unit.yalms";
    public const string UnitSeconds = "unit.seconds";
    public const string UnitMinutes = "unit.minutes";

    /// <summary>
    /// Every key above. The catalogue completeness test walks this, so a newly added key with
    /// no translation fails the build rather than showing up raw in the window.
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        WindowMainTitle, WindowConfigTitle, InstanceLabel,
        ListRank, ListName, ListLevel, ListKind, ListProgress, ListRemaining, ListDistance,
        ListAetheryte, ListActions, ListSync, SyncYes, SyncNo,
        ListEmpty, ListNotInWorld, ListDisabled,
        ButtonFlag, ButtonFlagTooltip, ButtonTeleport, ButtonTeleportTooltip,
        ButtonEngage, ButtonEngageTooltip, ButtonSettings, ButtonOpenMain, ButtonReturn,
        ButtonViewTable, ButtonViewCompact,
        ButtonAutomationOn, ButtonAutomationOff, ButtonAutomationTooltip, ButtonRunNow,
        ButtonAnnounceTooltip, ButtonAnnounceChannelTooltip,
        SettingAllowAnnounce, SettingAllowAnnounceHelp, SettingAnnounceIncludeName,
        SettingAnnounceChannel, SettingAnnouncePreview, SettingAnnounceSampleName,
        ChannelEcho, ChannelSay, ChannelParty, ChannelAlliance, ChannelFreeCompany,
        ChannelShout, ChannelYell, ChannelLinkshell, ChannelCrossWorld,
        TabBasics, TabAutomation, TabMap, TabFilter, TabAdvanced,
        SettingsDisplay, SettingsCounters, SettingsSteps, SettingsAfterFate,
        SettingsMapMarkers, SettingsChat, SettingsWeights, SettingsWhichFates, SettingsTravel,
        SettingsStepsHint, SettingsMapMarkersHint, SettingsWeightsHint,
        StatusBarTooltip, SettingShowStatusBar, SettingShowStatusBarHelp, StateNotStarted,
        SettingShowMinimapButton, SettingShowMinimapButtonHelp,
        SettingMinimapSize, SettingMinimapOffsetX, SettingMinimapOffsetY,
        ButtonPlacementMove, ButtonPlacementDone, PlacementHint,
        SettingShowRankMarkers, SettingShowRankMarkersHelp,
        SettingShowCompass, SettingShowCompassHelp,
        KindUnknown, KindBoss, KindSlay, KindCollect, KindEscort, KindDefend,
        KindSkirmish, KindCriticalEngagement, KindCriticalEncounter, KindSpecialObjective,
        ListParticipants, StateRegistrationOpen, NotificationRegistrationOpen,
        ExcludedNotPermittedHere, SettingWeightOccupancy, SettingWeightRegistration,
        ExcludedNotJoinable, ExcludedFiltered, ExcludedNearlyComplete, ExcludedExpiringSoon,
        ExcludedUnreachable, ExcludedRegistrationClosed, ExcludedRegistrationTooLate,
        StateRegistrationCloses,
        RouteWalkFaster, RouteNoAetheryte, RouteReturnFirst, RouteComparison,
        RouteTooLate, RouteSaves, RouteCosts,
        GemstonesLabel, GemstonesWarning, GemstonesFull,
        SharedFateProgress, SharedFateComplete, SharedFateNoData, SharedFateAge,
        SharedFateHeading, SharedFateDone,
        SettingTrackSharedFate, SettingTrackSharedFateHelp,
        HintRidingMapMissing, HistoryNextExpected, HistoryNotEnoughData,
        SettingsGeneral, SettingsEngage, SettingsRanking, SettingsFilter,
        SettingsNotifications, SettingsLanguage,
        SettingEnabled, SettingEnabledHelp, SettingAutoEngage, SettingAutoEngageHelp,
        SettingAutoRemount, SettingAutoRemountHelp, SettingStepDismount, SettingStepLevelSync,
        SettingStepTankStance, SettingRemountDelay, SettingTravelSpeed,
        SettingTravelSpeedExploratory, SettingTravelSpeedExploratorySlow, SettingTeleportOverhead,
        SettingReturnOverhead,
        SettingNearlyDone, SettingMinimumRemaining, SettingWeightDistance, SettingWeightTime,
        SettingWeightProgress, SettingExcludedKinds, SettingLevelRange, SettingSoundOnNewFate,
        SettingShowNotification, SettingShowNotificationHelp,
        SettingNotifyOnlyIncluded, SettingNotifyDistance, SettingTrackGemstones,
        SettingGemstoneHeadroom, SettingTrackHistory, SettingLanguageChoice, SettingLanguageAuto,
        SettingLevelMinimum, SettingLevelMaximum,
        SettingTypeFlagIntoChat, SettingTypeFlagIntoChatHelp,
        CommandAutoOn, CommandAutoOff, CommandEngageNothing, CommandEngageBlockedCombat,
        CommandEngageBlockedOccupied, CommandUnknown, CommandHelp,
        NotificationNewFate,
        WindowNewsTitle, ButtonNewsTooltip, ButtonNewsTooltipUnseen,
        NewsEmpty, NewsVersion, NewsInstalled,
        NewsKindAdded, NewsKindChanged, NewsKindFixed, NewsKindRemoved,
        SettingShowNewsOnUpdate, SettingShowNewsOnUpdateHelp,
        SettingConfirmReturnPrompt, SettingConfirmReturnPromptHelp,
        UnitYalms, UnitSeconds, UnitMinutes,
    ];
}
