# Localizing an Existing Project

## Contents

- [1. Find every string](#1-find-every-string)
- [2. Build the tables](#2-build-the-tables)
- [3. Bind components](#3-bind-components)
- [4. Translate with context](#4-translate-with-context)
- [5. QA](#5-qa)

## 1. Find every string

Text hides in two places; scanning one misses the other.

**Authored text on components** (scenes and prefabs): walk **both** legacy `UnityEngine.UI.Text` and `TMP_Text` (base of `TextMeshProUGUI` and `TextMeshPro`). On one real project `FindObjectsByType<Text>` found 1 component while `FindObjectsByType<TMP_Text>` found 13. Include inactive objects (`FindObjectsInactive.Include`) and prefabs not placed in any scene.

**Text composed in code** never exists on a component at edit time:

```bash
grep -rnE '\.text\s*(=|\+=)\s*\$?"|SetText\(\s*\$?"' --include='*.cs' Assets/
```

This catches plain, interpolated, concatenated, and `+=` assignments plus `SetText` with literals, and skips `label.text = someVariable` and already-routed calls. It misses literals stored in variables or consts elsewhere; if the count looks low, grep those files' string literals too.

**Report what you did not convert.** Composed strings often need a Smart String or format arguments — a judgment call — and some are not worth localizing (debug text). List every found site with "converted" or the reason it was not.

## 2. Build the tables

- One shared String Table Collection for UI (e.g. `UIStrings`) with the base language filled first.
- Key naming: `screen.element` (`menu.play`, `settings.audio.title`).
- Add translator context per key (where it appears, max length, tone).
- Map existing UI text to keys **longest string first**, so `"NO ITEMS FOUND"` binds to its own key rather than a shorter `"NO"`. Use case-insensitive matching where appropriate.

## 3. Bind components

Interactive per-label wiring is in `editor-wiring.md`. For whole-project passes use [../scripts/L10nBatchProcessor.cs](../scripts/L10nBatchProcessor.cs) (place it in an `Editor` folder):

```csharp
var unmatched = L10nBatchProcessor.LocalizeAll(mapping, "UIStrings");
foreach (var line in unmatched) UnityEngine.Debug.Log(line);
```

`mapping` is source text → key.

**Ask the user before running it:**

> "This will open every scene under Assets/, attach `LocalizeStringEvent` components, and save all modified scenes. It cannot be undone from here; use version control to revert. Proceed?"

The script enforces its own safeguards — keep them when adapting it: it refuses to run in batch mode, offers to save unsaved scenes first, and shows a blocking confirmation dialog listing the scenes. Tell the user to watch the Editor and confirm. If they cancel it throws `OperationCanceledException` and writes nothing; report that as a cancellation, not an error to work around.

It walks both `Text` and `TMP_Text`, sets listeners to `EditorAndRuntime`, restores the user's scene setup afterwards, and **returns labels it could not match** as `scene :: object :: "text"`. Print that list; pair it with the code scan from step 1 and the completeness check. It processes scenes only; run a similar pass over UI prefabs if they hold text.

On newer 6000.x minors, if `FindObjectsSortMode` overloads report as obsolete, switch to the `FindObjectsByType<T>(FindObjectsInactive)` overload.

Attach the font swap component ([../scripts/LocalizedFontAsset.cs](../scripts/LocalizedFontAsset.cs)) in the same pass when CJK locales are targeted (see `cjk-fonts.md`).

## 4. Translate with context

- Translate from the table with each key's context and the actual UI in view; space and meaning depend on where the string sits.
- Match the game's tone. Buttons use imperative verbs (German "Spielen", not "Spielend"); labels need correct plurals — use Smart String plural formatters rather than separate keys per count.
- Set up Smart Strings for any string with variables, after reading the scripts that fill them.

## 5. QA

- Switch locales with **Window > Asset Management > Localization Scene Controls**, or `LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("de");` in Play mode.
- Inspect every screen and prefab in the base and every target language: overflow, truncation, tofu, wrong font.
- Fix overflow with wrapping, `LayoutElement` sizing, or shorter translations — not by shrinking font sizes globally or enabling TMP AutoSize on changing text.
- Re-run the completeness check and rebuild Addressables content if Asset Tables changed.
