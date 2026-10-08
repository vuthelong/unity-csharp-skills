---
name: project-auditor-fixes
description: Runs Unity Project Auditor static analysis through the `unity` CLI (`unity command audit` / `audit_status`), reads the issue CSV (Category, Severity, Areas, Description, RelativePath, Line, DescriptorId, Recommendation), and fixes the reported code, asset-import and project-setting issues in small, reviewable, compiling batches with a before/after test case. Use when the user asks to run Project Auditor, audit the project for performance or best-practice problems, "fix the Project Auditor issues", work through an audit CSV, or clean up texture/mesh/audio import settings and player settings flagged by analysis. Requires a connected Editor via `unity-cli` and Project Auditor (built-in module plus `com.unity.project-auditor-rules`, or the `com.unity.project-auditor` package).
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: editor-tooling
  sources: "Unity-Technologies/skills/skills/project-auditor-fixes"
  unity: "6000.0+"
---

# Project Auditor: find and fix issues

Project Auditor statically scans the project (scripts, assets, project settings) and reports
diagnostics. Every row in its output is something to fix, and its `Recommendation` column says
how. The loop is: trigger, poll, read, fix in batches, re-audit.

## Prerequisites

- The `unity` CLI and a connected Editor with the `com.unity.pipeline` package — see `unity-cli`
  (`unity status` must show the Editor ready).
- Project Auditor with its rules. On Editors that ship it as a built-in module the analysis rules
  come from `com.unity.project-auditor-rules`; otherwise install the `com.unity.project-auditor`
  package. Install through `unity command package_add --identifier <id> --confirm true` when the
  Editor exposes it, or see `unity-package-management`.

## Run the audit

```bash
unity command audit                              # optional: --categories Code,ProjectSetting --output my.csv
unity command audit_status                       # repeat until a terminal status
```

- Terminal statuses: `completed` (with `csvPath` and `issueCount`), `failed`, `unavailable`,
  `interrupted` (a domain reload killed the scan; re-run `audit`).
- One scan at a time: a second `audit` returns `busy`. There is no cancel; stop polling to abandon.
- `unavailable` means no Project Auditor or no rules — read the `message` field. The command never
  reports an empty `completed` scan, so zero issues is never a silent failure.
- CSV columns: `Category, Severity, Areas, Description, RelativePath, Line, DescriptorId, Recommendation`.
- No `audit` command in `unity command`? Ask the user to run **Window > Analysis > Project
  Auditor**, analyze, and export the issue table to CSV, then continue from step 4 below.
- Polling stays responsive while the scan compiles assemblies. If the Editor is unfocused and
  progress stalls, run `unity command set_autotick` first (see `unity-cli` →
  `references/playmode-verification-loop.md`).

## Recipe

1. **Clean baseline.** The project opens with no compile errors. If errors exist, ask the user —
   they may know and want them ignored.
2. **Test case.** Agree on a check to run before and after changes: a scene to open and play, a
   platform build, or `unity test` (see `unity-cli`). If none exists, propose one and ask for
   confirmation. Run it now and record the result.
3. **Clean version control.** Commit or stash pending work so every fix is its own reviewable change.
4. **Audit and group.** Run the audit, then group rows by `DescriptorId` (issue type) and by
   `RelativePath` (file/folder).
5. **Fix in batches.**
   - Settings issues on assets or the project: fix **by issue type** — e.g. one commit that turns
     off Read/Write on every flagged texture, or one commit per subfolder if the set is large.
   - Code issues: fix **by file and subfolder**. The project must compile between commits.
   - Follow the `Recommendation` text; do not invent broader refactors.
   - Keep batches small enough for a human to review.
6. **Verify each batch.** No Console errors after the reimport/recompile, then commit with a
   message naming the issue type and scope.
7. **Re-audit and re-test.** Re-run the audit to confirm the rows are gone and the test case from
   step 2 still passes. Report fixed, remaining and deliberately skipped issues.

## Applying fixes efficiently

| Issue kind | How to apply | Notes |
|---|---|---|
| Import settings on many assets (texture Read/Write, mipmaps on UI sprites, max size, compression, mesh Read/Write, audio load type) | Editor script over `AssetImporter.GetAtPath(path)` cast to `TextureImporter` / `ModelImporter` / `AudioImporter`, set fields, `SaveAndReimport()`; wrap the loop in `AssetDatabase.StartAssetEditing()` / `StopAssetEditing()` in a `try/finally` | Set per-platform overrides with `GetPlatformTextureSettings` / `SetPlatformTextureSettings`. For recurring rules, add an `AssetPostprocessor` or a Preset so new assets stay fixed |
| Project / player settings | `PlayerSettings` with `NamedBuildTarget` overloads (e.g. `PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP)`), `QualitySettings`, `GraphicsSettings` | The `BuildTargetGroup` overloads are obsolete on Unity 6 |
| Obsolete or slow API calls | Edit the flagged line | `FindObjectOfType` → `FindFirstObjectByType` / `FindAnyObjectByType`; `Object.GetInstanceID()` is CS0619 on 6000.5+ → `GetEntityId()`; cache `Camera.main`, `GetComponent` results and `Shader.PropertyToID` instead of calling them per frame |
| Per-frame allocations (LINQ, string concat, boxing, closures in `Update`) | Hoist allocations, reuse collections, use `StringBuilder` or cached strings | Keep behavior identical; the test case must still pass |

Locate affected assets interactively with `generate-editor-search-query` (e.g. `t:texture dir:Assets/UI`).
For deeper domain fixes hand off to the relevant optimization skill (audio, text, web, rendering)
rather than guessing past the `Recommendation`.

## Pitfalls

- Reimporting thousands of assets one by one without `StartAssetEditing` is very slow.
- `audit_status` returning `interrupted` after your own script edits is expected: the recompile
  reloaded the domain. Re-run `audit`.
- Do not fix and refactor in the same commit; reviewers cannot tell the audit fix apart.
- Target a specific Editor with `--project-path <path>` when more than one is open.
