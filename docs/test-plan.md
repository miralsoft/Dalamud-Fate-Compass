# Test plan: Fate Helper 0.1.0

Purpose: the manual checks that close Phase 5 in `todos.md`. Nothing here can be automated,
because all of it needs a running game.

Ordered by risk, not by feature. The first sections cover the things that can be **silently
wrong**: values that compile fine and could still be the wrong number. The later sections cover
things that fail obviously if they fail at all.

Keep the Dalamud log open while testing (`/xllog`). Every action the plugin sends to the game
is logged with what triggered it, so the log answers "did it try and fail" versus "did it never
try", which are very different bugs.

## 0. Crash safety audit (before every handover)

Required by FH-11. This is a code review, not a play test, and it runs before the build is
handed over rather than after something goes wrong.

- [ ] `grep` for `unsafe`, `fixed`, `->`, `Instance()`, and `.Address`, then look at each hit
- [ ] Every native pointer is null-checked before it is followed (FH-09)
- [ ] No pointer into managed memory is passed to the game (FH-10)
- [ ] Every game call reachable from a UI callback goes through `OnGameThread` (FH-08)
- [ ] Record the outcome in `status.md`

Remember that `try`/`catch` does not catch an access violation. A block of unsafe code inside a
`try` is not protected, it only looks protected.

## Setup

1. `.\build.ps1`
2. `/xlsettings`, Experimental, Dev Plugin Locations, add:
   `src\FateHelper\bin\Release\FateHelper.dll`
3. `/xlplugins`, dev section, enable Fate Helper
4. After each later build: reload from the dev section. The path stays registered.

## 0. Does it load at all

- [ ] Plugin appears and enables without an error
- [ ] `/fh` opens the window
- [ ] `/fh cfg` opens the settings
- [ ] `/xllog` shows no exception from FateHelper
- [ ] `/fh help` prints the command list

If the window opens but stays empty in an open-world zone with active FATEs, stop here and
report it. That points at the FATE reading rather than at anything below.

## 1. The values that could be silently wrong

**This is the important section.** Each of these is a named constant that compiles fine and was
never confirmed against the live client. If one is wrong, the step does nothing and the log
says the action was "unavailable" rather than throwing.

### 1.1 Tank stance

Per tank job, stance **off**, then `/fh engage`:

- [ ] Paladin: Iron Will goes up
- [ ] Warrior: Defiance goes up
- [ ] Dark Knight: Grit goes up
- [ ] Gunbreaker: Royal Guard goes up
- [ ] On a non-tank job, nothing is sent and nothing breaks
- [ ] With the stance already up, nothing is sent (log says so)

If a stance does not go up, the action id in `TankStanceResolver` is wrong. If it goes up but
the plugin keeps trying again, the **status** id is wrong. The log distinguishes the two.

### 1.2 Dismount and mount

- [ ] Mounted, `/fh engage`: the character dismounts
- [ ] Settings, enable "Remount automatically after a FATE", finish a FATE: the character mounts
- [ ] Same, but stay in combat as the FATE ends: no mount attempt, log says `InCombat`

### 1.3 Level sync

- [ ] Enter a FATE well below your level, `/fh engage`: level sync applies
- [ ] Repeat while already synced: nothing is sent, log says "already synced"
- [ ] German client: the command still works (it should, `/levelsync` is identical in every
      language, but this is the check that proves it)

### 1.4 Bicolor gemstones

- [ ] The line above the list shows your real count and cap
- [ ] Spend down and back up: the number follows
- [ ] Near the cap it turns amber, at the cap red

## 2. The FATE list

- [ ] Active FATEs of the current zone all appear
- [ ] The order is plausible: near, fresh, and long-running ones near the top
- [ ] A FATE above the "treat as finished" threshold is greyed out, and hovering the name
      gives the reason
- [ ] A FATE about to expire is greyed out with its own reason
- [ ] Distance roughly matches what the map shows
- [ ] The nearest-aetheryte column names a plausible aetheryte
- [ ] Type column is right: boss, slay, collect, escort, defend

The type classification is my mapping of the game's rule column and is worth a sceptical look.
If types are wrong, say which FATE showed which type.

## 3. Notifications

- [ ] A FATE spawning while you are in the zone gives a notice and a sound
- [ ] **Changing zones does not produce a burst of notices.** This was a real bug that was
      fixed; the first look at a new zone should be silent
- [ ] Reloading the plugin does not produce a burst either
- [ ] Turning the sound off in the settings silences it
- [ ] With "only notify for FATEs that pass the filter" on, a filtered-out type stays quiet

## 4. The two buttons

- [ ] Flag: click, open the map, the flag sits on that FATE
- [ ] Then type `<flag>` in party chat and send: the map link points there
- [ ] Teleport: click, you teleport to the named aetheryte
- [ ] Teleport on a FATE you are already standing next to: the tooltip should have said
      travelling is faster

## 5. Settings and commands

- [ ] `/fh auto on`, `/fh auto off`, `/fh auto toggle` each confirm in chat
- [ ] With auto on, walking into a FATE runs the sequence without pressing anything
- [ ] With auto off, walking into a FATE does nothing until you press Prepare or run
      `/fh engage`
- [ ] Switching the language to German changes the window immediately
- [ ] Switching to Automatic follows the Dalamud interface language
- [ ] Excluding a type hides those FATEs, shown as filtered rather than vanishing
- [ ] The level range sliders work, and 0 means no limit
- [ ] Settings survive a plugin reload

## 6. Instanced content

This part has never run and is the least certain, since the event type numbering is not
documented anywhere.

### Bozja and Zadnor

- [ ] Skirmishes and critical engagements appear in the list
- [ ] The type column is right. **If not, note which activity showed which type**, that is
      exactly what is needed to fix the mapping
- [ ] The player count column shows a plausible number
- [ ] While registration is open, the row shows a green "Registration open" label
- [ ] A registering engagement ranks above ordinary entries
- [ ] The notification for it uses the registration wording, not the plain new-FATE wording
- [ ] Remount is skipped here, log says `NotPermittedHere` rather than attempting it
- [ ] The flag button still works inside instanced content
- [ ] The teleport button either works or fails harmlessly

### Occult Crescent

- [ ] Critical encounters appear, with the same checks as above

### Eureka

- [ ] Notorious monsters appear in the list like ordinary FATEs
- [ ] The level column: Eureka uses elemental levels, so this may read oddly. Note what it
      shows so the display can be adjusted

## 7. Performance

- [ ] `/xldev`, Plugins, Open Plugin Stats: Fate Helper's per-frame cost stays low
- [ ] The window open in a busy zone does not cause noticeable stutter

## What to report back

For anything that fails, the useful details are: what you did, what happened instead, and the
relevant lines from `/xllog`. The log distinguishes "tried and the game refused" from "never
tried", and those have completely different causes.
