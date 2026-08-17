# Fate Compass

You are working on `Fate Compass`, a Dalamud plugin for Final Fantasy XIV that finds, ranks and
routes to FATEs. This file is a signpost, not a rulebook. The rules are not in this repository;
they are in the MIRAL Soft foundation, and they are read from there rather than copied here.

## The foundation

This project is **external** in the sense of M-12: its documentation lives here in `docs/`, and
nothing is ever written back to the foundation. The foundation is read-only for this repository.

Clone it once, next to nothing else, and pull it at the start of every working session:

```sh
git clone https://github.com/MIRAL-Soft/miralsoft-foundation-docs.git .foundation-docs
cd .foundation-docs && git pull
```

Keep the clone out of this repository through the clone's own exclude file, not through
`.gitignore`:

```sh
echo '.foundation-docs/' >> .git/info/exclude
```

`.git/info/exclude` rather than `.gitignore` on purpose: the exclude file is never committed, so
the arrangement stays a local convenience and does not become a line in a repository that has
nothing to do with it.

## Read in this order, before doing anything

From `.foundation-docs/`:

1. `rules/_meta.md`, the constitution.
2. Every file in `rules/`. They are short.
3. The profiles this project declares in its `docs/project.md`:
   `rules/languages/csharp.md` and `rules/frameworks/dalamud.md`. The framework profile usually
   carries the constraints that can actually break the product, so it is not the optional one.
   For this project it is the one that decides what the plugin is allowed to do at all.
4. `blueprints/dalamud-plugin.md`. It is binding (M-14) and says how a project of this kind is
   built. Every entry in it is marked load-bearing, meaning there is a stated reason it has to be
   that way, or convention, meaning any consistent choice would work and this one was chosen so
   that projects look alike. Departing from either is recorded in `docs/decisions.md`; departing
   from a load-bearing entry answers the reason it states.

Then from this repository, in `docs/`:

5. The project memory first: `status.md`, `decisions.md`, `todos.md`, `open-points.md`. This is
   how the previous session hands over, and it is the only handover there is (M-08, I-07).
6. `project.md`, `architecture.md`, `rules-project.md`.

## Enforcement

Install the hooks from the foundation into this clone. They are not part of any repository, so a
fresh clone needs them again:

```sh
cp .foundation-docs/enforcement/hooks/commit-msg .git/hooks/commit-msg
cp .foundation-docs/enforcement/hooks/pre-commit .git/hooks/pre-commit
chmod +x .git/hooks/commit-msg .git/hooks/pre-commit
```

`.miralsoft-enforcement` at this repository's root configures them: the committer identity this
project uses (R-03, R-19) and the file patterns checked for em-dashes (I-02). Every check the
hooks perform also runs in CI (R-17), because a hook lives in a clone and gets forgotten.

If a hook blocks a commit, fix the cause. Do not bypass it (R-08).

## The rest of the local setup

None of this is in the repository, so a fresh clone or a new machine needs all of it again. It is
listed here because the first three are silent when missing: commits get the wrong author, the
developer tools are simply absent, and `gh` fails only at the moment it is needed.

**The committer identity**, set for this clone alone so it cannot leak into another project:

```sh
git config user.name  "Sanaka"
git config user.email "20637644+miralsoft@users.noreply.github.com"
```

This is what `.miralsoft-enforcement` checks and what `docs/project.md` declares (R-03, R-19).
The noreply address keeps a personal address out of a public repository.

**The developer tools**, which exist only where this file does:

```sh
cp Directory.Build.local.props.example Directory.Build.local.props
```

It is ignored by git and CI fails if it is ever committed, so no build anybody else makes can
contain the diagnostics window or the probes behind it. `build.ps1` says which of the two kinds
of build it just made.

**GitHub access** for releases and pull requests: `gh auth login`, HTTPS.

**The plugin itself**, once built: register the path `build.ps1` prints under `/xlsettings`,
Experimental, Dev Plugin Locations. It prints the staging folder rather than `bin/`, because the
main assembly is copied there last so a reload never sees a half-updated set of files.

## While working

- Global rules are binding and read-only. This project may tighten them in `docs/rules-project.md`,
  never weaken or contradict them (M-01, M-04). Its own rules are numbered FH-01 upwards.
- This project is bound by the foundation version it declares in `docs/project.md` (M-17). A rule
  added to the foundation later does not bind it until that declaration is raised. Review the
  declaration whenever a release is cut: read the foundation changelog from the declared version
  onward, then either raise it and do the work, or leave it and record why.
- Update `docs/status.md` at the end of every working session, and append to `docs/decisions.md`
  when something is decided, including the paths that were rejected and why (M-08, M-15).
- An AI is never named as author or co-author, anywhere (R-09, I-06).

## The two constraints that outrank convenience

Everything else in this project bends before these two do.

- **The plugin assists, it does not play.** Every feature leaves the final action with the
  player. This is the automation boundary in `rules/frameworks/dalamud.md` and FH-01 to FH-07
  here, and it is the reason the plugin is allowed to exist at all.
- **The game must never crash.** A plugin runs inside the game process, so a mistake here does
  not produce a stack trace, it ends somebody's session. See the crash-safety rules in the
  Dalamud profile and FH-08 to FH-12.

No rules are duplicated here. Follow the documents above.
