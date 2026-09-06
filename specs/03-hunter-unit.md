# Hunter Unit

## Concept

The **Hunter** is a dual-purpose unit that is both **economically productive** and **militarily capable**. It is the answer to a long-standing 4X design problem: what do non-combat military units do when they're not fighting?

In Foundations, Hunters:
- Provide food to their home city (passive economic function)
- Scout, track, and gather intelligence
- Set traps, ambush, and use deception
- Escort Founders, granting them resilience to environmental hazards

This unit is the pre-state martial tradition — historically, hunters fed the band, defended it, and scouted for it. Foundations unifies these functions instead of artificially splitting them.

## Why this matters for the game

Hunters are the unit that makes the rest of Foundations work:
- They make Founder journeys viable (escort + hazard immunity)
- They make exploration multi-layered (tracking reveals movement, not just terrain)
- They make peacetime military units worth building (food generation)
- They introduce asymmetric information from turn one without bolted-on espionage systems

See [`04-founding-parties.md`](./04-founding-parties.md) for the Founder/Hunter pairing in detail.

## Economic function: Forage

**Forage is the baseline ability every Hunter has from turn one.** No tech required.

When a Hunter is stationed within a certain radius of its home city (suggested: 3–4 tiles), it generates a passive food yield each turn. The yield:

- **Depends on terrain.** Rich forest > sparse forest > plains > desert. A Hunter in a good biome contributes meaningfully; a Hunter in a bad one barely covers their upkeep.
- **Diminishes with concentration.** Multiple Hunters around one city show diminishing returns — local game is finite. This caps Hunter spam naturally.
- **Pauses when the Hunter is away.** Deploying a Hunter as an escort or long-range scout trades economic value for exploration/protection value. Every Hunter assignment is a real decision.

This makes the Hunter **a worthwhile build even if war never happens**. The unit pays for itself.

## Tech-gated abilities

Each Hunter ability beyond Forage is unlocked by researching a specific tech. This means:
- The Hunter evolves across the game without ever being obsoleted
- Civilizations have *different Hunters* based on their tech investments
- Tech research has visible, immediate consequences for unit behavior

### Era I abilities

| Ability | Tech Required | What it does |
|---|---|---|
| **Forage** | *(baseline)* | Passive food yield near home city |
| **Track** | Pathfinding | Hunter sees recent movement traces on nearby tiles |
| **Camouflage** | Hide Working | Hunter invisible at range in forest/hills/jungle |
| **Snare** | Cordage | Hunter sets traps that damage or alert |
| **Bait** | Wild Herd Management | Hunter lures predators or game to a chosen tile |

### Era II+ abilities (preview)

| Ability | Tech Required | What it does |
|---|---|---|
| **Ambush** | Hunting Tactics | Hunter hides in place; surprise-attacks adjacent enemies with first-strike bonus |
| **Game Drive** | Cooperative Hunting | Multiple Hunters flush wildlife in a chosen direction |
| **False Trail** | Tracking Mastery | Hunter creates fake movement traces to deceive other civs |
| **Long-Range Tracking** | Animal Lore | Hunter identifies *what kind* of unit made traces |
| **Wilderness Survival** | Survivalism | Hunter ignores most environmental hazards; survives indefinitely off the land |

## Deception mechanics in detail

The Hunter introduces **deception** as a first-class mechanic in 4X — something the genre has largely ignored because of its wargame inheritance.

### Camouflage
A Hunter with this ability is **invisible to enemy units beyond 1 tile** when in appropriate terrain (forest, jungle, hills, tall grass). Adjacent units detect them. This makes Hunters genuinely different from scouts: scouts *see*, Hunters *hide*.

### Snare
Places a trap on a tile. Invisible until triggered. Effects scale with later techs:
- *Basic snare*: small damage to first non-friendly unit
- *Pit trap* (later): immobilization
- *Tripwire* (later): alerts the player and reveals the unit, no damage — pure intel

Snares cost cordage/provisions to set.

### Bait
Drops a lure (kill, fire, encampment marker) that draws hostile wildlife or curious enemy units toward a chosen tile. Pairs with Snare for trap setups.

### Ambush (Era II)
The Hunter enters a camouflaged state on their current tile. The next enemy unit ending its turn adjacent takes a surprise attack with significant first-strike bonus. The Hunter remains hidden if the ambush succeeds.

### False Trail (Era II)
Creates fake movement traces leading away from the Hunter's actual position. Other civs' tracking abilities pick up the misleading information. Costs an action and provisions. **This is deception as a deliberate act**, unique in the genre.

### Counterability
Deception must be counterable to avoid being oppressive:
- Other Hunters with Track can detect camouflage at closer range
- Tech like Watchtowers, Trained Dogs, and later Cartography reduce ambush effectiveness
- Snares can be detected by sufficiently skilled scouting units

## Escort function

A Hunter accompanying a Founder grants:

- **Immunity to wild animal attacks.** The Hunter detects, deters, or kills predators before they reach the Founder.
- **With Track: reduced risk of getting lost** in unfamiliar terrain.
- **With Hide Working: partial cold resistance** for the Founder (furs, shelter knowledge).
- **With Wilderness Survival (Era III): full hazard immunity** for the Founder while escorted.

When the Founder founds a city, the escorting Hunter **stays and becomes the new city's first Hunter**. They do not return home. See [`04-founding-parties.md`](./04-founding-parties.md).

## Settling-in bonus *(proposed)*

A Hunter whose home city was newly founded receives a temporary forage yield bonus for the first N turns. Represents the Hunter actively learning the new territory and laying the economic groundwork. Gives the player a felt sense that this Hunter is the founder of the new city's food supply.

## Civilizational identity through Hunter doctrine

Because each ability is a tech choice, civilizations end up with **different Hunter doctrines**:

- **Defensive/trapping tradition** (Snare + Bait + Ambush): turns home territory into a deathtrap for invaders
- **Intelligence tradition** (Track + Long-Range Tracking + False Trail): Hunters as the eyes and deceivers of the civilization
- **Economic tradition** (Forage + Game Drive + Wilderness Survival): Hunters as productive extractors of marginal land
- **Combat tradition** (Camouflage + Ambush + later skirmisher techs): Hunters as the kernel of an asymmetric military

These aren't pre-set playstyles you pick — they emerge from tech choices.

## Late-game evolution

The Hunter line continues into later eras as a parallel martial tradition to the Warrior → Spearman → Soldier line. Possible late-game evolutions:

- **Skirmishers** — ambush-and-retreat infantry
- **Rangers** — long-range scouts with tracking
- **Saboteurs** — set traps inside enemy borders
- **Scouts/Spies** — natural evolution of Hunters into the espionage role

A civilization that invested in Hunter techs early plays fundamentally differently from one that invested in Warrior techs. This is genuine asymmetric civilization design.

## Open questions

- **Forage yield numbers.** What's the right balance between "worth building" and "Hunter spam dominates the early economy"?
- **Range of the home-city forage radius.** 3 tiles? 4? Should it expand with tech?
- **Should Hunters require upkeep?** Probably yes (provisions), but the forage yield should generally exceed upkeep on suitable terrain.
- **AI competence with deception.** This is the implementation challenge — the AI must use camouflage, ambushes, and false trails plausibly, or the human player dominates.
- **Visibility of snares to the snare-setter's allies.** Friendly units should presumably not trigger them, but should they see them?
