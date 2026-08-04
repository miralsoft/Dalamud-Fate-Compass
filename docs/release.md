# Releasing

Purpose: how a version gets from this repository into a player's plugin list.

## The chain

1. A tag `vX.Y.Z` is pushed.
2. `.github/workflows/release.yml` builds Release, checks that the tag agrees with the built
   version, and publishes a GitHub release with two assets: `latest.zip` (the package
   DalamudPackager produced) and `pluginmaster.json`.
3. The aggregate index at
   [miralsoft/Dalamud-Plugins](https://github.com/miralsoft/Dalamud-Plugins) reads the newest
   release of every repository listed in its `plugins.json`, takes the manifest out of the zip,
   and rebuilds its own index. That is the address players enter.
4. Dalamud picks the new version up on its next refresh.

`pluginmaster.json` is the second, independent route: it lets somebody add this repository on its
own, without the aggregate index, through
`https://github.com/miralsoft/Dalamud-Fate-Compass/releases/latest/download/pluginmaster.json`.
The aggregate index does not read it. It is generated rather than committed, because a committed
copy would go stale the moment a release is cut.

## Cutting a version, step by step

This is the whole procedure. It exists so that "make a new version" is one sentence rather than
a memory test, and so that whoever runs it does not have to rediscover the two steps at the end
that are easy to forget: the index does not rebuild by itself for another hour, and the public
address caches for ten minutes after that.

Nothing here needs a decision except the first line.

### Decide the number

Semantic versioning (C-08), decided by what actually changed, not by how much work it was:

| | |
| --- | --- |
| **Patch** `1.0.x` | Something was supposed to work and did not. No new ground. |
| **Minor** `1.x.0` | Something the plugin could not do before. |
| **Major** `x.0.0` | A saved setting, a command or a behaviour people rely on changes meaning. |

A repair that took three days is still a patch. Judge the change, not the effort.

### Prepare it on a branch (FH-13)

```powershell
git checkout -b chore/release-1.2.3
```

1. Raise `<Version>` in `Directory.Build.props`.
2. Add a `## [1.2.3] - YYYY-MM-DD` section to `CHANGELOG.md`, written for whoever works on this.
3. Add the same version to **every** file in `src/FateCompass.Core/News/Notes/`, written for
   whoever plays with it. Both languages, always.
4. `.\build.ps1` and confirm it is green.

Three of those are enforced rather than trusted. A test fails the build when the newest release
notes do not name the version being built, when the languages disagree about which versions
exist, or when `CHANGELOG.md` has no section for it. That is deliberate: the "What's new" window
is the only place a player can find out what changed, so shipping a version it does not mention
would make it a liar.

### Merge it

```powershell
git push -u origin chore/release-1.2.3
gh pr create --base main --title "chore(release): 1.2.3" --body-file <notes>
```

CI has to be green on the branch before merging. That is the entire reason `main` is protected:
a tag names a commit, and a commit nobody checked is a release nobody checked.

### Tag it, after the merge and only on `main`

```powershell
git checkout main
git pull
git tag -a v1.2.3 -m "Fate Compass 1.2.3"
git push origin v1.2.3
```

The release workflow starts here. It refuses to publish if the tag and the built version
disagree, so a tag pushed before the version was raised fails loudly instead of shipping a
release nobody can install.

### Push it out to players

The release existing is not the same as players seeing it. Two more things stand between them:

```powershell
gh workflow run Index --repo miralsoft/Dalamud-Plugins
```

The index rebuilds hourly on its own (`17 * * * *`), so this only saves the wait. Without it a
release looks stuck for up to an hour and the natural reaction is to go looking for a fault that
is not there.

Then the website caches for ten minutes: `Cache-Control: public, max-age=600` on
`https://xivarsenal.app/plugins.json`. During that window the old version is still served and
nothing is wrong.

### Confirm it, do not assume it

```powershell
gh release view v1.2.3 --repo miralsoft/Dalamud-Fate-Compass
```

- The release carries `latest.zip` and `pluginmaster.json`, and is neither draft nor pre-release.
- The manifest **inside the zip** says the new version. That is the copy the index reads, and it
  is the one that went wrong in 1.0.0.
- `https://xivarsenal.app/plugins.json` names `FateCompass` at the new `AssemblyVersion`, once
  the ten minutes have passed.

## Before the first release of a plugin

The aggregate index needs three things to be true, and none of them involve this repository
holding any extra file:

- The repository is public.
- There is at least one published release, neither draft nor pre-release.
- The packaged zip is attached to it. Named `latest.zip`, it is found without further
  configuration.

Then one line in the index's `plugins.json` and one section in its README.

## When the artwork changes

Drop the new files into `src/FateCompass/images` and run:

```powershell
./scripts/prepare-icons.ps1
```

It rewrites both in place: `icon.png` to exactly 512 by 512, and `minimap.png` cropped to its
alpha channel, squared and scaled. Neither requirement is visible in an image viewer, and getting
either wrong means Dalamud quietly shows its default icon instead.

`IconUrl` in the manifest points at `main`, not at a tag, so replacing the file changes what the
plugin list shows without waiting for a release.

## Branch protection

`main` requires a pull request and a green `Build, test, format, scan` before anything lands.
That check name is the job name in `.github/workflows/ci.yml`; renaming the job renames the
check and silently breaks the requirement, so if the rule ever stops biting, look there first.

No approving review is required. This is a one-person project and nobody can approve their own
pull request, so requiring one would lock the repository rather than protect it. The protection
that matters here is the green check, not a second pair of eyes that does not exist.

## What is deliberately not automated

The version number. A workflow that derives the version from the tag would make the tag the
source of truth, and the tag is the one thing written by hand at the end. Keeping `Version` in
`Directory.Build.props` means the build fails loudly when the two disagree, rather than quietly
publishing whichever the tag happened to say.
