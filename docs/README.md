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
├── test-plan.md       the manual checks, ordered by risk
└── rules/
    ├── csharp.md      C# language profile: toolchain, linters, analysis, tests, security
    └── dalamud.md     Dalamud platform profile: versions, lifecycle, data access, restrictions
```

## Project memory

The foundation requires a set of project memory files that carry state between working
sessions and between people (M-03, M-08, I-07): `project.md`, `architecture.md`,
`rules-project.md`, `todos.md`, `decisions.md`, `status.md`, and `open-points.md`.

They belong in this folder and are not written yet, because the project's purpose and scope
have not been defined. They are created once that is settled, rather than filled with
placeholder text.

## Enforcement

`.miralsoft-enforcement` in the repository root configures the git hooks in `.git/hooks/`.
The hooks check the committer identity, scan staged content for secrets, and reject
AI-attribution markers in commit messages. They are installed per clone and are not part of
the repository, so a fresh clone has to install them again:

```sh
cp .foundation-docs/enforcement/hooks/commit-msg .git/hooks/commit-msg
cp .foundation-docs/enforcement/hooks/pre-commit .git/hooks/pre-commit
```

Note that the shared `pre-commit` hook runs PHP and JavaScript linters only. The C# check
(`dotnet format --verify-no-changes`) is not covered by it, see `rules/csharp.md`.
