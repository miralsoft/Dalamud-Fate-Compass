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
`https://github.com/miralsoft/Dalamud-Fate-Helper/releases/latest/download/pluginmaster.json`.
The aggregate index does not read it. It is generated rather than committed, because a committed
copy would go stale the moment a release is cut.

## Steps for a new version

1. Raise `<Version>` in `Directory.Build.props`.
2. Add the version to `CHANGELOG.md` (for whoever works on this) **and** to
   `src/FateHelper.Core/News/Notes/*.json`, in every language (for whoever plays with it). A test
   fails the build when the newest notes do not describe the version being built, which is
   deliberate: the "What's new" window is the only place a player can find out what changed, so
   shipping a version it does not mention would make it a liar.
3. `.\build.ps1` and confirm it is green.
4. Commit, push.
5. Tag and push the tag:

   ```powershell
   git tag v1.2.3
   git push origin v1.2.3
   ```

6. Watch *Actions* until the release run is green, then check that the release carries both
   assets.

## Before the first release of a plugin

The aggregate index needs three things to be true, and none of them involve this repository
holding any extra file:

- The repository is public.
- There is at least one published release, neither draft nor pre-release.
- The packaged zip is attached to it. Named `latest.zip`, it is found without further
  configuration.

Then one line in the index's `plugins.json` and one section in its README.

## What is deliberately not automated

The version number. A workflow that derives the version from the tag would make the tag the
source of truth, and the tag is the one thing written by hand at the end. Keeping `Version` in
`Directory.Build.props` means the build fails loudly when the two disagree, rather than quietly
publishing whichever the tag happened to say.
