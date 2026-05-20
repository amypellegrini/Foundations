# Tech Tree — Era I: The Sedentary Era

## Concept

Era I is **the Sedentary Era** — the pre-agricultural period when humans transitioned from nomadic foraging to staying in one place. This is a deliberate departure from the standard 4X "Stone Age" bucket, which usually treats agriculture as a prerequisite for cities.

In Foundations, **settling precedes agriculture** — historically accurate (Natufian culture, Göbekli Tepe) and mechanically more interesting. Cities exist from turn one; what evolves in Era I is *how cities sustain themselves and how civilizations spread*.

## Design goals for the tree

- **Slow evolution.** No single tech transforms the game. Each tech adds an incremental capability that compounds with others.
- **Parallel reinforcing threads.** Multiple branches (subsistence, shelter, social, mobility) develop in parallel and unlock each other.
- **Tech changes the map, not just the units.** Many techs make hazardous terrain less hazardous, or extend the safe range for Founders.
- **Tech enables abilities, not just stats.** Researching a tech often unlocks a *new thing a unit can do*, not just a numeric buff. This is especially true for the Hunter.

## The seven foundational techs

### Stone Tools *(starting tech, free)*
The baseline. Every player begins with this researched.
- Unlocks: basic Forager and Hunter actions; Founder's `Found City` action; basic tile clearing
- Prerequisite for every other Era I tech

### Food Processing
Sickles, grinding stones, mortars and pestles. The toolkit that turns wild grains and nuts into edible staples.
- Unlocks: meaningful yield from wild grain and nut tiles
- Without it, foraging is thin; with it, a city in a good biome can sustain itself indefinitely on wild resources
- Prerequisite for: Storage
- *Historical note*: this is what made Natufian villages viable

### Storage
Pit storage, basketry containers, sealed vessels. Preserves seasonal food into year-round supply.
- Unlocks: **Granary** improvement; food reserve buffer for cities
- Reduces the impact of bad seasons / yield fluctuations
- Prerequisite for: Preserved Foods (Era II)
- *Historical note*: storage may have been the actual pivot point for sedentism

### Shelter
Durable dwellings, hearths, organized living space.
- Unlocks: increased city population cap (beyond a small initial size)
- Without it, you have a camp, not a town
- Prerequisite for: Storage

### Ritual Gathering
The Göbekli Tepe branch. Monumental construction and ceremonial spaces that draw people together.
- Unlocks: **Gathering Place** improvement (yield bonus to surrounding tiles + acts as a growth magnet)
- Gives an early non-military way to differentiate civilizations culturally
- Prerequisite for: future culture/religion branch (Era II+)
- *Historical note*: gathering for ritual may have driven sedentism, not the other way around

### Wild Herd Management
Pre-domestication husbandry. Tagging wild herds, managing populations, light penning.
- Unlocks: Hunter's **Bait** ability
- Lets Foragers "tag" wild animal herds for slow trickle of food yield without depleting them
- Prerequisite for: Plant Domestication and Animal Domestication chains (Era II)

### Cordage & Basketry
Fibers, knots, weaving. The humble-but-essential tech.
- Unlocks: Hunter's **Snare** ability; fishing nets; improved Forager traps
- Prerequisite for: pottery (Era II), textile techs (Era II), and many others

## Hazard-mitigation techs (Era I)

These techs specifically address environmental hazards on Founder journeys. They can be researched in parallel with the foundational techs.

### Hide Working
Working animal skins into furs and rough clothing.
- Unlocks: Hunter's **Camouflage** ability; partial cold resistance for escorted Founders
- Requires: Wild Herd Management (need animals to skin)

### Waterskins
Containers for transporting water.
- Unlocks: light arid-terrain resistance; extends Founder/Hunter range in deserts
- Requires: Cordage & Basketry

### Pathfinding
Reading the land, recognizing landmarks, basic wayfinding.
- Unlocks: Hunter's **Track** ability; reduced "getting lost" hazard for Founders
- A knowledge tech rather than a material one
- Requires: Stone Tools only (independent thread)

## Dependency graph

```
                            Stone Tools (start)
                          /  |  |  |  |  |  \
                         /   |  |  |  |  |   \
        Food Processing  Shelter  Cordage  Ritual Gathering
              \              \      |  \          |
               \              \     |   \         |
                Storage <-----+     |    Hide Working
                                    |        |
                                    +--> Wild Herd Management
                                    |
                                    +--> Waterskins
                                    
                            Pathfinding (independent)
```

Notes on the graph:
- **Storage requires both Food Processing and Shelter** — you need something worth storing and somewhere to store it.
- **Hide Working requires Wild Herd Management** — you need access to animals.
- **Waterskins requires Cordage** — you need the fiber technology to make them.
- **Ritual Gathering stands alone** — appropriately, as the cultural/social branch should not be gated by subsistence.
- **Pathfinding is fully independent** — represents knowledge accumulated through exploration, not material progress.

## What Era I doesn't include

Deliberately absent from Era I:

- **Agriculture / farming.** Pushed to Era II. Cities in Era I subsist on foraging, wild grain harvesting, and managed herds.
- **Pottery.** Surprisingly late in the Near East historically; comes in Era II.
- **Animal domestication.** A multi-step chain starting in Era II.
- **Plant domestication.** Likewise.
- **Organized military.** Hunters are the martial tradition in Era I. Warriors come in Era II with organized conflict between sedentary groups.
- **Irrigation, wells.** Era II — the techs that allow large urban populations.

## What Era I makes possible

By the end of Era I, a civilization should be capable of:

- Sustaining 2–4 cities on improved foraging and wild resources
- Founding a city after a multi-turn journey through moderately hazardous terrain (with a proper founding party)
- Producing Hunters with several specialized abilities (some combination of Track, Camouflage, Snare, Bait)
- Beginning to differentiate from neighboring civs through tech choices (e.g. ritual focus vs. subsistence focus)

The transition to Era II is gradual — there's no era-unlock event. As cities accumulate techs in storage, food processing, and herd management, the prerequisites for Era II's domestication chains naturally come into reach.

## Open questions

- **Research speed.** How long should an Era I tech take in turns? This depends on turn scale (see [README](./README.md) open questions).
- **Research cost model.** Flat cost, scaling cost, or population/yield-driven? Affects pacing.
- **Should some Era I techs be regional?** E.g. Waterskins is only really useful if you're near desert. Maybe the cost is lower if you have relevant terrain in your borders.
- **Era I victory conditions?** Probably none — Era I is too short for win conditions. But "techs researched" might be a milestone metric.
- **Should ancient civilizations that *never* leave Era I be a valid playstyle?** A pure foraging civilization that focuses on Ritual Gathering and never adopts agriculture is historically plausible (San peoples, some Amazonian groups). Could be a deliberate identity choice.
