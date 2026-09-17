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

4.1.0 (M-06). Reviewed whenever a release is cut (M-17): read the foundation changelog from
this version onward, then either raise it and do the work, or leave it and record why.

Raised from 3.1.0 on 2026-09-16, with the work done rather than deferred. What the four
versions in between asked of this project, and what each one cost:

- **3.2.0** widened M-18 so a hosted project's agents may write its own documentation folder in
  the foundation. This project is external, so it never had that problem and gains nothing.
- **3.3.0** added R-22, ask before a git action starts a metered CI run. Behavioural, and it
  binds the agent rather than the code: every push, pull request and merge is asked for
  separately from the approval of what the change contains.
- **4.0.0** added the whole `O` operations area, S-14 to S-16, and R-23. The `O` area does not
  bind here and says so in its own scope statement: it governs software operated out of its
  repository, while a plugin somebody downloads is governed by `D`. S-14 and S-15 concern
  secrets this plugin does not have, since it holds none and reaches no network (FH-03). S-16
  forbids a development-only door in a deployment, which C-10 already answers here in the
  stronger form: the developer surface is absent from a released build rather than switched off
  in it, and CI proves it. **R-23 was the one that cost work**, and it found two real holes
  rather than none. The C# profile's new "Operational configuration" section states outright
  that a plugin has no instance configuration in O-03's sense.
- **4.1.0** added D-17, which constrains what a user-facing announcement may report. The
  existing release notes were read against it and hold, so it binds what gets written from here
  on rather than obliging a rewrite.

Raised from 2.0.0 on 2026-08-16. 3.0.0 asked four things of a project: an audit by reachability
rather than by grep (R-21), test values taken from the reality they describe (T-07), an install
instruction naming the address the channel documents (D-15), and endpoint checks that look at
the payload (D-16). The last two already held. The first found three real defects and they are
fixed. 3.1.0 permits something rather than requiring it, so it asked for nothing.

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
