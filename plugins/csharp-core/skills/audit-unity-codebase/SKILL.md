---
name: audit-unity-codebase
description: "Audits a whole Unity 6 project or folder in one pass: bugs, lifecycle and native leaks, what breaks at the target frame budget or object count, risky code without tests, slow per-frame paths, and code to delete, merge or split. Ranked, numbered findings in plain English, each with a concrete failure case. One-shot report, changes nothing. Triggers: \"audit this codebase\", \"review the whole repo\", \"find bloat\", \"what can I delete\", \"is this project healthy\". Not for a single diff or script (use review-unity-csharp) or Project Auditor CSVs (use project-auditor-fixes)."
license: MIT
metadata:
  category: csharp-core
  sources: "https://github.com/DietrichGebert/ponytail/tree/main/skills/ponytail-audit"
  unity: "6000.0+"
---

# Audit a Unity codebase

Audit like the developer who just inherited the project and must ship it. Order of importance: correct, safe, holds at the target load, tested, fast, lean. Lean still matters: every extra line must be read, tested and fixed later. This is a report the user asked for, so give it in full.

## Workflow

1. **Scope.** Audit what the user names: a folder, an assembly, or the whole project. Nothing named: everything under `Assets/` and embedded `Packages/`, excluding third-party and generated code.
2. **Context.** Read the README and `CLAUDE.md`/`AGENTS.md`, `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, the `.asmdef` layout, Enter Play Mode Options (`m_EnterPlayModeOptions` in `ProjectSettings/EditorSettings.asset`), the boot scene and the test assemblies.
3. **Find the target load.** Platform and frame budget (for example 16.6 ms at 60 fps on PC, 33 ms on mobile), and the largest counts the game creates: instances, agents, terrain tiles, particles, scene objects. Judge scale against that and say which load you assumed.
4. **Trace the main flows end to end:** boot and scene load, per-frame update loops, spawning and pooling, save and load, anything that runs every frame on many objects, and anything that allocates GPU or native memory.
5. **Big project:** go deep where a mistake costs the most, not file by file. Say which parts you did not read.
6. **Check every finding** (rules below), then **report**.

## Look for

1. **Bug:** wrong result, crash, missed edge case (empty, zero, last item, wrap-around at midnight or season end, float precision far from origin), callers that disagree with what a method returns, the same rule applied differently in two places, `Awake`/`Start` order assumptions, static state that breaks the second Play with domain reload off. Full list: `review-unity-csharp` checklist 1.
2. **Risk:** data loss (save written in place without temp file, errors swallowed, partial writes), `UnityEditor` references in runtime code that break player builds, unchecked input from files, mods or the network.
3. **Leak:** subscriptions without unsubscribe, runtime `Material`/`Mesh`/`Texture2D`/`RenderTexture` never destroyed, `NativeArray`/`ComputeBuffer`/`GraphicsBuffer` not disposed on every path, caches and registries that only grow. Full list: `review-unity-csharp` checklist 2.
4. **Scale:** fine for ten objects, wrong for ten thousand: `Update` on every instance instead of one manager, `GetComponent`/`Find*` per frame, GC allocation per frame (LINQ, boxing, closures, string building), O(n^2) neighbour searches, a draw call or material per object where instancing applies, work every frame that could run on change.
5. **Missing test:** risky logic (math, time and calendar, procedural placement, parsers, save format, state machines) with no EditMode or PlayMode test that fails when it breaks. One good test, not coverage.
6. **Speed:** big slowdowns against the frame budget are problems. Small wins (work repeated in a hot loop) are suggestions.
7. **Lean:** code that should not exist or should be smaller.
   - delete: dead code, unused options, serialized fields nobody sets, speculative features
   - reuse: two helpers doing the same thing (keep one, name the path)
   - engine / stdlib: Unity or .NET already does it (`Mathf`, `Physics.OverlapSphereNonAlloc`, `ObjectPool<T>`, `JsonUtility`); a package doing what a few lines can
   - yagni: interface with one implementation, factory with one product, manager that only forwards calls
   - merge: near-copies that must change together (two shaders or scripts drifting apart)
   - split: one class doing several unrelated jobs, so it is hard to read or test. Split by job, never by line count.

## Check before you report

- Every finding needs a concrete case: "this input or situation leads to this wrong result". No case, no finding.
- Before calling code unused, grep the whole project including tests, `.unity`, `.prefab` and `.asset` YAML (serialized method names in `UnityEvent`s, `m_Script` GUIDs), `SendMessage`/`Invoke` strings, reflection, `[RuntimeInitializeOnLoadMethod]`, `[InitializeOnLoad]` and menu items.
- A shortcut marked with a `shortcut:` comment that names its limit is a decision, not a finding, unless the target load already crosses it.
- Propose the smallest fix that works. Prefer fixes that delete code. Never add layers, frameworks or config the problem does not need.
- No style taste, no "consider", no vague worries. Style goes to `review-unity-csharp` checklist 3, only when asked.

## Output

Very simple English: short sentences, everyday words. Explain a technical term the first time you use it. The reader may never have seen this code.

Start with `What this project does:` in two or three sentences, and the target load you assumed.

Then the findings in three groups, most important first, skip empty groups:

- **Must fix:** bug, data loss, native leak, broken player build, misses the frame budget at the target load.
- **Should fix:** risky code without a test, real slowness, duplication, a class that mixes jobs, code that should not exist.
- **Nice to have:** small speed-ups, shorter forms.

Number findings across all groups, so the user can say "fix 2 and 5". At most 20 findings; if you left smaller ones out, say how many. Every finding has all four parts, each one or two short sentences:

```
2. **Grass flickers after a day passes** (`Assets/Scripts/GrassPainter.cs:L40-52`)
   - **What this is:** Every frame this script copies the grass instances into a GPU buffer.
   - **Problem:** It creates a new ComputeBuffer each time the season changes and never releases
     the old one. GPU memory grows each in-game day until the driver drops buffers.
   - **Fix:** Release the old buffer before creating the new one: `_buffer?.Release();`.
     One line, nothing else changes.
   - **If we skip it:** Long sessions run out of GPU memory and the grass disappears.
```

End with:

- `Verdict:` one line: healthy, or what to fix first.
- `Lean: -<N> lines, -<M> packages possible.` when lean findings exist.
- `Not checked:` the parts you did not read or could not run (for example "did not profile; scale findings are from reading code").

Nothing found: `What this project does:`, then `Healthy. Nothing to fix.` and one line on what you checked.

One-shot report, changes no code. Apply fixes only when the user picks findings, then follow `csharp-unity` and verify the compile with `unity-editor-safety`.

## Related skills

- `review-unity-csharp` - full bug, leak and convention checklists for a diff or a few scripts.
- `project-auditor-fixes` - Unity Project Auditor static analysis and fixes.
- `cpu-performance-profiling`, `memory-snapshot-profiling` - confirm scale and leak findings with real captures.
