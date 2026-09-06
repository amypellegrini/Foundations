# Implemented gameplay — reconciled 3D edition

This document describes implemented behavior, not the wider design ambition. Baseline: remote main `823d426` plus population-growth branch `bc52804`, reconciled on 2026-09-06. The 3D edition retains those gameplay systems and replaces their presentation with original modeled assets.

## World and movement

- Procedural 512 x 256 flat-top hex world with land and water, using the upstream noise generator and seed 42. Starting position is a random eligible land anchor, with the second unit on neighboring land.
- East-west wrapping; hard north/south boundaries. Pathfinding uses shortest uniform-cost land paths, including the wrap seam. Water cannot be entered by land units.
- Terrain height is visual: it does not introduce new movement penalties or line-of-sight rules. Trees and rocks are decorative.
- Each unit receives two movement points per turn. A queued move can spend fewer points if its destination is closer. Remaining points can be used for another order.
- Space commits movement or skips the selected unit; completed actions advance focus to another available unit. Tab can review spent units without spending an action.
- Finishing movement does not automatically advance the turn. An explicit Space/action-button press advances once all units have acted. Queued routes then execute one unit at a time, followed by focus reset.

## Units

The Founder and Hunter are distinct units. The Founder can found a city and has sight radius two. The Hunter cannot found and has sight radius three. Both contribute friendly vision even when they have spent their movement.

Name captions appear for the focused unit. The roster shows each unit's remaining moves, done state, and path length; the HUD shows the current turn and action prompt.

## Founding and settlement

An idle Founder with an unspent action and at least one move may found on land without an existing city. B or the button opens the name prompt. Empty names default to Rome, whitespace is trimmed, and the UI limits names to 32 characters. Cancel does not consume the unit. Confirm rechecks eligibility, creates the city, removes the Founder, and hands focus to a surviving unit.

Population starts at 100. Territory is a disk of hex distance three, clipped at the poles and wrapped east-west. The outer territory boundary is outlined. The city permanently reveals that territory. Clicking its tile or 3D model opens the population/growth panel; Close, Escape, or an outside click dismisses it. Modal UI suppresses world actions and camera controls.

Each turn, before queued auto-movement, a city checks for at least one Hunter within its territory. An eligible tick adds one to growth progress. Every third eligible tick adds one population and resets progress to zero. Additional Hunters do not increase the rate. A tick with no Hunter clears progress and the active-growth state. Returning restarts progress. The city caption shows the active +1 countdown.

This prototype does not yet enforce all proposed settlement-distance, ownership, or escort rules described in design specs.

## Visibility and information

Friendly unit and city sight are combined. Visibility follows each unit's animated position rather than jumping directly to its queued endpoint. Visible tiles show land/water, decorations, and grid lines. Leaving sight makes a tile explored; its memory becomes darker each turn and expires to unknown after ten ticks. Unknown tiles are fully obscured. City sight does not expire. The minimap depicts only currently visible/explored terrain and friendly unit positions, fitting the discovered region rather than showing the entire world.

Routes can be ordered into unknown terrain. The current upstream pathfinder still knows the land/water layer; knowledge-limited path planning is a future refinement.

## Presentation and input

The 3D edition preserves pan, zoom, edge pan, left-drag pan, focus following, turn recentering, and Tab navigation. Right-drag additionally orbits the 3D camera; Home recenters it. Click actions commit on release, so dragging does not also issue orders. Pointer input over the HUD/minimap is blocked from reaching the world.

Meshes are chunked and off-screen chunks are disabled. Wrap copies share terrain/fog geometry. Visible 3D decorations are generated separately and shifted to the camera's world copy. All rendering materials are referenced assets for build inclusion.

## Still design-only

Provisions, environmental hazards, attrition, combat, escort bonuses, founding-party group movement, Hunter forage/stealth abilities, technology adoption, trade, diplomacy, culture, additional Founder production, AI civilizations, and victory conditions remain design work. They were not implemented in either recovered branch.

## Verification

`ReconciliationVerification.Begin()` checks world dimensions, spawning, water blocking, seam paths/picking, arrow input, Tab focus, modal cancellation, sequential movement, explicit rollover, founding/consumption, city territory vision, growth timing/reset, and ten-turn fog expiry. It writes `.tw-ai/reconciliation-verification.txt`. The separate Windows build uses only `Foundations3D.unity`.
