# Project: Fate Compass

Purpose: the identity card of the project. Who it is, what it is, which rules apply to it.

## Identity

- **Name:** Fate Compass
- **Slug:** dalamud-fate-compass
- **Owner:** Sanaka (in-game name), GitHub `miralsoft`
- **Kind:** external (M-12). This repository holds both the plugin and its documentation, in
  `docs/`. The foundation carries no folder and no entry for it, and nothing is ever written
  back there. The entrypoint at the repository root (`CLAUDE.md`) is what tells an agent
  starting here that the foundation exists.
- **Committer identity:** `Sanaka`, a private project (R-03, R-19). The email is the GitHub
  noreply address, so no personal address is exposed in the repository. `.miralsoft-enforcement`
  at the root matches this and the hooks check it.
- **Summary:** A Dalamud plugin for Final Fantasy XIV that removes the repetitive manual steps
  around FATE farming. It shows which FATEs are active in the current zone, ranks them by how
  worthwhile they actually are, helps the player get there, and bundles the small preparation
  steps (dismount, level sync, tank stance) behind a single trigger. The player always
  performs the gameplay itself.

## Scope boundary

This plugin assists, it does not play. Every feature is designed so the final action stays
with the player. The plugin never fights, never joins a FATE on its own, and never sends
anything to the game server that the player did not trigger. See the automation boundary in
`.foundation-docs/rules/frameworks/dalamud.md`, which is the binding constraint on every
feature in this project, and FH-01 to FH-07 in `rules-project.md`, which tighten it.

## Declared languages, frameworks, and active profiles

- **Language:** C#, see `.foundation-docs/rules/languages/csharp.md`.
- **Platform:** Dalamud, see `.foundation-docs/rules/frameworks/dalamud.md`.
- **Blueprint:** `.foundation-docs/blueprints/dalamud-plugin.md`, binding under M-14.

Both profiles live in the foundation and are read from there (M-05). The foundation's PHP,
JavaScript, and WordPress profiles do not apply. This project once carried its own copies under
`docs/rules/`, written because the foundation had none; foundation 2.0.0 added both, so the
copies were retired rather than kept as a second source that would drift. What was genuinely
this project's own research rather than a rule, the reflected Dalamud API surface, moved to
`platform-notes.md`.

## Targeted foundation version

2.0.0 (M-06). Reviewed whenever a release is cut (M-17): read the foundation changelog from
this version onward, then either raise it and do the work, or leave it and record why.

## Code repositories

- `miralsoft/Dalamud-Fate-Compass`: the plugin. Single repository, no split.

## Hosting and deploy summary

No server and no backend. The plugin itself performs no network communication of any kind,
which is a deliberate scope decision recorded in `decisions.md`.

Distribution is a chain of static files rather than a service: a tagged release publishes the
zip and a manifest as assets, an aggregate index repository collects them, and a player adds
that index to Dalamud as a third-party repository. The chain, its two silent delays, and the
steps for the very first release of a plugin are in `release.md`, which owns them. A local
build installed through Dalamud's developer plugin path is the development case, not the
distribution one.

## High-level architecture summary

Three layers. A pure core with no Dalamud types, holding all the logic worth testing (ranking,
filtering, history, route hints, the engage plan). A thin adapter layer that wraps the Dalamud
services behind interfaces. A UI layer built on the Dalamud Windowing API. Every call that
reaches the game server is funnelled through one single adapter, so the automation boundary
sits in one auditable file. The full picture is in `architecture.md`.
