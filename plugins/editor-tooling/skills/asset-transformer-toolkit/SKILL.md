---
name: asset-transformer-toolkit
description: Imports CAD, 3D model and point-cloud files with Unity Asset Transformer Toolkit (formerly the Pixyz Plugin, `com.unity.industry.toolkit` 4.0+) and creates, edits, validates and runs its RuleSets, Rules, RuleBlocks and Actions (decimate, merge, repair, LOD generation) through the `ATTAssistantUtilities` API and ImporterScriptableObjects. Use when the user mentions Pixyz, Asset Transformer, importing CAD/STEP/point clouds into Unity, creating or configuring an importer, building a RuleSet or adding Actions to one, setting Action parameters, or generating LODs on imported models. Not for Asset Transformer Studio / Pixyz Studio (a separate product).
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: editor-tooling
  sources: "Unity-Technologies/skills/skills/asset-transformer-toolkit"
  unity: "6000.0+"
---

# Asset Transformer Toolkit (formerly Pixyz Plugin)

## Before you start: check the package version

This skill needs Asset Transformer Toolkit 4.0.0 or later (`com.unity.industry.toolkit`). Earlier
versions don't have the `ATTAssistantUtilities` API every workflow below relies on.

1. Read the resolved version of `com.unity.industry.toolkit` from `Packages/packages-lock.json`
   (fall back to `Packages/manifest.json`).
2. **Not installed:** tell the user this workflow needs Asset Transformer Toolkit 4.0 or later, an
   entitled Unity package they may need access to through their Unity plan, and stop. Don't add it
   to the manifest yourself.
3. **Installed below 4.0.0:** report the version found, say 4.0 or later is required, and ask
   before upgrading (see `unity-package-management`). Don't fall back to older APIs, raw C# against
   internal types, or reflection.
4. **4.0.0 or later:** continue.

## Running the C#

Every workflow writes C# and runs it in the user's open Editor. With the `unity` CLI that is
`unity command eval` (see `unity-cli`). `eval` compiles a statement block, not a file: drop the
`using` directives from the reference examples and fully qualify types
(`UnityEditor.PixyzPlugin4Unity.RuleEngine.RuleSet`, `UnityEngine.Debug`, …). Without a live
Editor connection, write an Editor script under an `Editor/` folder with a `[MenuItem]` and ask
the user to run it.

## Terms and API location

- Call it **Asset Transformer Toolkit** with the user unless they say "Pixyz".
- Tool functions live in `Unity.Pixyz.Plugin4Unity.Editor.AI.ATTAssistantUtilities` and are
  documented inline in each reference below. Most classes are in the
  `Unity.Pixyz.Plugin4Unity.Editor` assembly.
- Asset Transformer Toolkit is **not** Asset Transformer Studio / Pixyz Studio. Never rely on
  information about the Studio product.

## Workflows

| Task | Read |
|---|---|
| Create an importer for an external file, or reimport through an existing ImporterScriptableObject | [references/create-importer.md](references/create-importer.md) |
| Create, modify, validate or run RuleSets; set Action parameters; list available Actions | [references/rulesets-and-actions.md](references/rulesets-and-actions.md) |
| RuleSet class API | [references/ruleset-api.md](references/ruleset-api.md) |
| Rule class API | [references/rule-api.md](references/rule-api.md) |
| RuleBlock API, including `ActionBase.Id` for constructing `RuleBlock` instances | [references/ruleblock-api.md](references/ruleblock-api.md) |
| Levels of detail on imported models | [references/lods.md](references/lods.md) |

## Rules

- **Never trigger a reimport on your own.** Importing can take a long time; change importer
  settings and stop unless the user asked to import.
- Modify importer fields through the `ScriptableObject` / `SerializedObject` API. Never use
  reflection — importer internals are not reachable that way.
- Imports run asynchronously in the background: report that the import *started*, not that it
  finished.
- Cap fix-and-retry loops at three attempts per step, then report what failed.
- For runtime LOD behavior beyond what the toolkit generates (LOD Group tuning, mesh LOD on
  Unity 6.2+), hand off to the rendering/optimization skills rather than improvising here.
