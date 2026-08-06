# Changelog

All notable changes to this project are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html) (C-06, C-08).

## [Unreleased]

## [1.1.0] - 2026-08-04

### Added

- **Routes account for how high an aetheryte stands.** Until now the leg from an aetheryte to a
  FATE was measured flat, so a target on a plateau looked closest to the aetheryte directly
  underneath it, which is the one place you cannot walk from. Reported from play on 2026-08-01
  and open ever since. The nearest aetheryte is now chosen by travel distance rather than by map
  distance, with a climb weighted exactly as the ranking already weighted the player's own
  approach (`VerticalTravelWeight`).

  The note this was planned from proposed scanning the `Level` sheet and matching by proximity.
  That was checked against the game data before being built and does not hold: of 108 visible
  aetherytes only 15 are reachable through `Level.Object`, and `Aetheryte.Level` points at row
  ids like 3785149 against a sheet of 61346 rows. There is no static table to read.

  What works is not a data file at all. The aetheryte is a physical object standing in the zone,
  and the object table reports its real position. The catch is that the game only loads objects
  near the player, so heights are **learned rather than looked up**: whatever is in range is
  remembered and persisted across sessions, and the table fills in as a zone gets played. An
  aetheryte nobody has walked past measures flat, exactly as before, so the feature degrades into
  the old behaviour rather than into a wrong answer. Both sides have to be known for a climb to
  count, and setting the weight to zero restores the old measurement exactly.

- **The assumed cost of a teleport drops from 15 seconds to 10**, timed rather than estimated.
  A third of the fixed cost is exactly the amount that decides the close calls: at twenty yalms
  a second it moved the break-even point by a hundred yalms, and every route inside that band
  was advised the wrong way round.

  Existing configurations are corrected, which this project otherwise does not do for a changed
  default. The reasoning: fifteen was never a preference anybody expressed, it was a measurement
  the plugin got wrong and then wrote into every configuration it created, so leaving it would
  mean the correction reaches nobody who already installed. It is replaced only where it still
  stands at exactly the old default. A value somebody moved is a decision and stays theirs.

- **Returning to camp is costed at 8 seconds rather than 20**, timed in the Crescent where it
  comes in at seven to eight. The old figure was written down as a guess at the time and behaved
  like one: with the second leg on top, a trip through the exploratory zones was costed at thirty
  seconds, nearly twice what it is, so every close call there came out as "just fly".

  The upper end of the measured range on purpose. Overstating the cost errs towards flying, and
  that is the cheaper mistake: a flight taken when a return would have been quicker costs
  seconds, while a return taken when flying would have done costs its cooldown, which is measured
  in minutes.

  Corrected in existing configurations where it is still the old default, on the same reasoning
  as the teleport overhead above.

- **The route tooltip now shows its working**: the aetheryte it compared against, how far that
  is from the FATE, and both timings, in every case. Previously, when the verdict was "go
  direct" it said only that, so there was no way to tell which aetheryte had been rejected or by
  how much. A recommendation you cannot check is one you can only believe, which is exactly the
  wrong footing for the change above.

- **A direction needle under every entry**, turning with the character's facing so that up means
  straight ahead. It answers the question the list was silent about: the distance says how far,
  the needle says which way, and together they say whether the flight is going anywhere useful.

  The character's facing, not the camera's. The camera answers "where am I looking", which
  swings while you glance around mid-flight; the character answers "where would I go if I held
  forward", and that is the question somebody in the air is asking.

  It fades from grey to green over the last 45 degrees rather than switching at a threshold. A
  threshold flickers exactly when the heading sits on it, which is exactly while you are turning
  onto the target and watching, and a fade also says "warmer" on the way there.

  Once you are inside the objective's circle the needle becomes a ring. "Inside" is the FATE's
  own radius, which the game reports, not a distance chosen here: FATEs run from a courtyard to
  most of a field, so any single number is wrong for nearly all of them. The first attempt used
  a fixed fifteen yalms and still said "not yet" while the player was fighting in the middle of
  one.

  It is drawn from the facing as it is this frame rather than from the twice-a-second poll the
  rest of the window uses. That rate is plenty for a countdown and useless for something that
  follows a turning character.

  The needle points along the straight line. That is the whole idea and also its limit: it
  answers "am I heading at it", never "can I get there this way".

## [1.0.2] - 2026-08-03

One repair, in two parts: the travel button works in the exploratory zones, and it works all the
way through rather than stopping at a prompt. Patch rather than minor on purpose. Nothing here is
new ground, it is a button that was supposed to do this and did not.

### Changed

- **`main` is never written to directly any more.** Every change reaches it through a pull
  request, including one-line fixes and release preparation. From 1.0.1 onwards `main` is what a
  release is cut from, and a commit that arrived without passing CI on its own is a release
  nobody checked. Recorded as FH-13 in `docs/rules-project.md`.

### Fixed

- The travel button did nothing in Eureka, Bozja and the Occult Crescent. Those zones have no
  aetherytes that can be teleported to, so their travel points carry no identifier, and the
  button deliberately sent nothing rather than hand the game a destination it would refuse. That
  was defensible and still wrong: a travel button that does nothing reads as broken. It now casts
  Return, which is what a teleport is in those zones. The leg from the camp out to the waypoint
  stays with the player, through the camp's own travel menu, and the tooltip says so. The button
  is labelled "Return" there rather than "Teleport", because it does something different.

  The general action id was read out of the game's own `GeneralAction` sheet rather than
  remembered: 8 is Return, right beside 7 for Teleport, and the same lookup confirmed the 9 and
  23 this plugin already used for mount and dismount.

- The same button then stopped at the game's "return to your starting point?" prompt, which is
  half a repair. That prompt is now answered with yes: only that one, only while a Return this
  plugin sent is still unanswered, only once, and switchable under Automation. Every other
  yes/no prompt passes through untouched.

  Worth stating plainly, because it is the only place this plugin operates a game window rather
  than sending an action: Dalamud's published restrictions name dialog boxes among the things
  plugins should not answer, and none of the 479 plugins in the official repository does this.
  It is here because the owner decided the line that matters is whether the plugin *begins*
  something, and this begins nothing. It finishes a request made a second earlier by pressing a
  button. That is a defensible reading and it is not the only one, which is why there is a
  switch and why this paragraph exists. Reasoning in `docs/decisions.md`.

## [1.0.1] - 2026-08-03

The release 1.0.0 should have been is. That version carried an internal name nobody could install
under, so this is the first one that actually reaches a plugin list.

### Changed

- **Renamed from Fate Helper to Fate Compass**, internal name `FateCompass`. Not a preference: the
  official Dalamud repository already carries `FATEhelper`, "FATE Helper" by Teechep Bird, and
  Dalamud compares internal names without regard to case. It therefore refused our repository
  entry outright, which the client log recorded four times, and 1.0.0 could not be installed by
  anybody. It worked here only because a development plugin bypasses the index. The display name
  changed too: two plugins with the same name in one list is a problem for the player whichever
  one is technically allowed, and we are the second. Full account in `docs/decisions.md`.
- The command is now `/fatecompass`, with `/fate` as a short form. `/fc` was not available, the
  game uses it for free company chat. The short form is registered but allowed to fail, so
  another plugin owning `/fate` costs a convenience rather than the plugin.

### Added

- Licensed under AGPL-3.0-or-later, with the reasoning recorded in `docs/decisions.md`.
- The plugin has an icon: a gold emblem on deep violet, a needle pointing into a glowing ring.
  It appears wherever Dalamud shows the plugin. The minimap button keeps the game's own FATE
  marker, which players already read as "FATE" and which is drawn for that size.
- `scripts/prepare-icons.ps1`, which brings new artwork into the shape Dalamud needs: exactly
  512 by 512, which is a hard requirement and not visible in an image viewer.
- A test binding `CHANGELOG.md` to the version being built: the newest section has to name it,
  and every version described to players in the release notes has to appear here.
- `IconUrl` in the manifest, written in exactly one place and reaching both the installed entry
  and the repository listing from there. Two hand-kept copies is how the two views end up showing
  different pictures. Details in `docs/decisions.md`.
- The minimap defaults are the values dialled in against a live HUD and read back out of the
  saved configuration, rather than the first estimate: size 36, offset 60 by 18.

### Fixed

- The minimap button was cut off on its right and bottom edges. The window is sized to the icon
  exactly, but ImGui insets the content by the window padding while still clipping at the
  window's edge, so an image drawn at the window's own size lost a fifth of itself on two sides.
  A plain game glyph survived that well enough to hide it.

## [1.0.0] - 2026-08-03 [RETIRED]

First public release, and unusable. Its internal name `FateHelper` collides with the official
plugin `FATEhelper`, so Dalamud refused the repository entry and nobody could install it. Kept
here as a record rather than deleted; the release itself is withdrawn. Superseded by 1.0.1.

The developer surface (`DebugWindow.Enabled`) is off from this build onwards; the `/fate debug`
probes still write to the Dalamud log, which is what a problem report is built from.

### Added

- Repository scaffold: solution layout, shared build settings, formatting standard,
  dependency pinning, and the xUnit test project.
- Project documentation under `docs/`, including the C# and Dalamud rule profiles.
- Core logic, with no dependency on the game or the plugin framework:
  - FATE ranking that weighs distance, remaining time, and completion, and reports why a
    FATE was excluded rather than silently hiding it.
  - Filtering by FATE kind and level range.
  - Route hints naming the aetheryte nearest to a FATE, including whether teleporting is
    actually faster than travelling.
  - Engage planning for dismount, level sync, and tank stance, plus the remount case with its
    combat and cast guards.
  - Local FATE history with a respawn estimate that stays hidden until it has enough data.
  - Bicolor gemstone tracking against the cap.
  - Multilingual interface with German and English, resolved through a key catalogue with a
    fallback chain. Adding a language means adding one JSON file.
- Game adapters: FATE and player state reading, aetheryte lookup from local game data, the map
  flag, notifications, and a single gated component for everything that reaches the game
  server.
- Windows for the ranked FATE list and the settings, plus the `/fate` command family so the
  plugin can be driven from a macro without opening anything.
- `build.ps1`, which runs the format check, the build, and the tests, then prints the path to
  register as a Dalamud dev plugin.
- Support for the instanced engagement content of Bozja, Zadnor, and the Occult Crescent:
  skirmishes, critical engagements, and critical encounters appear in the same list as FATEs,
  with the number of players taking part, and with an open registration window called out and
  ranked ahead of everything else, because it is the one thing that can be missed outright.
  Eureka notorious monsters were already covered, being ordinary FATEs.
- A "What's new" window, reached from a scroll icon in the main window's title bar or with
  `/fate news`. It lists the releases newest first and labels every line as new, changed, fixed, or
  removed, so the three questions people arrive with are answered by sorting rather than reading.
  It opens by itself once after an update, never on a first installation, and the icon is
  highlighted until the notes have been looked at.
- Player-facing release notes as embedded data, one JSON file per language under
  `src/FateCompass.Core/News/Notes`, deliberately separate from this file: this one is written for
  whoever works on the plugin, that one for whoever plays with it. A test fails the build when the
  newest notes do not describe the version being built, or when the languages disagree about which
  versions exist.
- `README.md`, written for players rather than for the repository: what the plugin does, how to
  install it from the plugin repository, and where the automation boundary lies.
