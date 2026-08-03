# Decisions: Fate Helper

Purpose: append-only decision log. Never edit or delete past decisions, only add new ones. A
later decision may supersede an earlier one, but the history stays (M-08).

Each entry: date, decision, short rationale.

## Log

- (2026-08-01) Project documentation lives in `docs/` in the plugin repository, not in the
  MIRAL Soft foundation repository. Rationale: owner decision. The foundation stays the source
  of global rules only, and this project does not write back to it.

- (2026-08-01) The foundation is cloned locally to `.foundation-docs/` and excluded through
  `.git/info/exclude`. Rationale: the rules must be readable at any time, but they are
  reference material and must never end up in this repository. A local exclude is used instead
  of a `.gitignore` entry because the exclude file itself is never committed.

- (2026-08-01) Git identity for this repository is `Sanaka
  <20637644+miralsoft@users.noreply.github.com>`, set locally, not globally. Rationale: this
  is a private game plugin, so the owner's in-game name fits better than the business
  identity, and the GitHub noreply address keeps the personal address out of the repository.
  Covered by R-03, which names the identity configured for the project. The binding part of
  R-03, that no AI is ever named as author, is untouched.

- (2026-08-01) The project defines its own C# and Dalamud profiles in `docs/rules/`.
  Rationale: the foundation ships neither, and global rules C-03 and T-04 delegate the linter
  and test runner to a language profile. Without these files those rules would have nothing to
  point at.

- (2026-08-01) Both foundation git hooks are installed and configured through
  `.miralsoft-enforcement`. Rationale: R-08. Verified by direct invocation rather than assumed:
  the commit-msg hook rejects AI markers, the pre-commit hook accepts the configured identity
  and blocks a staged test secret.

- (2026-08-01) No backend, no network communication, and FATE data limited to the current
  zone. Rationale: the game client only receives FATE data for the zone the player is in, so
  cross-zone awareness would require an own server with crowdsourced data. That would pull in
  the full platform backend requirements for a feature that is not needed for farming a single
  zone. Recorded as FH-03.

- (2026-08-01) The engage sequence (dismount, level sync, tank stance) is built once with two
  triggers: a manual text command and an optional automatic trigger on entering a FATE. The
  automatic trigger ships disabled. Rationale: the Dalamud restriction targets server
  interaction "without direct interaction from the user". The manual trigger is unambiguously
  clean. The owner assessed the automatic variant as acceptable, since the player entering the
  FATE is itself the initiating action and the plugin only removes a click rather than playing
  the game. Both paths run identical code, so the difference stays a configuration question
  and remains visible in the log.

- (2026-08-01) Sharing a FATE position uses the client-side map flag plus the game's own
  `<flag>` chat placeholder. The plugin sets the flag marker through `AgentMap` and never
  touches the chat input or sends a message. Rationale: it reaches the same result as chat
  manipulation with zero server contact, and the player still presses send.

- (2026-08-01) First version is 0.1.0, developed on `main` only, with no branching strategy.
  Rationale: owner decision, and it matches the foundation default of a single `main` branch
  through to a project's first finished version.

- (2026-08-01) Scope of 0.1.0 includes the ranking by remaining time and progress, the filter
  by FATE kind, the bicolor gemstone counter with cap warning, and the local FATE history with
  respawn estimation. Rationale: owner selected all four proposed additions, and each is
  display-only logic in the core layer with no effect on the automation boundary.

- (2026-08-01) The interface is multilingual from the start, shipping German and English, with
  further languages possible. Rationale: owner requirement, and C-05 asks for it anyway.
  Retrofitting translation into a finished UI is expensive, so it went in before the UI rather
  than after.

- (2026-08-01) Translation lives in `FateHelper.Core`, not in the plugin project: string keys
  as constants in `StringKeys`, catalogues as embedded JSON under
  `Localization/Catalogs/<code>.json`, resolution in `Localizer` with the chain active
  language, then English, then the key itself. Adding a language means adding one JSON file
  and changing no code. Rationale: keeping it in the core means the tests can verify catalogue
  completeness without a game installation, and they do: a key declared in `StringKeys` that
  is missing from any catalogue fails the build, as does a stray key no longer declared.

- (2026-08-01) FATE level sync is triggered with the text command `/levelsync on` through
  `RaptureShellModule.ExecuteCommandInner`, and the outcome is verified afterwards with
  `FateManager.IsSyncedToFate`. Rationale: the reflection pass showed that the client structs
  have no function for performing a FATE level sync, because every member named after level
  sync belongs to duty content. The text command is the same mechanism a player uses by hand
  or in a macro, so it adds no capability beyond what the keyboard already offers. Verifying
  the state afterwards means the step reports honestly rather than assuming success, and it is
  skipped entirely when the sync is already in place.

- (2026-08-01) Tank detection reads `ClassJob.Role` from the game's own data rather than a
  hard-coded list of job ids, so a future job is classified correctly without a code change.
  The stance action and status ids stay as named constants, because the game data carries no
  flag that identifies a stance. Rationale: derive what can be derived, and make the rest
  obvious in one place instead of scattering magic numbers.

- (2026-08-01) Bozja, Zadnor, and the Occult Crescent are supported by mapping
  `DynamicEventContainer` entries onto the existing snapshot type rather than by building a
  second pipeline. Rationale: the two systems differ in the client but not to the player, who
  only wants to know whether something nearby is worth going to. Ranking, filtering, history,
  and routes therefore work on both unchanged, and an `ActivitySource` records the origin so
  the UI can label it and the engage sequence can adapt. The type kept the name
  `FateSnapshot`: a rename across the whole codebase would have been churn without changing
  behaviour, and the summary documents what it actually covers.

- (2026-08-01) Eureka needs no dedicated support. Its notorious monsters are ordinary FATEs
  and already arrive through the FATE table. Only the ranking needs checking in practice,
  because Eureka uses elemental levels and the level column will read oddly.

- (2026-08-01) An open registration window is carried as its own flag on the snapshot and
  scored with a bonus, rather than folded into the lifecycle state. Rationale: `Register`,
  `Warmup`, and `Battle` all mean the activity is live, so they all map to running. But
  registration is the only state a player can miss permanently, which makes it worth ranking
  and announcing differently rather than hiding inside an enum value.

- (2026-08-01) When both a zone mount restriction and combat block a remount, the zone
  restriction is reported. Rationale: combat ends, the restriction does not, so naming combat
  would imply that waiting helps. A test locks the order in.

- (2026-08-01) The API surface was verified by reflecting over the locally installed Dalamud
  15.0.3 rather than trusting documentation or recollection, and the findings are recorded in
  `rules/dalamud.md`. Rationale: I-10. This immediately caught one error that would otherwise
  have been written into the adapters: `IClientState.LocalPlayer` no longer exists in v15, the
  local player now comes from `IObjectTable.LocalPlayer`.

- (2026-08-03) The plugin is licensed AGPL-3.0-or-later. Rationale: copyleft rather than a
  permissive licence, because the failure mode this ecosystem actually sees is somebody taking
  an open plugin, renaming it and putting it behind a paywall or a closed community, which MIT
  expressly permits. AGPL rather than GPL because of the network clause: the plugin does no
  networking today, so the clause costs nothing, but the cross-zone FATE awareness noted in
  `open-points.md` as impossible without a backend is exactly the case it covers. It is also the
  licence Eorzea Arsenal uses, so one answer covers every plugin here. The cost, recorded so it
  is not a surprise later: nobody can lift parts of this into a closed plugin, and relicensing
  would need the agreement of everyone whose contributions were accepted.

- (2026-08-03) The plugin icon is one file, `src/FateHelper/images/icon.png`, feeding three
  destinations: copied beside the packaged plugin for Dalamud's list, referenced by `IconUrl` in
  the manifest for the repository index, and embedded into the assembly for the minimap button.
  Rationale: Dalamud resolves plugin icons two ways and layers its own status badges on top, so
  the same picture has to arrive through more than one route. Keeping several copies of it, or
  writing `IconUrl` in both the plugin manifest and the index, is how the routes end up
  disagreeing. The index entry is generated from the built manifest for the same reason.

- (2026-08-03) The minimap button reads its icon from an embedded resource rather than from a
  file next to the plugin. Rationale: the packaged zip does not carry the image, so a
  path-based load works in a development build and silently falls back to a game glyph for
  everyone who installed the plugin normally. That difference is invisible to whoever built it,
  which is the worst kind.
