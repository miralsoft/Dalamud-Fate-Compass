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

- **Version:** Dalamud 15.0.3, API level 15
- **Read on:** 2026-08-01
- **Method:** verified. Reflected over `Dalamud.dll` and `FFXIVClientStructs.dll` in the local
  installation, and read `Dalamud.xml`.

Recheck on a major bump and record the outcome in `decisions.md`. The profile in the foundation
says the same thing as a rule; this is where the answer lands.

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

## General actions

Read out of the game's own `GeneralAction` sheet rather than assumed, because the numbers are
not guessable and a wrong one sends the player somewhere unintended.

- `7` Teleport
- `8` Return
- `9` Mount Roulette
- `23` Dismount
