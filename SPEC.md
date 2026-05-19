# Foundations

*A 4X strategy game about civilizations as flows, not stocks.*

---

## 1. Design Thesis

Mainstream 4X games (Civilization, Humankind, Old World, Millennia) share a foundational modeling error: they treat the world as a collection of **discrete things you own** — tiles, units, wonders, technologies — competed for in zero-sum production races with transparent state.

History is not like this. Civilizations are defined less by what they possess than by **what moves through them and what they send outward**: goods, ideas, doctrines, art, rumors, refugees, religions, fashions, military techniques. The Pyramids are not impressive because Egypt "got there first"; they are impressive because three thousand years later people still travel to see them and other cultures built imitations.

**Foundations** is built on the inverse premise:

> A civilization is a participant in flows. Its strength is measured by what propagates from it and what it can shape, intercept, redirect, or hide.

Every major system in the game is an expression of this thesis. The map is not a board of tiles to be claimed; it is a network across which things travel. Victory is not the accumulation of stocks; it is the lasting influence of what you set in motion.

The name *Foundations* is provisional but captures two key ideas: (1) what you build is load-bearing for what comes after, and (2) foundations are themselves often invisible, contested, and mythologized after the fact.

---

## 2. Core Pillars

Four interlocking pillars define the game. Each addresses a specific frustration with conventional 4X design and each reinforces the propagation thesis.

### 2.1 Information Asymmetry and Deception

What you see on the map is what is **presented**, not what is true. Armies have banners and apparent compositions; their real contents are knowable only through contact, espionage, or inference. Cities present official narratives that may not match their internal state. Treaties can be broken, but breaking them is costly because reputation itself is a resource that propagates.

Information has provenance, age, and confidence. Stale intelligence is shown as stale. Spies do not appear in a side panel — they are the primary lens through which you see anything beyond your borders.

### 2.2 Flow-Based Economy and Warfare

Trade is not a number negotiated in a popup. Goods physically move along routes; routes can be taxed, blockaded, rerouted, or imitated. Geography produces chokepoints that matter.

War is overwhelmingly a logistics problem. Armies consume supply that must come from somewhere mappable. Most wars are decided by who runs out of food or money first; pitched battles are rare and consequential. "Strangling your neighbor's tin imports" is a legitimate and often superior alternative to invading them.

Trade and war share a substrate. Embargoes are acts of aggression. Naval power exists to protect commerce. Merchants and spies are the same kind of entity wearing different clothes.

### 2.3 Doctrine, Adoption, and Era Inhabitation

Technologies are not unlocked; they are **adopted**, gradually and partially. "Heavy cavalry exists in the world" is different from "my civilization has deeply committed to heavy cavalry doctrine for two hundred years." Two civilizations in the same era can look radically different because they have adopted different things to different depths.

Eras are spaces to inhabit, not gates to pass through. The interesting decisions happen *within* an era — which form of feudalism, which military tradition, what tensions emerge between knightly culture and merchant culture — not in how quickly you escape it. Tech progression is slow and broad rather than fast and linear.

Doctrinal experience compounds. A civilization that has practiced a doctrine for centuries is qualitatively better at it than one that just adopted it. Obsolescence is itself contested: when gunpowder arrives, knights do not become useless overnight; there is a long, messy transition in which the social class of knights resists their military obsolescence.

Units are not built one at a time. Standing forces scale organically with the social and economic structures that support them. If your civilization has a knightly order, you have as many knights as your feudal economy can sustain — not the six you had time to queue up before gunpowder arrived.

### 2.4 Cultural Abundance and Canonization

Civilizations do not produce one great artist per century. They produce constantly. Most output is mediocre or forgotten. The interesting question is not *whether* you produce notable figures and works but **which ones rise, get patronized, get exported, and get remembered**.

Wonders are not unique global trophies competed for in a build race. Both Egypt and Babylon may build monumental architecture; what matters is whose monumental style **influences** the next civilization's monumental architecture. Multiple Pyramids coexisting is historically accurate. Legacy, not uniqueness, is the contested resource.

Canonization is a mechanic. Critics, academies, religious institutions, and foreign admirers decide what is "great." Your rivals' tastemakers can elevate or dismiss your art. Figures have afterlives: a forgotten 12th-century philosopher may be rediscovered by a rival civilization four centuries later and reshape *their* culture in ways you did not intend.

---

## 3. Systems Detail

### 3.1 The Map

Hex-based. Tiles have terrain, resources, and visibility states. The novel layer is the **network graph** overlaid on the tile grid: routes connecting settlements, carrying flows of goods, information, people, and influence. Routes are first-class objects with capacity, security, and ownership properties.

Visibility is per-tile and time-stamped. "Last seen" matters. The map remembers what you saw and shows it as stale, with optional UI to dim or annotate aged information.

### 3.2 Settlements

Settlements are nodes in the flow network, not production buildings with queues. Each settlement has:

- **Population and composition** (rough social classes: laborers, artisans, merchants, clergy, nobility — varying by era and adopted doctrines)
- **Institutions** (academies, guilds, religious houses, military orders) which grow, decline, and influence what propagates
- **Outputs as continuous flows** rather than discrete completed items: cultural production, scholarly output, trade goods, recruits

Buildings exist but represent infrastructure that shapes flows, not slots that produce items on a timer.

### 3.3 Trade and Goods

Goods are tracked entities that move along routes. A trade route is a contract between settlements (and possibly intermediaries) with a path through the network. Disruption — banditry, blockade, war, weather — interrupts specific flows on specific routes, with downstream consequences.

Merchants and trading houses are semi-independent actors with their own interests. They are not extensions of state will. They can be courted, taxed, suppressed, or co-opted; they can also be bought by rivals or operate across borders in defiance of state policy.

Smuggling exists as a parallel flow layer when official trade is blocked.

### 3.4 Information and Espionage

Every piece of information about another civilization has:

- A **source** (direct observation, merchant report, spy network, foreign envoy, rumor)
- A **confidence level**
- A **freshness timestamp**
- A **possibility of being planted** by an adversary

Spies are not a minigame. They are the substrate of foreign perception. You see what your spies and merchants report; what you do not have eyes on is fog. Counter-espionage involves feeding adversary spies false information rather than simply catching them.

Units in the field broadcast a **presented identity** (banner, apparent composition, apparent numbers). Their true state is hidden until contact or successful intelligence penetration. Decoy units, false banners, and phantom armies are legitimate unit types or doctrines.

### 3.5 Military

Standing forces, scaled by supporting social structures. You do not queue knights; you maintain a knightly order whose size reflects your feudal economy.

Supply lines are mapped and finite. Armies in the field consume supply that flows from supporting settlements through the route network. Cutting supply is often a decisive act of war.

Combat resolution favors the side that engineered the conditions of engagement. Surprise, deception about composition, supply state, and doctrinal experience all matter more than raw stack size. Many "battles" are decided before contact — by maneuver, by deception, by attrition.

Doctrines are persistent modifiers that compound over time. A doctrine your civilization has practiced for two centuries is meaningfully better than one freshly adopted. Doctrines propagate across borders through mercenaries, prisoners, observers, and defeated enemies.

### 3.6 Culture and Canonization

Cultural output is continuous and ambient. Your civilization is constantly producing artists, philosophers, monuments, treatises, and notable figures. Most are unremarkable.

Canonization mechanics determine which outputs rise:

- **Patronage** (state, religious, private) elevates specific works and figures
- **Institutional endorsement** (academies, religious authorities) confers legitimacy
- **Foreign reception** is the dominant long-term factor: works exported, translated, copied, or imitated abroad accumulate influence
- **Rediscovery** allows past figures to gain new prominence centuries later, sometimes in rival civilizations

Influence is measured by propagation: how many other civilizations' institutions teach your texts, copy your styles, revere your figures. A wonder you built that nobody else cares about is just a big rock.

### 3.7 Diplomacy and Reputation

Treaties are commitments with mechanical weight because breaking them has propagating consequences. Reputation is tracked per-civilization and per-action, decays slowly, and affects all future negotiations.

Diplomacy is not a popup; it is an ongoing flow of envoys, gifts, marriages, hostage exchanges, and signals. Misreading or being deceived about another civilization's intent is common and often consequential.

### 3.8 Time and Eras

Turns represent variable timespans but the game runs on long horizons. Eras are inhabitable spaces with depth, not gates. A single era may span dozens or hundreds of turns of meaningful play before the transition to the next becomes a contested process rather than a click.

Tech adoption is gradual and partial. The "tech tree" is closer to an adoption graph where depth of commitment matters as much as breadth.

---

## 4. AI Design Notes

The AI operates under the **same information constraints as the player**. It does not see ground truth; it sees banners, reports, and rumors. Its mistakes are intended to feel earned because they come from the same fog the player operates in.

This is deliberately easier than building "smart" AI in the conventional sense. The AI does not need to be brilliant; it needs to be **fooled in believable ways** and to act on plausible assumptions that may be wrong. This aligns AI behavior with the design thesis rather than fighting against it.

AI civilizations have personalities expressed as doctrinal preferences, risk profiles, and reputation sensitivities. They are not min-maxers; they are participants in the same flow-based world the player inhabits.

---

## 5. Scope and Prototype Strategy

This document describes the full design vision. A first prototype should **not** attempt to implement all of it.

The recommended slice for an initial Unity prototype is a vertical test of the **information asymmetry layer**: a small hex map, two or three civilizations, units with presented-versus-true identity, fog of war with staleness, and a basic spy/scout system that exposes the difference. If deception feels good in five minutes of play, the design has spine. If it does not, the rest of the systems will not save it.

Subsequent slices, in rough priority order:

1. Flow-based trade routes between two or three settlements, with a blockade mechanic
2. Supply-driven combat between two small armies
3. Doctrinal adoption with compounding experience
4. Cultural output and a minimal canonization/reputation system

Each slice should answer one design question and be playable in isolation before integration.

---

## 6. Open Questions

These are unresolved and worth holding lightly until prototyping reveals what the game wants:

- How is the player surface kept legible when so much state is hidden or fuzzy? A "fog-first" UI is a genuine design challenge.
- How long should a typical game be in real time? The "inhabit eras" philosophy implies longer games, but long 4X games have well-known late-game tedium problems.
- How many civilizations make for good information dynamics? Too few and deception is trivial; too many and the player drowns in fog.
- What is the win condition? "Influence" is the obvious answer given the thesis but needs to be made concrete without collapsing back into a points race.
- Multiplayer is a natural fit for the deception layer but vastly expands scope. Single-player first.

---

## 7. Non-Goals

To keep scope honest, the following are explicitly **not** part of Foundations:

- A complete history-of-the-world simulation. The game is a designed system inspired by historical dynamics, not a historical model.
- Faithful Civilization-style breadth (religion, espionage, tourism, governments, etc. as separate subsystems). Foundations unifies these under the flow/information thesis.
- Real-time combat or tactical battle resolution. Combat is resolved at a strategic level; tactical detail lives in the conditions of engagement, not in moment-to-moment unit micromanagement.
- Asymmetric civilization "uniques" as the primary differentiator. Differentiation emerges from adopted doctrines and accumulated legacy, not from hardcoded faction bonuses.
