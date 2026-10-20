---
name: audio-setup-mixers
description: Routes scene AudioSources into the right AudioMixerGroup (Music, SFX, Foley, Voice, UI, Ambience) by classifying what each plays, and covers runtime mixer control - exposed parameters, snapshots, ducking, and dB-correct volume sliders. Use when cleaning up or setting outputAudioMixerGroup, deciding which group a sound belongs in, or wiring a settings slider to AudioMixer.SetFloat. Mixer and group creation stay manual (no public API).
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: audio-performance
  sources: "https://github.com/Unity-Technologies/skills/tree/main/skills/audio-setup-mixers, https://github.com/AlexeyPerov/Unity-Open-MCP/tree/master/skills/extensions/audio"
  unity: "6000.0+ (live eval path needs com.unity.pipeline)"
---

# Audio Mixer Setup

## Scope

Automated through public API:

- Find mixers (`AssetDatabase` + `UnityEngine.Audio.AudioMixer`).
- List groups (`AudioMixer.FindMatchingGroups("")`, flat list).
- Read and assign `AudioSource.outputAudioMixerGroup`.
- Read and set exposed parameters at runtime (`GetFloat` / `SetFloat` / `ClearFloat`), transition snapshots.

Handed back to the user: creating a mixer, creating or re-parenting groups, adding effects, exposing parameters, and editing group volumes in the asset. These exist only on the non-public `AudioMixerController` / `AudioMixerGroupController`. Do not use reflection on them and do not hand-edit `.mixer` files; both break silently across Unity versions.

## Step 0: Execution path

Preferred: a live Editor with `unity command eval`. Eval syntax and prerequisites: see the `unity-cli` skill (unity-editor-tooling plugin) → Eval syntax. Every snippet in [references/api.md](references/api.md) is already in eval form.

When `eval` is unavailable, continue without it:

- Inventory and classify read-only: `.mixer` YAML lists groups (`AudioMixerGroupController` entries with `m_Name`) and exposed parameters; scene and prefab YAML give each `AudioSource`'s `OutputAudioMixerGroup` reference and clip GUID. Report from that; never write `.mixer`, scene, or prefab files by hand.
- Routing: wrap the assign snippet in a static `[UnityEditor.MenuItem("Tools/Audio/Route Sources")]` method for the user to save under `Assets/Editor/` and run; it logs every routed, missing-group, and unmapped source.

## Step 1: Pre-flight

1. If the user did not explicitly ask for mixers, confirm they want routing set up.
2. Run the inventory snippet. It lists every mixer under `Assets/` and its groups as a flat list. If the hierarchy matters, ask the user to read it off the Audio Mixer window.
3. No mixer at all: stop and ask the user to create one (Window > Audio > Audio Mixer, **+** next to Mixers). Resume once it exists.

## Step 2: Classify sources

Run the routing-read snippet. Classify each AudioSource by, in order:

1. Clip asset name: `FootStep4_Sound` → Foley, `Dialogue_Female_Scene4` → Voice, `GunShot` → SFX, `Menu_Theme_Variation` → Music, `Wind_Loop` → Ambience, `Button_Click` → UI.
2. GameObject name, then sibling MonoBehaviour names.
3. Low confidence: propose an `Uncategorized` group rather than guessing.

Sources with no clip assigned in the scene (set from script) are classified by GameObject or component name; say so.

## Step 3: Agree the group list

Present each source and its proposed group. **WAIT** for the user.

- Prefer an existing group that genuinely covers the category, even under a different name.
- Do not collapse categories a mixing engineer keeps apart: Foley is a subset of SFX, not a synonym, and a gunshot does not go in `Foley` just because that group exists.
- For missing groups, hand over exactly (mixer name, group names, parent) using the template at the end of [references/api.md](references/api.md).
- Re-run the inventory to confirm the groups exist and are spelled as expected before routing.

## Step 4: Route

Run the assign snippet. It keys the mapping on clip name first, GameObject name second (matching Step 2), and wraps the pass in one undo group.

Report, never swallow:

- `NO SUCH GROUP` entries: the group is missing or spelled differently.
- `NOT IN THE MAPPING` entries: sources the classification missed. "Routed 4" while three were skipped reads as success and is the worst outcome.
- The scene changed, not the mixer. Routing persists only after the scene (or prefab) is saved. Save only with the user's agreement. Sources inside prefab instances may need the change applied to the prefab asset instead of left as overrides; ask.

## Step 5: Runtime control (when asked)

Volume sliders, mute toggles, snapshot switches, and ducking are runtime code, not Editor setup. Read [references/runtime-mixer-control.md](references/runtime-mixer-control.md) before writing it. Key rules:

- A parameter must be exposed in the Audio Mixer window first (right-click a field > Expose); `SetFloat` on an unexposed name returns `false`.
- Convert linear slider values to decibels with `20 * log10(v)`, clamped to -80 dB. A linear 0..1 → -80..0 dB map makes most of the slider inaudible.
- `SetFloat` in `Awake` is ignored on some versions; apply saved volumes in `Start` or later.
- Once `SetFloat` drives a parameter, snapshots stop controlling it until `ClearFloat`.

## Pitfalls

- Exactly one `AudioListener` may be enabled at runtime. Check inactive cameras too.
- Routing a source to a group does not reduce cost by itself; effect placement does. For DSP cost see `optimize-audio`.
- On Web builds, mixer effects are not processed; groups still work for volume. See `optimize-web`.

## References

- [references/api.md](references/api.md) — read before running inventory, routing-read, or assign snippets.
- [references/runtime-mixer-control.md](references/runtime-mixer-control.md) — read when writing volume sliders, mute, snapshots, or ducking code.

## See also

- `optimize-audio` — import settings, mixer CPU audit, DSP buffer.
- `unity-cli` (unity-editor-tooling plugin) — connecting to the Editor, `eval` syntax and prerequisites.
