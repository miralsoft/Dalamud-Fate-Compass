# Architecture: Fate Compass

Purpose: the technical architecture of the project.

## Overview

The plugin is a single assembly in three layers. The split is not decoration, it is driven by
two hard constraints from the rules:

1. Code that touches Dalamud cannot be unit tested, because it needs a running game process
   (`.foundation-docs/rules/languages/csharp.md`). So everything worth testing has to be
   reachable without Dalamud types.
2. Every call that reaches the game server has to stay visible and auditable
   (`.foundation-docs/rules/frameworks/dalamud.md`). So those calls live in exactly one place
   instead of being scattered
   across features.

```
UI layer          ImGui windows via the Dalamud Windowing API
                  reads view models, raises intents, never calls the game directly
      |
Core layer        pure C#, no Dalamud types in any signature
                  ranking, filtering, history, route hints, engage planning, config
      |
Adapter layer     thin wrappers over Dalamud services, behind interfaces
                  the only code that knows Dalamud exists
```

Dependencies point inward. The core knows nothing about the layers around it.

## Components

### Core (`src/Core/`)

No Dalamud reference. Fully covered by unit tests.

- **`FateSnapshot`**: an immutable record of one FATE at one moment. Id, name, level, kind,
  world position, progress percent, seconds remaining, state, icon id. This is the only FATE
  shape the core and the UI ever see.
- **`FateRanker`**: turns a set of snapshots plus the player position into an ordered list.
  Scores on travel distance, seconds remaining, and current progress, so a FATE that will
  expire before arrival or is nearly finished sinks instead of being recommended. The weights
  are configuration, not constants.
- **`FateFilter`**: include and exclude by FATE kind (boss, collect, escort, defend) and by
  level range. Feeds both the list and the notifier, so the sound can be narrower than the
  display.
- **`FateHistoryStore`**: records when each FATE was last seen active, per zone. Produces a
  rough respawn expectation after a few cycles. Local data only, persisted with the config.
- **`RouteHintCalculator`**: given a FATE position and the aetherytes of the zone, returns the
  nearest aetheryte and the remaining distance from there. This is what the dialog shows as
  "port here, then go that way".
- **`EngagePlanner`**: given a player state snapshot (mounted, in combat, job, current level,
  FATE level, stance active), returns the ordered list of steps that are actually needed. It
  decides nothing about timing and executes nothing, it only plans. This is the piece that
  makes the engage sequence testable.
- **`GemstoneTracker`**: current bicolor gemstone count against the cap, plus the warning
  threshold.
- **`PluginConfiguration`**: the full settings model, versioned for future migration.

### Adapters (`src/Adapters/`)

Each is a thin pass-through behind an interface, kept as close to trivial as possible because
it cannot be covered by tests.

- **`IFateProvider`**: reads `IFateTable` and maps to `FateSnapshot`. Polling is throttled on
  the framework tick rather than run every frame, because per-frame cost is a stated platform
  concern.
- **`IGameStateProvider`**: player position, job, level, mounted state, combat state, active
  stance, current territory.
- **`IAetheryteProvider`**: aetherytes of the current zone, read from Lumina game files. No
  remote data source.
- **`IMapService`**: sets the map flag marker through `AgentMap`, and places the FATE markers.
  Client-side only, no server contact.
- **`INotificationService`**: sound and on-screen notice on a new FATE.
- **`IGameActions`**: **the single server-touching component.** Dismount, mount, level sync,
  tank stance, teleport. Nothing else in the codebase may send an action to the game. Every
  method logs what it did and why it was triggered, so the automation boundary can be
  reviewed by reading one file.

### UI (`src/UI/`)

- **`MainWindow`**: the FATE list for the current zone. Rank number, name, level, kind,
  progress, time remaining, distance, and the nearest aetheryte. Per row: a flag button and a
  teleport button.
- **`ConfigWindow`**: all settings, including the automation toggles.
- Both registered through the Dalamud Windowing API rather than drawn ad hoc.

### Plugin host (`src/`)

- **`Plugin`**: lifecycle, service injection, command registration, framework tick
  subscription. Everything it registers, it unregisters in `Dispose`.

## The engage sequence

The one feature that sits on the automation boundary, so it is designed explicitly.

`EngagePlanner` produces the plan. `IGameActions` executes it. Two triggers exist for the same
plan:

- **Manual:** the `/fate engage` command. The player pressed a key, so this is direct user
  interaction and is unrestricted.
- **Automatic:** the FATE-entered event. This is reactive automation and is the case the
  Dalamud restrictions describe. It ships **disabled by default** and has to be switched on
  deliberately.

Both paths run identical code. The trigger is the only difference, and it is recorded in the
log line so it stays distinguishable.

Execution rules: mounting fails in combat and has a cast time that can be interrupted, so the
remount step waits for a safe state and gives up rather than retrying blindly. Steps that are
not needed are not sent at all, for example no stance action when the stance is already up.

## Commands

The plugin has to be controllable from a macro without opening a window.

| Command | Effect |
|---|---|
| `/fate` | Open or close the main window |
| `/fate cfg` | Open the configuration window |
| `/fate engage` | Run the engage sequence once, manually |
| `/fate auto on` / `off` / `toggle` | Switch the automatic trigger |

## Data model and persistence

One configuration file in the Dalamud plugin config directory, holding settings and the FATE
history. Versioned so a later schema change can migrate rather than reset. No other storage,
no database, no remote state.

## Contract between components

Single repository, so there is no cross-repo contract. The internal contract is the interface
set in the adapter layer. The core depends on those interfaces, never on their
implementations, which is what allows the core to be tested without a game.

## Deploy and update mechanism

Local build through `Dalamud.NET.Sdk`, loaded via Dalamud's developer plugin path. No server,
no release pipeline, no auto-update. If the plugin is ever published, that becomes its own
project phase and is recorded as a decision first.
