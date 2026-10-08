---
name: localization
description: Sets up and drives Unity Localization (`com.unity.localization`) — LocalizationSettings and Locales, String Table and Asset Table collections, Smart Strings, `LocalizeStringEvent` wiring that updates in Edit mode, runtime locale switching via `LocalizationSettings.SelectedLocale`, per-locale TMP font swapping for CJK through Asset Tables and Addressables, table completeness checks, and batch localization of existing scenes. Use when adding languages, translating UI text, building a language menu, fixing tofu (missing CJK glyphs) or "No translation found" text, extracting hard-coded strings, or mentions of i18n, l10n, multilingual, StringTable, LocalizedString, or Locale.
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: ui
  sources: "Unity-Technologies/skills/skills/localization"
  unity: "6000.0+"
---

# Unity Localization

Localize UI text and assets with the Localization package. TMP font quality and fallback chains are covered by `optimize-text-mesh-pro`; Canvas layout by `ui-ugui`.

## References

| File | Read when |
|---|---|
| [references/setup-and-tables.md](references/setup-and-tables.md) | Installing the package, creating settings/locales/collections, editing tables from code, Addressables, namespace conflicts |
| [references/cjk-fonts.md](references/cjk-fonts.md) | Supporting Chinese, Japanese, or Korean; creating TMP font assets from code; tofu |
| [references/editor-wiring.md](references/editor-wiring.md) | Attaching `LocalizeStringEvent` from code, verifying bindings, running the table completeness check |
| [references/translation-workflow.md](references/translation-workflow.md) | Localizing an existing project: finding all strings, batch processing scenes, translation QA |

Scripts (copy into the project, inside an `Editor` folder for `L10nBatchProcessor`):
- [scripts/L10nBatchProcessor.cs](scripts/L10nBatchProcessor.cs) — wires `LocalizeStringEvent` on every `Text`/`TMP_Text` in every scene, with confirmation safeguards; returns unmatched labels.
- [scripts/LocalizedFontAsset.cs](scripts/LocalizedFontAsset.cs) — runtime component that swaps a `TMP_Text` font per locale from an Asset Table.

## Workflow

1. **Package present?** Look for `com.unity.localization` in `Packages/packages-lock.json`; install if missing (see setup reference — installation is asynchronous and reloads the domain).
2. **Settings and locales.** Ensure an active `LocalizationSettings` asset and the requested `Locale`s.
3. **Tables.** Create or reuse String Table Collections (e.g. `UIStrings`) and, for fonts or images per locale, Asset Table Collections. Fill every key in every locale.
4. **Bind UI.** `LocalizeStringEvent` per text component (or `LocalizedString` in code), with Edit-mode-capable listeners.
5. **Fonts.** For CJK, create per-locale TMP font assets and swap them via an Asset Table; confirm no tofu.
6. **Verify.** Run the completeness check, switch locales in the Editor, and inspect for overflow and tofu. Report gaps instead of filling them silently.

## Locale switching

- **Runtime:** set `LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("de");`. Everything bound through `LocalizeStringEvent`/`LocalizedString` updates. Wait for `LocalizationSettings.InitializationOperation` before reading locales at startup.
- **Startup locale:** configure Startup Locale Selectors on the settings asset (command line, `PlayerPrefs`, system language, `SpecificLocaleSelector`). Persist the player's choice with the PlayerPrefs selector.
- **Authoring preview:** **Window > Asset Management > Localization Scene Controls** (Editor only).
- **Never hand-roll locale state.** A language menu is expected, but it must set `SelectedLocale`; a dropdown that keeps its own "current language" or swaps strings itself diverges from everything else.

## Binding text

- Add `UnityEngine.Localization.Components.LocalizeStringEvent`, set `StringReference` (table + entry), and route `OnUpdateString` to the text component's `text` setter (`TMP_Text` or legacy `UnityEngine.UI.Text`). The Inspector's "Localize" context menu does this for you; from code see `references/editor-wiring.md`.
- Do not reflect into the internal `LocalizeComponent_TMPro` / UGUI helpers in `UnityEditor.Localization.Plugins` — internal API with no stability guarantee.
- In scripts: `[SerializeField] LocalizedString m_Title;` then `m_Title.StringChanged += s => label.text = s;` (unsubscribe in `OnDisable`), or `await m_Title.GetLocalizedStringAsync().Task`. For legacy code, wrap `LocalizationSettings.StringDatabase.GetLocalizedString(table, key)` in a small static helper rather than rewriting callers.
- Dynamic values: use **Smart Strings** (`{score}`, plurals `{count:plural:{} item|{} items}`) with arguments or `LocalizedString` local variables instead of concatenating translated fragments.
- After changing text that drives layout, `UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(parent)` if you read sizes the same frame.

## Layout for variable-length text (uGUI)

- Parent `VerticalLayoutGroup`: Control Child Size Height on, Child Force Expand Height off.
- TMP labels: Text Wrapping enabled, Overflow `Overflow` or `Ellipsis`; no `ContentSizeFitter` on children of a layout group (use `LayoutElement`).
- German and Finnish run ~30% longer than English; CJK is shorter but taller. Test the longest locale.

## Rules

- **Scope `AssetDatabase.FindAssets` to `new[] { "Assets" }`.** Unscoped searches return read-only package assets.
- **Match table data by `Locale.Identifier.Code`**, never by the index order of `GetLocales()`.
- **After editing tables from code:** `EditorUtility.SetDirty` on each table, the collection, and its `SharedData`; `LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection)`; then `AssetDatabase.SaveAssets()`.
- **Asset Table entries must be Addressable** and must not live under `Resources/`.
- **Ask before batch operations** that open and save every scene, and report what was not converted.
- **Fully qualify `UnityEngine.UI` types** in generated code (`UnityEngine.UI.Image`) to avoid CS0104 ambiguity with `UnityEngine.UIElements` and project types.

## Definition of done

- Completeness check reports `COMPLETE`, or the gap list was reported to the user.
- Every found text site (components and code-composed strings) is either bound or listed as intentionally skipped.
- Each target locale previewed in the Editor: no tofu, no "No translation found" text, no overflow.
- Addressables content rebuilt (`AddressableAssetSettings.BuildPlayerContent()`) if Asset Tables changed.
