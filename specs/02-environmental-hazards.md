# Environmental Hazards

## Concept

In Foundations, **the environment is the first antagonist**. Terrain, climate, and wildlife are genuine hazards that shape where civilizations can go and how they get there. This is a deliberate rejection of the 4X convention that any unit can move freely across any terrain.

This system applies primarily to **Founders** in the early game, but extends to all units to varying degrees.

## Two axes of hazard

Hazards fall into two categories with different mechanical behavior:

### Biome hazards (passive, constant)
- **Cold** (tundra, taiga, glacier)
- **Heat & aridity** (desert, scrubland)
- **Wet** (swamp, marsh)
- **Dense vegetation** (jungle, deep forest)
- **High altitude** (mountains)

These apply every turn a unit is in or crossing the relevant tile. They drain provisions faster and roll for incidents.

### Biotic hazards (event-based)
- **Predators** (wolves, bears, big cats — region-dependent)
- **Disease** (more common in wet/jungle biomes)
- **Hostile fauna** (snakes, swarms, etc.)
- **Hostile humans** (raiders, other civs — out of scope for this spec)

These are triggered by chance per turn in relevant terrain, not constant.

## Resilience stats

Units exposed to hazards have three relevant stats:

### Hardiness
A base resilience value. Modified by:
- Unit type (Hunters have higher hardiness than Founders)
- Tech (Hide Working, Furs, Cold Weather Gear, etc.)
- Escorts (a Hunter escort grants hardiness bonuses — see [`03-hunter-unit.md`](./03-hunter-unit.md))

### Provisions
A depleting resource representing food, water, and supplies. Each turn, the unit consumes provisions; the rate increases in harsh biomes. Zero provisions triggers attrition damage.

### Morale / cohesion *(optional, may be folded into hardiness)*
Long journeys through unknown or hostile terrain may wear down the group. Low morale could mean the unit stops moving or even disbands and returns home. To be decided whether this is its own stat or a derivative effect.

## Hazard resolution

Each turn a unit ends its turn in (or moves through) a hazardous tile:

1. **Provision drain.** Provisions decrease by an amount scaled by biome severity.
2. **Hazard roll.** A check against hardiness + provisions + escorts determines if an incident occurs.
3. **Incident outcomes** (ordered from most to least common):
   - *Minor*: lose extra provisions, take small damage, lose part of next turn's movement
   - *Moderate*: significant damage, forced halt, lose an escort unit if present
   - *Severe*: major damage, attrition state triggered
   - *Catastrophic*: unit lost (should be rare; reserved for reckless play like sending an unescorted Founder across a glacier)

**Design principle: attrition before death.** The vast majority of bad outcomes should be incidents that wound, delay, or cost provisions. Outright loss should be rare and earned by genuinely reckless play.

## Legibility

The single most important UX requirement: **the player must be able to see what they're getting into before they commit**.

Before moving into a hazardous tile, the player should see information like:

> *"Crossing dense forest. Estimated 2 turns. Hazards: predators (moderate), getting lost (low). Survival estimate: 73%."*

Exact numbers vs. qualitative bands (Low/Moderate/High) is TBD, but the principle stands: **hazard is a bet the player makes with their eyes open**, not a punishment for not knowing.

Scouting (by Foragers or Hunters) improves the accuracy of hazard estimates for known terrain.

## Recovery & camping

A unit can choose to **hold/camp** in a safe tile. Doing so:
- Recovers a small amount of provisions (foraging in place)
- Recovers some HP if wounded
- Recovers morale
- Allows escorts to catch up

This gives the player a deliberate non-action — sometimes the right move is to stop and wait.

## Tech interactions

Many techs in the tree exist specifically to mitigate environmental hazards. See [`05-tech-tree-era-1.md`](./05-tech-tree-era-1.md) for Era I. Examples across eras:

| Tech | Era | Mitigates |
|---|---|---|
| Hide Working / Furs | I | Cold (light) |
| Waterskins | I | Aridity (light) |
| Pathfinding | I | Getting lost |
| Preserved Foods | II | Provision depletion |
| Cold Weather Gear | II | Cold (heavy) — enables tundra settlement |
| Pack Animals | II | Carrying capacity (extends range) |
| Camel Domestication | II/III | Desert specialization |
| Cartography / Maps | III | Pathfinding across known terrain |
| Roads | II/III | Turn wilderness into safe corridors |

## Terrain cost reference

(Movement costs duplicated here for completeness — also in [`01-founder-unit.md`](./01-founder-unit.md))

| Terrain | Movement cost | Provision drain | Hazard category |
|---|---|---|---|
| Plains, grassland | 1 | Low | None |
| Light forest | 2 | Low | Light predators |
| Hills | 2 | Low | None |
| Light desert | 2 | Moderate | Heat, dehydration |
| Light tundra | 2 | Moderate | Cold |
| Dense forest | 3 | Moderate | Predators, getting lost |
| Jungle | 3 | High | Disease, predators, getting lost |
| Heavy desert | 3+ | High | Severe heat, dehydration |
| Swamp / marsh | 3 | High | Disease |
| Deep tundra / taiga | 3+ | High | Severe cold |
| Mountains | Impassable* | — | — |
| Glacier | Impassable* | — | — |

*Impassable without specific techs in later eras.

## Open questions

- **Hazard visibility:** exact numbers, qualitative bands, or learned-through-play?
- **Should weather/seasons modulate hazards?** A tundra crossing in summer vs. winter is wildly different historically. Adds depth but also complexity.
- **Should some hazards be biome-specific events** (e.g. flash floods in desert wadis, blizzards in tundra)? Or stick to the steady drain model?
- **How does the AI handle hazards?** It needs to make sensible routing decisions, not blunder Founders into glaciers.
