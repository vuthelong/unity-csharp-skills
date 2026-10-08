# Pointer Manipulators and Drag and Drop

A `Manipulator` packages event handling so it can be attached to any element: `element.AddManipulator(new DragManipulator(...))`. Pointer manipulators derive from `PointerManipulator` and override `RegisterCallbacksOnTarget` / `UnregisterCallbacksFromTarget`.

For simple clicks use `Button.clicked` or `new Clickable(action)`; use a manipulator for drag, long-press, or custom gestures.

## Drag-and-drop manipulator

```csharp
using System;
using UnityEngine;
using UnityEngine.UIElements;

public class DragManipulator : PointerManipulator
{
    private readonly VisualElement m_DragLayer;
    private readonly Func<VisualElement, bool> m_IsDropTarget;
    private readonly Action<VisualElement, VisualElement> m_OnDrop;

    private Vector2 m_PointerStart;
    private VisualElement m_OriginalParent;
    private int m_OriginalIndex;
    private bool m_Dragging;

    public DragManipulator(VisualElement dragLayer, Func<VisualElement, bool> isDropTarget,
        Action<VisualElement, VisualElement> onDrop)
    {
        m_DragLayer = dragLayer;
        m_IsDropTarget = isDropTarget;
        m_OnDrop = onDrop;
        activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
    }

    protected override void RegisterCallbacksOnTarget()
    {
        target.RegisterCallback<PointerDownEvent>(OnPointerDown);
        target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        target.RegisterCallback<PointerUpEvent>(OnPointerUp);
        target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
    }

    protected override void UnregisterCallbacksFromTarget()
    {
        target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
        target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
        target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
        target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (!CanStartManipulation(evt)) return;

        m_OriginalParent = target.parent;
        m_OriginalIndex = m_OriginalParent.IndexOf(target);
        var worldPos = target.worldBound.position;

        m_DragLayer.Add(target);
        target.style.position = Position.Absolute;
        var local = m_DragLayer.WorldToLocal(worldPos);
        target.style.left = local.x;
        target.style.top = local.y;
        target.style.translate = new Translate(0, 0);
        target.pickingMode = PickingMode.Ignore;
        target.usageHints = UsageHints.DynamicTransform;
        target.AddToClassList("dragging");

        m_PointerStart = evt.position;
        m_Dragging = true;
        target.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (!m_Dragging || !target.HasPointerCapture(evt.pointerId)) return;
        var delta = (Vector2)evt.position - m_PointerStart;
        target.style.translate = new Translate(delta.x, delta.y);
        evt.StopPropagation();
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (!m_Dragging || !target.HasPointerCapture(evt.pointerId) || !CanStopManipulation(evt)) return;
        var dropTarget = FindDropTarget(evt.position);
        target.ReleasePointer(evt.pointerId);
        Finish(dropTarget);
        evt.StopPropagation();
    }

    private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
    {
        if (m_Dragging) Finish(null);
    }

    private VisualElement FindDropTarget(Vector2 position)
    {
        var picked = target.panel.Pick(position);
        while (picked != null && !m_IsDropTarget(picked))
            picked = picked.parent;
        return picked;
    }

    private void Finish(VisualElement dropTarget)
    {
        m_Dragging = false;
        target.RemoveFromClassList("dragging");
        target.pickingMode = PickingMode.Position;
        target.style.position = StyleKeyword.Null;
        target.style.left = StyleKeyword.Null;
        target.style.top = StyleKeyword.Null;
        target.style.translate = StyleKeyword.Null;

        if (dropTarget != null)
        {
            dropTarget.Add(target);
            m_OnDrop?.Invoke(target, dropTarget);
        }
        else
        {
            m_OriginalParent.Insert(m_OriginalIndex, target);
        }
    }
}
```

Usage:

```csharp
var dragLayer = root.Q("dragLayer");
foreach (var item in root.Query(className: "inventory-item").ToList())
    item.AddManipulator(new DragManipulator(dragLayer,
        e => e.ClassListContains("inventory-slot"),
        (dragged, slot) => m_Inventory.Move(dragged.userData, slot.userData)));
```

`dragLayer` is a full-screen element last in the root (so it draws on top) with `picking-mode="Ignore"` and `position: absolute` covering the panel.

## Rules

- **Capture the pointer** on down and release on up; handle `PointerCaptureOutEvent` so a lost capture (window blur, another capture) does not leave the item stuck.
- **Move with `style.translate`**, not `left`/`top` each frame — translate skips layout. Set `usageHints = UsageHints.DynamicTransform` on the dragged element.
- **Reparent to a top layer** while dragging so the item draws above siblings and is not clipped by `overflow: hidden` slots. `BringToFront()` alone only reorders within the current parent.
- **`pickingMode = Ignore` while dragging** so `panel.Pick` finds the slot under the pointer, then restore it.
- **`evt.StopPropagation()`** so parents (ScrollView, Buttons) do not react to the drag.
- **Keep data separate from visuals.** Store the item id in `userData` or a dictionary; the drop callback updates the model, and the view follows (or rebinds).
- **Validate before dropping** (slot type, occupancy) and return to the origin on invalid drops.
- Visual feedback through classes: `.dragging`, `.drop-zone-active` toggled on candidate slots during `PointerMove`.

## Inventory and crafting

Ask first:
- Should items drag between slots, or is it a static grid?
- Do slots accept only certain types (equipment slots)?
- Do items stack, split, or swap on drop onto an occupied slot?

Static layout only if drag-and-drop is not wanted. When it is, the checklist: draggable manipulator, slot elements as drop targets, USS states for dragging and active drop zones, a data model independent of the visual tree, drop validation, and swap/return behavior.

## Editor drag and drop

Dragging assets or objects from the Project/Hierarchy into editor UI uses `DragAndDrop` with `DragUpdatedEvent` / `DragPerformEvent` (editor only), not pointer manipulators.
