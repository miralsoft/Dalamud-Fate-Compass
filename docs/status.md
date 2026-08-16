# Status: Fate Compass

Purpose: the current state of the project. The shared handover channel between sessions and
between different people or tools. Update at the end of every working session (M-08, R-02).

## How to test a build

```
.\build.ps1
```

Runs the format check, the build, and the tests, then prints the path to register under
`/xlsettings` in Dalamud, Experimental, Dev Plugin Locations:

```
src\FateCompass\bin\Release\FateCompass.dll
```

Registering it once is enough. After every later build, reload the plugin from the dev section
of `/xlplugins`. Nothing needs pushing to test.

## Instanced engagement content (Phase 6, written 2026-08-01)

Bozja, Zadnor, and the Occult Crescent are in. `DynamicEventProvider` reads
`DynamicEventContainer` and maps skirmishes, critical engagements, and critical encounters
onto the same snapshot the FATE table produces, so the controller simply concatenates the two
sources and everything downstream works unchanged.

- New: participant count as a column and a ranking factor, an open registration window as its
  own state with a score bonus and a row label, three new filterable kinds, and the remount
  step skipped where the zone forbids mounting.
- Eureka needed no code at all: its notorious monsters are ordinary FATEs.
- 99 tests, including that a plain FATE is not penalised for reporting no participation.
- **Not run in game.** Three items under Phase 6 in `todos.md` need confirming there, the main
  one being which `DynamicEventType` values map to which kind.

## Occult Crescent pass (2026-08-02, from play testing)

Everything below came out of a session in Crescentia and is in the current build.

- **Registration deadline.** A critical encounter can only be joined during its sign-up window,
  which the game shows as "Teilnahmeschluss in 2:17". The plugin now carries that deadline on
  the snapshot, ranks against it instead of against the fight's own countdown, and excludes an
  encounter whose door has shut or would shut before the player could arrive. Whether an event
  has a door at all is taken from its type byte, because the client structs layer keeps
  `SecondsRegistrationTime` private; unrecognised types are treated as ungated on purpose, so a
  classification gap can never hide something joinable.
- **The Forked Tower** is classified as `FateKind.SpecialObjective` by its total duration
  (45 minutes or more) and kept out of the numbered order entirely. It sits off at the end of
  the compact row behind a wider gap, with a star instead of a rank.
- **Remount worked nowhere in instanced content.** `CanUseMount` was "not bound by duty", which
  is false in exactly the zones this plugin cares about. It now reads the territory's own mount
  flag. Separately, entering and leaving a critical encounter was invisible to the controller,
  because it only watched `FateManager`; it now falls back to the dynamic event container's
  current event, which is what makes both the engage sequence and the remount fire there.
- **Travel speed is per content.** Measured at 20.5 y/s in the open world and 15.4 in the
  Occult Crescent, which is the difference between flying and not. Stored in two settings and
  chosen by `PlayerSnapshot.Content`.
- **Gemstones and shared FATE standing are hidden** in Eureka, Bozja, and the Crescent. Neither
  can move there, and the window has no room for a line that cannot change.
- Level sync needs no special case: the Crescent offers none, and the planner already only asks
  for sync when the game offers it.

## Map numbering (2026-08-02)

The game allows one flag per map and its markers carry an icon but no caption of our choosing,
so there is no way to ask it to write "1" next to a FATE. Two mechanisms now run together:

- `MapService.DrawRankMarkers` places the game's own markers on the map **and the minimap**,
  handing them a world position so the game does the projection itself. This is correct through
  panning, zooming, and HUD scale changes by construction. The tooltip pointer is passed as
  null, deliberately (FH-10). Redrawn only when the order changes, not every poll.
- `MapNumberOverlay` draws the digit on top, which needs a screen position and therefore a
  conversion of our own (`MapProjection`, anchored on the player marker the game reports).
  Behind its own setting, so it can be switched off if the digits sit beside their markers.

`DebugWindow` has a "Map geometry" button that prints the map's own cursor coordinate readout
next to ours for the same screen point, which settles an offset error against a scale error in
one press. **The conversion has not yet been confirmed in game.**

## Crash safety audit, redone by reachability (2026-08-16, R-21)

The passes below were greps: every `unsafe`, `fixed`, `->`, `Instance()` and `.Address` in the
plugin, each hit inspected. R-21 says that is a filter and not an audit, and it was right here.

**What this pass looked for**, written down because R-21 asks for it and because it is what makes
a gap visible without waiting for a measurement to surface one:

1. Every type under `Adapters/` and `Services/` that touches game memory or calls a game
   function. Twelve of them: `ChatInput`, `CurrencyProvider`, `Diagnostics`,
   `DynamicEventProvider`, `GameActions`, `GameSnapshotProvider`, `MapService`,
   `MountSpeedProvider`, `NativeMapMarkers`, `NotificationService`, `SharedFateProvider`,
   `WarpWatcher`.
2. For each, every caller, and for each caller the thread it runs on. The two that matter are
   the framework tick (`OnUpdate`, and anything inside `DalamudServices.OnGameThread`) and the
   ImGui draw callback (`Draw`, `PreDraw`, `PostDraw` on any window).
3. The question asked at each call site: does this reach a game function or game memory from the
   draw callback without going through the marshalling helper.

**Found three, all in `MainWindow`, all fixed the same day.** `PreDraw` reaching
`IsInstancedArea()`, `DrawGemstones` reaching `GetInventoryItemCount`, and `DrawRidingMapHint`
reading `PlayerState->CanFly`. Two were function calls; FH-08 permits only reads of addon
geometry from that thread.

Fixed by reversing the direction rather than by wrapping: `Adapters.GameFacts` takes those
readings on the framework tick and the windows read what it left behind. A wrapper was not
available, because these values are wanted while a frame is being built and the marshalling
helper is asynchronous. The cost is one frame of staleness on values that change when the player
zones, mounts or picks up a gemstone.

**Re-run after the fix**, the same three steps: no window in a released build reaches a
native-touching adapter directly any more. Both `MapService.SetFlagAndEcho` call sites are inside
`OnGameThread`, the diagnostics probes likewise, `WarpWatcher` and `GameFacts` are read as
managed snapshots, and `NativeMapMarkers` exposes managed status fields.

Every other path is clean. The diagnostics probes and both `MapService.SetFlagAndEcho` call sites
already go through `OnGameThread`. `WarpWatcher` is polled from the tick and the window reads only
the managed snapshot it kept, behind a lock. `NativeMapMarkers` exposes managed status fields.

Why the greps missed it: they found the adapter files, confirmed their null checks, and never
asked who called them from where. The constraint is on the call site and the search was on the
spelling, which is the sentence R-21 is built around.

## Crash safety audit (FH-11), the earlier grep passes

Last run 2026-08-01, against every `unsafe`, `fixed`, `->`, `Instance()`, and `.Address` in the
plugin. Three defects found and fixed:

- **Managed pointer handed to the game.** `AddMapMarker` takes a `byte*` that the client stores
  and reads on a later frame. A pinned managed array was passed, which is only valid inside its
  `fixed` block. This crashed the client from `AddonAreaMap.OnRequestedUpdate` and is also why
  no marker ever appeared. Now uses the managed-string overload.
- **Game calls from the draw thread.** The flag, teleport, and ready-up buttons called into the
  game directly from the ImGui callback. All game calls now go through
  `DalamudServices.OnGameThread`.
- **Two unchecked pointers**: the `Utf8String` allocated for the level sync command, which the
  cleanup path would have dereferenced a second time, and `AtkValues` in the diagnostics dump.

Everything else was already null-checked. Note that the `try` blocks around the unsafe code
were never the protection: they catch managed exceptions, not access violations.

**Re-run 2026-08-02** over the new unsafe code: `MapProjection`, `ChatInput`,
`MapService.DrawRankMarkers`, `DynamicEventProvider.CurrentEventId`, and
`Diagnostics.DumpDynamicEvents`. No defects found.

- Every addon and agent pointer is checked, and so is each child pointer reached through it
  (`addon->TextInput`, `addon->AtkUnitBase.RootNode`), on every path.
- `CurrentEventId` bounds-checks the container's own index before using it as a subscript.
- `AddMapMarker` is given a null tooltip pointer rather than a managed string. The parameter is
  the same one that crashed the client before, so it gets nothing to keep.
- `MapProjection` and `MapNumberOverlay` read addon geometry from the draw callback, which
  FH-08 now permits explicitly for reads. They guard on `IsFrameworkUnloading` and call no
  game function. The flag button in the table view was still calling the map agent straight
  from the draw callback and has been moved onto the framework thread.
- `ChatInput` uses the managed-string overload of `InsertText`, so nothing of ours is handed to
  the game as a pointer (FH-10). It types into the chat box and never sends: the channel and
  the send stay with the player (FH-01).

## Where this actually stands

Phases 1 to 4 are **complete**. Phase 5 is manual in-game verification, which needs the owner
and the game, not more code. Phase 6 (Eureka, Bozja, Zadnor, Occult Crescent) is deferred by
decision until the FATE version has been used in practice.

Two defects were found and fixed during a self-review on 2026-08-01, both of the kind that
only shows up when running:

- Every FATE already active on arriving in a zone counted as newly spotted, so a zone change
  or a plugin reload fired a burst of alerts. The first poll after arriving now seeds the set
  silently.
- The FATE list could leave the ImGui style stack unbalanced if drawing a row threw. That
  would not have broken one row, it would have corrupted every window drawn afterwards.

## Overall

Last updated 2026-08-01. Phases 1, 2, and the localization work are done and verified. The
solution builds clean, the core is covered by **85 passing tests**, and the plugin packages
into a loadable archive. Phase 3 (adapters) is in progress: the API surface has been verified
against the installed Dalamud, but no adapter is written yet. Phase 4 (UI) has not started, so
the plugin still does nothing in game.

## Localization (done)

- **Done:** `FateCompass.Core/Localization` with `StringKeys` (the key constants),
  `Localizer` (fallback chain), `LanguageResolver` (auto mode against Dalamud's
  `UiLanguage`), `DictionaryCatalog`, and `EmbeddedCatalogs`. German and English catalogues
  ship as embedded JSON.
- Tests enforce that both catalogues carry every declared key and no stray ones, so a new
  string cannot ship untranslated.
- **Adding a language:** drop `Localization/Catalogs/<code>.json` into the core project. No
  code change anywhere.

## Repository and enforcement

- **Done:** Repository connected to `miralsoft/Dalamud-Fate-Compass`, branch `main`, tracking
  `origin/main`. Foundation cloned to `.foundation-docs/` and locally excluded. Git identity
  set locally to `Sanaka <20637644+miralsoft@users.noreply.github.com>`. Both foundation hooks
  installed and verified by direct invocation. `.miralsoft-enforcement` configured, and since
  the move to foundation 2.0.0 its globs cover source, JSON, PowerShell and workflow files
  rather than markdown alone. CI mirrors the hooks (R-17), with each detector proving it can
  fire before it is trusted (R-20).
- **Next:** Nothing outstanding. The C# format check does not go into the shared pre-commit
  hook, which cannot perform it; it runs in `build.ps1` and in CI, which is what R-17 asks for.

## Documentation

- **Done:** `docs/README.md`, all seven project memory files required by M-03, and
  `platform-notes.md`. The C# and Dalamud profiles this project used to carry under
  `docs/rules/` were retired when foundation 2.0.0 shipped both.
- **Next:** Keep this file and `decisions.md` current as implementation proceeds.

## Foundation 2.0.0 (2026-08-12)

- **Done:** This project declares foundation version 2.0.0 and did the work M-17 asks for
  rather than deferring it. `CLAUDE.md` at the root is the entrypoint an agent starting here
  needs; `docs/project.md` declares kind, committer identity and version; the local C# and
  Dalamud profiles were retired in favour of the foundation's, with the research they contained
  moved to `platform-notes.md`; six em-dashes left over from the narrower I-02 are gone; and the
  draft pull request trap is written into `release.md`. The audit itself is in `decisions.md`,
  listing what was checked and found holding rather than only what changed.
- **Done:** The content checks are the foundation's template (`enforcement/ci/content-checks.yml`),
  copied rather than written here, with its steps inside the existing build job because branch
  protection names that job. The one deviation is `shell: bash` on each step, since the template
  needs a POSIX shell and this job runs on Windows. Two things were wrong on the way and are
  worth knowing: the checks first landed with the trigger still limited to `main`, so they ran
  nowhere on this branch, and an earlier hand-written copy of the attribution patterns went stale
  against the foundation within a day. Both are why the run is now read step by step rather than
  taken as green.
- **Done:** Consequences belonging to another repository have a section in `open-points.md`
  (M-18). The one entry is whether the aggregate index really satisfies D-06, which affects this
  plugin directly and is still not this project's to change.
- **Next:** Review the declared version whenever a release is cut (M-17): read the foundation
  changelog from 2.0.0 onward, then either raise it and do the work, or leave it and record why.
  That review now includes re-copying what was copied: the content checks in
  `.github/workflows/ci.yml` carry a provenance line naming the foundation version they came
  from (M-19), and it is what the comparison is against.

## Solution scaffold (Phase 1, done)

- **Done:** `global.json` pinning SDK 10.0.302, `.editorconfig` as the single formatting
  standard, `.gitattributes`, `.gitignore`, `NuGet.config` limited to nuget.org, and
  `Directory.Build.props` carrying the shared settings (nullable on, warnings as errors,
  analyzers at `latest-recommended`, lock files on).
- Three projects in `FateCompass.slnx`: `FateCompass.Core`, `FateCompass` (the plugin), and
  `FateCompass.Core.Tests`.
- Verified: build clean with zero warnings, `dotnet format --verify-no-changes` clean, no
  vulnerable packages, generated manifest carries `AssemblyVersion 0.1.0.0` and
  `DalamudApiLevel 15`, and `latest.zip` is produced.
- **Note:** `dotnet new sln` on .NET 10 produces the newer `.slnx` format, so the solution
  file is `FateCompass.slnx`, not `.sln`.

## Core logic (Phase 2, done)

- **Done:** `Model` (WorldPosition, FateSnapshot, PlayerSnapshot, FateKind,
  FateProgressState, TankJob), `Configuration` (FateCompassSettings, RankingWeights),
  `Ranking` (FateFilter, FateRanker, RankedFate, FateExclusionReason), `Engage`
  (EngagePlanner, EngagePlan, EngageStep, PlanBlockedReason), `Routing` (Aetheryte,
  RouteHint, RouteHintCalculator), `History` (FateHistoryStore, FateSighting,
  RespawnEstimate), `Gemstones` (GemstoneTracker, GemstoneStatus).
- 60 xUnit tests, all passing, covering the required edge cases: a FATE that would expire
  before arrival, a nearly complete FATE, an empty zone, an unclassified FATE kind, and a full
  gemstone purse.
- FH-04 verified by inspection of the built assembly: `FateCompass.Core` references only
  `System.Collections`, `System.Linq`, and `System.Runtime`.

## Plugin (Phases 3 and 4 written, not yet run in game)

- **Adapters:** `GameActions` (the sole server gate), `TankStanceResolver`,
  `TextCommandResolver`, `GameSnapshotProvider`, `AetheryteProvider`, `MapService`,
  `NotificationService`.
- **Orchestration:** `FateCompassController`, polling every 500 ms rather than per frame,
  detecting new FATEs, FATE entry and exit, and running the engage and remount plans.
- **UI:** `MainWindow` (ranked list, flag and teleport buttons per row, excluded FATEs greyed
  out with their reason on hover), `ConfigWindow` (every setting including the language
  picker), and the `/fate` command family.
- **Everything compiles against the installed Dalamud 15.0.3 and packages cleanly. None of it
  has been executed in game yet.** The first run is the real test.

## Superseded detail from the previous session

- **Done:** `Services/DalamudServices.cs` collecting the injected services, and the two pieces
  that carry the real risk:
  - `Adapters/GameActions.cs`, the single gate to the game server (FH-01). Dismount, mount,
    level sync, tank stance, and teleport, each logged with its trigger. Level sync goes
    through the text command and its result is verified with `FateManager.IsSyncedToFate`.
    Actions are checked with `GetActionStatus` before being sent, so an unusable action is
    skipped rather than fired at the server.
  - `Adapters/TankStanceResolver.cs`, resolving the tank job from `ClassJob.Role` in the game
    data rather than a hard-coded job list.
- All of this compiles against the installed Dalamud 15.0.3, which is what validates the API
  usage. It has **not** been run in game.
- **Next:** the remaining adapters (`FateProvider` over `IFateTable`, `GameStateProvider`,
  `AetheryteProvider` from Lumina, `MapService` over `AgentMap`, `NotificationService`), then
  Phase 4, the two windows and the text commands.
- The values under "Still to verify in game" in `open-points.md` compile but could still be
  wrong. That is the dangerous kind, so check them before trusting the stance and mount steps.

## Notes for whoever picks this up

- Scope boundary is not negotiable inside the project: the plugin assists, it does not play.
  See FH-05 in `rules-project.md` and the automation boundary in
  `.foundation-docs/rules/frameworks/dalamud.md`.
- The engage sequence is planned in `EngagePlanner` and must be executed only through
  `IGameActions` (FH-01). Both triggers run the same plan on purpose, and a test locks that in.
- Platform facts in `platform-notes.md` were verified on 2026-08-01 against Dalamud 15.0.3 as
  installed locally. Recheck on a Dalamud major bump.
- Nothing has been committed yet. The first commit will carry documentation, scaffold, and
  core together.
