# Dalamud platform profile

Applies to this project because it declares Dalamud as its platform. Adds to the global rules
and to the C# profile (`csharp.md`). Dalamud is treated as its own platform with its own
lifecycle, its own data access model, and its own published restrictions on what a plugin may
do.

All version facts below were read from the official documentation and from the reference
sample project. They are pinned in time and must be rechecked when Dalamud releases a new
major version.

## Platform versions

| Item | Value |
|---|---|
| Dalamud version | 15.x |
| API level | 15 (from v9 onward the API level equals the major version) |
| Runtime | .NET 10 |
| Build SDK | `Dalamud.NET.Sdk/15.0.0` |
| Verified on | 2026-08-01 |

A Dalamud major version bump changes the API level and can break the plugin. Treat it the
same way M-06 treats a foundation major bump: review before adopting, do not follow blindly.

## Project layout

- The csproj uses the Dalamud SDK and stays deliberately small:

  ```xml
  <Project Sdk="Dalamud.NET.Sdk/15.0.0">
  ```

- The SDK supplies the target framework, the platform target, the Dalamud assembly references,
  and the packaging step. Do not restate those properties in the csproj. Overriding what the
  SDK sets is how plugins drift out of sync with the API level.
- **Dalamud assemblies are never committed and never bundled.** They are resolved by the SDK
  from the local Dalamud installation. A repository that carries game or framework DLLs is
  wrong, both technically and legally.
- The plugin manifest (`<InternalName>.json`) inside the release archive must be accurate.
  Since v15 the repository manifest no longer overwrites it, so a wrong manifest ships as is.
- The manifest version and the csproj version stay in sync (C-06).

## Crash safety

A plugin shares the game's process. A mistake here does not produce a stack trace and a
recovered feature, it closes the client. Two of them happened in this project, so the specifics
are written down rather than assumed:

- **Threads.** Game functions are called from the framework thread only. The ImGui draw
  callback runs elsewhere, so a button handler must marshal rather than call directly.
- **Pointers.** Anything returned by `Instance()` or an addon lookup can be null, including on
  paths that worked a moment earlier. Check before following, every time.
- **Managed memory.** Never hand the game a pointer into managed memory. `fixed` pins only for
  its block, while the game may store the pointer and read it on a later frame. Prefer the
  overloads taking a managed string, or allocate a `Utf8String` and free it.
- **`try`/`catch` is not protection here.** It catches managed exceptions. An access violation
  is not one, and it ends the process regardless of any handler.

The project rules state these as FH-08 to FH-11, including the audit that has to run before a
build is handed over.

## Services and lifecycle

- Dalamud services are obtained through injection, not constructed:

  ```csharp
  [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
  ```

- Anything the plugin registers, it unregisters in `Dispose`: UI builder callbacks, command
  handlers, framework update handlers, windows, and hooks. A plugin is loaded and unloaded
  repeatedly within one game session, so a leak here compounds instead of being cleaned up by
  process exit.
- `IAsyncDalamudPlugin` allows asynchronous initialization with a 60-second timeout. Long
  setup work belongs there, not in a constructor that blocks the game.
- Never block the framework thread. Per-frame work stays cheap, and anything expensive moves
  off the tick. Dalamud's plugin statistics window (`/xldev`) is the tool to check this
  against, not intuition.
- Deprecated in v15: `IClientState.LocalPlayer` and `IClientState.LocalContentId`. Do not
  build new code on them.

## Game data access

- Read game data through **Lumina**, which reads the local game files. Do not call XIVAPI or
  any other remote data service for data that exists locally. Lumina is accurate, current, and
  faster, and it introduces no network dependency (this also serves S-04 and P-01).
- Use the Dalamud Windowing API for windows such as settings and utility panels, rather than
  drawing ad-hoc ImGui windows.

## Network and backend communication

This plugin should need no backend at all. If that ever changes, the platform rules apply in
full and they are stricter than the foundation baseline:

- HTTPS or TLS with a trusted certificate authority, connecting by DNS hostname and never by
  raw IP address.
- Send the minimum data necessary, and hash anything sensitive on the client side first.
- Telemetry is opt-in and explicit. Never on by default.
- Identifiers are pseudo-random and resettable, never derived from personal or account data.
- Never expose a list of users, and never allow testing whether a specific player uses the
  plugin.

## Plugin restrictions

These come from Dalamud's published restrictions. They are not style preferences, they are the
line between an acceptable plugin and a banned one. They bind this project regardless of
whether it is ever submitted anywhere, because they describe what is safe to do to a live game
account.

**Forbidden:**

- Interacting with the game servers **automatically**. This is the central one. Emote looping,
  cutscene and dialog skipping, automated crafting, and autoroll on loot are the named
  examples.
- Augmenting, altering, or interfering with combat, other than presenting information the
  player already has in a different form.
- Damage meters, parsing, and raid logging.
- Any PvP advantage.
- Collecting account IDs of player characters other than your own, in any form.
- Requests outside specification, meaning letting the player do or submit anything to the
  server that normal play could not.
- Circumventing Square Enix's monetary interests, for example Mog Station items or Fantasia.
- Functionality that is only useful out of bounds.

**Allowed:**

- Presenting information the player can already reach, in a clearer form.
- UI improvements that still require the player to act, for example a tweak that preselects a
  loot entry without confirming it.
- Behavior that stays inside normal game bounds.

**The dividing line for this project:** the plugin may show, sort, rank, filter, time, and
route. The player performs every action. The moment code sends something to the server without
a human input behind it, the plugin is in breach. Design every feature so the final action
stays with the player, and treat any request to automate a server interaction as a change of
project scope, not as a feature request.

## Open point: the official repository and AI disclosure

If this plugin is ever submitted to the official Dalamud repository, two of its policies need
an owner decision:

- Submissions must be open source. Closed-source plugins are not accepted.
- The AI usage policy requires that assistance beyond basic autocomplete is disclosed in the
  pull request description. Entirely generated submissions with no meaningful human
  involvement are auto-rejected, and repeating that leads to a permanent ban.

This does not conflict with I-06, which governs the codebase, the commits, and the product
text, all of which stay clean either way. The disclosure would live in the submission pull
request, and it is the owner's statement to make, not something written into the repository.

While the plugin stays private, neither policy applies. Recorded here so the decision is not
discovered late.

## Verified API surface

Read on 2026-08-01 out of the locally installed Dalamud 15.0.3, by reflecting over
`Dalamud.dll` and `FFXIVClientStructs.dll` and by reading `Dalamud.xml`. These are facts, not
recollections. Recheck on a major bump.

**Services**

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
deprecated. Use `IObjectTable.LocalPlayer`.

**Enum values**

- `FateState`: `Preparing = 3`, `Running = 4`, `Ending = 5`, `Ended = 7`, `Failed = 8`.
- `ConditionFlag`: `Mounted = 4`, `Occupied = 25`, `InCombat = 26`, `Casting = 27`,
  `BetweenAreas = 45`, `Mounting = 64`.

**ClientStructs signatures**

```csharp
// Sets the map flag without opening the map. Client-side only, no server contact.
void AgentMap.SetFlagMapMarker(uint territoryId, uint mapId, Vector3 worldPosition, uint iconId);
bool AgentMap.AddMapMarker(Vector3 position, uint icon, int scale, byte* text, byte textPosition, byte textStyle);

bool Telepo.Teleport(uint aetheryteId, byte subIndex);

bool ActionManager.UseAction(
    ActionType actionType, uint actionId, ulong targetId, uint extraParam,
    UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted);
```

## Project-facing notes

- Recheck this file when Dalamud publishes a new major version, and record the outcome in
  `decisions.md`.
- Feature scope decisions that touch the restriction list belong in the project rules, with a
  short note on why the feature stays on the allowed side.
