---
name: unity-editor-safety
description: Safety rules and recovery steps for an agent changing a Unity 6 project from outside or inside the Editor - verifying C# edits actually compiled, reading CSxxxx errors from Editor.log, recovering from Safe Mode, stale assemblies, domain reload and Enter Play Mode Options side effects, never launching a second Editor on the same project, never hand-editing an open or dirty scene, preserving GUIDs and fileIDs when touching .unity/.prefab/.asset/.meta YAML, and running tests headless. Use after editing .cs files, when Unity shows compile errors or "Enter Safe Mode?", when changes "don't take effect", when tests fail exactly as before a fix, before editing or moving scene/prefab/asset/.meta files by hand, before deleting assets, or when the Editor hangs or a batchmode run reports success but did nothing.
license: MIT
metadata:
  category: csharp-core
  sources: "AlexeyPerov/Unity-Open-MCP/skills/unity-open-mcp"
  unity: "6000.0+"
---

# Unity Editor safety

Unity projects have two copies of the truth: files on disk and the Editor's in-memory state (compiled assemblies, open scenes, imported assets). Most agent damage comes from assuming they agree. Follow these rules whenever you change a project.

## Non-negotiable rules

1. **One Editor per project.** If `Temp/UnityLockfile` exists and a Unity process for the project is alive, do not start another Editor or a `-batchmode` run on that project. A live process with no response is booting, compiling, in Safe Mode, or blocked by a modal dialog: diagnose, do not relaunch.
2. **Never hand-edit an open or dirty scene/prefab.** The Editor holds its own copy and will overwrite your edit on save, or prompt to reload and discard the user's unsaved work. Prefer Editor APIs or a live-Editor tool (see `unity-cli`). Edit YAML only when the Editor is closed or the file is not loaded, and say so explicitly.
3. **Preserve GUIDs and fileIDs.** Never delete, regenerate or hand-write `.meta` files for existing assets; move and rename assets together with their `.meta`. Never renumber `fileID`s. See [references/asset-files.md](references/asset-files.md).
4. **Compile errors first.** After any `.cs` edit, confirm a fresh compile with zero `error CS` before running tests, entering Play Mode or concluding a fix works.
5. **A tool or command returning success is not proof.** Verify the post-state: assembly timestamps, console errors, the asset actually changed, tests actually ran.
6. **Never discard user state without asking:** unsaved scenes, `Library/` deletion (forces a full reimport that can take hours), `git checkout`/`clean` over user changes, `AssetDatabase.DeleteAsset`, `EditorApplication.Exit`.
7. **Never block the Editor main thread** from editor code (`Thread.Sleep`, `.Result`, `.Wait()`, spin-waiting on a callback). The Editor freezes and must be killed.

## After editing C#: verify the compile

1. Get Unity to notice the change. The Editor imports external file changes when it regains focus (Preferences > Asset Pipeline > Auto Refresh). If the user has the Editor open but unfocused, ask them to focus it, or trigger a refresh through a connected Editor tool (`AssetDatabase.Refresh()` / `CompilationPipeline.RequestScriptCompilation()`).
2. Wait until compilation finishes (the Editor's busy indicator, or the log stops growing).
3. Read the newest compile result from `Editor.log`:
   - Windows `%LOCALAPPDATA%\Unity\Editor\Editor.log`
   - macOS `~/Library/Logs/Unity/Editor.log`
   - Linux `~/.config/unity3d/Editor.log`
   Errors look like `Assets/Scripts/Foo.cs(12,5): error CS0103: The name 'bar' does not exist in the current context`.
4. Confirm the errors are current, not historical: the relevant `Library/ScriptAssemblies/<Assembly>.dll` (for example `Assembly-CSharp.dll` or your asmdef name) must be newer than your last `.cs` edit. If the DLL is older than the source, no compile has completed since your edit; any error block or "clean" result in the log is stale.
5. Fix the first error, then repeat. Later errors are often cascades.

Detailed triage (Safe Mode, stale logs, wrong-version logs, local packages, hangs): [references/compile-recovery.md](references/compile-recovery.md).

## Compile errors and what still runs

- While any assembly has compile errors, Unity keeps running the last successfully compiled assemblies. New code, new `[MenuItem]`s, new tests and `[InitializeOnLoad]` changes do not exist yet. Tests "failing exactly as before your fix" usually mean the fix never compiled.
- An error in one asmdef blocks every assembly that references it.
- Opening a project with compile errors shows "Enter Safe Mode?". In Safe Mode only scripts are imported; fix the CS errors and Unity leaves Safe Mode after a clean compile. Do not launch another instance to "get past" the dialog.
- `-batchmode` runs fail on compile errors. `-ignorecompilererrors` exists but runs stale code; do not use it to make a run "pass".

## Domain reload

Every script recompile (and, by default, entering Play Mode) reloads the scripting domain:

- All static fields, static events and singletons are reset; in-flight async/editor tasks are dropped; `[InitializeOnLoad]` constructors run again.
- Editor state that must survive belongs in `SessionState` (per session) or `EditorPrefs` (per machine), or in serialized `ScriptableSingleton<T>`.
- With Enter Play Mode Settings set to skip domain reload, statics are **not** reset between Play sessions. Every mutable static needs a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` reset (see `csharp-unity`).
- A reload triggered by your own action (saving a script, adding a package, editing an asmdef) can cut off the response of the tool that triggered it. Assume the change applied; verify state before retrying, or you may apply it twice.
- Editing scripts during Play Mode follows Preferences > General > Script Changes While Playing; the default recompiles after Play Mode ends. Scene changes made during Play Mode are lost when it ends.

## Scenes and prefabs

- Before an editor script switches scenes, check `SceneManager.GetSceneAt(i).isDirty`; `EditorSceneManager.OpenScene(path, OpenSceneMode.Single)` closes open scenes without saving. Save with `EditorSceneManager.SaveScene` / `SaveOpenScenes` or ask the user. `EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()` shows a modal and must not be used in unattended automation.
- Make sure you edit the intended scene: a script that edits "the active scene" changes whatever the user has open.
- Edit prefab assets through `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset` / `UnloadPrefabContents`, and scene objects through `Undo.RecordObject` so changes are undoable, marked dirty and kept as prefab overrides.
- Wrap batch asset changes in `AssetDatabase.StartAssetEditing()` / `StopAssetEditing()` inside `try/finally`.

## Before deleting or moving assets

1. Find the asset's GUID in its `.meta`.
2. Search for it in text assets (`*.unity`, `*.prefab`, `*.asset`, `*.mat`, `*.controller`, `*.overrideController`, `*.anim`, `*.playable`, `*.spriteatlas*`, `*.vfx`, `*.shadergraph`, `ProjectSettings/*.asset`) and in Addressables groups.
3. Report the references and get confirmation. Move with `AssetDatabase.MoveAsset` (or move file and `.meta` together while the Editor is closed).
4. Code references by path (`Resources.Load("...")`, Addressables addresses, `AssetDatabase.LoadAssetAtPath`) do not show up in GUID searches; grep the code too.

## Headless runs and tests

- Use the exact Editor version from `ProjectSettings/ProjectVersion.txt`. A different version silently upgrades project files, packages and serialized assets.
- Close the interactive Editor first; a headless run fails with a project-lock error while it is open.
- Tests: `Unity -batchmode -nographics -projectPath <p> -runTests -testPlatform EditMode -testResults r.xml -logFile run.log`. Never add `-quit` to `-runTests` (the Editor exits before tests run and still returns 0). Exit 0 = passed, 2 = failures, 3 = run error. Check that `r.xml` exists and the test count is non-zero; a filter that matches nothing "passes".
- Always pass `-logFile`; the headless run does not write to the interactive `Editor.log`, so read its own log for errors.
- Some tests only fail under `-nographics` (GPU readback, SceneView, screenshots). Confirm them in the GUI Editor before "fixing" them.
- Run one test session at a time per project.

## Verification checklist before reporting done

- [ ] `Library/ScriptAssemblies/*.dll` newer than the last `.cs` edit, and no `error CS` in the newest compile.
- [ ] Console has no new errors (missing script, missing reference, import errors) after asset changes.
- [ ] Edited assets reimported; hand-edited YAML reserialized and reloaded without warnings.
- [ ] Affected tests ran (non-zero count) and passed.
- [ ] No scene was saved, closed or discarded that the user did not ask for.

## Related skills

- `unity-cli` - drive a running Editor from the terminal instead of hand-editing files.
- `csharp-unity` - static reset hooks, editor scripting APIs, Unity 6 API changes.
- `review-unity-csharp` - finding bugs and leaks once the code compiles.
- `unity-package-management` - package installs that trigger resolves and domain reloads.
