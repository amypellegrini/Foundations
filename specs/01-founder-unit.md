# Founder Unit

## Concept

The **Founder** is the starting unit of every civilization and the only unit capable of founding a city. It represents not a single person but the *collective decision of a group of people to stop migrating and settle*. The Founder is an abstraction of a founding population — when they commit to a location, they become the city. The unit itself is consumed.

## Role in the game

- Every player starts the game with **one Founder**.
- The Founder's only purpose is to found a city. It has no combat ability and no economic function on its own.
- Additional Founders can be produced later by cities (cost model TBD — see open questions in [README](./README.md)).

## Consumption on founding

When a Founder uses its `Found City` action on a valid tile, **the Founder is permanently consumed**. The new city replaces the Founder on the map.

Rationale:
- Historically truthful. Founding groups didn't leave a settlement once they built it; they *were* the settlement.
- Makes city-founding feel like a real commitment. Each new city costs a meaningful action and a meaningful investment.
- Avoids the design pitfall of persistent founders that enable settler-spam.

## Fragility

The Founder is **fragile**. This is a core design choice. The Founder can be:

- Attacked by wild animals
- Killed by environmental hazards (cold, heat, aridity, exhaustion)
- Lost to attrition over long journeys without provisions
- Killed by hostile units (other civilizations, raiders)

Fragility is what gives the Founder's journey weight. See [`02-environmental-hazards.md`](./02-environmental-hazards.md) for the hazard system, and [`04-founding-parties.md`](./04-founding-parties.md) for how escorts mitigate this.

## Movement

- **Baseline movement:** 2 points per turn.
- This gives the player at least one tile of recovery room when things go wrong (e.g. ambush, sudden hazard) — fragility coexists with agency.
- Terrain modifies actual tile cost:

| Terrain | Movement cost |
|---|---|
| Plains, grassland | 1 |
| Light forest, hills, light desert, light tundra | 2 |
| Dense forest, heavy desert, swamp, deep tundra | 3+ |
| Mountains, glacier | Impassable without specific techs |
| Rivers | Crossing costs full remaining movement |

Founders **do not gain baseline movement speed over the course of the game** through tech. Instead, techs change the *map* around the Founder:
- Techs reduce terrain penalties (e.g. Pathfinding makes forests cheaper)
- Techs open new biomes (e.g. Cold Weather Gear makes tundra passable)
- Roads (later eras) create fast corridors
- Pack animals add carrying capacity (provisions), not speed

Late-game founding feels different because the *landscape* is settled and connected, not because units are zoomier.

## Provisions

The Founder carries **provisions** — a depleting resource representing food, water, and supplies for the journey. Provisions are consumed each turn, faster in harsh biomes. When provisions hit zero, the Founder begins taking attrition damage.

Provisions can be replenished by:
- Returning to a friendly city
- Being accompanied by a Hunter or Forager (who provide ongoing supply)
- Researching techs like Preserved Foods (extends max provisions)
- Pack animals (later, increases carrying capacity)

## Attrition states

If a Founder is damaged or starving, it enters an **attrition state**:
- Movement reduced (down to 1, possibly 0 for a turn)
- Higher hazard susceptibility
- May suffer further incidents

Attrition is consequence, not surprise. A Founder reaching an attrition state should be the visible result of decisions the player made — and the player should still have time to react (turn back, hunker down, send help).

## Loss conditions

A Founder is lost (removed from the game) when:
- Killed in combat
- Reduced to 0 health by attrition or hazard
- Catastrophic incident (rare, only after multiple ignored warnings)

When a Founder is lost, the player receives a clear notification explaining what happened. Loss should feel like a story, not a glitch.

## Founding action

The `Found City` action:
- Requires the Founder to be on a valid tile (not impassable, not within another civilization's borders, minimum distance from existing cities — exact rules TBD)
- Consumes the Founder
- Spawns a new city on the tile
- Any units accompanying the Founder remain on the map and now belong to (or are stationed at) the new city

See [`04-founding-parties.md`](./04-founding-parties.md) for how escort units transition into the new city.

## Open questions

- **Production cost for new Founders.** Pure production cost (Civ-style) vs. population cost (Civ 4 / Old World-style). The latter is more historically grounded — you need surplus people before a group can leave to found elsewhere.
- **Founding distance restrictions.** Minimum distance from existing cities? Maximum distance the AI/UI will plan for?
- **Should Founders have any peacetime function?** Currently they have one job. Could they generate a small amount of culture/legitimacy while moving (representing the group spreading the parent civilization's identity)? Probably not — keeps the unit clean.
