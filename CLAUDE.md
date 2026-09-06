# Foundations — working instructions

A Unity 6 (URP) 4X strategy game. Design vision lives in `SPEC.md`; build it
in small, iterative, playable slices rather than all at once.

## Always drive the Unity editor through the Unity Bridge

This project has the **Unity Bridge** package installed. Whenever a task needs
the Unity editor — creating or wiring up GameObjects, editing the scene,
compiling scripts, inspecting the hierarchy, checking how something looks —
**use the bridge.** Do not ask the user to do editor steps by hand, and do not
assume a change took effect without verifying through the bridge.

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

### Standard workflow for a new feature

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
