---
name: unity-cli
description: "Drives the official `unity` command-line tool: controls an open Unity Editor live (GameObjects, scenes, assets, C# through `unity command eval` with com.unity.pipeline), installs Editors and modules, manages auth and licenses, creates and opens projects, sets up source control, and runs headless builds and tests. Use for any `unity …` command, \"install Unity 6000.x\", \"build/test from the terminal\", \"edit the scene in the open Editor\", or CLI connection failures (including Safe Mode blocking the connection). Owns the canonical eval syntax and test-runner rule. Compile-error diagnosis via Editor.log, domain reload and asset/YAML safety belong to unity-editor-safety."
license: Unity Companion License
metadata:
  category: editor-tooling
  sources: "https://github.com/Unity-Technologies/skills/tree/main/skills/unity-cli"
  unity: "6000.0+"
---

# Unity CLI

**Always use the latest version of the Unity CLI unless you are told to use a
specific version.** Newer releases regularly add commands and output fields
these docs describe, so an outdated CLI fails in ways that read like the docs
being wrong. Check with `unity self-update --check --format json`.

## Drive a running Unity Editor (if one is open)

**If a Unity Editor is open on this machine, this CLI can control it live** — create and modify GameObjects, edit scenes and assets, inspect the hierarchy, and run arbitrary C# — through the project's **Pipeline** package (`com.unity.pipeline`). This runs entirely on your local machine, in your own user account, against your own open Editor: it is not remote access and grants no privilege you don't already have at your own terminal. When an Editor is available, drive it instead of hand-editing scene or asset files.

```bash
unity status                    # confirm a connected Editor (look for state "ready")
unity command                   # list the commands the Editor exposes
unity command editor_play       # run one — e.g. enter Play mode
unity command eval 'new UnityEngine.GameObject("Joe");'   # run C# (see Eval syntax)
```

Requires the project's `com.unity.pipeline` package (Unity 6000.0+) — add it once with `unity pipeline install`, ideally before the Editor opens the project. Headless Editors to drive, `unity list` tool discovery, and custom `[CliCommand]` tools: [integration-advanced.md](references/integration-advanced.md). The package also ships a deeper `unity-pipeline` agent skill; in a project with the package, run `unity skill install <client> --local` once to mirror it beside this skill.

> **Verifying a running game, not just editing a scene?** Entering Play mode is setup, not proof:
> an unfocused Editor can freeze at frame 1 while `unity status` still reports it as playing. The
> ordered recipe (play, confirm frames advance, capture, read the console, tune with `eval`) is in
> [playmode-verification-loop.md](references/playmode-verification-loop.md).

### Eval syntax

The canonical form, used by every skill in this marketplace ("see `unity-cli` → Eval syntax"):

```bash
unity command eval "<C# statements>"                        # positional = eval's `code` parameter
unity command eval "return UnityEngine.Application.unityVersion;"
unity command eval -- --code '<C#>' --timeout 15000          # only to raise eval's own timeout
unity command eval_file path/to/snippet.cs                   # long snippets
```

- `eval` compiles a **statement block, not a file**: no `using` directives, fully qualify every type, and `return` a value to get it back.
- Its own `timeout` is **milliseconds** (default 5000). Named parameters go after a `--` fence, otherwise the CLI's `--timeout` (seconds) swallows them.
- Shell quoting: single-quote the snippet in POSIX shells when it contains `"`; for anything that could contain user text, write a file and use `eval_file`.
- Prerequisites: a connected Editor and `com.unity.pipeline`. `eval` is registered by the package, not the CLI, so confirm it with `unity command --query eval` before relying on it.

### Targeting: `--project-path`

**Pass `--project-path` whenever more than one Editor may be running** — without it the CLI targets the Editor whose project contains the current directory. Its meaning differs by command:

- `unity command` / `list` / `job` / `mcp` / `recompile`: the **project path** (`unity status`'s `project` field is the value to pass). Matching nothing fails with `AMBIGUOUS_EDITOR` and lists the candidates.
- `unity status`: a **case-insensitive substring filter** on the project path (`--project-path MyGame` works).

[Details](references/integration-advanced.md#targeting-one-of-several-running-editors).

### Can't connect? Rule out two false negatives

- **Safe Mode.** A project with C# compile errors boots into Safe Mode, where the Pipeline package doesn't load, so `unity command`, `status`, `list` and `recompile` can't connect (`recompile` exits `7`). Run `unity pipeline list` to confirm and fix the compile errors. Unity leaves Safe Mode on its own after a clean recompile; if the CLI still can't connect (or the Editor is headless), restart the Editor. [Recovery loop](references/integration-advanced.md#recovering-from-safe-mode-connection-fails-because-of-compile-errors).
- **A sandboxed agent shell.** A restrictive sandbox can hide a genuinely running Editor from `unity status`. Don't treat "no instances" as proof, and don't improvise a headless Editor as a substitute; say your sandbox may be blocking the view and ask whether an Editor is open. [Details](references/integration-advanced.md#sandboxed-agent-tooling-can-hide-a-running-editor).

## Install the CLI (if not already installed)

Check with `which unity && unity --version`. If not found:

```bash
curl -fsSL https://unity.com/install.sh | bash          # macOS / Linux
```
```powershell
irm https://unity.com/install.ps1 | iex                 # Windows (PowerShell)
```

Open a new shell so `unity` is on PATH, then verify with `unity --version`. If the install script fails or the binary is still not found, tell the user and stop; a permissions error or crash on run suggests a broken install — suggest re-running the script.

## Conventions you need on every call

Full tables — every global flag, every `UNITY_*` environment variable, exit codes, pager
behavior, the `notifications` envelope and output-parsing rules — are in
[cli-conventions.md](references/cli-conventions.md). Read it before scripting the CLI or parsing
its output. The essentials:

- **Parse `--format json` (or `--json`), never human text.** Branch on `success` and the exit
  code; read failures from stdout (`errors[0].code`), not stderr. NDJSON streams end with exactly
  one terminal `{"type":"result"}` frame.
- **CI:** `--non-interactive` plus `--yes`; `--no-banner`/`--no-pager` in scripts. Authenticate
  with `UNITY_SERVICE_ACCOUNT_ID` + `UNITY_SERVICE_ACCOUNT_SECRET` so secrets stay out of argv.
- **Exit codes:** `0` ok, `2` bad arguments, `3` auth failure (sign in again), `4` precondition
  (e.g. no license), `6` command failure, `7` transient/network, `8` `unity test` ran and tests
  failed (do not retry), `9` install lock busy (retry later).
- **Notifications:** when a JSON envelope carries `notifications`, tell the user. If
  `remediation.requiresUserApproval` is true, never run `remediation.command` yourself.
- `--accelerator`/`--no-accelerator` are accepted only after `run`, `test` and `build`.
- `unity <version> [path]` is shorthand for `unity open [path] --editor-version <version>`.

## Getting help

Append `-h` or `--help` to any command at any level: `unity --help`, `unity projects create --help`.
**Search before you guess a command name:** `unity commands --grep <pattern>` matches command names, descriptions, options and the plugin catalog (case-insensitive regex), and names the install command for plugins that aren't installed:

```bash
unity commands --grep license
unity commands --grep 'build|test' --format json
```

---

## Commands

The full per-command reference — syntax, flags, and examples — lives in grouped files under
[`references/`](references/). **Read the file for the command group you need**; all the global
flags, environment variables, and exit codes in [cli-conventions.md](references/cli-conventions.md) apply throughout.

| Commands | Reference file |
|---|---|
| `auth` (login / logout / status / list / switch / default / consumers / revoke), `license` (activate / return / server), `cloud` (org / project) | [auth-license-cloud.md](references/auth-license-cloud.md) |
| `pipeline cloud-build` (targets incl. groups / builds / project / tooling), `pipeline automation` (apps / pipelines / jobs / automations / bots / profiles / templates, plus `apps versions`, `pipelines versions`, `jobs stats`) | [cloud-automation.md](references/cloud-automation.md) |
| `editors` (list / running / add / default / path / install-path / info / upgrade / prune / verify / module), `install`, `uninstall`, `modules`, `install-modules` | [editors-install.md](references/editors-install.md) |
| `projects` (list / create / new / clone / open / link / require / upgrade / export / import / pin / size / clean / exec), `open`, `close`, `releases`, `templates` (list / info / create / pack / delete), `assets` (`inspect` / `export`) | [projects-templates.md](references/projects-templates.md) |
| Idea → created, version-controlled, Pipeline-ready project (raw CLI recipe) | [new-project-bootstrap.md](references/new-project-bootstrap.md) |
| `config` (proxy / update-check / accelerator / get / set / list / unset / resolve), `context` (save / use / list / current / delete), `hub install` | [config-hub.md](references/config-hub.md) |
| `run`, `test`, `build` (+ `build run`), `recompile`, `watch` (`test`) | [build-run-test.md](references/build-run-test.md) |
| `logs`, `doctor`, `env`, `version`, `cache`, `ci init`, `analytics`, `changelog`, `docs`, `language`, `completion`, `bug`, `self-update`, `self-uninstall`, `diagnose proxy`, `diagnose accelerator`, `diagnose update` | [diagnostics-maintenance.md](references/diagnostics-maintenance.md) |
| `mcp` (+ `configure`), `setup claude`, `skill` (install / refresh / show), `plugin` (install / remove / upgrade / list / changelog), local `pipeline` (install / upgrade / list / list-versions), `command` / `commands` / `status` / `list`, `job` (status / wait / cancel), `shell` | [integration-advanced.md](references/integration-advanced.md) |
| Play mode verification loop (`editor_play`, `wait_for`, `capture_game_view`, `console`, `eval`) | [playmode-verification-loop.md](references/playmode-verification-loop.md) |
| `vcs` — `setup` / `status` / `sync` / `switch` / `doctor` / `providers` / `merge-setup` / `conflicts` / `explain` / `resolve` / `diff` / `blame` / `summarize` / `affected` / `hooks`, `vcs git` (`migrate-lfs` / `worktree`), `vcs uvcs` (`locks` / `changesets` / `review`), `uvcs` / `cm` passthrough | [version-control.md](references/version-control.md) |
| `collaboration` (alias `collab`) — `annotations` / `attachments` / `thumbnail` / `reactions` / `read` / `subscribe` / `jira` | [collaboration.md](references/collaboration.md) |
| Global flags, `UNITY_*` env vars, exit codes, pagers, `notifications`, output parsing | [cli-conventions.md](references/cli-conventions.md) |
| `plugin` catalog — `ai` generators, `TOOL_NOT_INSTALLED` | [catalog-plugins.md](references/catalog-plugins.md) |

## Common workflows

### Edit a scene, GameObject, or asset — `unity status` first

**Before editing any scene, GameObject, prefab, or asset, run `unity status` to detect a connected Editor.** If one is reachable, drive it with live commands instead of touching project files — the Editor applies changes to the *actual active scene* and keeps its in-memory state in sync.

```bash
unity status                       # is an Editor connected? (look for state "ready")
unity command                      # discover the scene/GameObject commands THIS Editor exposes
unity command create_gameobject    # e.g., if listed: act on the live, active scene
unity command save_scene           # persist the active scene
```

Command names are defined by the Editor, so run `unity command` (or `unity list`) to see the exact set — don't assume a name.

> **Never hand-edit `.unity`, `.prefab`, or `.asset` YAML while a live Editor is reachable.** Raw
> edits get fileIDs/GUIDs wrong, stay invisible to the running Editor until a reimport, and can hit
> the wrong file (e.g. `SampleScene.unity` while the active scene is `Demo2.unity`).

Only fall back to editing files directly once you've ruled out Safe Mode and a sandboxed shell (see [Can't connect?](#cant-connect-rule-out-two-false-negatives)) — and say so explicitly ("no live Editor detected, editing the file directly").

### Run tests headless

The single rule other skills reference ("see `unity-cli` → Run tests headless"). Pick the first that applies:

1. **Editor open with the project and `com.unity.pipeline`:** run tests in that Editor with `unity command run_tests` (check `unity command --query test` for the exact commands; follow with `test_status`, or use `--detach` and `unity job`). Never launch a second Editor on an open project.
2. **Editor closed, `unity` CLI available:** `unity test <project>`. It spawns a batch-mode Editor (`-runTests`, no `-quit`). Exit `0` = passed, `8` = tests failed (do not retry), `6` = no verdict (compile error, license, crash, timeout; safe to retry).
3. **Editor closed, no CLI:** raw `Unity -batchmode -nographics -projectPath <p> -runTests -testPlatform EditMode -testResults r.xml -logFile run.log`, never with `-quit`. Exit `0` = passed, `2` = tests failed, `3` = run error.

For paths 2 and 3, also check the results file exists and the test count is non-zero; a filter that matches nothing "passes".

```bash
unity test /path/to/MyProject --editor-version 6000.0.47f1 --mode EditMode \
  --report-format junit --output ./test-results.xml --allow-install --timeout 600
case $? in
  0) echo "All tests passed" ;;
  8) echo "Tests failed — report to developers, do not retry" ;;
  *) echo "Run did not complete — infrastructure failure, safe to retry" ;;
esac
```

Under `--format json` the same split is `errors[0].code`: `TESTS_FAILED` versus `TEST_RUN_ERROR` / `TEST_TIMED_OUT`. `--report-format junit` writes a JUnit report CI ingests natively; `--coverage` collects coverage when the Code Coverage package is present. Sharding, retries, `--affected`: [build-run-test.md](references/build-run-test.md).

### Bootstrap a new project from scratch

For the **guided** flow (concept questions, background Editor install, package selection, monetization handoff) use the **`new-unity-project`** skill. The raw CLI recipe it builds on — sign in, install the LTS Editor with platform modules, pick a URP template by id, `projects create` with Git/GitHub/GitLab/UVCS, `pipeline install` **before** the first open, then `open` + `status --until-ready` and build the scene in the live Editor — is in [new-project-bootstrap.md](references/new-project-bootstrap.md).

The CLI has no top-level UPM package command: add packages through a connected Editor (`unity command package_add`) or the C# Client API — see the **`unity-package-management`** skill. For monetization/backend, hand off to `implement-in-app-purchases`, `levelplay-unity-integration` or `build-live-game`.

### Find and install a missing editor

```bash
unity editors --installed --format json          # what's installed
unity releases --lts --limit 5 --format json     # same as --stream lts; --stream also takes tech/beta/alpha
unity install 6000.0.47f1 --yes --accept-eula
```

### Open a project with the correct editor

```bash
unity projects info /path/to/MyProject --format json   # read "editorVersion"
unity editors --installed --format json                # confirm it is installed
unity open /path/to/MyProject                          # warns if the version is missing
```

### CI: sign in, activate a license, build

Prefer the dedicated `unity build` command (it handles batch mode, logging and CI flags):

```bash
unity auth login --client-id "$UNITY_SERVICE_ACCOUNT_ID" --secret-from-stdin <<<"$UNITY_SERVICE_ACCOUNT_SECRET"
unity license activate                 # entitlement; or --serial / --floating

unity build /path/to/MyProject \
  --editor-version 6000.0.47f1 \
  --target StandaloneLinux64 \
  --execute-method Builder.PerformBuild \
  --allow-install
echo "Exit code: $?"

unity license return --yes             # return the seat (floating/assigned)
```

`Builder.PerformBuild` is your own static Editor method calling `BuildPipeline.BuildPlayer(BuildPlayerOptions)` or, to reuse a Build Profile, `BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions { buildProfile = …, locationPathName = … })`. Fail the build with `EditorApplication.Exit(1)` when `report.summary.result != BuildResult.Succeeded`. `unity run <project> -- -executeMethod …` also works (batch mode is automatic — never pass `-batchmode`/`-quit`). See [build-run-test.md](references/build-run-test.md).

### Inspect cloud builds or Pipeline Automation resources

1. Read [cloud-automation.md](references/cloud-automation.md) first: `pipeline cloud-build` reads Build Automation, `pipeline automation` reads Pipeline Automation. Neither needs an Editor or the Pipeline package, and both are read-only (no triggering builds/jobs, fetching logs/artifacts, or changing config).
2. Sign in, and use numeric organization IDs with service accounts.
3. Use `--format json`; results sit under `data` in their native API shape. Keep native fields as returned and rely on the reference's redaction rules rather than stripping fields yourself.
4. Check each leaf's pagination support in the reference; some reject `--page`/`--limit` instead of ignoring them.

### Generate a texture, sprite, image, or 3D model

Check the catalog (`unity plugin list --format json`) for the Unity AI Generators plugin (`ai`) before approximating the request another way. Never install a catalog plugin silently — ask first. Details: [catalog-plugins.md](references/catalog-plugins.md).

### Debug the CLI

```bash
unity doctor --format json             # auth + installed editors + recent errors
unity logs --follow --level info       # follow live logs during an install
```
