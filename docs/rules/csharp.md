# C# profile

Applies to this project because it declares C#. Adds to the global rules of the MIRAL Soft
foundation and never weakens them (M-01, M-04). This profile is platform-neutral: everything
that Dalamud itself imposes lives in the Dalamud platform profile (`dalamud.md`), which this
project activates in addition.

The foundation carries no C# profile, so this document takes that role for this project.
Where a global rule names a language profile as the place that defines a detail (C-03 for
linters, T-04 for the test runner), this file is that place.

## Toolchain

- **Target:** .NET 10. The exact pin comes from the Dalamud platform profile, because the
  runtime is dictated by the Dalamud version the plugin is built against.
- **SDK:** .NET SDK 10.0.101 or newer. The 10.0.100 SDK is known to have package restore
  problems and must not be used.
- Pin the SDK in a `global.json` at the repository root, so every developer machine and every
  CI run builds with the same version (S-08, R-13).
- x64 only. The plugin runs inside a 64-bit game process, so no other platform target is
  meaningful.

## Coding standard and linters

- A single `.editorconfig` at the repository root is the standard. It is the source of truth
  for formatting, naming, and analyzer severities. Do not scatter per-project overrides.
- `dotnet format --verify-no-changes` must pass. This is the check C-03 refers to for C#.
  It exits non-zero when files would have been reformatted, which makes it usable in a hook
  and in CI without extra tooling.
- Nullable reference types are enabled solution-wide (`<Nullable>enable</Nullable>`). A
  nullable warning is a real defect, not noise.
- Warnings are errors (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`). Suppressing a
  diagnostic requires a comment naming the reason. A blanket suppression file is not
  acceptable.

## Static analysis

- The built-in .NET analyzers are enabled (`<EnableNETAnalyzers>true</EnableNETAnalyzers>`)
  with `<AnalysisLevel>latest-recommended</AnalysisLevel>` as the floor. A project may raise
  this, never lower it.
- This is the C# counterpart to the PHPStan requirement in the foundation's PHP profile: the
  build fails on analyzer findings rather than reporting them.

## Dependencies

- Keep the dependency list minimal and justify every addition (S-04).
- `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>`, and the resulting
  `packages.lock.json` is committed. Restores in CI use `--locked-mode` so an unnoticed
  version drift fails the build (S-08).
- NuGet sources are pinned in a repository-level `NuGet.config` to `https://api.nuget.org`
  only. No implicit machine-wide feeds (S-05, S-08).
- `dotnet list package --vulnerable --include-transitive` runs in CI and fails the build on a
  finding (S-08).
- Assemblies that Dalamud already provides are never bundled. See `dalamud.md`.

## Tests

- **Runner:** xUnit, invoked with `dotnet test`. This is the runner T-04 refers to for C#.
- Be honest about what is testable here. Code that calls into Dalamud services or game memory
  cannot run outside the game process, and T-01 exempts code with no testable logic. The
  consequence is a structural rule, not an excuse:
  - Pure logic (rules, filters, sorting, timers, distance and route math, state machines,
    configuration handling) lives in plain classes with no Dalamud types in their signatures,
    and is unit tested.
  - Everything that touches Dalamud sits in a thin adapter layer behind an interface. The
    adapter stays as close to a pass-through as possible, because it cannot be covered.
- A bug fix ships with a test that fails without the fix (T-02). If a bug is only reproducible
  in the game, record the manual reproduction steps in the fix's commit body instead, and say
  plainly that it is not covered automatically.

## Security specifics

- Validate every value that crosses a boundary: configuration files, imported presets, any
  network response, and anything read out of the game that is then used as an index, a path,
  or a size (S-01).
- No dynamic code execution. No `Reflection.Emit`, no compiled expressions, and no assembly
  loading built from user input or downloaded content (S-05).
- `unsafe` blocks and raw pointer access are permitted where the platform requires them, but
  each one is kept as small as possible and carries a comment stating why it is needed and
  what guarantees its bounds. Prefer `Span<T>` and `ReadOnlySpan<T>` over raw pointers.
- Every `IDisposable` is disposed. Anything registered with a Dalamud service is unregistered.
  See the lifecycle section in `dalamud.md`, which is where this actually bites.
- Exceptions are caught at the boundaries the platform calls into, logged, and never allowed
  to escape into the game process. Fail closed: a feature that cannot work disables itself
  rather than running on half-valid state (S-09).
- Log through the Dalamud logging service, never to a self-managed file. No secrets, tokens,
  or other players' identifying data in log output (S-10).

## Build and deploy

- The build produces the plugin package through the Dalamud SDK. Nothing is hand-assembled.
- Build output (`bin/`, `obj/`) is git-ignored. Only sources, the lock file, and configuration
  are committed.
- Version numbers follow SemVer and stay in sync between the csproj and the plugin manifest
  (C-06). The changelog is updated with every release (C-08).

## Project-facing notes

- User-facing strings belong in one place so they can be translated later (C-05). This is a
  convention to hold from the first commit, because retrofitting it is expensive.
- The foundation's `pre-commit` hook runs PHPCS and ESLint, neither of which applies here. The
  C# equivalent (`dotnet format --verify-no-changes`) is therefore not covered by the shared
  hook and has to be wired in separately if it should block a commit.
