# Wiring and Verifying LocalizeStringEvent from Code

## Contents

- [Persistent listener](#persistent-listener)
- [Verify the binding](#verify-the-binding)
- [Table completeness check](#table-completeness-check)

## Persistent listener

`LocalizeStringEvent.OnUpdateString` needs a **persistent** listener pointing at the text component's public `text` setter. Lambdas are not persistent and fail.

```csharp
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

if (!label.TryGetComponent<LocalizeStringEvent>(out var lse))
    lse = label.gameObject.AddComponent<LocalizeStringEvent>();
lse.StringReference = new LocalizedString("UIStrings", "menu.play");

var setText = (UnityAction<string>)System.Delegate.CreateDelegate(
    typeof(UnityAction<string>), label, "set_text");

for (var i = lse.OnUpdateString.GetPersistentEventCount() - 1; i >= 0; i--)
    UnityEventTools.RemovePersistentListener(lse.OnUpdateString, i);

UnityEventTools.AddPersistentListener(lse.OnUpdateString, setText);
var index = lse.OnUpdateString.GetPersistentEventCount() - 1;
lse.OnUpdateString.SetPersistentListenerState(index, UnityEventCallState.EditorAndRuntime);

EditorUtility.SetDirty(lse);
lse.RefreshString();
```

- `label` is a `TMP_Text` or `UnityEngine.UI.Text`; both expose a public `set_text`. Building the delegate by name is reflection over a public member, which is fine.
- Use `TryGetComponent`, not `GetComponent(...) ?? AddComponent(...)`: `??` bypasses Unity's null check, so a destroyed or Editor "fake null" component is treated as present.
- **Clear existing persistent listeners first** so repeated runs do not stack duplicates.
- **Set the call state to `EditorAndRuntime`.** `AddPersistentListener` leaves it `RuntimeOnly`: correct in builds, but switching locale in the Editor does nothing, even after save and reload (verified on 6000.5.8f1).
- Do not write `m_PersistentCalls`, `m_MethodName`, or `m_Mode` through `SerializedObject`; they are private serialized names. The public API produces the same serialized call (target = component, method = `set_text`, mode = EventDefined).
- In a prefab or scene, save it afterwards (`PrefabUtility.SavePrefabAsset` / `EditorSceneManager.SaveScene`).

## Verify the binding

A component that looks wired but never fires reads as done and is worse than an unlocalized label. Check all four with public API:

| Check | Call | Expect |
|---|---|---|
| Something wired | `GetPersistentEventCount()` | `> 0` |
| Correct target | `GetPersistentTarget(i)`, `GetPersistentMethodName(i)` | the text component, `set_text` |
| Fires while authoring | `GetPersistentListenerState(i)` | `EditorAndRuntime` |
| Actually updates | `lse.RefreshString()`, then read `label.text` | the localized value |

## Table completeness check

Run before declaring the work done and report its output. A key with a missing or empty value makes the package print "No translation found…" **into the game UI**.

```csharp
var gaps = new System.Collections.Generic.List<string>();
var checkedCount = 0;

foreach (var col in UnityEditor.Localization.LocalizationEditorSettings.GetStringTableCollections())
{
    foreach (var key in col.SharedData.Entries)
    {
        foreach (var table in col.StringTables)
        {
            checkedCount++;
            var entry = table.GetEntry(key.Id);
            if (entry == null || string.IsNullOrWhiteSpace(entry.Value))
            {
                gaps.Add($"{col.TableCollectionName} / {table.LocaleIdentifier.Code} / {key.Key}");
            }
        }
    }
}

if (checkedCount == 0)
{
    return "INCONCLUSIVE: no table entries found. Either no String Table Collection exists yet, "
         + "or the collection has no locale tables. Fix that before trusting this check.";
}

return gaps.Count == 0
    ? $"COMPLETE: {checkedCount} entries checked, no gaps"
    : $"GAPS ({gaps.Count} of {checkedCount} checked):\n  " + string.Join("\n  ", gaps);
```

Zero entries checked is not a pass; it means nothing was examined. Verified on 6000.5.8f1: one emptied `ja` value reports `GAPS 1 of 4`, filling it reports `COMPLETE`, and a project with no tables reports `INCONCLUSIVE`.

**Report gaps; do not fill them silently.** Some are decisions (a locale not yet requested, a brand name identical in every language). Copying English into them hides the decision — list them and let the user choose.
