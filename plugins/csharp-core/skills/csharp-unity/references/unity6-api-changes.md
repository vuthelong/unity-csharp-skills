# Unity 6 API changes for C# code

Check the exact editor version in `ProjectSettings/ProjectVersion.txt` (`m_EditorVersion: 6000.x.y`). "Obsolete" means CS0618 warning (still compiles); "error" means CS0619 (does not compile). Many renames are auto-upgraded by the API Updater when the project is opened, but code you write fresh must use the new names.

## Object lookup and identity

| Old | Unity 6 replacement | Status |
|---|---|---|
| `Object.FindObjectOfType<T>()` | `FindFirstObjectByType<T>()` (deterministic, slower) or `FindAnyObjectByType<T>()` (fastest) | Obsolete since 2023.1 |
| `Object.FindObjectsOfType<T>()` | `FindObjectsByType<T>(FindObjectsSortMode.None)` | Obsolete since 2023.1 |
| `FindObjectOfType<T>(true)` (include inactive) | `FindFirstObjectByType<T>(FindObjectsInactive.Include)` | Obsolete |
| `Object.GetInstanceID()` | `GetEntityId()` returning `EntityId` | Error (CS0619) on 6000.5+ |

Notes:
- Prefer `FindObjectsSortMode.None`; sorting by ID is slow and tied to the InstanceID-to-EntityId migration.
- On 6000.5+ other int-instance-ID APIs gain `EntityId` counterparts. Do not persist instance IDs or entity IDs to disk; they change between sessions. Use the object reference itself as a dictionary key where possible.
- For code shared across 6000.0 and 6000.5+, guard with `#if UNITY_6000_5_OR_NEWER`.

## Physics

| Old | Unity 6 replacement |
|---|---|
| `Rigidbody.velocity` | `Rigidbody.linearVelocity` |
| `Rigidbody.drag` | `Rigidbody.linearDamping` |
| `Rigidbody.angularDrag` | `Rigidbody.angularDamping` |
| `Rigidbody2D.velocity` | `Rigidbody2D.linearVelocity` |
| `Rigidbody2D.drag` / `angularDrag` | `linearDamping` / `angularDamping` |
| `Physics.autoSimulation` | `Physics.simulationMode = SimulationMode.Script` / `FixedUpdate` / `Update` |
| `PhysicMaterial` | `PhysicsMaterial` (and `PhysicMaterialCombine` to `PhysicsMaterialCombine`) |

## Rendering

| Old | Unity 6 replacement |
|---|---|
| `GraphicsSettings.renderPipelineAsset` | `GraphicsSettings.defaultRenderPipeline` |
| `QualitySettings` pipeline lookups | `QualitySettings.renderPipeline` per level, `GraphicsSettings.currentRenderPipeline` for the active one |
| URP `ScriptableRenderPass.Execute` / `Configure` | Render Graph `RecordRenderGraph` (compatibility mode is off by default in new 6000.0 projects); see `validate-urp-render-graph-renderer-feature` |

## Async

| Old | Unity 6 |
|---|---|
| Coroutines for simple delays | `await Awaitable.WaitForSecondsAsync(t, destroyCancellationToken)` |
| Custom `AsyncOperation` wrappers | `await asyncOperation` directly, or `Awaitable.FromAsyncOperation(op, ct)` |
| Manual destroy flags | `MonoBehaviour.destroyCancellationToken`, `Application.exitCancellationToken` |

## Input

- Unity 6 templates enable the Input System package. With `activeInputHandler = 1` (Input System only), `UnityEngine.Input.GetKey` and friends throw `InvalidOperationException` at runtime.
- `InputSystem` namespace: `UnityEngine.InputSystem`. `Keyboard.current` can be null on devices without a keyboard.

## UI and text

- TextMeshPro ships inside `com.unity.ugui` 2.0 in Unity 6. The namespace stays `TMPro`; do not add the old `com.unity.textmeshpro` package.
- `UnityEngine.UI.Text` still exists but prefer `TMP_Text` for uGUI and `Label` for UI Toolkit.

## Editor

| Old | Unity 6 |
|---|---|
| `EditorApplication.playmodeStateChanged` | `EditorApplication.playModeStateChanged` (+ `PlayModeStateChange`) |
| `PrefabUtility.CreatePrefab` / `ReplacePrefab` | `PrefabUtility.SaveAsPrefabAsset` / `SaveAsPrefabAssetAndConnect` |
| `EditorUtility.SetDirty` on scene objects | `Undo.RecordObject` before the change (marks the scene dirty and supports undo) |

## Language

- C# 9.0. No C# 10+ syntax. `record`/`init` need the `IsExternalInit` shim (see SKILL.md).
- .NET Standard 2.1 API profile by default; `.NET Framework` profile is available in Player Settings but rarely needed.

## Detecting obsolete usage

- Unity Console and IDE warnings CS0618 list obsolete calls; treat them as tasks when targeting a newer minor.
- `Project Auditor` (see `project-auditor-fixes`) reports obsolete API and performance issues across the project.
