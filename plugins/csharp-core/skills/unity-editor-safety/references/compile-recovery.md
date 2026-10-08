# Compile and Editor-state recovery

## Classify the Editor state first

Misclassifying the Editor's state is the most common agent mistake. Work through these in order before launching Unity, running tests, or retrying a failed operation.

| Observation | State | Action |
|---|---|---|
| No Unity process for the project, no `Temp/UnityLockfile` (or a stale one from a crash) | Not running | Open the project, or use a headless run with the exact version |
| Process alive, Editor log still growing with import/compile lines | Booting, importing or compiling | Wait. Do not edit more files or start tests |
| Process alive, "Enter Safe Mode?" dialog or Safe Mode banner | Compile errors at startup | Read errors from `Editor.log`, fix source, let it recompile |
| Process alive, log idle, Editor unresponsive | Modal dialog (unsaved changes, "modified externally", import error) or hang | Ask the user to look at the Editor; a modal cannot be closed from outside |
| Process alive, log shows `Could not register to wait for file descriptor` / `Bee.BeeDriver` / `Unhandled exception during build` | Build driver crashed (see below) | Save work, restart the Editor |
| Process alive, responsive, edits not taking effect | Not refreshed or stale assembly | See "Edits do not take effect" |

Never conclude "Unity is not running" while a Unity process for the project is alive. Safe Mode and modal dialogs look identical to "not running" from the outside.

## Reading compile errors

- `Editor.log` locations: Windows `%LOCALAPPDATA%\Unity\Editor\Editor.log`, macOS `~/Library/Logs/Unity/Editor.log`, Linux `~/.config/unity3d/Editor.log`. The previous session is in `Editor-prev.log`.
- Grep for `error CS` and `warning CS0618` / `CS0619` (obsolete API). Each line carries `path(line,col)`.
- The log accumulates every compile of the session. Only the newest compile block matters.

### Stale-log traps

1. **Errors already fixed.** If a file cited by an error is newer than the newest `Library/ScriptAssemblies/*.dll`, no compile has finished since you edited it. Do not "fix" that code again; trigger a recompile and read again.
2. **Log mtime lies.** Asset imports keep touching `Editor.log`, so a fresh file mtime does not mean the error block is fresh. Compare DLL time with source time instead.
3. **Clean log, stale assembly.** If any `.cs` file is newer than the newest DLL, a "no errors" reading cannot be trusted either. Unity's incremental compiler can no-op a recompile request.
4. **Wrong author.** The log header records the Unity version and whether it was a `-batchmode` run. If a different Unity version (for example a newer batch run) wrote the log, its errors (such as an API that is only an error on that version) may not apply to the user's Editor. Confirm against the live Editor before acting.
5. **Previous session.** With the Editor closed, `Editor.log` is from the last session and does not reflect your edits. Compile verification needs a running Editor or a headless run with its own `-logFile`.
6. **Headless runs do not write to Editor.log.** Read the `-logFile` you passed; a clean `Editor.log` proves nothing about a batch run.

## Edits do not take effect

1. The Editor was not focused, so it never imported the change. Focus it or trigger `AssetDatabase.Refresh()`.
2. The change is in a package outside `Assets/` (an embedded package in `Packages/<name>` or a local `file:` package). Unity does not always detect these. Force it with `AssetDatabase.ImportAsset("Packages/<name>/...", ImportAssetOptions.ForceUpdate)` followed by `CompilationPipeline.RequestScriptCompilation()`, a package re-resolve, or a refocus after touching a file under `Assets/`.
3. A compile error elsewhere blocks the assembly (or a referenced asmdef). Check for any `error CS`.
4. The file is excluded by asmdef `defineConstraints`, platform filters, or an `#if` symbol that is not defined.
5. Verify: `Library/ScriptAssemblies/<Assembly>.dll` mtime > your edit mtime. Do not run tests until it is.

## Build driver crash (file descriptor exhaustion)

Long sessions with many domain reloads can leak file descriptors until Mono's internal limit (~1024) is crossed. The Bee build driver then throws `System.NotSupportedException: Could not register to wait for file descriptor N` and the Editor hangs mid-build or stays idle with a stale DLL. There is no C# error to fix and a recompile does not clear it.

Recovery: save scene work, close and restart the Editor, then discard any conclusions drawn from results since the hang (they ran against the old assembly). A clean `isCompiling == false` with a DLL that never updates and no CS errors is this crash, not an incremental no-op.

## Modal dialogs

Modal dialogs (unsaved scene changes, "scene modified externally", Safe Mode prompt, import failures, API Updater prompt) block the Editor main thread, so any automation waiting on the Editor stalls. A modal cannot be dismissed by more code. Ask the user to answer it. Prefer non-interactive APIs that never raise modals in automation (for example `EditorSceneManager.SaveScene` rather than `SaveCurrentModifiedScenesIfUserWantsTo`).

## Timeouts and retries

- A timeout while waiting on the Editor means the wait elapsed, not that the operation failed. Long operations (`Assets/Refresh` after an importer change, full reimports, lightmap bakes) routinely run for minutes.
- Before retrying any mutating operation, inspect the post-state. A blind retry of an asset-authoring step can write assets twice.
- If an operation never started (the main thread was blocked), raising the timeout does not help; clear the blocker first.

## Headless compile check

To answer "does this compile from clean?":

```
Unity -batchmode -nographics -quit -projectPath <p> -logFile compile.log
```

Requirements: the interactive Editor is closed (project lock), the exact version from `ProjectVersion.txt` is used, and you read `compile.log`, not `Editor.log`. Exit code 0 alone is not proof: confirm the log reached the end of script compilation and contains no `error CS`. Package-resolution failures appear near the end of the log.
