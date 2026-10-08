---
name: review-unity-csharp
description: Reviews existing Unity C# code for correctness bugs, lifecycle mistakes, memory and native-resource leaks, Unity 6 obsolete APIs, and violations of the csharp-unity house style, reporting findings with file:line, severity and minimal fixes. Use when asked to review, audit, deep-analyze or find bugs/leaks in Unity scripts, a diff or a branch; for "why does memory keep growing", "check for leaks", "is this code correct", "works the first time I press Play but not the second", "check coding conventions"; or to vet AI-generated Unity code. Covers event subscription leaks, undisposed NativeArray/RenderTexture/Material/Mesh, Addressables handles, destroyed-object access via ?., async lifetime, static state with domain reload disabled, serialization data loss. Not for writing new code (use csharp-unity).
license: MIT
metadata:
  category: csharp-core
  sources: "github.com/vuthelong/unity-csharp-skills (original), AlexeyPerov/Unity-Open-MCP/skills/extensions/memoryprofiler"
  unity: "6000.0+"
---

# Review Unity C#: bugs, leaks, conventions

Read every in-scope file completely. Leaks and lifecycle bugs are found by pairing an allocation or subscription in one method with its missing cleanup in another; grep excerpts miss the pairing.

## Workflow

1. **Scope.** Confirm the files. For "my changes" / "this branch", list changed `.cs` files with `git diff --name-only` against the base branch instead of guessing.
2. **Context.** Read `ProjectSettings/ProjectVersion.txt` (API drift), `Packages/manifest.json` (UniTask, Addressables, DOTween, Input System), and whether Enter Play Mode Options disable domain reload (`m_EnterPlayModeOptionsEnabled` / `m_EnterPlayModeOptions` in `ProjectSettings/EditorSettings.asset`).
3. **Read fully** every in-scope file, plus the declarations of any events, singletons or managers they subscribe to.
4. **Widen the net.** Use IDE diagnostics if an IDE integration is available, otherwise grep the project with the sweep patterns in [references/leak-checklist.md](references/leak-checklist.md). Use `git log -p` / `git blame` to tell new problems from pre-existing ones.
5. **Pair lifecycles.** For every subscribe/allocate/register, find its cleanup. Missing cleanup is a finding.
6. **Run the three checklists** below.
7. **Report** in the output format. Apply fixes only if asked, and then make the smallest edit per finding.

## Checklist 1: correctness bugs

Details and before/after code: [references/bug-checklist.md](references/bug-checklist.md).

- `?.`, `??`, `??=`, `is null`, `is not null` on a `UnityEngine.Object` (bypass the destroyed-object check)
- `GetComponent` result used without null check or `[RequireComponent]`
- `Time.fixedDeltaTime` used in `Update`; physics forces or `Rigidbody.MovePosition` in `Update`
- Mutating a copy of a struct (`foreach` variable, property getter returning a struct, `List<struct>[i].x = ...`)
- Collection modified while iterating with `foreach`
- Cross-object `Awake`/`Start` ordering assumptions without `[DefaultExecutionOrder]` or explicit init
- `async void` outside Unity messages/UI handlers; discarded `Task`; awaits without a lifetime token; same `Awaitable` awaited twice
- `UnityEngine` API from a background thread (`Task.Run`, thread callbacks, `Awaitable.BackgroundThreadAsync` sections, job `Execute`)
- Singleton without duplicate guard, or not cleared in `OnDestroy`
- `StopCoroutine("Name")` for a coroutine started by `IEnumerator`; coroutine expected to stop when only the component is disabled
- Static state not reset when domain reload is disabled ("works the first Play, breaks the second")
- Multi-step state mutation in a callback that can throw half-way
- ScriptableObject asset mutated at runtime (persists in Editor, resets in builds)
- Serialized field renamed without `[FormerlySerializedAs]`; polymorphic field without `[SerializeReference]`
- `[Flags]` missing on an enum combined with `|`, or flag values not powers of two
- Magic tag/layer/scene/animator strings instead of constants or cached hashes
- `UnityEngine.Input` used while the project is Input System only (throws)
- Runtime script referencing `UnityEditor` without `#if UNITY_EDITOR` (breaks player builds)
- Obsolete Unity 6 APIs: `FindObjectOfType`, `FindObjectsOfType`, `Rigidbody.velocity`/`drag`, `GetInstanceID` (CS0619 on 6000.5+). Full list: `csharp-unity` reference `unity6-api-changes.md`
- C# 10+ syntax (file-scoped namespace, `global using`, `record struct`, `required`) that Unity 6's C# 9 compiler rejects

## Checklist 2: memory and resource leaks

Details, pairing table, grep sweep and profiler confirmation: [references/leak-checklist.md](references/leak-checklist.md). This is the highest-value part of the review.

- `+=` / `AddListener` without `-=` / `RemoveListener` in `OnDisable`/`OnDestroy`, especially on static events, singletons, ScriptableObject channels, `SceneManager.sceneLoaded`, `Application.logMessageReceived`, `InputAction.performed`
- Runtime `Material`, `Texture2D`, `RenderTexture`, `Mesh`, `Sprite`, `AudioClip`, `ScriptableObject.CreateInstance` never destroyed; `renderer.material` / `meshFilter.mesh` getter instances never destroyed
- `RenderTexture.GetTemporary` without `ReleaseTemporary`; `new RenderTexture` without `Release` + `Destroy`
- Addressables `LoadAssetAsync`/`InstantiateAsync` without `Release`/`ReleaseInstance`; `AssetBundle` never unloaded
- `NativeArray`/`NativeList`/`NativeHashMap` with `Allocator.Persistent`/`TempJob` not disposed on every path; `ComputeBuffer`/`GraphicsBuffer` not released
- `UnityWebRequest` not disposed
- `InvokeRepeating` without `CancelInvoke`; tweens never killed
- Registry/cache collections that only grow; static collections holding scene objects
- Lambdas capturing `this` registered with long-lived owners and never removed
- Async/UniTask/Awaitable work with no cancellation tied to the object's lifetime
- Cleanup placed only in `OnDestroy` for objects that may never have been active (Unity skips `OnDestroy` if `Awake` never ran) or that are pooled instead of destroyed

## Checklist 3: conventions

Authoritative rules live in the `csharp-unity` skill; load it before flagging so fixes match exactly. Enforce them unconditionally, even if the project's `.editorconfig` or surrounding code uses a different style.

| Rule | Violation looks like |
|---|---|
| Naming | `_camelCase` private fields; camelCase without `_` on `[SerializeField] private`; PascalCase public members and statics |
| `this.` | Missing on instance-field access, or present on properties/locals/params/statics |
| `var` | Explicit `int`/`float`/`string`/`bool` where the right-hand side is obvious |
| `#region` | Missing, misnamed, out of order, empty, or nested. Order: Fields, Properties, Unity Lifecycle, Public Methods, Private Methods, Unity Callbacks |
| LINQ | Any LINQ, in runtime, editor, build or test code |
| Comments | "What" comments, commented-out code; missing *why* on a real workaround |
| Async | `async void` instead of `UniTaskVoid`/`Awaitable`; missing cancellation token |
| `[SerializeField]` | Missing `private`, or `public` field used for Inspector exposure |
| Caching | `GetComponent`/`Find*` in per-frame code |

## Severity

| Severity | Use for |
|---|---|
| High | Crash, data loss, unbounded growth, native leak, wrong behavior in builds |
| Medium | Leak or bug with limited blast radius, perf issue in hot path, obsolete API that will become an error |
| Low | Conventions, minor perf, style |

Never bury a leak under a list of convention nits; order findings by severity.

## Output format

1. **Summary** - one paragraph: overall risk and the top 1-3 findings.
2. **Confirmed issues** - grouped Bugs / Leaks / Conventions. Each: `file:line`, one-sentence defect, concrete failure scenario (input or sequence that triggers it, or what grows), severity.
3. **Likely issues** - same format, for findings that depend on context outside the reviewed files.
4. **Recommended fixes** - minimal targeted fix per issue.
5. **Corrected snippets** - before/after for high-severity findings only.
6. **Missing information** - what would raise confidence (for example "is this handle released elsewhere?").

## Guardrails

- Do not invent Unity, UniTask or .NET APIs. If unsure an API exists on the project's Unity version, say so.
- Separate confirmed (visible in code) from likely (depends on unseen context).
- Cite `file:line` for every finding.
- Prefer minimal fixes; do not rewrite files for style unless asked.
- When applying fixes, do not restyle unrelated code.

## Related skills

- `csharp-unity` - the style rules and correct patterns.
- `unity-editor-safety` - recompiling and verifying after fixes; serialized-data safety.
- `validate-urp-render-graph-renderer-feature` - Render Graph renderer features.
- `project-auditor-fixes` - project-wide static analysis with Project Auditor.
