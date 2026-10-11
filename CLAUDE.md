# Foundations — working instructions

A Unity 6 (URP) 4X strategy game. Design vision lives in `SPEC.md`; build it
in small, iterative, playable slices rather than all at once.

## Always drive the Unity editor through MCP for Unity (fallback: Unity Bridge)

Whenever a task needs the Unity editor — creating or wiring up GameObjects,
editing the scene, compiling scripts, inspecting the hierarchy, checking how
something looks — drive it yourself. Do not ask the user to do editor steps by
hand, and do not assume a change took effect without verifying it in the editor.

### Preferred: MCP for Unity

The **MCP for Unity** package (`com.coplaydev.unity-mcp`) exposes the editor as
`mcp__UnityMCP__*` tools over HTTP at `http://127.0.0.1:8080/mcp`. The server
is started from Unity via **Window → MCP for Unity → Start Server**; if the
tools are missing or report a connection error, ask the user to start it and
run `/mcp` to reconnect.

- Read editor state through resources (`mcpforunity://editor/state`,
  `mcpforunity://instances`, `mcpforunity://custom-tools`); act through tools.
- After script changes, `refresh_unity`, then `read_console` for compile
  errors before using new types. `data.advice.ready_for_tools` on
  `mcpforunity://editor/state` says when the domain reload is done.
- Use `manage_scene`, `manage_gameobject`, `manage_components`, etc. for scene
  work; `manage_scene` `save` before entering Play Mode (a dirty scene opens a
  modal dialog that blocks the editor).
- Verify visuals with `manage_camera` screenshots, then `Read` the PNG.
- `unity-mcp.ps1` is a raw JSON-RPC client for the same server, for debugging
  outside the MCP tools.

### Fallback: Unity Bridge

If MCP for Unity is unavailable, or a task hits a gap in its tools, use the
**Unity Bridge** package (`com.cziberpv.unity-bridge`).

Send commands with the wrapper script. Pick the one for your OS — both
talk to the same file-based bridge (`Assets/LLM/Bridge/request.json` →
`response.md`), so they're interchangeable.

Windows (PowerShell):

```powershell
& "C:\Users\amyvp\Foundations\unity-cmd.ps1" '{"type":"help"}' -Timeout 90
```

macOS / Linux (bash):

```bash
/Users/amypellegrini/Documents/GitHub/Foundations/unity-cmd.sh '{"type":"help"}' -t 90
```

`{"type":"help"}` lists every command. For complex JSON, pass a file with
`-File <path.json>` (PowerShell) or `-f <path.json>` (bash) instead of an
inline string.

### Bridge workflow for a new feature

1. Write or edit C# under `Assets/` with the normal file tools.
2. `{"type":"refresh"}` — compile. Check the result reports **Success**;
   fix any errors before continuing. Use `-Timeout 180`.
3. Create/wire GameObjects with `create`, `set`, `add-component`, etc.
4. `{"type":"save-scene"}` before entering Play Mode — a dirty scene makes
   Play Mode open a modal dialog that freezes the bridge.
5. `{"type":"screenshot"}` to verify the result visually, then `Read` the
   PNG it returns. Verify visuals; don't assume.

### Known bridge quirks (learned the hard way)

- **`set` properties:** use the single-property form
  `{"type":"set","path":...,"component":...,"property":"m_Foo","value":...}`.
  The `properties` array form did not bind names correctly.
- **Quaternions** can't be set via `set` ("not supported"). Set rotations
  through the `scratch` command instead (`transform.rotation = Quaternion.Euler(...)`).
- **Batch arrays** (`[ {...}, {...} ]`) failed to parse here — send commands
  one at a time.
- **`scratch`:** edit `Assets/Editor/BridgeScratch.cs`, `refresh`, then run
  `{"type":"scratch"}` for one-off editor C#. Reset it to the empty template
  when done.

## Builds vs. editor

Things that work in the editor can break in a player build. Notably: a shader
must be referenced by an asset to be included in a build — create **material
assets** rather than calling `Shader.Find` + `new Material` at runtime. When a
change could behave differently in a build, say so and ask the user to verify.

## Git

Commit only when asked. Atomic commits, Conventional Commits format — see
`.claude/skills/commit`.
