# Particle System scripting reference

## Contents

- [Collision callbacks](#collision-callbacks)
- [Trigger callbacks](#trigger-callbacks)
- [Sub-emitters](#sub-emitters)
- [Reading and writing particles](#reading-and-writing-particles)
- [Particle jobs](#particle-jobs)
- [Editor automation](#editor-automation)

## Collision callbacks

1. Collision module: enabled, Type `World`, *Send Collision Messages* on, *Collides With* restricted to the layers that matter.
2. `OnParticleCollision(GameObject other)` is called on **both** the particle system's GameObject (with the hit object) and on the collider's GameObject (with the particle system's GameObject).

```csharp
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public sealed class ParticleDamage : MonoBehaviour
{
    [SerializeField] float _damagePerHit = 2f;

    ParticleSystem _ps;
    readonly List<ParticleCollisionEvent> _events = new(16);

    void Awake() => _ps = GetComponent<ParticleSystem>();

    void OnParticleCollision(GameObject other)
    {
        if (!other.TryGetComponent(out IDamageable target)) return;
        int count = _ps.GetCollisionEvents(other, _events);
        for (int i = 0; i < count; i++)
            target.TakeDamage(_damagePerHit, _events[i].intersection, _events[i].normal);
    }
}
```

Reuse the list; `GetCollisionEvents` fills it without allocating once capacity is large enough.

## Trigger callbacks

1. Triggers module: enabled, add colliders to the list (or set *Collider Query Mode* to `One` / `All` to test against any collider), choose Inside/Outside/Enter/Exit actions (`Ignore`, `Kill`, `Callback`).
2. Implement `OnParticleTrigger()` on the particle system's GameObject.

```csharp
readonly List<ParticleSystem.Particle> _enter = new();

void OnParticleTrigger()
{
    int n = _ps.GetTriggerParticles(ParticleSystemTriggerEventType.Enter, _enter);
    for (int i = 0; i < n; i++)
    {
        var p = _enter[i];
        p.startColor = Color.red;
        _enter[i] = p;
    }
    _ps.SetTriggerParticles(ParticleSystemTriggerEventType.Enter, _enter);
}
```

`Particle` is a struct; modify a copy and write it back, then call `SetTriggerParticles`.

## Sub-emitters

- Sub Emitters module: add a child `ParticleSystem` with trigger `Birth`, `Collision`, `Death`, `Trigger`, or `Manual`, and choose which properties to inherit (color, size, rotation, lifetime, duration).
- The child system must be a child GameObject and is controlled by the parent; do not enable its own emission rate unless intended.
- `Manual` sub-emitters are fired from code with `ps.TriggerSubEmitter(index, ref particle)` or from a job.
- Sub-emitter particles count against the child's Max Particles.

## Reading and writing particles

```csharp
ParticleSystem.Particle[] _buffer;

void LateUpdate()
{
    int max = _ps.main.maxParticles;
    if (_buffer == null || _buffer.Length < max) _buffer = new ParticleSystem.Particle[max];

    int count = _ps.GetParticles(_buffer);
    for (int i = 0; i < count; i++)
        _buffer[i].velocity += (_attractor.position - _buffer[i].position) * (Time.deltaTime * 2f);
    _ps.SetParticles(_buffer, count);
}
```

- Do this in `LateUpdate` so you act on this frame's simulation.
- Allocate the array once. `GetParticles(NativeArray<Particle>)` overloads avoid managed arrays entirely.
- Writing particles every frame makes the system non-procedural.
- `remainingLifetime <= 0` kills a particle on the next update.
- `GetCustomParticleData` / `SetCustomParticleData` read and write the Custom Data streams used by Custom Vertex Streams.

## Particle jobs

For thousands of particles modified per frame, move the loop to a Burst job:

```csharp
using Unity.Burst;
using UnityEngine;
using UnityEngine.ParticleSystemJobs;

[RequireComponent(typeof(ParticleSystem))]
public sealed class ParticleAttractorJob : MonoBehaviour
{
    [SerializeField] Transform _target;
    [SerializeField] float _strength = 2f;

    void OnParticleUpdateJobScheduled()
    {
        new AttractJob
        {
            Target = _target.position,
            Strength = _strength,
            DeltaTime = Time.deltaTime
        }.ScheduleBatch(GetComponent<ParticleSystem>(), 1024);
    }

    [BurstCompile]
    struct AttractJob : IJobParticleSystemParallelForBatch
    {
        public Vector3 Target;
        public float Strength;
        public float DeltaTime;

        public void Execute(ParticleSystemJobData data, int startIndex, int count)
        {
            var px = data.positions.x; var py = data.positions.y; var pz = data.positions.z;
            var vx = data.velocities.x; var vy = data.velocities.y; var vz = data.velocities.z;
            int end = startIndex + count;
            for (int i = startIndex; i < end; i++)
            {
                vx[i] += (Target.x - px[i]) * Strength * DeltaTime;
                vy[i] += (Target.y - py[i]) * Strength * DeltaTime;
                vz[i] += (Target.z - pz[i]) * Strength * DeltaTime;
            }
        }
    }
}
```

`OnParticleUpdateJobScheduled` is a magic method on a script attached to the system's GameObject; Unity schedules the job after the built-in simulation and completes it before rendering. Cache the `ParticleSystem` in production code.

## Editor automation

Batch-editing effects in the Editor (for example, raising Max Particles across prefabs):

```csharp
using UnityEditor;
using UnityEngine;

public static class ParticleBatchTools
{
    [MenuItem("Tools/Particles/Set Selected Max Particles 500")]
    static void SetMaxParticles()
    {
        foreach (var go in Selection.gameObjects)
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            Undo.RecordObject(ps, "Set Max Particles");
            var main = ps.main;
            main.maxParticles = 500;
            EditorUtility.SetDirty(ps);
        }
    }
}
```

For prefab assets, edit inside `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset` / `UnloadPrefabContents` rather than on scene instances, so changes do not land as instance overrides.
