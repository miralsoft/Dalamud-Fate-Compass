# Project: Fate Compass

Purpose: the identity card of the project. Who it is, what it is, which rules apply to it.

## Identity

- **Name:** Fate Compass
- **Slug:** dalamud-fate-compass
- **Owner:** Sanaka (in-game name), GitHub `miralsoft`
- **Summary:** A Dalamud plugin for Final Fantasy XIV that removes the repetitive manual steps
  around FATE farming. It shows which FATEs are active in the current zone, ranks them by how
  worthwhile they actually are, helps the player get there, and bundles the small preparation
  steps (dismount, level sync, tank stance) behind a single trigger. The player always
  performs the gameplay itself.

## Scope boundary

This plugin assists, it does not play. Every feature is designed so the final action stays
with the player. The plugin never fights, never joins a FATE on its own, and never sends
anything to the game server that the player did not trigger. See the automation rules in
`rules/dalamud.md`, which are the binding constraint on every feature in this project.

## Declared languages, frameworks, and active profiles

- **Language:** C#, see `rules/csharp.md`.
- **Platform:** Dalamud, see `rules/dalamud.md`.

The foundation ships no profile for either, so these two documents are the profiles for this
project (M-05). The foundation's PHP, JavaScript, and WordPress profiles do not apply.

## Targeted foundation version

1.2.0 (M-06).

## Code repositories

- `miralsoft/Dalamud-Fate-Compass`: the plugin. Single repository, no split.

## Hosting and deploy summary

None. The plugin is a local build installed through Dalamud's developer plugin path. There is
no server, no backend, and no network communication of any kind. This is a deliberate scope
decision, recorded in `decisions.md`.

## High-level architecture summary

Three layers. A pure core with no Dalamud types, holding all the logic worth testing (ranking,
filtering, history, route hints, the engage plan). A thin adapter layer that wraps the Dalamud
services behind interfaces. A UI layer built on the Dalamud Windowing API. Every call that
reaches the game server is funnelled through one single adapter, so the automation boundary
sits in one auditable file. The full picture is in `architecture.md`.
