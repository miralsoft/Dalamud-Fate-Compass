# Project rules: Fate Compass

Purpose: project-specific rules that extend or tighten the global rules, plus the additional
rule prefix this project introduces.

> Reminder: these rules may only add to or tighten the global rules. They may never weaken,
> contradict, or override them (M-01, M-04).

## Added prefixes

- **`FH`** Fate Compass project rules. Global prefixes are defined in the foundation's
  `rules/06-numbering.md`.

## Rules

- **FH-01 One gate to the server.** Every call that reaches the game server goes through the
  `IGameActions` adapter. No other type in the codebase may send an action, a command, or a
  request to the game. This exists so the automation boundary can be reviewed by reading one
  file instead of auditing the whole plugin. Tightens S-06.

- **FH-02 Automation is always switchable.** Every automatic trigger has a setting, is
  reachable from a macro through a text command, and ships disabled. A feature that cannot be
  switched off is not finished. Tightens P-02.

- **FH-03 No network.** The plugin performs no network communication of any kind. Game data
  comes from Lumina, which reads local files. Adding any remote call is a change of project
  scope and needs a recorded decision first, not a pull request. Tightens S-04 and P-03.

- **FH-04 The core stays clean.** No type under `src/Core/` may reference a Dalamud or
  FFXIVClientStructs type, in a signature or a field. This is what keeps the logic testable
  without a running game. A violation here is a build-breaking defect, not a style issue.
  Serves T-01.

- **FH-05 The player plays.** The plugin never performs combat actions, never moves the
  character, and never joins or completes content on its own. Preparation steps around an
  action the player already took are the limit. Anything past that line is out of scope,
  regardless of how it is requested. Serves P-01 and the Dalamud restrictions in
  `rules/dalamud.md`.

- **FH-06 Traceable actions.** Every execution in `IGameActions` logs what was sent and which
  trigger caused it, manual or automatic. Extends S-10.

- **FH-07 Fail quiet, not loud.** A step that cannot run (mounting in combat, an unavailable
  level sync, a teleport without the required currency) is skipped with a log line. The plugin
  does not retry in a loop and does not spam the player. Extends S-09.

## Crashing the game is never acceptable

These four exist because the plugin took the client down twice: once from a pointer into
managed memory that the game read a frame later, and once from calling into the game off the
framework thread. Both were written, reviewed, and shipped without anyone noticing, which is
exactly why they are rules now rather than lessons.

The distinction that makes them necessary: **`try`/`catch` does not catch an access violation.**
A managed exception is recoverable, a bad pointer dereference ends the process. Wrapping unsafe
code in a `try` block looks like safety and provides none. Only the checks below actually
prevent a crash.

- **FH-08 The game is only ever called from the framework thread.** Any call into game memory
  or a game function goes through `DalamudServices.OnGameThread`. Button handlers run inside
  the ImGui draw callback, which is a different thread, so a button may never call the game
  directly. Exceptions inside that helper are logged, never rethrown: an exception escaping the
  framework thread is itself a crash.

  Reading a field out of an addon struct is not a call, and an overlay cannot avoid it: to draw
  against the game's own window it has to know where that window is, this frame. Reads of addon
  geometry from the draw callback are therefore allowed, under FH-09 and with a guard on
  `IsFrameworkUnloading`, because Dalamud's draw callback runs on the same main thread. Invoking
  a game *function* from there stays forbidden; that is what took the client down.

- **FH-09 Every native pointer is checked before it is followed.** `Instance()`, `GetInstance()`,
  and every addon lookup can return null, and an addon under construction can report a non-zero
  count for an array that does not exist yet. Check the pointer, and check any array or child
  pointer reached through it, on every path.

- **FH-10 The game never receives a pointer into managed memory.** A `fixed` block pins for the
  duration of the block; the game may keep the pointer and read it frames later. Use the
  overloads that take a managed string, or allocate in game memory (`Utf8String.FromString`)
  and free it afterwards. If an API only offers a raw pointer and no safe alternative exists,
  do not use that API.

- **FH-12 Third-party code that touches native UI memory is fenced off.** It may be used, but
  only behind a single adapter that every call goes through. That adapter switches the whole
  feature off for the session at the first failure rather than retrying, reports its own health,
  releases in a fixed order during teardown, and has a fallback so losing it costs presentation
  rather than function. Be honest about the limit: a `try` block catches a managed exception, not
  an access violation. What the fence actually buys is early detection of a changed API, one
  failure instead of one every frame, and a clean unload path.

  Today this is exactly one dependency, KamiToolKit, behind `NativeMapMarkers`.

- **FH-11 Audit before handing over a build.** Before any build goes to the owner, review every
  `unsafe` block, every `->`, every `fixed`, and every game call reachable from a UI callback,
  against FH-08 to FH-10. This is a checklist pass, not a feeling: grep for the constructs and
  look at each hit. Record the result in `status.md`.

## Since the first release

- **FH-13 `main` is never written to directly.** Every change goes onto a branch and reaches
  `main` through a pull request, including one-line fixes, documentation and release
  preparation. The rule starts with 1.0.1, the first version other people can install.

  The reason is not review for its own sake, it is that `main` is now the thing a release is cut
  from. A tag names a commit, and a commit that arrived without passing CI on its own is a
  release nobody checked. A pull request also gives every change a place where the reasoning
  lives that is not the commit message, and it makes "what went into 1.0.2" a question with an
  answer.

  Branch names say what the change is: `fix/`, `feat/`, `chore/`, `docs/`. The release
  preparation for a version is part of the branch that finishes it, not a commit that appears on
  `main` afterwards. Tags are pushed only after the merge, and only to `main`.
