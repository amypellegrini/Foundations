# Founding Parties

## Concept

A **founding party** is a Founder accompanied by support units (Hunters, Foragers, Warriors) traveling together to establish a new city.

This concept is not a hard-coded game object — it's an *emergent pattern* that the game's mechanics naturally encourage. A lone Founder is fragile and likely to die. A Founder with a Hunter is viable. A Founder with a Hunter and a Forager is well-equipped. The player's incentives lead them to assemble founding parties without the game forcing them to.

Foundations recognizes and rewards this pattern lightly through UI affordances, but does not require it.

## The historical truth

Lone-settler founding is a 4X convention, not a historical reality:
- Polynesian voyaging canoes carried navigators, fishers, planters, and warriors together
- Norse settling Iceland brought farmers, hunters, and craftsmen
- The Natufians and other early settlers were extended groups, not lone pioneers

A founding party is the truth of how human groups expanded. Foundations should make this feel natural.

## Composition

A typical Era I founding party:

| Unit | Role | Required? |
|---|---|---|
| **Founder** | The decision to settle; consumed on founding | Yes |
| **Hunter** | Hazard immunity (predators), food on arrival, local knowledge | Strongly recommended |
| **Forager** | Scouts the route ahead, reduces pathfinding hazard | Optional |
| **Warrior** | Protection against human enemies (less useful in early eras when civs are distant) | Optional |

The game does not enforce any composition. The player can send a lone Founder if they want to gamble. The game's hazard system makes that gamble visibly risky.

## The Founder/Hunter pairing

The Founder + Hunter pairing is the **archetypal founding party**. It works because:

1. **The Hunter solves the Founder's biggest Era I problem** — wild animal hazards. A Hunter escort grants the Founder immunity to predator attacks, the most common biotic threat.
2. **The Hunter becomes part of the new city.** When the Founder consumes itself to found the city, the Hunter remains. The new city starts with a working economic unit already in place.
3. **The Hunter brings local knowledge.** The territory around the new city is already partially scouted because the Hunter walked through it. Mechanically, the surrounding tiles could arrive partially revealed.
4. **It encourages distinctive expansion patterns.** A civ that builds many Hunters can sustain rapid expansion. A civ that neglects Hunters expands slowly and tentatively.

## What happens at the founding

When the Founder uses `Found City`:

1. The Founder is consumed.
2. The new city is created on the tile.
3. **Any escorting units remain on the map** and now belong to the new city as their home city.
4. *(Proposed)* Surrounding tiles within a small radius are revealed, representing the founding party's scouting on approach.
5. *(Proposed)* The escorting Hunter receives a temporary "settling-in" bonus to forage yield for N turns.

This avoids the Civ problem of the escort just standing around after the city is founded. In Foundations, **the escort *belongs* to the new city now.** The motion forward isn't reversed.

## UI affordances

The game should recognize founding parties without forcing them:

### Party indicator
When a Founder and one or more support units are stacked or moving together as a coherent group, the UI displays a **Founding Party indicator**. This surfaces:

- Combined provisions
- Expected travel time to a target
- Hazard assessment for the whole group (escort bonuses applied)
- Estimated survival chance

### Path planning
The player can right-click a destination and the game suggests a route. With a founding party, the route accounts for:
- The slowest unit's movement
- Hazard mitigation from escorts
- Provision constraints

The player isn't obligated to follow the suggested route, but it's surfaced.

### Group movement
A founding party can be moved as a group with a single command, with the game handling the individual unit movements (slowest unit sets the pace). This avoids tedious micromanagement.

## Why this matters for the game's feel

The founding party concept ties together everything else in Foundations:

- **It makes the Founder's fragility tractable.** Players don't feel cheated; they understand they need an escort.
- **It makes Hunters essential.** Hunters aren't an optional military side-investment; they're how you safely expand.
- **It gives expansion narrative weight.** Sending out a founding party feels like dispatching an expedition, not placing a piece.
- **It creates story moments.** "My founding party crossed the Northern Wastes and built a city on the far side" is a memorable outcome. "My settler moved 12 tiles and right-clicked" is not.

## Failure modes

Things that can go wrong with a founding party, and how the player can adapt:

| Situation | Player options |
|---|---|
| Ambush in difficult terrain | Pull back to safer tile; fight with escort; hunker down |
| Hazard incident en route | Camp to recover provisions and HP |
| Predator attack on unescorted Founder | (Should be rare — escort the Founder!) |
| Founder reaches destination but tile is now unsuitable | Choose adjacent tile or continue searching |
| Escort lost during journey | Continue at higher risk, or turn back and try again |

The game should make all of these recoverable in most cases. Catastrophic loss (entire party wiped out) should be rare and earned by genuinely reckless choices.

## Open questions

- **Should the game formalize "party" as an object** (a named expedition with shared state)? Or keep it emergent (just a group of units happening to travel together)? Emergent is more flexible; formalized makes the UI cleaner.
- **Should the player be able to "commission" a founding party** from a city — i.e. a single action that produces Founder + Hunter + Forager together? Could be a nice mid-game convenience once the player has internalized the pattern.
- **How do other civs perceive a founding party?** A group of units moving through neutral territory could be diplomatic incident, opportunity for raid, etc.
- **What about sea/river founding parties?** Out of scope for this spec but a future concern. Polynesian-style ocean voyaging is a beautiful design space.
