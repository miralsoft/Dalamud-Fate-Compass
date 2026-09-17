# Platform notes

Purpose: what this project established about the Dalamud API by looking, so the next session
does not have to look again.

This is not a profile and carries no rules. The rules for this platform are in
`.foundation-docs/rules/frameworks/dalamud.md`, and the construction is in
`.foundation-docs/blueprints/dalamud-plugin.md`. What is here is research: the members, values
and signatures this plugin actually depends on, read out of the installed platform rather than
recalled or taken from documentation. It stayed with the project when the profiles moved to the
foundation, because a list of member names is a finding about one version, not a rule anybody
else should inherit.

## Verified against

- **Version:** Dalamud 15.0.3.4, API level 15
- **Read on:** 2026-09-16 (the surface below on 2026-08-01, against 15.0.3)
- **Method:** verified. Reflected over `Dalamud.dll` and `FFXIVClientStructs.dll` in the local
  installation, and read `Dalamud.xml`.

Recheck on a major bump and record the outcome in `decisions.md`. The profile in the foundation
says the same thing as a rule; this is where the answer lands.

**A patch bump moved the surface too, so "recheck on a major" is not enough on its own.**
15.0.3.4 made `IDtrBarEntry` inherit `IDisposable` and added `IReadOnlyDtrBarEntry` alongside it.
Nothing was removed and nothing was marked obsolete, so no call stopped compiling; what broke the
build was the analyzer, because a disposable field that is only `Remove()`d is a CA2213 error and
warnings are errors here. The lesson worth keeping is the shape of it: the platform can tighten a
build without changing a single signature this project calls, and the first sign of it is a build
that fails on a machine where nothing was edited.

## Services

| Need | Member |
|---|---|
| Active FATEs | `IFateTable`, a read-only collection of `IFate` |
| FATE data | `IFate`: `FateId`, `GameData`, `TimeRemaining`, `Duration`, `StartTimeEpoch`, `Name`, `State`, `Progress`, `HandInCount`, `HasBonus`, `IconId`, `MapIconId`, `Level`, `MaxLevel`, `Position`, `Radius`, `TerritoryType` |
| Local player | **`IObjectTable.LocalPlayer`** returning `IPlayerCharacter` |
| Zone and language | `IClientState`: `TerritoryType`, `MapId`, `ClientLanguage`, `IsLoggedIn`, `IsPvP` |
| Player condition | `ICondition`, indexed by `ConditionFlag` |
| Per-frame work | `IFramework`, including `RunOnTick` and `RunOnFrameworkThread` |
| Interface language | `IDalamudPluginInterface.UiLanguage` plus the `LanguageChanged` event |
| Config storage | `IDalamudPluginInterface`: `GetPluginConfig`, `SavePluginConfig`, `ConfigDirectory` |

**Important:** `IClientState.LocalPlayer` no longer exists in v15. It was removed, not merely
deprecated. Use `IObjectTable.LocalPlayer`. This was the first thing reflection caught that
memory had wrong, and it is the reason the profile requires looking rather than recalling.

## Enum values

- `FateState`: `Preparing = 3`, `Running = 4`, `Ending = 5`, `Ended = 7`, `Failed = 8`.
- `ConditionFlag`: `Mounted = 4`, `Occupied = 25`, `InCombat = 26`, `Casting = 27`,
  `BetweenAreas = 45`, `Mounting = 64`.

## ClientStructs signatures

```csharp
// Sets the map flag without opening the map. Client-side only, no server contact.
void AgentMap.SetFlagMapMarker(uint territoryId, uint mapId, Vector3 worldPosition, uint iconId);
bool AgentMap.AddMapMarker(Vector3 position, uint icon, int scale, byte* text, byte textPosition, byte textStyle);

bool Telepo.Teleport(uint aetheryteId, byte subIndex);

bool ActionManager.UseAction(
    ActionType actionType, uint actionId, ulong targetId, uint extraParam,
    UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted);
```

## What kind of warp is happening

`FFXIVClientStructs.FFXIV.Client.Game.UI.WarpInfo` is a singleton reached through
`WarpInfo.Instance()` and carries a `WarpType` field. `GameMain.Instance()` carries
`TerritoryTransitionState`, `TerritoryLoadState` and `CurrentTerritoryTypeId` alongside it.

The enum has thirty values. The ones that matter here:

| Value | Name |
|---|---|
| 0 | `None` |
| 3 | `Translate` |
| 4 | `Teleport` |
| 7 | `Return` |
| 12 | `EnterInstanceContent` |
| 13 | `LeaveInstanceContent` |
| 15 | `TownTranslate` |

**Measured in the Occult Crescent (territory 1346) on 2026-08-12**, by sampling every framework
tick and recording the changes:

- An **aetheryte hop inside the zone** reports `TownTranslate` (15), not `Teleport` and not
  `Translate`. This is the finding that justified measuring instead of assuming: `Teleport` is
  what anybody would guess, and a feature built on that guess would never have fired once.
- **Return** reports `Return` (7).
- The value is **transient**. It appeared about four seconds before the sample and was back to
  `None` within two to three: roughly 1.8 seconds for the aetheryte hop and 2.7 for Return.
  Reading it on demand after arriving therefore sees nothing, which is why anything built on it
  has to sample continuously rather than ask when it wants to know.
- `TerritoryTransitionState` ran 2 to 1 and back to 2 across the warp, with the 1 at the moment
  of the transition itself.
- `CurrentTerritoryTypeId` never changed. That is the whole reason this exists: inside these
  zones an aetheryte hop is not a territory change, so watching for one sees nothing.

A teleport from outside **into** such a zone reports `EnterInstanceContent` (12), measured the
same day. That run also settled a question about where the reading has to be taken, and the
answer was not the obvious one:

```
-10.2s  EnterInstanceContent  transition=2  load=2  territory=1278   the origin zone
- 9.0s  EnterInstanceContent  transition=1  load=1  territory=0      the loading screen
- 5.0s  EnterInstanceContent  transition=2  load=2  territory=1346   arrived in the zone
- 4.6s  None                  transition=2  load=2  territory=1346   the arrival
```

The warp is already set while the player is still in the origin zone, and `TerritoryTypeId` is
**zero** during the loading screen. So anything watching this has to watch everywhere: sampling
only inside the zones the feature cares about would miss the start of the journey and therefore
never see its end. Gate the action on where the player is standing when the warp ends, not the
sampling on where it began.

Note also how much longer the value lives here, roughly 5.6 seconds against 1.8 for a hop inside
the zone, because a loading screen sits in the middle of it.

The name `TownTranslate` says plainly that the same value is used by the aethernet in cities, so
anything acting on it needs its own reason to be in an exploratory zone rather than treating the
value as proof of one.

## What the packager puts in the zip

Measured on 2026-08-16, against DalamudPackager 15.0.0, after three releases shipped without the
plugin icon inside the package.

The packager writes the assemblies and the manifest into the archive and nothing else. Files that
sit in the output folder, the icon among them, do not travel with it. Its MSBuild task takes
`Include`, `Exclude`, `HandleImages` and `ImagesPath`, none of which the SDK's default targets
pass, and a project can override those targets by putting its own `DalamudPackager.targets` next
to the csproj: the package imports that file when it exists and stands its own targets down.

What was tried and what it did:

- **`HandleImages="true"`** puts `images/icon.png` into the staging folder. It does not put it
  into the zip.
- **`Include`** is a list of **literal filenames** relative to the output path, and it **replaces**
  the default set rather than adding to it. Setting it to just the icon produced a 22 byte archive
  with zero entries. Globs are not patterns there: `*.dll` is opened as a filename and the build
  fails with an invalid-path exception.

So using `Include` would mean naming every assembly by hand and getting it wrong the next time a
dependency arrives. The icon is added to the archive in the release workflow instead, next to a
step that fails the release if the package is missing the assembly, the manifest or the icon.

## General actions

Read out of the game's own `GeneralAction` sheet rather than assumed, because the numbers are
not guessable and a wrong one sends the player somewhere unintended.

- `7` Teleport
- `8` Return
- `9` Mount Roulette
- `23` Dismount

## The FATE level band, and the minimum level that does not exist

Read on 2026-09-16 with Lumina 7.0 against the installed client, over all 2111 rows of the
`Fate` sheet, 1712 of which carry a name.

**Every FATE has a level band, not a level.** `ClassJobLevel` is what the map shows.
`ClassJobLevelMax` is the top of the band, and it is not the level anybody is synced to, which
is what the code used to say it was.

| Band width | FATEs | Example |
|---|---|---|
| level + 4 | 676 | The Serpentlord Seethes, 100 to 104 |
| level + 5 | 371 | Sprig Cleaning, 30 to 35 |
| level + 3 | 114 | |
| exactly the level | 219 | Excitable Boys, 60 to 60 |
| 255 | 292 | Eureka rows, Mendicant's Court, Base Camp Alpha |

For modern content the band is almost always plus four: of the 364 FATEs at level 70 or above,
300 are plus four, 58 have no band, and 6 are plus ten.

**255 is a sentinel and not a level.** It is a one-byte field standing in for "no cap". 142 of
those 292 rows are flagged `EurekaFate`, and Eureka's notorious monsters arrive through the
ordinary FATE table, so the value reaches the plugin's own snapshot type. Handed through it
produced a sync column reading "to 255".

**There is no minimum level for a FATE anywhere in the game data**, and this was searched for
rather than assumed (R-21). What the search covered:

- all ten `RowRef` fields on `Fate`: they point at `EventItem`, `Quest`, `BGM`, `ScreenImage`,
  `Status` and `FateRuleEx`, and none of them is a level gate;
- every sheet type whose name contains "Fate", which is eighteen, eight of them `GFate*` gold
  saucer minigames, plus `FateMode`, `FateShop`, `FateTokenType`, `FateProgressUI` and
  `WKSFateControl`;
- `FateRuleEx` in full: 38 rows carrying a single byte that mirrors the row id. Nothing.

The client's own accessor is `FateDirector.GetRecommendedLevel`, **recommended** rather than
required, and the game lets anybody join anything. Being under-levelled costs a contribution
that counts for less, not a closed door. So any threshold the plugin draws is a judgement, which
is why the level fit thresholds are settings rather than constants (C-12).

**A separate gate that does exist:** `RequiredQuest` is set on 58 of the 1712 named FATEs. That
is a real prerequisite and the plugin does not check it today. Noted in `open-points.md`.
