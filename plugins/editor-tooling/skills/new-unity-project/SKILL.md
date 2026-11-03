---
name: new-unity-project
description: "Guides a brand-new Unity 6 game from idea to a running, version-controlled URP project: asks concept, platforms and monetization while the Editor installs in the background, creates the project with Git or UVCS, installs packages in the open Editor, applies a visual baseline and makes the first commit. Use when starting a greenfield game or game-jam project. Triggers: \"make a new game\", \"bootstrap a Unity project\", \"I want to build a <genre> game\". Not for raw CLI commands (unity-cli), packages in an existing project (unity-package-management) or gameplay code (csharp-unity)."
license: Unity Companion License
metadata:
  category: editor-tooling
  sources: "https://github.com/Unity-Technologies/skills/tree/main/skills/new-unity-project"
  unity: "6000.0+"
---

# New Unity Project

A guided flow from an idea to a running, version-controlled Unity project. This skill owns the
**flow** — the questions, their ordering, running slow installs in the background while you ask,
and the handoffs. It deliberately does **not** re-document commands; it delegates the mechanics
to other skills.

**Delegates to (read these for the actual commands — don't reinvent them):**
- **`unity-cli`** — CLI install, auth/license, Editor install, project creation, source control,
  opening the project. Its "Bootstrap a new project from scratch" recipe
  (`references/new-project-bootstrap.md`) is the backbone here.
- **`unity-package-management`** — installing packages through the open Editor (`package_add`)
  or the C# PackageManager Client API, and choosing packages by genre / platform / monetization.
- **`urp-postprocessing`**, **`ui`**, **`2d-pixel-perfect`** — the visual baseline (Step 6):
  post-processing volume setup, HUD framework choice, pixel-perfect 2D.
- **`implement-in-app-purchases`**, **`levelplay-unity-integration`**, **`build-live-game`** —
  monetization / backend *integration* (invoked at the end).

The visual-baseline and monetization skills (and `migrate-birp-to-urp`) live in other plugins of this
marketplace — `ui` in unity-ui, `2d-pixel-perfect` in unity-2d, `urp-postprocessing` and
`migrate-birp-to-urp` in unity-rendering-vfx, the IAP/ads/live-game skills in
unity-services-monetization. If one isn't installed, proceed without it and do that step directly.

**Work one step at a time.** Ask only the current step's questions and wait for the user before
moving on — platform and monetization answers change what you install, so don't gather everything
up front or scaffold before they're settled.

## The flow — and where the parallelism is

1. **Concept** — what they're building.
2. **Platforms & monetization** — then, as soon as platforms are known, **kick off the Editor
   install in the background** (it takes minutes) and keep talking.
3. **(joins)** Editor + platform modules finish installing.
4. **Project + source control** — create from a **URP** template matching 2D/3D; init git; add the
   Pipeline package and open the Editor.
5. **Packages** — install in the open Editor with `unity command package_add`.
6. **Visual baseline** — post-processing, camera, quality tier, UI stack, pipeline-correct
   shaders, so the first frame doesn't look like an untouched template.
7. **Save & first commit.**
8. **Hand off** monetization / backend.

The whole point of a guided flow over a raw recipe: the multi-minute Editor install overlaps the
minutes the user spends answering concept questions, so setup feels instant.

## Step 1 — Concept

Use a multiple-choice question tool if your client has one so the user can pick fast, but let them answer freely too. Cover:

- **Genre / core loop** — platformer, top-down shooter, puzzle, idle, RPG, racing, card, tower
  defense, sim, hyper-casual, first-person, etc.
- **Dimension & look** — 2D or 3D; art style (pixel, low-poly, stylized, realistic, UI-only).
- **Gameplay** — the one-sentence "what the player does moment to moment."
- **Scope** — single-screen prototype vs. multi-scene game; single-player or multiplayer.

Also settle on a **project name**. Write a 2–4 line **project brief**, read it back to confirm.
The brief drives template choice (Step 4), packages (Step 5) and the visual baseline (Step 6):
note the palette / mood words the user gives, and whether the look is pixel art.

## Step 2 — Platforms & monetization, then start installing

Two decisions, because both change what you install:

- **Target platforms** (multi-select): Desktop (Win/macOS/Linux), Mobile (iOS/Android), WebGL,
  Console. These map to Editor **modules** (Step 3) and argue for leaner packages on mobile/WebGL.
- **Monetization**: none / premium / in-app purchases / ads / mix. This only decides which
  handoff skill you invoke in Step 8 — don't integrate it now.

Confirm the Editor version to use (**default: latest LTS** — see `unity-cli` for the LTS vs. Tech
vs. beta trade-off). Ask this *now*, before kicking off the install, so you don't install the
wrong one.

Then confirm prerequisites and **launch the Editor install as a background task** so it runs while
you continue. See the `unity-cli` skill for exact syntax, module names per platform, and auth /
license setup:

```bash
unity --version
unity auth status --format json      # if signed out:  unity auth login
unity license status --format json   # if none active: unity license activate

# Start in the BACKGROUND, then go straight back to the conversation. Module names per platform
# (android / ios / webgl / …) are in the unity-cli skill.
unity install lts --module <platform-modules> --yes --accept-eula
```

Run that install as a **background task** (don't block on it). If you have nothing left to ask,
it's fine to just wait — the parallelism only helps when there's a conversation to overlap.

## Step 3 — Join: Editor ready

Before creating the project, confirm the background install finished:

```bash
unity editors --installed --format json
```

If it failed, surface the error (see `unity-cli` troubleshooting) and stop — nothing downstream
works without an Editor.

## Step 4 — Create the project + source control

Follow the **`unity-cli`** "Bootstrap a new project from scratch" recipe
(`references/new-project-bootstrap.md`) verbatim:

- List the **real** template ids the Editor offers (`unity templates list --type core`) and pick by
  **render pipeline**, not just 2D/3D — don't guess ids. **Default to URP:**
  `com.unity.template.urp-blank` ("Universal 3D") for 3D, `com.unity.template.universal-2d`
  ("Universal 2D") for 2D. `com.unity.template.3d` / `com.unity.template.2d` are the **Built-in
  Render Pipeline** templates — deprecated from Unity 6.5, removed in 6.7 — so choose them only
  when the user explicitly asks for Built-in. Confirm the pick against the JSON `renderPipeline`
  field (blank for universal-2d on current releases, so match that one by id).
- Create with `unity projects create "<Name>" --path <dir> --editor-version <v> --template <id>`.
- Set up source control — **ask the user which they want**, don't assume: Git (GitHub / GitLab;
  add `--git-lfs` for asset-heavy games) or **Unity Version Control** (`--vcs uvcs`, which handles
  large binary assets natively — no LFS), or a purely local `git init` + Unity `.gitignore`.
  Publish in one step with `unity projects create --vcs … --git-token-stdin --no-initial-commit`
  (tokens on stdin). Pass **`--no-initial-commit`** so the CLI doesn't commit the bare project
  before packages and `.meta` files exist — you make the real first commit/check-in in Step 7.
  See the `unity-cli` workflow for exact flags.
- **Add the Pipeline package before the first open:** `unity pipeline install --project-path
  "<project-path>"`. Running C# in the Editor, `unity command package_add` and `unity command
  screenshot` all need `com.unity.pipeline`, and templates don't include it. The install updates
  `Packages/manifest.json`, which Unity reads at project load. Run it against an already-open
  project and the CLI reports `PIPELINE_MANIFEST_WRITE_FAILED`; ask the user to close the Editor
  and re-run. Without the package, every `unity command` fails to connect, which looks like the
  Safe Mode failure `unity-cli` describes but has a different cause.
- **Open the project:** `unity open "<project-path>"`, then wait for `unity status --until-ready`.
  Steps 5-7 all run against this open Editor.

## Step 5 — Packages

Map the brief to a concrete package list and install it in the open Editor with
`unity command package_add` (see **`unity-package-management`** → "Editor already open?") —
**never** hand-edit `manifest.json`. Read that skill for the genre/platform/monetization → package
mapping. Its headless batch-mode installer is only for CI or a machine where no Editor can stay
open; don't launch it while the Editor from Step 4 has the project open.
The template already provides the render pipeline — never add `com.unity.render-pipelines.universal`
to a Built-in template project (nothing assigns a URP asset; materials go pink) or vice versa.
Read the final list back to the user before installing; wait for `unity status --until-ready`
after the domain reload, then verify the ids in `Packages/packages-lock.json`.

## Step 6 — Visual baseline

A fresh template renders correctly but looks like a default: no tonemapping, untouched quality
tier, flat colors. Left there, agents reach for `OnGUI` and guess shader names, which is where
washed-out or magenta materials come from. Apply this floor **before** any gameplay work.

The Editor opened in Step 4 is still running. Apply each item below by running C# in it.
**`unity-cli` owns those commands and their syntax** — don't re-derive them here.

**Items 1, 2, 5 and 6 assume a URP template**, which is the Step 4 default. If the user
explicitly chose Built-in, don't run them as written: `UniversalAdditionalCameraData`, `Light2D`,
the `urp-postprocessing` volume framework and every `Universal Render Pipeline/*` shader are URP
types that do not exist there, so the generated C# won't compile. On Built-in, items 3 and 4
still apply as written, post-processing means the legacy Post Processing Stack, and the shader
names are `Standard` / `Unlit/Color`. Don't reach for `migrate-birp-to-urp` — the user asked for
Built-in.

1. **Post-processing.** Global Volume with Tonemapping (ACES), low Bloom (intensity 0.5–1,
   threshold 0.9) and a subtle Vignette (≈ 0.25); set `renderPostProcessing = true` on the main
   camera's `UniversalAdditionalCameraData`. **REQUIRED SUB-SKILL:** `urp-postprocessing` — its
   code templates create the volume and check HDR and the Volume layer mask.
2. **Camera and light.** 2D → orthographic, solid background color from the brief's palette, and
   a `Light2D` of type Global in the scene if the template scene has none (the Sprite-Lit shaders
   render black without one). 3D → perspective; keep the template's directional light and skybox,
   main-light shadows on.
3. **Quality tier.** Read `QualitySettings.names` first — tier names differ per template — then
   `QualitySettings.SetQualityLevel` to the one matching the primary target: the highest tier for
   desktop, the lowest for mobile/WebGL. Leave color space Linear and the Input System as the
   template set them.
4. **UI stack.** HUD and menus use a uGUI Canvas + TextMeshPro or UI Toolkit — **never `OnGUI` for player-facing HUD/menus** (runtime debug overlays: see `ui-imgui`).
   **REQUIRED SUB-SKILL:** `ui` picks between them for the project.
5. **Materials and shaders.** Under URP use `Universal Render Pipeline/Lit`, `…/Unlit`,
   `…/2D/Sprite-Lit-Default` or `…/2D/Sprite-Unlit-Default`. `Standard` and `Unlit/Color` render
   pink or washed out under URP. Prefer `GraphicsSettings.currentRenderPipeline.defaultMaterial`
   / `.default2DMaterial` over `Shader.Find`, and treat a `null` from `Shader.Find` as an error.
   `currentRenderPipeline`, not `defaultRenderPipeline`: a quality tier can carry its own pipeline
   asset, and item 3 above just set the tier.
6. **Pixel art only.** Point filter mode on sprites and a Pixel Perfect Camera — see
   `2d-pixel-perfect`.

Save the scene, then confirm both of these. **Not with `unity logs`** — that reads the CLI's own
log, never the Editor's, so it reports clean whatever the scene looks like:

- No render-pipeline or shader errors Editor-side. Read the Editor console through `unity-cli`,
  or read `Editor.log` directly (per-platform paths: `unity-editor-safety`).
- `unity command screenshot --output baseline.png` looks lit and tonemapped rather than flat
  gray.

## Step 7 — Save & first commit

The project is already open from Step 4, so `.meta` files exist. Make the first commit **with
whichever VCS you set up in Step 4**:

```bash
unity open "<project-path>"     # only if not already open; for headless/CI use the
                                # "Import & save headlessly" method in unity-package-management
```

- **Git (GitHub / GitLab / local):**
  ```bash
  cd "<project-path>"
  git add -A
  git status                    # Library/ Temp/ obj/ Build/ must NOT be staged
  git commit -m "Initial Unity project: <Name>"
  ```
  Every `.cs`/asset must be committed together with its `.meta`.
- **Unity Version Control (UVCS):** check in through your UVCS client/workspace (created during
  Step 4) — there's no `git` step. Generated folders are still excluded by the ignore rules.

If you published via `--vcs` in Step 4 **without** `--no-initial-commit`, the CLI already made an
initial commit of the bare project — add a follow-up commit here rather than double-committing.

## Step 8 — Hand off

Based on Step 2 monetization, invoke the matching skill for the actual integration:
- IAP → **implement-in-app-purchases**
- Ads → **levelplay-unity-integration**
- Accounts / cloud save / economy / remote config / leaderboards → **build-live-game**

Report the project path, Editor version, render pipeline and template, installed packages, the
visual baseline applied, and next steps.

## Scope — what this skill does NOT do

- **No gameplay scaffolding.** It gets you to a running, wired project with a visual floor;
  building the actual game (scenes, controllers, art) is the next conversation — iterate there
  in the open Editor through `unity command` (see `unity-cli`) and `unity-package-management`. Generic genre skeletons
  tend to produce throwaway mocked primitives, so this skill intentionally stops at a clean
  starting point. The visual baseline (Step 6) is settings, not content.
- **No command reference.** Syntax lives in `unity-cli` / `unity-package-management`.

## Checklist

- [ ] Concept brief captured and confirmed (genre, look, gameplay, scope, name)
- [ ] Platforms + monetization recorded; Editor version chosen
- [ ] Editor + platform modules installed (started in the background during Step 2)
- [ ] Project created from a **URP** template (`urp-blank` / `universal-2d`) unless Built-in was
      explicitly requested; git initialized with a Unity `.gitignore`
- [ ] `com.unity.pipeline` installed before the first open; Editor open and `ready`
- [ ] Packages installed in the open Editor (`package_add`); ids verified in `packages-lock.json`; no render-pipeline package added on top of the template
- [ ] Visual baseline applied: global Volume (tonemapping, bloom, vignette) + camera post-processing on,
      quality tier set for the target, UI stack chosen (no `OnGUI`), URP shader names, screenshot checked
- [ ] Project opened/saved so `.meta` files exist; first commit made; `Library/` excluded
- [ ] Handed off to the monetization/backend skill if applicable

## Common mistakes

- **Blocking on the Editor install** instead of backgrounding it while you ask questions.
- **Installing the wrong Editor** because the version wasn't confirmed before the background install.
- **Gathering all questions up front** — platform/monetization answers change the modules and packages.
- **Hand-editing `manifest.json`** instead of `package_add` / the Client API (see `unity-package-management`).
- **Running the headless package installer while the Editor is open** — the project is locked;
  use `unity command package_add` in that Editor.
- **Committing `Library/`/`Temp/`/`obj/`/`Build/`**, or scripts without their `.meta` files.
- **Missing Editor modules** — a mobile target needs `android`/`ios`; WebGL needs `webgl`.
- **Picking `com.unity.template.2d` / `.3d` because the name matches** — those are the Built-in
  pipeline templates. Use `universal-2d` / `urp-blank`.
- **Skipping the visual baseline** and shipping the template's flat defaults, then building the
  HUD with `OnGUI` and materials with `Shader.Find("Standard")` under URP.

## Related skills

- unity-cli: exact commands for Editor install, project creation, source control, Pipeline install and live Editor control.
- unity-package-management: package selection by genre/platform/monetization and the `package_add` / Client API install paths.
- urp-postprocessing, ui, 2d-pixel-perfect: the Step 6 visual baseline.
- implement-in-app-purchases, levelplay-unity-integration, build-live-game: the Step 8 monetization/backend handoff.
- unity-editor-safety: Editor.log paths and compile verification while setting up.
