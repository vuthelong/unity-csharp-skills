---
name: ui
description: Routes ambiguous Unity UI requests to the right skill. Use when a UI request names no framework (a menu, HUD, popup or editor window with no UI Toolkit, uGUI or IMGUI signal), when choosing between UI Toolkit and uGUI, or for framework comparison questions. Picks ui-uitk, ui-ugui or ui-imgui from project context and layers screen-navigator, optimize-text-mesh-pro or localization on top. Skip it when the request already names a framework.
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: ui
  sources: "Unity-Technologies/skills/skills/ui"
  unity: "6000.0+"
---

# Unity UI Router

Pick the UI system, then hand the request to the matching skill. Answer directly only for conceptual or comparison questions ("UI Toolkit or uGUI for mobile?", "how does Canvas batching differ from UITK panels?").

## Workflow

1. **Check explicit signals** (table below). If one matches, activate that skill immediately.
2. **Otherwise detect from the project** (second table).
3. **Still unclear:** apply the defaults in "Choosing a framework".
4. **Route** the request (understanding, editing, or generation) to the chosen skill. Layer a specialist skill on top when its topic is involved.

### Explicit signals

| User mentions | Route to |
|---|---|
| `.uxml`, `.uss`, `.tss`, UI Toolkit, UITK, UIElements, `UIDocument`, `PanelSettings`, `CreateGUI`, `[UxmlElement]`, data binding, Manipulator | `ui-uitk` |
| Canvas, uGUI, `RectTransform`, Layout Group, `ScrollRect`, `EventSystem`, `.prefab` UI | `ui-ugui` |
| IMGUI, `OnGUI`, `OnInspectorGUI`, `EditorGUILayout`, immediate mode | `ui-imgui` |
| Page/modal/sheet transitions, popup stack, back history, UnityScreenNavigator | `screen-navigator` (uGUI only) |
| TextMeshPro font assets, atlases, SDF, fallback fonts, text performance | `optimize-text-mesh-pro` |
| Languages, translation, i18n/l10n, String/Asset Tables, `com.unity.localization` | `localization` |
| Figma URL or "import from Figma" | Not available — see below |

### Project detection

| Look for | Indicates |
|---|---|
| `.uxml`/`.uss` files, `UIDocument` in scenes | UI Toolkit (runtime) |
| `.uxml` under `Editor/`, scripts with `CreateGUI()` | UI Toolkit (editor) |
| `Canvas` in scenes/prefabs, heavy `RectTransform` use | uGUI |
| Editor scripts with `OnGUI()` / `OnInspectorGUI()` | IMGUI |

## Choosing a framework

- **Existing project:** follow the framework already used for similar UI.
- **New runtime UI, no preference:** default to uGUI (`ui-ugui`). Ask once if the user seems open to options: UI Toolkit is CSS/flexbox-like with data binding; uGUI is GameObject/Canvas-based, mature, and has the widest third-party support (TextMeshPro, UnityScreenNavigator, world-space UI, Animator-driven UI).
- **Mobile or tight perf budgets, world-space UI, heavy animation:** bias toward uGUI.
- **New editor tools (EditorWindow, inspector, PropertyDrawer):** UI Toolkit (`ui-uitk`) by default. Use IMGUI only when maintaining existing IMGUI code or when the user asks for it.
- **IMGUI is never for shipped game UI** — only debug overlays.

### Mixed projects

- Runtime requests (menus, HUDs) → whichever runtime system similar screens use.
- Editor requests → UI Toolkit for new tools; IMGUI only when editing an existing `OnGUI`/`OnInspectorGUI` script.
- New runtime UI with no similar precedent and no stated preference → uGUI.

## Figma import

Automated Figma import requires Unity's Figma integration inside Unity AI Assistant and has no client-side equivalent. Say so, ask for a description or screenshot, and build with the chosen framework skill.

## Rules for every UI system

- **Do only what is asked.** Question → answer without edits. Targeted edit → change only that. Generation → only the requested files.
- **Layout requests do not imply scripts.** "Proper buttons", "working UI", "menu screen", "inventory screen" mean visual layout. Add C# only for "with logic", "functional", "wire up", or an explicit behavior.
- **Search before creating.** Reuse existing stylesheets, prefabs, fonts, icons, and folder conventions.
- **Honor exact values.** Use given hex colors, pixel sizes, and spacing verbatim.
- **Readable by default.** Ensure text contrasts with its background, except intentional low contrast (disabled, placeholder, decorative).
- **Follow project naming first**; fall back to the conventions in the framework skill.
