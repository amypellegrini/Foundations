# Foundations — Design Specs

This folder contains early design specifications for **Foundations**, a 4X strategy game.

These are working documents capturing design intent and rationale. They are *not* implementation specs yet — they describe what the game should feel like and why, with enough mechanical detail to start fleshing out requirements.

## Design pillars

Three commitments shape every spec in this folder:

1. **Slow evolution, no sudden jumps.** The tech tree progresses gradually, with parallel reinforcing threads rather than discrete tier unlocks. Civilizations change incrementally over many turns.
2. **The environment is the first antagonist.** Terrain, climate, and wildlife are genuine hazards from turn one. Civ's "blank map of free movement" is rejected. Where you go and how you get there matters.
3. **Founding is a journey, not an instant act of placement.** A founder unit represents a group of people committing to settle. Who travels with them, what they carry, and what they survive shapes what the new city becomes.

## Spec index

| File | What it covers |
|---|---|
| [`01-founder-unit.md`](./01-founder-unit.md) | The Founder unit: role, fragility, movement, consumption-on-founding |
| [`02-environmental-hazards.md`](./02-environmental-hazards.md) | Terrain costs, biome hazards, attrition, recovery mechanics |
| [`03-hunter-unit.md`](./03-hunter-unit.md) | The Hunter unit: economic function, tech-gated abilities, deception mechanics |
| [`04-founding-parties.md`](./04-founding-parties.md) | Founder + escort dynamics, the founding-party narrative, what arrives at the new city |
| [`05-tech-tree-era-1.md`](./05-tech-tree-era-1.md) | Era I (Sedentary Era) tech tree: techs, dependencies, what each unlocks |
| [`06-fog-of-war.md`](./06-fog-of-war.md) | Three-tier visibility (Visible / Explored / Unknown), per-unit sight, what each tier hides and reveals |

## Open questions

These come up across multiple specs and need answers eventually:

- **Turn scale.** What does one turn represent in calendar time? This affects how journey lengths *feel* (a 10-turn migration is epic if turns are decades, tedious if turns are weeks).
- **New Founder production cost.** Built from cities (Civ-style) or population-cost (Civ 4 / Old World-style)? The latter is more historically grounded.
- **How visible should hazard probabilities be to the player?** Exact percentages, qualitative bands, or learned through play?
- **AI competence with deception mechanics.** Hunters with ambush/false-trail abilities are powerful in player hands; the AI needs to use them well too.

## Current implementation

See [07-implemented-gameplay.md](07-implemented-gameplay.md) for the reconciled executable rules. Earlier design documents include proposals and may describe features that are not yet implemented.
