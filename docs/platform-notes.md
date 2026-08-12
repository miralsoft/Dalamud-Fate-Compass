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

## General actions

Read out of the game's own `GeneralAction` sheet rather than assumed, because the numbers are
not guessable and a wrong one sends the player somewhere unintended.

- `7` Teleport
- `8` Return
- `9` Mount Roulette
- `23` Dismount
