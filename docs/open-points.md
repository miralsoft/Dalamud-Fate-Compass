# Open points: Fate Compass

Purpose: open questions and unresolved items. Distinct from `todos.md`, which is planned work.
These still need a decision or a verification.

## Open work in other repositories

M-18 allows reading another repository and forbids changing it, however small the change and
however obviously right it looks. Where something decided here has a consequence somewhere else,
it is written down here, marked as belonging there, and it stops. Whoever works in that
repository picks it up.

- **(open since 2026-08-12, `miralsoft/Dalamud-Plugins`) Does the aggregate index actually
  satisfy D-06?** D-06 says an automated index never removes an entry as a consequence of a
  failure, only as a deliberate act. The index rebuilds hourly and reads this plugin's
  `pluginmaster.json` from its release assets. What happens when that read fails, whether the
  entry survives or the rebuild publishes a list without it, has not been looked at. If it drops
  the entry, this plugin disappears from every player's installer until the next successful run,
  for a reason on the index's side rather than ours.

  D-07 applies to the answer as much as to the question: this gets settled by reading that
  repository's workflow, not by assuming it behaves sensibly.

## Resolved on 2026-08-01

Verified by reflecting over the installed Dalamud 15.0.3. Details in `platform-notes.md`.

- FATE data, player state, condition flags, framework ticks, and the interface language are
  all available and their exact members are recorded.
- `AgentMap.SetFlagMapMarker` and `Telepo.Teleport` signatures confirmed, so the flag button
  and the teleport button are both buildable as designed.
- `IClientState.LocalPlayer` is gone in v15. `IObjectTable.LocalPlayer` replaces it.

### Level sync and FATE participation, solved

`FateManager` carries what was missing, and all of it now compiles against the installed
client:

```csharp
ushort       FateManager.GetCurrentFateId();            // participation
bool         FateManager.IsSyncedToFate(FateContext*);  // sync state
bool         FateManager.IsInFateRadius(Vector3*);
FateContext* FateManager.GetFateById(ushort);
```

There is no direct function to *perform* a FATE level sync: every client-structs member named
after level sync belongs to duty content instead. The trigger is therefore the text command
`/levelsync on`, executed through `RaptureShellModule.ExecuteCommandInner`, which is the same
path a macro takes. Because `IsSyncedToFate` reports the state, the result is checked
afterwards rather than assumed, and the action is skipped entirely when the sync is already
in place.

## Open after the first play test (2026-08-01)

### Fixed

- **Dismount and tank stance did nothing.** The cause was not a wrong id: all of them were
  checked against the game data and are correct (dismount 23, mount roulette 9, Iron Will 28,
  Defiance 48, Grit 3629, Royal Guard 16142). The cause was `GameActions.UseAction` refusing to
  send whenever `GetActionStatus` returned non-zero, which it does for general actions and
  stances even when they are usable. Level sync was unaffected because it goes through a text
  command, which is exactly why only that one worked. The status is now logged and the action
  is sent regardless.
- **Royal Guard status id was wrong**: 392, not the value written from memory. The plugin could
  never tell that a Gunbreaker already had the stance up.

### Diagnosed, not yet fixed

- **"No aetheryte in this zone" is always shown.** Root cause found: `Aetheryte.Level` points at
  row ids far outside the `Level` sheet (for example 3661246 against 61346 rows), so every
  position lookup failed and every aetheryte was dropped. All 108 visible aetherytes were
  affected. The fix is to read positions from the `MapMarker` sheet instead, keyed by
  `DataKey` where `DataType == 3`, which was confirmed to carry them. That needs a coordinate
  conversion from marker space, which must be verified rather than guessed.

### Numbered map markers do not appear at all

Confirmed by screenshot on 2026-08-01: the flag marker works, the game's own FATE icons show,
and not one marker from `MapService.DrawRankMarkers` is visible. So the problem is the call
itself, not the label.

What was assumed and never verified:

- That `AgentMap.AddMapMarker` adds a marker that survives to the next draw. These go into the
  temporary marker list, which the game clears whenever it rebuilds the map, so adding them
  from the polling loop may simply race against that rebuild.
- That the `textPosition` and `textStyle` bytes (1 and 0 were used) mean anything sensible.
- That world coordinates are the right space for this call, given the flag uses them but the
  sibling `AddGatheringTempMarker` takes map coordinates instead.

Before the next attempt, verify each of those rather than adjusting values and hoping.
`AddGatheringTempMarker(int mapX, int mapY, int radius, uint iconId, uint styleFlags, string
tooltip)` is worth trying too: it takes a tooltip string directly rather than a raw byte
pointer, which suggests it is the supported path for exactly this.

### Shared FATE rank: no data source exists

Searched exhaustively on 2026-08-01 against the installed client structs:

- No type anywhere in `FFXIVClientStructs` contains "Fate" outside of `AddonFateProgress`
  itself. There is no manager, no state struct, no sheet.
- `PlayerState` carries Grand Company and Beast Tribe ranks but nothing for FATEs.
- `UIState` exposes unlock checks (aetherytes, cutscenes, cards) but no FATE progress.

So the rank, the per-zone counter, and the completed flag exist **only inside the game's own
FATE progress window**. Any implementation therefore reads UI values rather than data, which
is the most fragile thing this plugin would do and is the reason it was not simply built.

Agreed approach, in order of preference:

1. Read the values whenever the window happens to be open, cache them with a timestamp, and
   display them as a snapshot rather than as live data.
2. Keep the cache current between readings from what the plugin already observes: it knows
   when a FATE in a zone completed, so the counter can be advanced locally.
3. If neither is enough, offer an explicit refresh button that opens the window, reads it, and
   closes it again. The Eorzea Arsenal plugin does this and it is barely visible in practice.

Whatever is built, the display must be honest that it is a snapshot, with the time it was
taken, rather than presenting a possibly stale number as fact.

### Aetheryte positions carry no height

Reported from play on 2026-08-01: the plugin recommended teleporting to the nearer aetheryte,
but the FATE sat far above it, so a more distant aetheryte at a similar height would have been
quicker.

The player's and the FATEs' elevation is known and is now weighted into the ranking
(`VerticalTravelWeight`). The aetherytes' is not, and this is why:

- `Aetheryte.Level` points at row ids outside the `Level` sheet, so it never resolves. That was
  the earlier bug that made every zone report no aetheryte at all.
- The working source, `MapMarker` with `DataType == 3`, is a 2D map position with no height.

**Solved on 2026-08-04, and not the way this note proposed.** The suggestion was to scan the
`Level` sheet and match by proximity. That was checked against the game data before being built,
and it does not hold: of 108 visible aetherytes only **15** can be reached through
`Level.Object`, and the `Aetheryte.Level` references are row ids like 3785149 against a sheet of
61346 rows. There is no static table to read, by that route or any other.

The source that does work is the obvious one, and it is not a data file: the aetheryte is a
physical object standing in the zone, and the object table reports its real position, height
included. `ObjectKind.Aetheryte` picks it out and `BaseId` is its row id.

The cost is that the game only loads objects near the player, so a zone cannot be asked about
all at once. The heights are therefore **learned rather than looked up**: whatever is in range is
remembered and persisted, and the table fills in as a zone gets played. An aetheryte that has
not been walked past yet measures flat, which is what every aetheryte did before, so the feature
degrades into the old behaviour rather than into a wrong answer.

Both sides have to be known for the climb to count. A height on the aetheryte but not the FATE,
or the other way round, falls back to flat rather than comparing measured against unmeasured on
different terms.

### Requested, not yet built

- **Shared FATE rank per zone**, as shown by the game's own FATE progress window: rank 1 to 4,
  progress such as 13/40, or completed, plus the gemstone total. `AddonFateProgress` exists in
  the client structs and is the likely source.

## Confirmed by play testing on 2026-08-01

These were the values that compiled but could still have been wrong. They are now proven
correct in the running game, so they no longer need checking.

- Stance action ids and status ids: the stance goes up on a tank. The earlier failure was the
  localised job abbreviation, not the ids.
- General action ids for dismount and mount: both work.
- `/levelsync` sent as English on a German client: works. The command is identical in every
  language and the localised forms are only aliases.
- Bicolor gemstone count and cap: the display matches the game.
- Aetheryte lookup, sync level, and the ranking of FATEs that have not started yet.

## Still to verify in game

None of these block the build. They are values that compile fine and could still be wrong,
which is the dangerous kind.

- **Stance action ids and status ids.** Whether a job is a tank is read from the game's own
  `ClassJob.Role`, so that part needs no confirmation. The four stance action ids (28, 48,
  3629, 16142) and the four status ids (79, 91, 743, 1833) in `TankStanceResolver` are the
  exception: the game data carries no flag identifying a stance, so they are written as named
  constants. Confirm them before trusting the stance step.
- **General action ids** for dismount (23) and mount roulette (9) in `GameActions`.
- **Localised text commands.** `/levelsync` is sent as English. Whether a German client
  accepts the English command needs checking. If it does not, the command has to be resolved
  per client language.
- **Bicolor gemstones.** Which item id holds them and where the cap is exposed.
- **Mount.** Whether the mount roulette general action is the right choice, or whether the
  player should pick a specific mount, and how the cast is confirmed as finished.

## Product decisions still open

- Which mount the remount step uses. A configured favourite, the mount roulette, or whatever
  was last used. Affects cast time and therefore the wait logic.
- Where the notification sound comes from. A game sound effect avoids shipping an audio file
  and the licensing question that comes with it.
- Whether the respawn estimation is worth showing before it has enough data, or should stay
  hidden until the history is meaningful.
- Default weights for the ranking. Distance against remaining time against progress is a
  matter of taste and probably needs tuning after real use.

## Idea, agreed but not started

- **Hand-written travel points for individual objectives.** The route advice measures straight
  lines, so it cannot know that the way from the Corrupted Quarter to a target on higher ground
  does not exist. A small table of overrides, saying that this encounter is reached from that
  waypoint, would settle exactly the cases the player already knows are wrong, and the automatic choice
  would carry on wherever no entry exists.

  Feasible and cheap: the route calculator already picks a waypoint, so the override only has to
  replace that one choice. It belongs in the configuration rather than in the source, so a wrong
  entry can be corrected without a build. Deliberately not built yet; the automatic answer is
  right most of the time and the shape of the exceptions is not known well enough to design a
  table around.

  The alternative is real path finding, which would mean navigation mesh data for every zone and
  a second third-party dependency at a place where correctness is hard to check. Not worth it
  for an advisory arrow.

## Deferred by decision

- Cross-zone FATE awareness. Impossible without an own backend, deliberately out of scope for
  now (FH-03).
- Publishing to the official Dalamud repository. Two conditions apply there. Open source is
  already met: this repository is public. Disclosure of AI assistance in the submission pull
  request would apply at the moment of submitting, and it is the owner's statement to make.
  It does not conflict with I-06, which keeps an AI out of the codebase, the commits, the
  metadata and the product text, all of which stay clean either way. Deferred because nothing
  has been submitted, not because anything blocks it. The general shape of this question is in
  `.foundation-docs/rules/frameworks/dalamud.md`; what is above is this project's answer.
