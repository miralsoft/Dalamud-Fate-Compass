# Documentation

All documentation for this plugin lives here. Nothing is written back to the MIRAL Soft
foundation repository.

## Where the rules come from

Rules apply in two layers:

1. **Global**, from the MIRAL Soft foundation repository
   (https://github.com/MIRAL-Soft/miralsoft-foundation-docs). Binding and read-only. These
   are the principles (`P`), process and git (`R`), security (`S`), code quality (`C`), AI
   conduct (`I`), and testing (`T`) rules. They are never copied into this repository, they
   are referenced by number.
2. **Project-local**, in `rules/` next to this file. These add to the global rules and may
   tighten them, never weaken or contradict them (M-01, M-04).

The foundation ships no C# profile and no Dalamud profile, so this project defines both. Where
a global rule points at a language profile for a detail, for example C-03 for linters or T-04
for the test runner, the files in `rules/` are that place.

The foundation profiles for PHP, JavaScript, and WordPress do not apply here and are ignored,
as are the rules belonging to other projects.

## Layout

```
docs/
├── README.md          this file
├── project.md         the identity card: kind, profiles, targeted foundation version
├── architecture.md    the three layers and why the boundaries sit where they do
├── rules-project.md   FH-01 upwards: this project's own rules, tightening the global ones
├── status.md          where the work stands
├── decisions.md       what was decided and why, append-only, including rejected paths
├── todos.md           what is planned
├── open-points.md     what is unresolved, and what was deliberately deferred
├── platform-notes.md  the Dalamud API surface this plugin depends on, read by reflection
├── release.md         the release runbook and the chain into the plugin directory
└── test-plan.md       the manual checks, ordered by risk
```

The language and platform profiles are not here. They live in the foundation, at
`.foundation-docs/rules/languages/csharp.md` and `.foundation-docs/rules/frameworks/dalamud.md`,
together with the construction blueprint at `.foundation-docs/blueprints/dalamud-plugin.md`.
This project carried its own copies under `docs/rules/` until foundation 2.0.0 shipped both;
keeping them would have left two sources for the same facts. See `CLAUDE.md` in the repository
root for how the foundation is cloned and in which order it is read.

## Project memory

The foundation requires a set of project memory files that carry state between working
sessions and between people (M-03, M-08, I-07): `project.md`, `architecture.md`,
`rules-project.md`, `todos.md`, `decisions.md`, `status.md`, and `open-points.md`.

All seven are present, listed above. Start with the four that hand over state: `status.md`
for where things stand, `decisions.md` for why they stand that way, `todos.md` for what is
planned, and `open-points.md` for what is unresolved.

## Enforcement

`.miralsoft-enforcement` in the repository root configures the git hooks in `.git/hooks/`.
The hooks check the committer identity, scan staged content for secrets, and reject
AI-attribution markers in commit messages. They are installed per clone and are not part of
the repository, so a fresh clone has to install them again:

```sh
cp .foundation-docs/enforcement/hooks/commit-msg .git/hooks/commit-msg
cp .foundation-docs/enforcement/hooks/pre-commit .git/hooks/pre-commit
```

Note that the shared `pre-commit` hook runs PHP and JavaScript linters only. The C# check is
`dotnet format --verify-no-changes`, which the shared hook cannot perform, so it runs in
`build.ps1` and in CI instead. That is what R-17 asks for: a check the shared hook cannot do is
named in the language profile and wired into the build and the pipeline, rather than quietly
going unenforced. See `.foundation-docs/rules/languages/csharp.md`.
