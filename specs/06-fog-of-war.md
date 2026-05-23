# Fog of War

## Concept

Players (and AI civilizations) do not see ground truth. They see what their
units, settlements, and contacts have line of sight to, and they *remember*
what they have seen. Everything else is dark — and what is dark is genuinely
unknown, not just rendered as grey.

Fog of war is the **first surface** of the information-asymmetry pillar
([`SPEC.md` §2.1](../SPEC.md)). Other systems (presented vs. true identity,
spies, merchant reports, stale intelligence) layer on top of it later. This
spec covers only the basic three-tier visibility model.

## Three visibility tiers

Every tile, for every civilization, is in exactly one of three states:

| Tier | Definition | Render |
|---|---|---|
| **Visible** | At least one friendly unit (or settlement) has direct line of sight to the tile *this turn*. | Full color. Tile texture, decor, and grid borders all visible. Live state — units, hazards, animations all render here. |
| **Explored** | Has been visible at some point in the past, but is not visible right now. The tile carries a **freshness** value — 100% the turn the unit leaves it, decaying by 10 percentage points each turn. | Dimmed under a translucent dark overlay whose opacity scales with how stale the memory is. At 100% freshness the overlay is light enough to read the ground beneath; at 10% it is almost as dark as Unknown. Grid borders are not drawn; decor is hidden. |
| **Unknown** | Has never been visible — or was Explored and the memory has fully decayed. | Fully covered by a near-opaque dark overlay. No ground, no decor, no borders — just shadow. |

Memory is not permanent. Explored is a transitional state: every turn that
passes without re-establishing sight, the tile slides ten percentage points
closer to Unknown, and after ten such turns it returns to Unknown
entirely. Re-entering sight at any point resets the tile to fresh.

```
Unknown                  --(enters sight)----->  Visible
Visible                  --(leaves sight)----->  Explored (freshness 100%)
Explored                 --(re-enters sight)-->  Visible
Explored                 --(turn elapses)----->  Explored (freshness −10%)
Explored (freshness 0%)  --(turn elapses)----->  Unknown
```

The decay is what makes fog feel **temporal** rather than just spatial.
Old memory should look old; very old memory should be indistinguishable
from never having seen the tile at all. This is the seed of the
stale-intelligence system ([`SPEC.md` §3.4](../SPEC.md)) — confidence in
what you know about a tile is bounded by how recently you saw it.

## Sight and how it propagates

Each unit has a **sight radius**, expressed in hex tiles (Chebyshev/cube
distance). A unit illuminates every tile within that radius, *including its
own tile*. The Visible set for a civilization is the union of every friendly
unit's illuminated tiles plus every settlement's illumination.

### Per-unit sight radii (initial slice)

| Unit | Sight radius |
|---|---|
| Founder | **2** |
| Hunter | 3 *(per [`03-hunter-unit.md`](./03-hunter-unit.md); not yet implemented)* |
| Settlement | 2 *(not yet implemented)* |

Sight radius is independent of movement. A unit with no moves left still
sees as far as it did the turn before.

### Future modifiers (not in initial slice)

These are listed so the system is built with room for them, not because
they are implemented yet:

- **Terrain blocks sight.** Forests, hills, and mountains will eventually
  occlude line of sight — a unit on a plain sees less far through a forest
  than across open ground.
- **Elevation extends sight.** A unit on a hill sees further than one in a
  valley.
- **Night and weather** reduce sight radius temporarily.
- **Scouts and lookouts** (units, buildings, doctrines) extend sight.
- **Stale intelligence** ([`SPEC.md` §3.4](../SPEC.md)) timestamps what was
  last seen on an Explored tile, so the player can see *when* their memory
  was formed.

## What the player can and can't do in each tier

| Action | Visible | Explored | Unknown |
|---|---|---|---|
| Click a tile to select / inspect | yes | terrain only, no live state | no |
| Order a unit to move there | yes | yes (route planned across known map) | no — the route can be ordered into Unknown, but the player commits without seeing what is there |
| See enemy units on the tile | yes | no (only what was seen when it was last Visible — to be added later with stale intelligence) | no |
| See hazards / wildlife | yes | no | no |
| See decorations / grid lines | yes | no | no |

The "order a move into Unknown" rule matters: pathing through fog is how
exploration *feels like* exploration. The player commits to a direction and
discovers what is there as the unit walks.

## Multiple units, multiple civilizations

- Sight is **per civilization**. Two friendly units share fog state.
  Enemy civilizations have their own, independent fog state of the map.
- The same tile can be Visible to one civ, Explored by another, and Unknown
  to a third, all at once.
- The current single-player prototype only renders the human player's fog.
  AI civilizations will operate under their own fog state without rendering
  it ([`SPEC.md` §4](../SPEC.md)).

## When fog updates

Two clocks drive fog state:

- **Sight changes** — recompute whenever a unit's tile changes, both at
  the start of a turn (unit position may have changed) and during a slide
  animation (so the reveal happens in step with the unit's movement, not
  in an instant after). Newly-illuminated Unknown tiles transition
  straight to Visible; the Explored state only applies when a tile that
  *was* Visible last frame is no longer in the union.
- **Turn ticks** — each time a turn advances, every Explored tile loses
  10% freshness. Tiles whose freshness reaches zero transition to
  Unknown. Freshness only ticks on full turn advances, not on every
  player action.

## Rendering notes (implementation)

- The fog overlay is a set of regenerated meshes layered above the hex
  grid, rebuilt only when the tile state changes — not every frame.
- To support the freshness gradient, the overlay is split into one mesh
  per decay step plus one for Unknown. Each mesh shares the same base
  material; per-mesh alpha is overridden via `MaterialPropertyBlock`.
  A tile at freshness step *k* (0 = just lapsed, *decayTurns − 1* = about
  to fully decay) draws at `lerp(exploredAlpha, unknownAlpha, k / decayTurns)`.
- Grid border lines are part of the fog system: only tiles in the Visible
  tier draw their borders. Explored tiles look like dimmed ground without
  the grid structure on top; Unknown tiles are pure shadow.
- Decorations (grass tufts, etc.) are toggled off on every non-Visible
  tile. They represent things the player can currently *see*, not features
  of the ground that are remembered.

## Open questions

- **Sight through partial obstruction.** When forests reduce sight, do
  they block at the edge of the obstructing tile or one tile beyond? Both
  are defensible; the latter feels more generous and probably reads
  better.
- **Settlements and their footprint.** A settlement should illuminate a
  larger radius than a single unit (city walls, watchmen, traffic), but
  the radius should not feel infinite. Maybe 3 tiles for a small
  settlement, scaling with city size or specific doctrines.
- **Should Explored show *when* it was last seen?** A subtle "last seen
  turn N" overlay sets up the stale-intelligence system cleanly but adds
  UI weight. Probably worth it — but not in this slice. The continuous
  freshness fade already conveys recency at a glance, which may be
  enough for the prototype.
- **Decay rate per biome.** A scout's memory of an open plain probably
  ages slower than their memory of a contested border. Holding the rate
  flat (10% per turn) is fine for the slice; per-biome or
  per-circumstance decay can come later.
- **Fog of war and the AI.** The AI is supposed to operate under the same
  fog ([`SPEC.md` §4](../SPEC.md)). The data model already supports
  per-civ fog; the AI behavior that *uses* it is a separate, later spec.
