---
name: asset-transformer-toolkit
description: "Imports CAD, 3D model and point-cloud files with Unity Asset Transformer Toolkit (formerly the Pixyz Plugin, com.unity.industry.toolkit 4.0+) and creates, edits and runs its RuleSets, Rules and Actions (decimate, merge, repair, LOD generation) by running C# in the open Editor. Use when importing CAD/STEP or point clouds, building an importer or RuleSet, or generating LODs on imported models. Triggers: \"Pixyz\", \"Asset Transformer\", \"import this STEP file\", \"decimate the imported CAD\". Not for Pixyz Studio, or DCC/Blender FBX export and import settings (blender-to-unity-pipeline)."
license: Unity Companion License
metadata:
  category: editor-tooling
  sources: "https://github.com/Unity-Technologies/skills/tree/main/skills/asset-transformer-toolkit"
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
`unity command eval` (see `unity-cli` → Eval syntax): drop the `using` directives from the
reference examples and fully qualify types (`UnityEditor.PixyzPlugin4Unity.RuleEngine.RuleSet`,
`UnityEngine.Debug`, …). If the CLI cannot connect, rule out Safe Mode and a sandboxed shell first
(`unity-cli` → Can't connect?); only then write an Editor script under an `Editor/` folder with a
`[MenuItem]` and ask the user to run it.

## Terms and API location

- Call it **Asset Transformer Toolkit** with the user unless they say "Pixyz".
- Tool functions live in `Unity.Pixyz.Plugin4Unity.Editor.AI.ATTAssistantUtilities` and are
  documented inline in each reference below. Most classes are in the
  `Unity.Pixyz.Plugin4Unity.Editor` assembly.
- Asset Transformer Toolkit is **not** Asset Transformer Studio / Pixyz Studio. Never rely on
  information about the Studio product.

## Workflows

Pick the reference for the task from [References](#references) and read it before writing C#.

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

## References

- [references/create-importer.md](references/create-importer.md): read when creating an importer for an external file, or reimporting through an existing ImporterScriptableObject.
- [references/rulesets-and-actions.md](references/rulesets-and-actions.md): read when creating, modifying, validating or running RuleSets, setting Action parameters, or listing available Actions.
- [references/ruleset-api.md](references/ruleset-api.md): read when calling the RuleSet class API.
- [references/rule-api.md](references/rule-api.md): read when calling the Rule class API.
- [references/ruleblock-api.md](references/ruleblock-api.md): read when constructing `RuleBlock` instances, including `ActionBase.Id`.
- [references/lods.md](references/lods.md): read when generating levels of detail on imported models.

## Related skills

- unity-cli: the eval syntax and connection troubleshooting for running the C#.
- unity-package-management: checking or upgrading `com.unity.industry.toolkit`.
- blender-to-unity-pipeline: DCC/Blender FBX export and model import settings.
- optimize-3d-rendering: LOD Group tuning and mesh LOD beyond what the toolkit generates.
