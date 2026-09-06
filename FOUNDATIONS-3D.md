# Foundations � reconciled 3D edition

Open `Assets/Scenes/Foundations3D.unity`, or launch `Builds/Foundations3D/Foundations3D.exe`.

This edition reconciles the implemented gameplay from remote main (`823d426`) and `feat/hunter-city-population-growth` (`bc52804`) with the original 3D assets created in this workspace. The upstream history is retained alongside the 3D integration. The pre-reconciliation 3D scripts, editor utilities, and scene were backed up under `.tw-ai/reconciliation-backup`.

## Play

- Start with a **Founder** and **Hunter** together on adjacent land tiles. Both have two moves per turn; sight is two hexes for the Founder and three for the Hunter.
- Click a unit or press **Tab** to focus it. Click land to queue a shortest route. Water is impassable.
- **Space** or the action button moves the selected unit, skips its remaining action, or confirms the next turn. Units with queued routes move sequentially on later turns. Partial movement keeps unused moves available.
- **B / Found City** opens a naming dialog for an eligible Founder. Enter confirms; Escape cancels. Founding consumes the Founder and places a settlement with population 100 on its tile.
- Click a city to inspect population and growth. Its three-hex territory is outlined and permanently visible.
- Keep a Hunter within the city's territory for three turn ticks to gain one population. Multiple Hunters do not stack the rate. A turn without a Hunter resets partial growth. Growth is checked before queued movement at the start of the turn.
- **Arrow keys / WASD** pan; **left-drag** grabs the map; moving the pointer to screen edges pans; **right-drag** orbits; **scroll** zooms; **Home** recenters on the selected unit. Selection changes and moving units retain camera-follow behavior.

The map is the upstream **512 x 256 cylindrical world**, generated with seed 42: east-west coordinates wrap, north-south coordinates stop at the poles, and continents are separated by oceans. Terrain and fog use chunk culling; 3D trees and rocks are created only for currently visible tiles.

Fog has three states: visible, explored, and unknown. Only visible tiles show decorations and grid lines. Explored ground dims, then returns to unknown after ten turn ticks without sight. The minimap fits the currently discovered region. Friendly city territory never decays.

## Documentation

- `specs/07-implemented-gameplay.md` records the current executable rules and reconciliation scope.
- `specs/README.md` indexes the recovered design specifications, including Founder, Hunter, founding parties, hazards, technology, and fog.
- `SPEC.md` remains the broader design vision. Proposed hazards, provisions, escorts, combat, research, diplomacy, and trade are not implemented merely because they appear in the design documents.

## Assets and verification

`Assets/Foundations3D` contains original mesh/material assets and prefabs for Founder, Hunter, settlement, fir tree, and boulder. The Founder has a staff and travelling pack; the Hunter carries a bow and quiver. The settlement contains three thatched roundhouses and a standard. A material asset references the fog shader so it is included in player builds.

`Foundations3DBuilder.Build()` (menu: **Foundations / Create 3D Edition**) regenerates this edition and overwrites its generated scene. Save other scene edits before using it.

`ReconciliationVerification.Begin()` runs the integrated Play Mode regression scenario through the bridge. Results: `.tw-ai/reconciliation-verification.txt`. `Foundations3DVerification.BuildPlayer()` builds only this edition, without replacing the project build scene list; results: `.tw-ai/3d-build.txt`.

## Unity MCP

The official MCP for Unity package is pinned in `Packages/manifest.json`. Server version 10.2.0 is registered in Codex as `unityMCP` at `http://127.0.0.1:8080/mcp`.

```powershell
uvx --from mcpforunityserver==10.2.0 mcp-for-unity --transport http --http-url http://127.0.0.1:8080
```

`unity-mcp.ps1` talks to that MCP server. `unity-cmd.ps1` remains the project's editor bridge wrapper.
