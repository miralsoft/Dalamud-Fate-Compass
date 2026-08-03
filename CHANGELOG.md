# Changelog

All notable changes to this project are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html) (C-06, C-08).

## [Unreleased]

## [1.0.0] - 2026-08-03

First public release, published through
[miralsoft/Dalamud-Plugins](https://github.com/miralsoft/Dalamud-Plugins). The developer surface
(`DebugWindow.Enabled`) is off in this build; the `/fh debug` probes still write to the Dalamud
log, which is what a problem report is built from.

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
- Windows for the ranked FATE list and the settings, plus the `/fh` command family so the
  plugin can be driven from a macro without opening anything.
- `build.ps1`, which runs the format check, the build, and the tests, then prints the path to
  register as a Dalamud dev plugin.
- Support for the instanced engagement content of Bozja, Zadnor, and the Occult Crescent:
  skirmishes, critical engagements, and critical encounters appear in the same list as FATEs,
  with the number of players taking part, and with an open registration window called out and
  ranked ahead of everything else, because it is the one thing that can be missed outright.
  Eureka notorious monsters were already covered, being ordinary FATEs.
- A "What's new" window, reached from a scroll icon in the main window's title bar or with
  `/fh news`. It lists the releases newest first and labels every line as new, changed, fixed, or
  removed, so the three questions people arrive with are answered by sorting rather than reading.
  It opens by itself once after an update, never on a first installation, and the icon is
  highlighted until the notes have been looked at.
- Player-facing release notes as embedded data, one JSON file per language under
  `src/FateHelper.Core/News/Notes`, deliberately separate from this file: this one is written for
  whoever works on the plugin, that one for whoever plays with it. A test fails the build when the
  newest notes do not describe the version being built, or when the languages disagree about which
  versions exist.
- `README.md`, written for players rather than for the repository: what the plugin does, how to
  install it from the plugin repository, and where the automation boundary lies.
