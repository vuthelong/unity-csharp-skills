---
name: unity-cli
description: Drives the official `unity` command-line tool — controls a running Unity Editor live (create/modify GameObjects, edit scenes and assets, inspect the hierarchy, run C# via `unity command eval` through the com.unity.pipeline package), installs/upgrades Editors and modules, manages auth and licenses, creates/opens/clones projects from templates, sets up Git/GitHub/GitLab/UVCS source control, runs headless builds and EditMode/PlayMode tests in CI, reads logs, browses releases, and configures the Unity MCP server. Use for any `unity …` command, "install Unity 6000.x", "open this project with the right Editor", "build/test from the terminal", "edit the scene in the open Editor", Safe Mode/connection failures, or exit-code handling in CI. For the guided idea-to-project flow use `new-unity-project`; for UPM packages use `unity-package-management`.
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: editor-tooling
  sources: "Unity-Technologies/skills/skills/unity-cli"
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
# Run arbitrary C# — e.g. add a GameObject named "Joe" — when the Editor exposes eval:
unity command eval 'new UnityEngine.GameObject("Joe");'
```

> **Verifying a running game, not just editing a scene?** Entering Play mode is setup, not proof:
> an unfocused Editor can freeze at frame 1 while `unity status` still reports it as playing, and a
> screenshot can render a frozen frame that looks alive. The ordered recipe for playing, confirming
> the game is actually advancing, capturing the view, reading the console, and tuning it live with
> `eval` is in [playmode-verification-loop.md](references/playmode-verification-loop.md).

### More than one Editor open? Pass `--project-path`

Every Editor-driving command takes `--project-path <path>`. **Pass it whenever more than one Editor may be running** — without it the CLI targets the Editor whose project contains the current directory, so the target follows the shell's cwd:

```bash
unity command editor_play --project-path /path/to/MyProject
```

A `unity status` instance's `project` field is what `--project-path` takes. For `unity command`/`list`/`job`/`mcp`, matching no running project fails with `AMBIGUOUS_EDITOR` and lists the candidates. [Details](references/integration-advanced.md#targeting-one-of-several-running-editors).

Requires the project's `com.unity.pipeline` package (Unity 6.0+) — add it once with `unity pipeline install`. Full details — launching a headless Editor to drive, `unity list` tool discovery, and authoring custom `[CliCommand]` tools — are in [integration-advanced.md](references/integration-advanced.md).

The package also ships a deeper `unity-pipeline` agent skill, invisible to clients inside `Library/PackageCache` — in a project with the package, run `unity skill install <client> --local` once to mirror it beside this skill.

> **Can't connect / commands time out? Check for Safe Mode first.** When a project has C# compile errors, the Editor boots into **Safe Mode**, where the Pipeline package doesn't load — so `unity command`, `unity status`, `unity list`, and `unity recompile` can't connect at all. Note what that means for `unity recompile` specifically: it reports errors you introduce into an Editor that is **already running**, but an Editor that *started* with broken code never loads the package, so there is nothing to ask and it exits `7` rather than reporting the errors. Don't fall back to blind file-editing: run `unity pipeline list` to confirm, then fix the compile errors and restart Unity. Full recovery loop in [integration-advanced.md → Recovering from Safe Mode](references/integration-advanced.md#recovering-from-safe-mode-connection-fails-because-of-compile-errors).

> **Running as a sandboxed coding agent and `unity status` reports no instances?** A restrictive sandbox can hide an Editor that is genuinely running from this CLI's view of it — don't treat that alone as proof the Editor is down. Full detail in [integration-advanced.md → Sandboxed agent tooling can hide a running Editor](references/integration-advanced.md#sandboxed-agent-tooling-can-hide-a-running-editor).

## Install the CLI (if not already installed)

First check if the CLI is available:

```bash
which unity && unity --version
```

If not found, install it:

**macOS / Linux**
```bash
curl -fsSL https://unity.com/install.sh | bash
```

**Windows (PowerShell)**
```powershell
irm https://unity.com/install.ps1 | iex
```

After installing, open a new shell so `unity` is on PATH, then verify with `unity --version`. If the install script fails or the binary is still not found, tell the user and stop; if the command itself fails with a permissions error or crash, the installation may be broken — suggest re-running the install script.

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

Append `-h` or `--help` to any command or subcommand, at any level: `unity --help`, `unity projects create --help`.

**Not sure which command does something? Search before you guess a name.** `unity commands --grep <pattern>` matches command names, descriptions, and options, plus the plugin catalog, installed or not:

```bash
unity commands --grep license                 # one match per line
unity commands --grep 'build|test' --format json
```

Each result says whether it is a `command` or a `plugin`. A plugin that isn't installed also names its install command (`unity plugin install <id>`). Matching is case-insensitive, and the pattern is a regular expression evaluated with a timeout, so a plain keyword works as-is. See [integration-advanced.md](references/integration-advanced.md) for the output fields.

---

## Commands

The full per-command reference — syntax, flags, and examples — lives in grouped files under
[`references/`](references/). **Read the file for the command group you need**; all the global
flags, environment variables, and exit codes in [cli-conventions.md](references/cli-conventions.md) apply throughout. Every command also supports
`-h` / `--help` (see [Getting help](#getting-help)).

| Commands | Reference file |
|---|---|
| `auth` (login / logout / status / list / switch / default / consumers / revoke), `license` (activate / return / server), `cloud` (org / project) | [auth-license-cloud.md](references/auth-license-cloud.md) |
| `pipeline cloud-build` (targets incl. groups / builds / project / tooling), `pipeline automation` (apps / pipelines / jobs / automations / bots / profiles / templates, plus `apps versions`, `pipelines versions`, `jobs stats`) | [cloud-automation.md](references/cloud-automation.md) |
| `editors` (list / running / add / default / path / install-path / info / upgrade / prune / verify / module), `install`, `uninstall`, `modules`, `install-modules` | [editors-install.md](references/editors-install.md) |
| `projects` (list / create / new / clone / open / link / require / upgrade / export / import / pin / size / clean / exec), `open`, `close`, `releases`, `templates` (list / info / create / pack / delete), `assets` (`inspect` / `export`) | [projects-templates.md](references/projects-templates.md) |
| `config` (proxy / update-check / accelerator / get / set / list / unset / resolve), `context` (save / use / list / current / delete), `hub install` | [config-hub.md](references/config-hub.md) |
| `run`, `test`, `build` (+ `build run`), `recompile`, `watch` (`test`) | [build-run-test.md](references/build-run-test.md) |
| `logs`, `doctor`, `env`, `version`, `cache`, `ci init`, `analytics`, `changelog`, `docs`, `language`, `completion`, `bug`, `self-update`, `self-uninstall`, `diagnose proxy`, `diagnose accelerator`, `diagnose update` | [diagnostics-maintenance.md](references/diagnostics-maintenance.md) |
| `mcp` (+ `configure`), `setup claude`, `skill` (install / refresh / show), `plugin` (install / remove / upgrade / list / changelog), local `pipeline` (install / upgrade / list / list-versions), `command` / `commands` / `status` / `list`, `job` (status / wait / cancel), `shell` | [integration-advanced.md](references/integration-advanced.md) |
| `vcs` — `setup` / `status` / `sync` / `switch` / `doctor` / `providers` / `merge-setup` / `conflicts` / `explain` / `resolve` / `diff` / `blame` / `summarize` / `affected` / `hooks`, `vcs git` (`migrate-lfs` / `worktree`), `vcs uvcs` (`locks` / `changesets` / `review`) | [version-control.md](references/version-control.md) |
| `collaboration` (alias `collab`) — `annotations` / `attachments` / `thumbnail` / `reactions` / `read` / `subscribe` / `jira` | [collaboration.md](references/collaboration.md) |
| Global flags, `UNITY_*` env vars, exit codes, pagers, `notifications`, output parsing | [cli-conventions.md](references/cli-conventions.md) |
| `plugin` catalog — `ai` generators, `TOOL_NOT_INSTALLED` | [catalog-plugins.md](references/catalog-plugins.md) |

## Common workflows

### Inspect cloud builds or Pipeline Automation resources

Read [cloud-automation.md](references/cloud-automation.md) for every read-only
command in both groups, context/authentication, per-command filters and sorting,
output fields, pagination (including the leaves that refuse `--page`/`--limit`
rather than ignoring them), errors, and redaction. `pipeline cloud-build` reads Build Automation;
`pipeline automation` reads Pipeline Automation. Neither needs a running Editor
or the local Pipeline package. Use numeric organization IDs for service accounts.
These commands don't trigger builds/jobs, fetch logs/artifacts, or change
configuration.

JSON/NDJSON return full API-shaped results under `data`, as Collab does; NDJSON
has one terminal result, without item frames. Preserve native fields and free-form
metadata, subject to the reference's bounded secret protections and public-API
redaction assumption. Table projections remain separate. Explicit local build
targets retain `_local`; missing targets aren't local. These conventions apply
to follow-up cloud-automation work too.

### Edit a scene, GameObject, or asset — `unity status` first

**Before editing any scene, GameObject, prefab, or asset, run `unity status` to detect a connected Editor.** If one is reachable, drive it with live commands instead of touching project files — the Editor applies changes to the *actual active scene* and keeps its in-memory state in sync.

```bash
unity status                       # is an Editor connected? (look for state "ready")
unity command                      # discover the scene/GameObject commands THIS Editor exposes
# then drive it with the commands it lists — for example, if your Editor exposes them:
unity command create_gameobject    # act on the live, active scene
unity command save_scene           # persist the active scene
```

Command names are defined by the Editor, so run `unity command` (or `unity list`) to see the exact set — don't assume a name.

> **Never hand-edit `.unity`, `.prefab`, or `.asset` YAML while a live Editor is reachable.** Raw-file edits are:
> - **error-prone** — fileIDs and GUIDs are assigned by hand and easy to get wrong;
> - **invisible** to the running Editor until a reimport, so the change silently fails to take effect; and
> - **prone to hitting the wrong file** — e.g. writing to `SampleScene.unity` while the Editor's active scene is actually `Demo2.unity`, producing valid-looking YAML that changes nothing the user sees.

**Rule out two false negatives before concluding no Editor is reachable — both look identical to a genuinely closed Editor, and both are easy to get wrong under time pressure:**

- **Safe Mode.** If an Editor *is* running for this project but `unity status` / `unity command` won't connect, it may be stuck in **Safe Mode** from a compile error rather than genuinely absent. Run `unity pipeline list` — if it reports Safe Mode, editing the C# source to fix the compile errors (and then restarting Unity) *is* the correct move, not a fallback. See [integration-advanced.md → Recovering from Safe Mode](references/integration-advanced.md#recovering-from-safe-mode-connection-fails-because-of-compile-errors).
- **A sandboxed agent shell.** If your own shell commands run inside a restrictive sandbox — the normal case for a coding agent like this one — the sandbox can hide a genuinely running Editor from `unity status` the same way. This applies to **every** scene/GameObject/prefab/asset task that reaches this preflight, not only ones that obviously need a live Editor: a task you could otherwise finish without any CLI involvement (e.g. generating an asset through ordinary Editor APIs) can still get funneled into "no Editor" here and derailed. Don't treat "no instances" as proof the Editor is down, and don't quietly improvise a third path — like driving a separate headless Editor process to approximate what a live connection would have done — as a substitute for a disclosed file edit. Say plainly that your sandbox may be blocking your view of a real Editor, and ask whether one is actually open before falling back. Full detail: [integration-advanced.md → Sandboxed agent tooling can hide a running Editor](references/integration-advanced.md#sandboxed-agent-tooling-can-hide-a-running-editor).

Only fall back to editing files directly once you've ruled out both of the above — and say so explicitly ("no live Editor detected, editing the file directly").

### Generate a texture, sprite, image, or 3D model

Before approximating the request another way, check the catalog plugin list
(`unity plugin list --format json`) for the Unity AI Generators plugin (`ai`). Never install a
catalog plugin silently — ask first. The install/skill-mirroring flow and the alpha gating are in
[catalog-plugins.md](references/catalog-plugins.md).

### Bootstrap a new project from scratch

> For a **guided** end-to-end experience — concept questions, installing the Editor in the
> background while you plan, package selection, and monetization handoff — use the
> **`new-unity-project`** skill. This section is the raw CLI recipe that skill builds on; use it
> directly when you just want the commands.

Take an idea to a running, version-controlled project using only the CLI. Decide the **target
platforms first** — they determine which Editor modules you install in step 2. You can add
modules later (`unity install-modules`), but a project can't build for a platform until that
platform's module is installed, so it's simplest to decide up front.

```bash
# 1. Confirm the CLI works and you're signed in and licensed (see references/auth-license-cloud.md).
unity --version
unity auth status --format json      # if signed out:      unity auth login
unity license status --format json   # if none active:      unity license activate

# 2. Pick and install an Editor with the modules your target platforms need.
#    Default to the latest LTS (most stable, ~2 years of patches). Reach for a Tech-stream
#    release (--stream tech) only for a feature not yet in LTS; treat --stream beta/alpha as
#    evaluation-only, never for a project you intend to ship. A deadline argues for LTS.
#    (lts / latest aliases work almost everywhere a version is accepted — `templates` is the
#     exception; see step 3.)
unity releases --stream lts --limit 5 --format json
unity install lts --module android --module ios --yes --accept-eula   # add --module webgl, etc.
unity editors --installed --format json                               # confirm it landed

# 3. List the real template ids this Editor offers — don't guess them — and pick by RENDER
#    PIPELINE, not just by 2D/3D. Default to the URP templates:
#      3D → com.unity.template.urp-blank      ("Universal 3D")
#      2D → com.unity.template.universal-2d   ("Universal 2D": URP + the 2D packages)
#    com.unity.template.3d and com.unity.template.2d are the Built-in Render Pipeline templates
#    (displayName "… (Built-In Render Pipeline)"): deprecated from Unity 6.5, gone in 6.7. Use
#    them only when the user explicitly asks for Built-in. Confirm the pick with the JSON
#    `renderPipeline` field — it is blank for universal-2d on current releases, so match that
#    one by id.
#    NOTE: `templates` does NOT resolve the lts / latest aliases — unlike `install` and
#    `projects create`, it passes --editor straight through and rejects anything that is not a
#    concrete 6000.x.y. Use the version you just installed (read it from `editors --installed`).
unity templates list --editor <6000.x.y> --type core --format json

# 4. Create the project. The first positional arg is the NAME; --path sets the parent directory.
#    All options supplied, so it won't prompt; add --non-interactive in CI.
#    (To publish it to a remote in the same step, use the source-control forms below instead.)
unity projects create "MyGame" --path ~/UnityProjects \
  --editor-version lts --template com.unity.template.urp-blank

# 5. Add the Pipeline package BEFORE the first open. `unity command`, `unity status` and
#    `unity command eval` all need it, and templates don't include it. The Editor reads
#    Packages/manifest.json when it loads the project, so installing first means the package
#    is live from the first open.
unity pipeline install --project-path ~/UnityProjects/MyGame

# 6. Open the project and wait until its Editor is ready to take commands. Run from inside the
#    project so the CLI targets its Editor (--project-path here is a substring filter, not a path).
cd ~/UnityProjects/MyGame
unity open .
unity status --until-ready --project-path MyGame --format json
```

**Then build the scene in the live Editor, not in batch mode.** Create GameObjects, wire
components, and set asset references with `unity command eval` (or the Editor's own `unity command`
tools) against the running Editor, and read the result back the same way. Each step can be
checked before the next one. A script run through `unity run -- -executeMethod` is the
**fallback** for when no Editor can stay open (CI, a headless build box). It can't see what it
produced: a reference saved as null, such as a `UIDocument` with no `PanelSettings`, still
reports success. If you do use batch mode, open the result in a live Editor and inspect it before
calling the work done. How to get an Editor to drive:
[integration-advanced.md → Getting an Editor to drive](references/integration-advanced.md#getting-an-editor-to-drive).

**Source control — let the user choose.** The CLI publishes the new project to a fresh remote in
one step for any provider. **Always pass tokens on stdin** (`--git-token-stdin`) so secrets never
land in shell history or the process list. Pick based on the project — don't default to one:

- **Git — GitHub / GitLab** (`--vcs github` / `--vcs gitlab`). Ubiquitous. For asset-heavy games
  add **Git LFS** (`--git-lfs`) so large binaries don't bloat history.
- **Unity Version Control — UVCS** (`--vcs uvcs`). Unity's own VCS, built for large binary game
  assets: it handles them natively (**no LFS needed**) and supports file locking — often the
  better fit for art-heavy projects or larger teams. Auth uses your Unity sign-in; `--vcs-region`
  selects the region.

```bash
# Git (GitHub) — drop --git-lfs if the game isn't asset-heavy. Add --no-initial-commit if you
# want to add packages/assets BEFORE the first commit (see the new-unity-project flow).
unity projects create "MyGame" --path ~/UnityProjects \
  --editor-version lts --template com.unity.template.urp-blank \
  --vcs github --git-namespace my-org --git-repo my-game \
  --git-visibility private --git-default-branch main --git-token-stdin --git-lfs

# Unity Version Control (UVCS) — handles binaries natively, so no LFS:
unity projects create "MyGame" --path ~/UnityProjects \
  --editor-version lts --template com.unity.template.urp-blank \
  --vcs uvcs --git-namespace my-org --git-repo my-game --vcs-region <region>
```

Feed the token to `--git-token-stdin` from a secret store, never a literal — e.g.
`… --git-token-stdin <<<"$GIT_TOKEN"` where `$GIT_TOKEN` comes from your CI/secret manager
(UVCS uses your Unity sign-in, so no token is needed).

**Working with a UVCS workspace day to day: a few wrapped reads, everything else straight through
to `cm`.** The split is deliberate and worth teaching, because guessing wrong wastes a user's time:

- **`unity vcs uvcs <verb>`** wraps the reads that **join `cm`'s data to your project** —
  `locks` (who holds a lock, *and which locks cover files you have already changed*),
  `changesets`, and `review`. Those joins are the thing `cm` cannot do for you, and they come in a
  stable envelope, so prefer them whenever something *parses* the output.
- **`unity uvcs <args>`** forwards the whole command line to `cm` verbatim, `--help` and
  `--format` included. That is the supported route, not a workaround: `cm` owns and versions this
  vocabulary, so wrapping it would pin a paraphrase that goes stale. Reach for it for **partial
  checkout**, **shelves**, and **taking or releasing a lock**, and when a human reads the output.

```bash
unity vcs uvcs locks                       # who holds what, and what collides with your changes
unity uvcs lock list                       # the raw listing, cm's own flags and output
unity uvcs partial update /Assets/Levels   # cm's own vocabulary, unchanged
unity uvcs shelve -c "wip: lighting pass"
```

Every verb, flag and trap: [version-control.md](references/version-control.md).

`unity cm <args>` is the same passthrough under cm's own name. Both need the `cm` client; install
it with `unity plugin install plastic` if a command says it is missing.

**Beyond setup, the `vcs` group covers the whole day-2 loop** — `status`, `sync`, `switch`,
`merge-setup`, `conflicts` / `explain` / `resolve`, `diff`, `blame`, `summarize`, `affected`,
`hooks`, `doctor`, `providers` — and the Unity semantics are the reason to reach for it over raw
`git`. Full reference, with the flags and the traps:
[version-control.md](references/version-control.md).

**Git tokens belong to the user's credential manager, not the CLI.** When no token flag or env var
is given, the CLI asks `git credential fill` and uses whatever the configured helper returns; it
stores nothing it is passed or told. Don't suggest the CLI can save a Git token, and don't reach for
a token flag when the user already has a working credential helper. If they want a different token
per organization, that is `git config --global credential.useHttpPath true` plus a multi-account
helper such as [Git Credential Manager](https://github.com/git-ecosystem/git-credential-manager).
The CLI passes the full repo URL so the helper can discriminate, but it never installs or
reconfigures a helper. `UNITY_GITHUB_TOKEN` / `UNITY_GITLAB_TOKEN` are one token per provider, so a
CI job spanning several orgs should pass `--git-token-stdin` per invocation instead. See
[references/projects-templates.md](references/projects-templates.md) for the full
source-control flag set. For a purely local Git repository instead, initialize git with a
Unity-appropriate ignore so the multi-GB `Library/` and other generated folders are never committed:

```bash
cd ~/UnityProjects/MyGame
git init -b main
# Download (do not pipe to a shell) a maintained Unity .gitignore:
curl -fsSL https://raw.githubusercontent.com/github/gitignore/main/Unity.gitignore -o .gitignore

# Asset-heavy game? Keep large binaries out of git history with Git LFS:
git lfs install
git lfs track "*.psd" "*.fbx" "*.wav" "*.mp3" "*.png"   # adjust to your asset types
git add .gitattributes

git add -A
git status                             # sanity-check: Library/ Temp/ obj/ Build/ must NOT be staged
git commit -m "Initial Unity project: MyGame"
git ls-files | grep -c '^Library/'     # must print 0
```

**What the CLI does and doesn't cover.** The CLI handles editor, project, and source control.
It does **not** manage UPM (Unity Package Manager) packages — to add packages beyond the
template headlessly, use the **`unity-package-management`** skill (C# PackageManager Client
API). For monetization/backend, hand off to the dedicated skills: `implement-in-app-purchases`
(IAP), `levelplay-unity-integration` (ads), or `build-live-game` (accounts, cloud save,
economy, remote config, leaderboards). Once steps 5 and 6 above are done, work in the open
Editor.

**Installed the Pipeline package while the Editor was already open?** The Editor picks up the
manifest change only when it next refreshes. Until then `unity status` reports
`STATUS_PIPELINE_LOAD_PENDING` (or `STATUS_NO_INSTANCES` on CLI releases before that code
existed). An Editor that is still opening or importing reports the same code until it finishes,
so wait for that first (`unity status --until-ready`). The CLI can't trigger the refresh itself:
if the Editor has finished opening, ask the user to switch to the Unity Editor window, then
re-run `unity status --until-ready`. Installing before the first open (step 5) avoids this
entirely.

### Find and install a missing editor

```bash
# 1. Check what's installed
unity editors --installed --format json

# 2. Browse available LTS versions
unity releases --lts --limit 5 --format json

# 3. Install
unity install 6000.0.47f1 --yes --accept-eula
```

### Open a project with the correct editor

```bash
# 1. Check the project's required editor version
unity projects info /path/to/MyProject --format json
# Look at "editorVersion" in the result

# 2. Confirm that editor is installed
unity editors --installed --format json

# 3. Open (warns if the editor version is missing)
unity open /path/to/MyProject
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

`Builder.PerformBuild` is your own static Editor method. On Unity 6 it can call
`BuildPipeline.BuildPlayer(BuildPlayerOptions)` or, to reuse a Build Profile asset,
`BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions { buildProfile = …, locationPathName = … })`
(6000.0+). Fail the build by calling `EditorApplication.Exit(1)` when
`report.summary.result != BuildResult.Succeeded`.

`unity run` also works (batch mode is automatic — never pass `-batchmode`/`-quit`):

```bash
unity run /path/to/MyProject --editor-version 6000.0.47f1 --allow-install \
  -- -executeMethod Builder.PerformBuild -logFile build.log
```

### CI: run tests and publish results

```bash
unity test /path/to/MyProject \
  --editor-version 6000.0.47f1 \
  --mode EditMode \
  --report-format junit \
  --output ./test-results.xml \
  --allow-install \
  --timeout 600
case $? in
  0) echo "All tests passed" ;;
  8) echo "Tests failed — report to developers, do not retry" ;;
  *) echo "Run did not complete — infrastructure failure, safe to retry" ;;
esac
```

Exit `8` means the run finished and reported failing tests; any other non-zero code means it never produced a verdict. Under `--format json` the same split is `errors[0].code`: `TESTS_FAILED` versus `TEST_RUN_ERROR` / `TEST_TIMED_OUT`.

`--report-format junit` makes `--output` a JUnit-schema report, which GitHub Actions and GitLab ingest as native test results with no converter step. It is written even when tests fail. Drop the flag for the NUnit3 default, or use `--report-format nunit,junit` to get both from one run. Add `--coverage` to collect coverage via the Unity Code Coverage package — it warns and carries on if the project doesn't have the package. See [build-run-test.md](references/build-run-test.md).

### Debug the CLI

```bash
# Check auth + installed editors + recent errors in one command
unity doctor --format json

# Follow live logs during an install
unity logs --follow --level info
```
