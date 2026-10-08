# Runtime Data Binding (Unity 6+)

## Contents

- [When to bind](#when-to-bind)
- [Data source](#data-source)
- [Binding from C#](#binding-from-c)
- [Binding in UXML](#binding-in-uxml)
- [Binding modes and update triggers](#binding-modes-and-update-triggers)
- [PanelRenderer (6000.6+)](#panelrenderer-60006)
- [Pitfalls](#pitfalls)

## When to bind

Bind when game state drives UI (health, score, settings), several elements show the same value, or two-way input edits data. Skip binding for one-off assignments or values you already push every frame — set the property directly.

## Data source

Properties exposed to binding need `[CreateProperty]` (namespace `Unity.Properties`):

```csharp
using Unity.Properties;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Player Data")]
public class PlayerData : ScriptableObject
{
    [SerializeField, DontCreateProperty] private int m_Health = 100;
    [SerializeField, DontCreateProperty] private int m_MaxHealth = 100;

    [CreateProperty]
    public int Health
    {
        get => m_Health;
        set => m_Health = Mathf.Clamp(value, 0, m_MaxHealth);
    }

    [CreateProperty]
    public float HealthPercent => m_MaxHealth > 0 ? (float)m_Health / m_MaxHealth * 100f : 0f;

    [CreateProperty]
    public string HealthText => $"{m_Health} / {m_MaxHealth}";
}
```

`[SerializeField]` fields are already visible to the property system; mark them `[DontCreateProperty]` so only the public property is bound. Data sources can be any class or struct, not only ScriptableObjects.

## Binding from C#

Always build paths with `nameof` so renames and typos fail at compile time:

```csharp
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class HealthHud : MonoBehaviour
{
    [SerializeField] private PlayerData m_Player;

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        root.dataSource = m_Player;

        root.Q<Label>("healthLabel").SetBinding(nameof(Label.text), new DataBinding
        {
            dataSourcePath = new PropertyPath(nameof(PlayerData.HealthText)),
            bindingMode = BindingMode.ToTarget
        });

        root.Q<ProgressBar>("healthBar").SetBinding(nameof(ProgressBar.value), new DataBinding
        {
            dataSourcePath = new PropertyPath(nameof(PlayerData.HealthPercent)),
            bindingMode = BindingMode.ToTarget
        });
    }
}
```

- `dataSource` is inherited: set it on the root and every descendant binding resolves against it, unless a child sets its own `dataSource` or `dataSourcePath`.
- Nested paths: `new PropertyPath($"{nameof(Game.Player)}.{nameof(PlayerData.Health)}")`; list items: `"Items[2].Name"`.
- Runtime-created elements: call `SetBinding` and add them under an element that has the data source.
- `UIDocument` rebuilds its tree when re-enabled, discarding bindings — set them up in `OnEnable`. Clear explicitly with `element.ClearBinding(property)` only if you keep the tree and swap data.

## Binding in UXML

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
  <ui:VisualElement name="root" data-source="project://database/Assets/Data/Settings.asset?fileID=11400000&amp;guid=GUID&amp;type=2#Settings">
    <ui:Slider name="volumeSlider" low-value="0" high-value="1">
      <Bindings>
        <ui:DataBinding property="value" data-source-path="MasterVolume" binding-mode="TwoWay" />
      </Bindings>
    </ui:Slider>
  </ui:VisualElement>
</ui:UXML>
```

A `data-source` asset reference lets UI Builder preview bound values; UI Builder writes the exact URL (GUID included) when you assign the source in its Inspector — prefer that over hand-typing GUIDs. Alternatively set `root.dataSource` from C# and keep only `data-source-path` in UXML, or use `data-source-type` to get path completion in UI Builder without an asset.

## Binding modes and update triggers

| Mode | Direction | Use |
|---|---|---|
| `TwoWay` (default for `DataBinding`) | Data ↔ UI | Input fields, sliders, toggles |
| `ToTarget` | Data → UI | Labels, bars — set this explicitly for display-only bindings |
| `ToSource` | UI → Data | Rare write-only cases |
| `ToTargetOnce` | Data → UI once | Static values |

`updateTrigger`: `OnSourceChanged` (default) or `EveryUpdate`, `WhenDirty`. Change detection: implement `IDataSourceViewHashProvider` (return a version counter) and/or `INotifyBindablePropertyChanged` on the source so the system skips unchanged sources instead of comparing every frame. For high-frequency data with many bindings, this is the main performance lever.

Type conversion: register converters with `ConverterGroups` or add per-binding converters (`binding.sourceToUiConverters.AddConverter((ref int v) => $"{v} HP")`).

## PanelRenderer (6000.6+)

Unity 6000.6 adds `PanelRenderer`, a runtime component alternative to `UIDocument` with `RegisterUIReloadCallback`, which re-runs your setup whenever the UI is (re)built, keeping bindings alive across live reload:

```csharp
private void OnEnable()
{
    GetComponent<PanelRenderer>().RegisterUIReloadCallback(OnUIReload);
}

private void OnUIReload(PanelRenderer renderer, VisualElement root)
{
    root.dataSource = m_Stats;
    root.Q<Label>("hpLabel").SetBinding(nameof(Label.text), new DataBinding
    {
        dataSourcePath = new PropertyPath(nameof(Stats.HP)),
        bindingMode = BindingMode.ToTarget
    });
}
```

Use it only when the project targets 6000.6+; otherwise use `UIDocument` with setup in `OnEnable`. If you mutate a ScriptableObject data source at runtime in the Editor, bind to a copy (`Instantiate(asset)`) to avoid persisting play-mode changes into the asset.

## Pitfalls

- Missing `[CreateProperty]` → binding silently resolves nothing; the Console logs a binding warning only in some cases.
- Binding the target property by a wrong name (`"Text"` vs `"text"`) → use `nameof(Label.text)`.
- Binding a `Label.text` to an `int` with no converter → nothing shown or a conversion warning; expose a string property or add a converter.
- Forgetting `ToTarget` on display-only bindings leaves the default `TwoWay`, which also tries to write UI changes back and can log errors when the source property is get-only.
