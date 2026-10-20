# Choosing an AI architecture

Read when choosing FSM vs Unity Behavior vs utility AI, installing `com.unity.behavior`, writing a custom action node, or sketching a utility scorer.

## Decision guide

| Question | Answer → choice |
|---|---|
| Fewer than about 8 states, transitions easy to list in a table? | FSM |
| Designers (not programmers) must author or tweak behavior? | Unity Behavior graph |
| Bosses with phases and scripted sequences (taunt, summon, enrage)? | Behavior graph, or an FSM per phase |
| Creatures choosing among many needs (eat, flee, hunt, sleep, regroup) by context? | Utility AI picking a goal, FSM or graph executing it |
| Dozens of archetypes sharing pieces (patrol, flee at low health)? | Behavior graph subgraphs, or composition of small FSM states |

Hybrids are normal: utility or a graph decides *what* to do, a small FSM or the shared helpers (`Perception`, `ThreatTable`, NavMeshAgent driving) do *how*. Keep perception and threat outside the decision layer so every architecture reuses them.

## FSM (default)

`EnemyBrain` is an enum + `switch` FSM: no allocations, one place to read every transition, easy to gizmo (`State`). Grow it by:

- Adding a state: enum value, `Tick*` method, entry code in a `Begin*` method, transitions out in the states that lead to it.
- Moving to a class-per-state FSM (`IEnemyState` with `Enter/Tick/Exit`, instances created once in `Awake`) when one state's logic exceeds about 50 lines or archetypes need different variants of one state.
- Hierarchical states (Combat → Chase/Attack/Strafe) only when several states share the same exits (leash, death, lost target).

Keep transitions on the decision tick and continuous work (facing, attack phase timers) in `Update`.

## Unity Behavior (`com.unity.behavior`)

Facts from the package docs: package name `com.unity.behavior`, display name Behavior, listed as released for Unity 6000.0 with version 1.0.16 at time of writing (`https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.behavior.html`). It authors behavior graphs (Unity's term for behavior trees that can also merge branches), with a Blackboard, prebuilt movement and decision nodes, subgraphs, events and Play mode debugging. The changelog notes a 6000.3 runtime serialization issue in 1.0.14 and EntityId migration for 6000.5+ in 1.0.16; read the changelog for the installed version.

Install: Package Manager → Add package by name → `com.unity.behavior` (see `unity-package-management`).

Runtime API (namespace `Unity.Behavior`, verified against the 1.0 API reference):

| Member | Use |
|---|---|
| `BehaviorGraphAgent` (MonoBehaviour) | Runs a graph on a GameObject; `Graph` property, `Init()`, `Start()`, `Restart()`, `End()` |
| `SetVariableValue<T>(string name, T value)` | Push perception results (current target, last-known position) into the Blackboard; returns `bool` |
| `GetVariable<T>(string name, out BlackboardVariable<T> variable)` | Read Blackboard values; returns false in edit mode |
| `Action` (abstract, derives from `Node`) | Base class for custom action nodes |
| `Node.OnStart()` / `OnUpdate()` return `Node.Status`; `OnEnd()` | Node lifecycle |
| `Node.Status` | `Uninitialized`, `Running`, `Success`, `Failure`, `Waiting`, `Interrupted` |
| `NodeDescriptionAttribute(name, description, story, icon, category, id, hideInSearch, filePath)` | Node metadata in the graph editor |

Create custom nodes from the graph editor's node creation wizard, which generates the boilerplate; compare anything you write by hand with that generated file. A custom action has roughly this shape (unverified against a compiler; check the attributes and `partial`/property-bag requirements against the generated template):

```csharp
using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, Unity.Properties.GeneratePropertyBag]
[NodeDescription(name: "Strike Target", story: "[Agent] strikes [Target]", category: "Action/Combat", id: "replace-with-generated-id")]
public partial class StrikeTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    protected override Status OnStart()
    {
        if (Agent.Value == null || Target.Value == null) return Status.Failure;
        return Status.Success;
    }
}
```

Behavior graphs with this skill's helpers:

- Keep `Perception` and `ThreatTable` as components; a small bridge MonoBehaviour ticks them on the staggered schedule, listens to `ThreatTable.TargetChanged` and calls `agent.SetVariableValue("Target", target != null ? target.gameObject : null)` only when the target changes. Never use `?.` on a `UnityEngine.Object`; it skips Unity's destroyed-object check.
- Co-op: the `BehaviorGraphAgent` is a MonoBehaviour, so gate it exactly like `EnemyBrain` (enabled on the server only, see `coop-networking.md`). Older changelog entries mention Netcode for GameObjects interactions; check the changelog of your installed version before relying on any built-in netcode integration.
- Profile it: the agent ticks its graph from its own `Update`. If dozens of agents show up in the Profiler, lower the count of always-running branches and use event-driven nodes, or fall back to the FSM for crowds and keep graphs for bosses and NPCs.

## Utility AI sketch

Score each option 0..1 from considerations, multiply, pick the best with a small bonus for the current option (hysteresis), re-evaluate on the decision tick.

```csharp
using UnityEngine;

public interface IUtilityOption
{
    float Score(in UtilityContext context);
    void Execute(in UtilityContext context);
}

public readonly struct UtilityContext
{
    public UtilityContext(float healthFraction, float hunger, float threatDistance, bool hasTarget)
    {
        this.HealthFraction = healthFraction;
        this.Hunger = hunger;
        this.ThreatDistance = threatDistance;
        this.HasTarget = hasTarget;
    }

    public float HealthFraction { get; }
    public float Hunger { get; }
    public float ThreatDistance { get; }
    public bool HasTarget { get; }
}

public sealed class UtilitySelector
{
    private readonly IUtilityOption[] _options;
    private readonly float _stickiness;
    private int _current = -1;

    public UtilitySelector(IUtilityOption[] options, float stickiness = 0.1f)
    {
        this._options = options;
        this._stickiness = stickiness;
    }

    public void Tick(in UtilityContext context)
    {
        var best = -1;
        var bestScore = 0f;
        for (var i = 0; i < this._options.Length; i++)
        {
            var score = this._options[i].Score(context);
            if (i == this._current) score += this._stickiness;
            if (score <= bestScore) continue;

            bestScore = score;
            best = i;
        }

        this._current = best;
        if (best >= 0) this._options[best].Execute(context);
    }
}

public sealed class FleeOption : IUtilityOption
{
    private readonly AnimationCurve _healthCurve;

    public FleeOption(AnimationCurve healthCurve) => this._healthCurve = healthCurve;

    public float Score(in UtilityContext context)
    {
        if (!context.HasTarget) return 0f;

        var lowHealth = this._healthCurve.Evaluate(1f - context.HealthFraction);
        var closeThreat = Mathf.Clamp01(1f - context.ThreatDistance / 15f);
        return lowHealth * closeThreat;
    }

    public void Execute(in UtilityContext context)
    {
    }
}
```

Tune curves in the Inspector (`AnimationCurve` per consideration) and log each option's score on a debug key; utility AI is only debuggable with visible scores.
