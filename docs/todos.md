# To-dos: Fate Compass

Purpose: the project to-do list, grouped per phase. Each phase names the gates its work has to
pass before the phase counts as done (R-14). The gates are stated here on purpose, so they
cannot be skipped by whoever picks the work up.

Target for this list: version 0.1.0.

## Phase 1: Scaffold

- [x] `global.json` pinning the .NET SDK
- [x] `.editorconfig` at the repository root
- [x] `.gitignore` covering `bin/`, `obj/`, and build output
- [x] `NuGet.config` restricted to nuget.org
- [x] Plugin csproj on `Dalamud.NET.Sdk/15.0.0`, version 0.1.0
- [x] Plugin manifest with matching version
- [x] Test project on xUnit
- [x] `CHANGELOG.md` started
- [x] `Directory.Build.props` for the shared settings, `.gitattributes` for line endings

**Gates:** `dotnet build` succeeds. `dotnet format --verify-no-changes` passes. Warnings are
errors and nullable is enabled. `packages.lock.json` is committed. Manifest version and csproj
version match (C-06).

## Phase 2: Core logic

- [x] `FateSnapshot` and the supporting value types
- [x] `FateFilter`: by kind and level range
- [x] `FateRanker`: distance, remaining time, progress, configurable weights
- [x] `RouteHintCalculator`: nearest aetheryte and remaining distance
- [x] `EngagePlanner`: decides the needed steps from a player state snapshot
- [x] `FateHistoryStore`: sighting log and respawn estimation
- [x] `GemstoneTracker`: count against cap and warning threshold
- [x] `FateCompassSettings`: versioned settings model

**Gates:** no Dalamud or FFXIVClientStructs reference anywhere under `src/Core/` (FH-04).
xUnit covers each type above, including the edge cases that matter: a FATE that expires before
arrival, a FATE near completion, an empty zone, an unknown FATE kind, a full gemstone purse.
`dotnet test` green (T-03). Analyzers clean.

## Phase 3: Adapters

- [x] `GameSnapshotProvider` over `IFateTable`, polled every 500 ms rather than per frame
- [x] Player state: position, job, level, mounted, combat, stance, territory
- [x] `AetheryteProvider` from Lumina
- [x] `MapService`: flag marker through `AgentMap`
- [x] `NotificationService`: sound and notice
- [x] `GameActions`: dismount, mount, level sync, tank stance, teleport
- [x] `TankStanceResolver`, resolving the tank job from `ClassJob.Role`
- [x] `TextCommandResolver`, reading the level sync command from the game data
- [x] `CurrencyProvider` for the bicolor gemstone count and cap

**Gates:** every adapter stays a pass-through with no logic worth testing. `IGameActions` is
the only type that sends anything to the server (FH-01) and logs every call with its trigger
(FH-06). Verify the tank stance action and the level sync command in game before wiring them,
rather than trusting the names (I-10).

## Phase 4: UI

- [x] `MainWindow`: ranked FATE list with rank, name, level, kind, progress, remaining time,
      distance, nearest aetheryte
- [x] Per row: flag button and teleport button
- [x] Excluded FATEs shown greyed out, with the reason and the respawn estimate on hover
- [x] Gemstone purse line, coloured by how close the cap is
- [x] `ConfigWindow`: all settings including the automation toggles and the language picker
- [x] Text commands `/fate`, `/fate cfg`, `/fate engage`, `/fate auto on|off|toggle`

**Gates:** both windows registered through the Dalamud Windowing API, not drawn ad hoc. Every
automatic behaviour has a visible toggle and a command (FH-02). User-facing strings collected
in one place (C-05). Everything registered is unregistered in `Dispose`.

## Phase 5: Integration and verification

**This phase needs the game running and cannot be completed from the code side.** Everything
below is a manual check by the owner. Nothing here can be covered by a test, which is exactly
why it is written down rather than assumed.

- [ ] Engage sequence end to end, manual trigger
- [ ] Engage sequence end to end, automatic trigger
- [ ] Remount behaviour in combat and with an interrupted cast
- [ ] New FATE detection, sound, and filtering
- [ ] Flag placement plus `<flag>` in chat
- [ ] Teleport button against a zone with several aetherytes
- [ ] Performance check in the Dalamud plugin statistics window

**Gates:** `dotnet test` green, `dotnet format --verify-no-changes` clean, analyzers clean,
`dotnet list package --vulnerable --include-transitive` clean (S-08). Manually tested in game,
because none of this can be covered automatically. `CHANGELOG.md` updated for 0.1.0 (C-08).
`status.md` updated (M-08).

## Phase 6: Eureka, Bozja, Zadnor, and the Occult Crescent

Requested 2026-08-01. Feasibility was checked against the installed client before planning, so
the following is what the game actually exposes, not an assumption.

**Two different systems, not one:**

- **Eureka** notorious monsters are ordinary FATEs. They already come through `IFateTable`, so
  they should largely work today. What needs checking is that the ranking still makes sense
  there, since Eureka uses elemental levels and the level column will read oddly.
- **Bozja, Zadnor, and the Occult Crescent** use a separate system:
  `FFXIVClientStructs.FFXIV.Client.Game.InstanceContent.DynamicEventContainer`, holding a span
  of `DynamicEvent`. That covers skirmishes, critical engagements, and the Crescent's critical
  encounters. A parallel `MycDynamicEvent` exists under the Occult Crescent agent.

**`DynamicEvent` carries everything the ranking needs**, which is why this fits the existing
core rather than needing a second one:

| Field | Maps to |
|---|---|
| `Name`, `Description` | `FateSnapshot.Name` |
| `State` (`DynamicEventState`) | `FateSnapshot.State` |
| `Progress` | `FateSnapshot.ProgressPercent` |
| `SecondsLeft`, `SecondsDuration`, `StartTimestamp` | `FateSnapshot.SecondsRemaining` |
| `MapMarker` (`MapMarkerData`) | `FateSnapshot.Position` |
| `DynamicEventType`, `EnemyType` | a new kind, alongside `FateKind` |
| `Participants`, `MaxParticipants` | new, and genuinely useful here |

- [x] Both sources map onto the same snapshot, with an `ActivitySource` recording which system
      an entry came from. Ranking, filtering, history, and routes needed no change at all.
- [x] `DynamicEventProvider` adapter over `DynamicEventContainer`
- [x] Participant count as a column and as a ranking factor, contributing nothing where the
      game does not report it so plain FATEs are not penalised
- [x] Registration window surfaced as its own state, scored with a bonus and called out on the
      row, because it is the one thing here that can be missed outright
- [x] Three new kinds (skirmish, critical engagement, critical encounter), filterable like any
      other
- [x] Remount skipped where the zone forbids mounting, reported as `NotPermittedHere` rather
      than attempted (FH-07). Level sync needed no change: it is only planned inside a FATE,
      and `FateManager` reports none in this content.
- [ ] **In game:** confirm the `DynamicEventType` values behind the three kinds. The numbering
      is not documented, so an unrecognised value falls to critical engagement rather than to
      unknown.
- [ ] **In game:** confirm teleport and the map flag behave normally inside instanced content
- [ ] **In game:** confirm Eureka notorious monsters rank sensibly, since Eureka uses
      elemental levels and the level column will read oddly

**Gates:** the core stays free of Dalamud types (FH-04), the new provider stays a
pass-through, and existing FATE behaviour is unchanged. Verify in each zone type before
trusting it, since none of this can be covered by tests.

## Ongoing

- [ ] Wire `dotnet format --verify-no-changes` into the pre-commit hook, since the shared hook
      only knows PHP and JavaScript
- [ ] Recheck `rules/dalamud.md` when Dalamud publishes a new major version
