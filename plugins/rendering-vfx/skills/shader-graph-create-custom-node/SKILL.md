---
name: shader-graph-create-custom-node
description: Turns HLSL into reusable Shader Graph nodes for URP on Unity 6 - Custom Function nodes (File or String mode, _float/_half suffixes, UnityTexture2D/UnitySamplerState, SHADERGRAPH_PREVIEW guards), Sub Graphs, and reflected HLSL function nodes (UNITY_EXPORT_REFLECTION with sg: hint tags, Shader Graph 17.5+). Also covers Shader Graph structure (targets, material types, blackboard properties, keywords) and safe ways to inspect or create .shadergraph assets. Use when the user wants a custom Shader Graph node, to call existing HLSL from Shader Graph, to package node clusters for reuse, to fix "undeclared identifier" or "_float" errors in a Custom Function node, or to set up a graph for VFX Graph or fullscreen effects.
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: rendering-vfx
  sources: "Unity-Technologies/skills/skills/shader-graph-create-custom-node, AlexeyPerov/Unity-Open-MCP/skills/extensions/shadergraph"
  unity: "6000.0+"
---

# Custom Shader Graph Nodes

Three ways to get custom HLSL into Shader Graph. Pick by Shader Graph version and reuse needs:

| Approach | Version | Use when |
|---|---|---|
| Reflected function node (`UNITY_EXPORT_REFLECTION`) | `com.unity.shadergraph` 17.5+ (Unity 6000.5 line) | You want a first-class node in the Create Node menu with typed, hinted ports (colors, ranges, dropdowns, UV/position referables) |
| Custom Function node | All Unity 6 versions | One-off logic, or projects below 17.5 |
| Sub Graph (`.shadersubgraph`) wrapping nodes or a Custom Function | All | Reusable building block shared across graphs; works on every version |

Check the installed version in `Packages/manifest.json` or `Packages/packages-lock.json` before choosing. Do not emit reflected nodes for a project below 17.5: `ShaderApiReflectionSupport.hlsl` and `UNITY_EXPORT_REFLECTION` do not exist there, so the include fails to compile and no node appears.

## Workflow: reflected function node (17.5+)

1. **Write the HLSL function** and precede it with `UNITY_EXPORT_REFLECTION`. Return value becomes the main output; `out` parameters become extra outputs; `inout` parameters are both.
2. **Add hint tags** in `///` doc comments directly above the function:
   - Required function hints in `<funchints>`: `sg:ProviderKey` (unique and stable; changing it breaks existing graphs), `sg:SearchCategory`, `sg:SearchTerms`, `sg:DisplayName`.
   - Optional parameter hints in `<paramhints name="...">`: display name, default, color, range, dropdown, static (inspector-only), referables (UV, Position, Normal...), linkage.
   - Full tag list and a worked file: [references/reflection-hints.md](references/reflection-hints.md).
3. **Write it to an asset**:
   - Search the project for an existing `.hlsl` ShaderInclude that already holds custom Shader Graph nodes. If one fits, show the user its current contents and ask before appending.
   - Otherwise create a new `.hlsl` file under `Assets/` (for example `Assets/Shaders/Nodes/MyNodes.hlsl`).
   - The file must begin with `#include "ShaderApiReflectionSupport.hlsl"`.
4. **Verify**: let Unity import, open a Shader Graph, search the Create Node menu by the `sg:SearchTerms` or display name, and check the Console for HLSL errors from the include.

```hlsl
#include "ShaderApiReflectionSupport.hlsl"

/// <funchints>
///     <sg:ProviderKey>MyStudio.Remap01</sg:ProviderKey>
///     <sg:DisplayName>Remap 0-1</sg:DisplayName>
///     <sg:SearchCategory>MyStudio/Math</sg:SearchCategory>
///     <sg:SearchTerms>remap, range, normalize</sg:SearchTerms>
/// </funchints>
/// <paramhints name="inMin">
///     <sg:Default>0</sg:Default>
/// </paramhints>
/// <paramhints name="inMax">
///     <sg:Default>1</sg:Default>
/// </paramhints>
UNITY_EXPORT_REFLECTION float Remap01(float value, float inMin, float inMax)
{
    return saturate((value - inMin) / max(inMax - inMin, 1e-5));
}
```

## Workflow: Custom Function node

1. Create the node (Create Node > Utility > Custom Function). Set inputs/outputs in the Graph Inspector with exact types.
2. **Type = File**: point *Source* at an `.hlsl` asset; *Name* is the function name **without** the precision suffix.
3. **Type = String**: paste only the function body; Shader Graph generates the signature from the ports.
4. In File mode, define the function with a `_float` suffix (and `_half` if the graph may use half precision). Outputs are `out` parameters; the function returns `void`.
5. Guard the include and anything unavailable in node previews.

```hlsl
#ifndef MYSTUDIO_LIGHTING_NODES_INCLUDED
#define MYSTUDIO_LIGHTING_NODES_INCLUDED

void MainLightInfo_float(float3 PositionWS, out float3 Direction, out float3 Color, out float ShadowAtten)
{
#ifdef SHADERGRAPH_PREVIEW
    Direction = normalize(float3(0.5, 0.5, -0.5));
    Color = 1;
    ShadowAtten = 1;
#else
    float4 shadowCoord = TransformWorldToShadowCoord(PositionWS);
    Light light = GetMainLight(shadowCoord);
    Direction = light.direction;
    Color = light.color;
    ShadowAtten = light.shadowAttenuation;
#endif
}

void SampleTinted_float(UnityTexture2D Tex, UnitySamplerState SS, float2 UV, float4 Tint, out float4 Out)
{
    Out = Tex.Sample(SS, UV) * Tint;
}

#endif
```

- Texture ports arrive as `UnityTexture2D` / `UnityTexture2DArray` / `UnityTexture3D` / `UnityTextureCube`; samplers as `UnitySamplerState`. Use `Tex.Sample(SS, uv)` or `SAMPLE_TEXTURE2D(Tex.tex, SS.samplerstate, uv)`. Do not declare `sampler2D`.
- Main-light shadows in Unlit graphs need the `_MAIN_LIGHT_SHADOWS` / `_MAIN_LIGHT_SHADOWS_CASCADE` / `_SHADOWS_SOFT` keywords added as multi_compile keywords on the graph, otherwise `shadowAttenuation` is always 1.
- Common errors: "undeclared identifier MyFunc_float" -> Name field includes the suffix, or precision mismatch; "redefinition" -> missing include guard; works in game but preview is pink -> missing `SHADERGRAPH_PREVIEW` fallback.

## Shader Graph structure (for context)

- A `.shadergraph` asset has a **target** (Universal) and **material type** in Graph Settings: Lit, Unlit, Sprite Lit/Unlit/Custom Lit, Decal, Fullscreen, Canvas (uGUI). Pick the template on creation; switching later re-maps master stack blocks.
- **Blackboard properties** become material properties; set *Reference* names deliberately (`_BaseColor`, `_BaseMap`) so scripts and material conversions keep working. Uncheck *Exposed* for per-material constants set only from code.
- **Keywords** (Boolean/Enum): *Shader Feature* strips unused variants in builds; *Multi Compile* keeps all. Every keyword multiplies variants; prefer branches on uniforms for cheap toggles.
- **Sub Graphs** expose their own blackboard as node ports; their output node defines output ports.
- **VFX Graph**: Graph Settings > *Support VFX Graph* to use the graph in VFX outputs (see `unity-vfx-graph`).
- **Fullscreen** material type pairs with the Full Screen Pass Renderer Feature or a Render Graph blit (see `validate-urp-render-graph-renderer-feature`).

## Inspecting and creating graphs safely

- A `.shadergraph` file is multi-document JSON (nodes, slots, edges with node GUIDs and integer slot IDs). Reading it to list nodes, properties, and edges is safe and version-stable.
- The editor graph API (`UnityEditor.ShaderGraph.GraphData`, `AbstractMaterialNode`) is partly internal and changes between versions. Do not build graphs by reflection or by writing JSON by hand; small mistakes produce graphs that fail to load. Create assets from the *Assets > Create > Shader Graph* templates, wire nodes in the window, and keep custom logic in HLSL files referenced by nodes.
- The compiled result is inspectable at runtime: `Shader.Find`, `shader.GetPropertyCount()`, `GetPropertyName(i)`, `GetPropertyType(i)`, and `ShaderUtil.GetShaderMessages(shader)` in the Editor for compile errors.

## Related skills

- `unity-vfx-graph` for Shader Graph outputs in VFX.
- `validate-urp-render-graph-renderer-feature` for fullscreen passes that use these shaders.
- `migrate-birp-to-urp` when porting Built-in surface shaders into Shader Graph.
