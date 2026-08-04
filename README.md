# Fate Compass

**Find, rank and reach FATEs without the busywork.**

FATE farming is mostly bookkeeping. Which of the six FATEs in this zone is worth doing? Is that
one already at 80 %? Will it still be there by the time I arrive, or should I teleport? Did I
remember to sync down before hitting the first mob?

Fate Compass answers all of that in one small window, and takes the two-second chores off your
hands. It does not play for you: see [What it will not do](#what-it-will-not-do).

---

## Install

All of my plugins come from one address. Add it once and Fate Compass appears in the plugin list.

```
https://xivarsenal.app/plugins.json
```

1. In game, open `/xlsettings` and go to the **Experimental** tab.
2. Under *Custom Plugin Repositories*, paste the address above.
3. Click **+**, then **Save**.
4. Open `/xlplugins`, search for **Fate Compass** and install it.
5. Type `/fate` to open the window.

More plugins and the same instructions in full:
**[miralsoft/Dalamud-Plugins](https://github.com/miralsoft/Dalamud-Plugins)**

> Dalamud is a third-party tool and not an official Square Enix product. Using it is entirely your
> own choice and your own responsibility.

---

## At a glance

- **A ranked list of every FATE in your zone**, sorted by how worthwhile each one actually is,
  not just by distance.
- **Route advice:** the nearest aetheryte for each FATE, and whether teleporting really beats
  running.
- **One-click flag** to put the map marker on a FATE and hand it to your party.
- **Ready up:** dismount, level sync and tank stance in one go, by hand or automatically.
- **Bozja, Zadnor and the Occult Crescent:** critical engagements and encounters in the same
  list, with sign-up deadlines and how full they are.
- **Numbers on the map,** the running order drawn where you are already looking.
- **Gemstone counter** with a warning before you start throwing rewards away.
- **German and English**, and nothing ever leaves your computer.

---

## What it does, in more detail

### The list

The main window shows every FATE in the zone you are standing in, best first. "Best" is a mix of
how far away it is, how much time is left, how far along it already is, and how busy it is, and
you can change how much each of those counts.

Two views, switchable in the bottom right:

- **Compact:** a row of tiles. Icon, position, progress and remaining time. Made to be glanced
  at while you ride.
- **Table:** every value in full, including the nearest aetheryte and whether you would be level
  synced.

FATEs the plugin decides against are **not hidden**. They stay in the list, greyed out, with the
reason next to them: *almost finished*, *would expire before you arrive*, *hidden by your
filter*. You can always disagree with it, but you can see what it thought.

### Getting there

For every FATE, the plugin names the aetheryte closest to it and works out both routes: running
from where you stand, or teleporting and running the rest. It then says which is faster and by
roughly how much.

The travel speed it calculates with is measured while you play and remembered **per zone**, so
your times get more accurate the more you use it. Zones that behave differently (Eureka, Bozja
and the Occult Crescent, where the return to camp is part of the journey and the riding map
changes your speed) are handled on their own terms.

### Ready up

The small ritual before every FATE: get off the mount, sync your level down, put the tank stance
up. One button does all three, and you can have it happen automatically the moment you enter a
FATE. Each of the three steps has its own switch, and anything that cannot run right now, because
you are in combat, or casting, or the stance is already up, is simply skipped rather than forced.

After the FATE it can put you back on your mount, with a delay you set.

### Bozja, Zadnor and the Occult Crescent

Critical engagements, critical encounters and skirmishes are not FATEs, but they are the same
question, so they appear in the same list, with two things the game itself makes hard to see:

- **How many people are already in**, out of the limit.
- **When sign-up closes.** This is the one thing you can miss outright, so an open registration
  is ranked ahead of everything else and counts down.

Encounters that are already running and can no longer be joined move to the right-hand edge, out
of the ranking but still visible. Standing objectives such as the Tower sit there too, because
they run all evening and are not a "next target".

### On the map

The best few FATEs get numbered markers on the map and the minimap, so the order you read in the
window is the order you see while riding. This is the one feature that draws into the game's own
map; if anything about that goes wrong it switches itself off for the session rather than taking
any risk with your client.

### Chat

The flag button puts the map marker on a FATE and, if you want, types `<flag>` into your chat
box. There is also an optional line that gets written for you whenever a new FATE takes the lead,
in a channel you pick.

**Nothing is ever sent.** The line is typed into your chat box and sits there. You press enter,
or you don't. And it only ever writes into a chat box that is empty, so it can never overwrite
something you were in the middle of.

### Counters

- **Bicolor gemstones** with the cap, and a warning while you still have room to spend them,
  because a full purse means FATE rewards are being thrown away.
- **The zone's FATE rank**, read from the game's own progress window whenever you open it.

### Noticing things

A button in the server info bar and an icon you can drag onto your minimap, both carrying the
number of targets worth doing. Optionally a sound or a pop-up when a new FATE appears.

### What's new

A scroll icon in the title bar opens the release notes, sorted by version and labelled **New**,
**Changed** and **Fixed**, so "was that always like this?" has an answer from inside the game.

After an update it opens by itself, once, showing what that version brought. Never on a first
installation, and never twice. The icon stays lit until you have looked.

---

## Commands

| Command | What it does |
| --- | --- |
| `/fate` | Open or close the FATE list |
| `/fate cfg` | Open the settings |
| `/fate engage` | Get ready right now: dismount, level sync, tank stance |
| `/fate auto on \| off \| toggle` | Switch the automatic preparation |
| `/fate news` | Show what changed |
| `/fate help` | The list above |

Everything switchable has a command, so you can drive the plugin from a macro without opening a
window.

---

## Settings

Five tabs, and every setting explains itself behind the **(?)** next to it.

- **Basics:** master switch, where the plugin shows itself, counters, language.
- **Automation:** what happens when you enter a FATE, and what happens after it.
- **Map & chat:** markers, the flag, the chat line, notifications.
- **Filter:** level range, and which types of FATE you never want to see.
- **Advanced:** travel speeds, teleport cost, and how much distance, time and progress each count
  towards the ranking.

---

## What it will not do

This plugin assists. It does not play.

- It never targets, never fights, never loots, and never walks anywhere on its own.
- It never joins a FATE for you.
- It never sends anything to chat. It types; you press enter.
- The preparation steps only ever run **after you entered a FATE yourself**, and every one of
  them can be switched off.

Where it does act on your behalf, it is always finishing something you started. Pressing the
travel button teleports, or casts Return in the zones that have no aetherytes, and answers the
one confirmation the game raises for that Return. Nothing else, and only in the seconds after
your press. That last part is a switch in the settings if you would rather answer it yourself.

That boundary is not a setting. It is how the plugin is built, and it is why it can be used
without worrying about what it might do on its own.

---

## Your data

There is none to speak of. Fate Compass has no account, no server and **no network access
whatsoever**. The FATE history it records, used only to estimate when something might come back,
is a file on your own machine and never goes anywhere.

---

## Languages

German and English, following your Dalamud language automatically or set explicitly. Both are
complete: no half-translated windows.

---

## Problems or requests?

Open an issue here:
**[github.com/miralsoft/Dalamud-Fate-Compass/issues](https://github.com/miralsoft/Dalamud-Fate-Compass/issues)**

The more useful the report, the faster it gets fixed: which zone, which FATE, and what you
expected instead.

---

## Licence

[GNU Affero General Public License v3.0 or later](LICENSE).

In plain terms: use it, read it, change it, pass it on. If you pass on a changed version, or run
one as a service, the people who get it have the same rights you did, including the source. What
this rules out is somebody taking the code, closing it, and selling it back to you.

FINAL FANTASY XIV is a trademark of Square Enix Holdings Co., Ltd. This plugin is an independent
project and is not affiliated with, endorsed by or connected to Square Enix in any way.

---

## Building it yourself

Requires the .NET SDK and an installed Dalamud.

```powershell
.\build.ps1
```

That runs the format check, the build and the tests, then prints the path to register under
`/xlsettings`, *Experimental*, *Dev Plugin Locations*.
