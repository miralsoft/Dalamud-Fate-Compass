# Decisions: Fate Compass

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

- (2026-08-01) Translation lives in `FateCompass.Core`, not in the plugin project: string keys
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

- (2026-08-03) The plugin icon is one file, `src/FateCompass/images/icon.png`, feeding three
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

- (2026-08-03) Supersedes the one-file icon decision above: there are two images, not one.
  `images/icon.png` keeps the frame and the dark backdrop and serves Dalamud's plugin list;
  `images/minimap.png` is the same emblem cut out, with a real alpha channel, and is what the
  minimap button draws. Rationale: the two places want genuinely different pictures. In a list
  the icon owns a tile and its frame gives it an edge; over the HUD a frame competes with the
  minimap around it, and a backdrop would be a coloured box sitting on the game. The cost is
  that two files now have to be kept in step by hand, so replacing one and not the other shows
  two different pictures in two places. `scripts/prepare-icons.ps1` at least makes their shape
  automatic.

- (2026-08-03) Incoming artwork is normalised by `scripts/prepare-icons.ps1` rather than by hand,
  and the repository holds only finished images. Rationale: both files have requirements that are
  invisible in an image viewer. Dalamud discards an icon that is not exactly 512 by 512 square
  and shows its default instead, and generated artwork arrives on whatever canvas the generator
  felt like, so the cut-out version has to be found by its alpha channel and squared before it is
  scaled. Written down as a script because those are exactly the rules nobody remembers a year
  later.

- (2026-08-03) Supersedes the two-file icon decision above: the minimap button uses the game's
  own FATE marker again, and the plugin's emblem exists only as `images/icon.png` for the plugin
  list. Rationale: two reasons, and the second decided it. A borrowed glyph is drawn by the
  people who drew the rest of the interface, for exactly that size, so it sits on the HUD without
  effort; an own mark is always a little foreign there. And it already means something. Every
  player reads the FATE marker as "FATE" without learning anything, where an own emblem means
  nothing until it has been used for a while, and a HUD is read in a quarter of a second. The
  plugin's identity does not need that button: it lives in the plugin list, the window title and
  the settings, where the emblem is large enough to work. What we give up is that nothing on the
  HUD distinguishes this plugin from another, which is the right trade at 32 pixels.

- (2026-08-03) Icons are judged at the size they are drawn at, not at the size they arrive in.
  Three rounds of artwork were needed here, and each one looked fine at 1024 and failed at 32.
  Rendering a candidate at 24, 32 and 44, in colour and in the greyed-out state, answers in
  seconds what otherwise takes a rebuild and a look in game. An ornate emblem spends its pixels
  on frames and interiors; a HUD glyph is one shape with a hard outline, and that difference only
  becomes visible in a small render.

- (2026-08-03) The plugin is renamed from Fate Helper to Fate Compass, internal name
  `FateCompass`. Rationale: not taste, a collision. The official Dalamud repository already
  carries `FATEhelper`, "FATE Helper" by Teechep Bird, and Dalamud compares internal names
  without regard to case. The evidence was in the client's own log, four times over:
  "The repository https://xivarsenal.app/plugins.json tried to replace the plugin FateHelper,
  which is already installed through the official repo - this is no longer allowed for security
  reasons." So the v1.0.0 release could not be installed by anybody; it only worked here because
  it was loaded as a development plugin, which bypasses the index. The display name is changed
  as well as the internal one: two plugins of the same name in one list is a problem for the
  player regardless of which one is technically allowed, and we are the second.

  The new name says what the plugin does rather than what it is, and it matches the artwork that
  already existed: a needle pointing into a ring.

- (2026-08-03) The command is `/fatecompass`, with `/fate` registered as a short form and
  allowed to fail. Rationale: `/fc` is the game's free company chat and cannot be taken. A short
  command is worth having and is exactly the kind another plugin may already own, so the long
  form is the one the plugin relies on and the short one is a convenience that is checked rather
  than assumed.

- (2026-08-03) The name collision was found by reading the client log rather than by inspecting
  our own code. The visible symptom was a missing icon, and two rounds of looking at icon
  plumbing found nothing because the plumbing was correct. Worth remembering: when something
  displays wrongly and the code that produces it checks out, the next place to look is what the
  host says about us, not what we say about ourselves.

- (2026-08-03) The plugin answers the game's confirmation prompt after a Return that it cast
  itself. Decided by the owner against my recommendation, and recorded here with both sides
  because that is what this log is for.

  Against: Dalamud's published restrictions list "Skip Cutscenes/Dialogs" among the things that
  violate the automation rules, and name dialog boxes explicitly. A check of the official
  repository found none of its 479 plugins doing this; the well known plugin that does,
  YesAlready, is distributed outside it. And once a plugin can answer one window, the question
  stops being whether it operates windows and becomes which ones, which is a line that has to be
  redrawn at every new case.

  For: the prompt exists because the player pressed the travel button, and the answer was
  decided by that press. The plugin begins nothing here; it finishes a request that is one
  second old. Refusing to complete it costs the player a keystroke at the exact spot their hand
  already is.

  The implementation is the narrowest version of the idea, so that the reading above stays true:
  one named window, only while a Return this plugin sent is unanswered, only within five
  seconds, once, and the latch is cleared before anything else can go wrong. It is switchable
  (FH-02) and it ships on, because it only ever completes the player's own request.

  What this costs is a line in the README's "what it will not do", which has been rewritten to
  say what actually happens rather than a version of it that reads better.
