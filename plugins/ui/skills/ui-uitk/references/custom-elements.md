# Custom VisualElements (`[UxmlElement]`)

Unity 6 declares custom controls with `[UxmlElement]` and `[UxmlAttribute]`. The old `UxmlFactory<T>` / `UxmlTraits` pattern is obsolete; convert it when touching such code.

## Contents

- [Basic pattern](#basic-pattern)
- [Requirements](#requirements)
- [UXML namespace](#uxml-namespace)
- [Attribute types](#attribute-types)
- [Advanced attributes](#advanced-attributes)
- [Best practices](#best-practices)

## Basic pattern

```csharp
using UnityEngine.UIElements;

namespace Game.UI.Controls
{
    [UxmlElement]
    public partial class StatBar : VisualElement
    {
        public static readonly string ussClassName = "stat-bar";
        public static readonly string fillUssClassName = ussClassName + "__fill";
        public static readonly string labelUssClassName = ussClassName + "__label";

        private readonly VisualElement m_Fill;
        private readonly Label m_Label;
        private float m_Value = 1f;

        [UxmlAttribute]
        public string title
        {
            get => m_Label.text;
            set => m_Label.text = value;
        }

        [UxmlAttribute, UnityEngine.Range(0f, 1f)]
        public float value
        {
            get => m_Value;
            set
            {
                m_Value = UnityEngine.Mathf.Clamp01(value);
                m_Fill.style.width = Length.Percent(m_Value * 100f);
            }
        }

        public StatBar()
        {
            AddToClassList(ussClassName);
            m_Fill = new VisualElement();
            m_Fill.AddToClassList(fillUssClassName);
            m_Label = new Label();
            m_Label.AddToClassList(labelUssClassName);
            Add(m_Fill);
            Add(m_Label);
        }
    }
}
```

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:controls="Game.UI.Controls">
  <ui:Style src="StatBar.uss" />
  <controls:StatBar title="HP" value="0.75" />
</ui:UXML>
```

Children are built in the constructor; attribute setters update them. Static parts are styled from USS by class; the fill width is a computed runtime value, which is the legitimate case for `style.*`.

## Requirements

1. `[UxmlElement]` on the class, and the class is `partial` (a source generator adds the serialization code).
2. Inherits `VisualElement` or a subclass, with a public parameterless constructor.
3. `[UxmlAttribute]` on properties (or fields) to expose them. Names convert camelCase → kebab-case: `maxHealth` → `max-health`.
4. Custom element names default to the class name; `[UxmlElement("HealthBar")]` overrides it.

## UXML namespace

`xmlns:prefix="Exact.CSharp.Namespace"` — namespace only, case-sensitive.

| Declaration | Valid |
|---|---|
| `xmlns:controls="Game.UI.Controls"` | yes |
| `xmlns:controls="Game.UI.Controls, Assembly-CSharp"` | no — assembly name |
| `xmlns:controls="game.ui.controls"` | no — wrong case |
| `xmlns:controls="Game.UI.Controls.StatBar"` | no — includes class name |

Elements in the global namespace are written without a prefix: `<StatBar />`.

## Attribute types

Supported directly: `string`, `int`, `long`, `float`, `double`, `bool`, `Color`, enums, `Vector2/3/4`, `Rect`, `Bounds`, `Hash128`, `Type` (with `[UxmlTypeReference]`), arrays/`List<T>` of these, and `UnityEngine.Object` references (`Texture2D`, `Sprite`, `VisualTreeAsset`, `ScriptableObject`, …).

Images: static images belong in USS (`background-image: url("project://database/Assets/UI/Icons/fire.png")`); per-instance images use an `[UxmlAttribute] public Sprite portrait { get; set; }`.

## Advanced attributes

Rename: `[UxmlAttribute("hp")] public float health { get; set; }`.

Unsupported types need a converter:

```csharp
public class HealthDataConverter : UxmlAttributeConverter<HealthData>
{
    public override HealthData FromString(string value)
    {
        var parts = value.Split(',');
        return new HealthData
        {
            current = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
            max = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    public override string ToString(HealthData value) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1}", value.current, value.max);
}
```

Converters are discovered automatically by type; `HealthData` must be `[Serializable]`. Parse with the invariant culture so locales using `,` as the decimal separator do not break UXML.

UI Builder inspector polish: `[Tooltip]`, `[Range]`, `[Header]`, `[TextArea]`, and `[Delayed]` work on `[UxmlAttribute]` members. A custom `PropertyAttribute` with a `CreatePropertyGUI` drawer (see `editor-ui.md`) customizes the field further, including overriding a base property:

```csharp
[UxmlElement]
public partial class SliderIntField : IntegerField
{
    [UxmlAttribute("value"), SliderDrawer]
    internal int sliderValue
    {
        get => value;
        set => this.value = value;
    }
}
```

## Best practices

- Expose USS class name constants (`ussClassName`, BEM-style child classes) so users can restyle.
- Provide defaults on every attribute.
- Update visuals in setters; call `MarkDirtyRepaint()` for Painter2D content.
- Do not do work in `GeometryChangedEvent` that changes layout — it re-triggers itself.
- Keep inline `style.*` for values computed at runtime only; everything else in USS.
