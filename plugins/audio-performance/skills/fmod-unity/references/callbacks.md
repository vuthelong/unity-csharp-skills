# Event callbacks: timeline markers, beats, programmer sounds

FMOD calls managed code from native code through `EventInstance.setCallback(EVENT_CALLBACK, EVENT_CALLBACK_TYPE mask)`. These rules apply on every platform, and IL2CPP crashes or fails to build if you skip any of them:

1. **Static method.** IL2CPP cannot marshal a delegate that targets an instance method into a native function pointer.
2. **`[AOT.MonoPInvokeCallback(typeof(FMOD.Studio.EVENT_CALLBACK))]`** on that method.
3. **Keep the delegate alive.** Store it in a `static readonly` field. A delegate created inline can be garbage collected while native code still holds the pointer.
4. **Pass state through user data.** Pin your object with `GCHandle.Alloc`, hand `GCHandle.ToIntPtr` to `setUserData`, recover it with `getUserData` and `GCHandle.FromIntPtr`, and `Free()` it on `EVENT_CALLBACK_TYPE.DESTROYED`. That means DESTROYED must be in the mask.
5. **Wrong thread.** In the default async mode, callbacks run on FMOD's Studio update thread. Never touch `UnityEngine` objects, `Transform`s, `Debug.Log` with context objects, or anything not thread-safe there. Copy the data into a thread-safe queue and drain it on the main thread.
6. **Return `FMOD.RESULT.OK`.** Any other value is logged as an error.
7. Recreate the instance wrapper from the pointer with `new FMOD.Studio.EventInstance(instancePtr)`, and read the parameters with `Marshal.PtrToStructure<T>(parameterPtr)`.

Callback types used for music and dialogue:

| Type | Payload |
|---|---|
| `TIMELINE_BEAT` | `TIMELINE_BEAT_PROPERTIES`: `bar`, `beat`, `position` (ms), `tempo`, `timesignatureupper`, `timesignaturelower`. Comes from tempo markers in Studio. |
| `TIMELINE_MARKER` | `TIMELINE_MARKER_PROPERTIES`: `name`, `position` (ms). In the Unity C# wrapper `name` is an `FMOD.StringWrapper`, so cast it to `string`. |
| `NESTED_TIMELINE_BEAT` | Beats from nested events. |
| `STARTED`, `STOPPED`, `START_FAILED` | Lifecycle. |
| `CREATE_PROGRAMMER_SOUND`, `DESTROY_PROGRAMMER_SOUND` | Programmer instruments (below). |
| `DESTROYED` | Last callback for the instance. Free the `GCHandle` here. |

## Reusable timeline listener

A plain C# class that owns one music instance, collects beats and markers on the FMOD thread, and raises C# events on the main thread from `Pump()`. Requires `FMODUnity` in the asmdef references.

```csharp
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using FMOD.Studio;

public readonly struct TimelineBeat
{
    #region Fields
    public readonly int Bar;
    public readonly int Beat;
    public readonly int PositionMs;
    public readonly float Tempo;
    #endregion

    #region Public Methods
    public TimelineBeat(int bar, int beat, int positionMs, float tempo)
    {
        this.Bar = bar;
        this.Beat = beat;
        this.PositionMs = positionMs;
        this.Tempo = tempo;
    }
    #endregion
}

public sealed class FmodTimelineListener : IDisposable
{
    #region Fields
    private const EVENT_CALLBACK_TYPE Mask =
        EVENT_CALLBACK_TYPE.TIMELINE_BEAT | EVENT_CALLBACK_TYPE.TIMELINE_MARKER | EVENT_CALLBACK_TYPE.DESTROYED;

    private static readonly EVENT_CALLBACK Callback = OnEventCallback;

    private readonly ConcurrentQueue<TimelineBeat> _beats = new ConcurrentQueue<TimelineBeat>();
    private readonly ConcurrentQueue<string> _markers = new ConcurrentQueue<string>();
    private EventInstance _instance;
    private volatile bool _disposed;

    public event Action<TimelineBeat> BeatReached;
    public event Action<string> MarkerReached;
    #endregion

    #region Properties
    public EventInstance Instance => this._instance;
    #endregion

    #region Public Methods
    public FmodTimelineListener(EventInstance instance)
    {
        this._instance = instance;
        var handle = GCHandle.Alloc(this);
        this._instance.setUserData(GCHandle.ToIntPtr(handle));
        this._instance.setCallback(Callback, Mask);
    }

    public void Pump()
    {
        while (this._beats.TryDequeue(out var beat)) this.BeatReached?.Invoke(beat);
        while (this._markers.TryDequeue(out var marker)) this.MarkerReached?.Invoke(marker);
    }

    public void Dispose()
    {
        if (this._disposed) return;

        this._disposed = true;
        this.BeatReached = null;
        this.MarkerReached = null;
        if (!this._instance.isValid()) return;

        this._instance.stop(STOP_MODE.ALLOWFADEOUT);
        this._instance.release();
    }
    #endregion

    #region Private Methods
    [AOT.MonoPInvokeCallback(typeof(EVENT_CALLBACK))]
    private static FMOD.RESULT OnEventCallback(EVENT_CALLBACK_TYPE type, IntPtr instancePtr, IntPtr parameterPtr)
    {
        var instance = new EventInstance(instancePtr);
        if (instance.getUserData(out var userData) != FMOD.RESULT.OK || userData == IntPtr.Zero) return FMOD.RESULT.OK;

        var handle = GCHandle.FromIntPtr(userData);
        if (type == EVENT_CALLBACK_TYPE.DESTROYED)
        {
            handle.Free();
            return FMOD.RESULT.OK;
        }

        if (handle.Target is not FmodTimelineListener listener || listener._disposed) return FMOD.RESULT.OK;

        if (type == EVENT_CALLBACK_TYPE.TIMELINE_BEAT)
        {
            var beat = Marshal.PtrToStructure<TIMELINE_BEAT_PROPERTIES>(parameterPtr);
            listener._beats.Enqueue(new TimelineBeat(beat.bar, beat.beat, beat.position, beat.tempo));
        }
        else if (type == EVENT_CALLBACK_TYPE.TIMELINE_MARKER)
        {
            var marker = Marshal.PtrToStructure<TIMELINE_MARKER_PROPERTIES>(parameterPtr);
            listener._markers.Enqueue((string)marker.name);
        }

        return FMOD.RESULT.OK;
    }
    #endregion
}
```

Usage: create the instance after its bank has loaded, wrap it, call `Pump()` once per frame (from `Update`, or VContainer `ITickable`), and `Dispose()` in `OnDestroy`. A full adaptive-music component is in [adaptive-music.md](adaptive-music.md).

Notes:

- The `GCHandle` is freed only in `DESTROYED`, which fires after `release()` once the instance has stopped. Do not free it in `Dispose`, because the FMOD thread may be inside the callback at that moment.
- The marker string is allocated on the FMOD thread, not the main thread. To make markers allocation-free, compare against a few known names, or have designers encode the meaning in the marker position.
- `ConcurrentQueue<T>` enqueue is lock-free and allocates only when it grows a segment. For a UniTask project you can replace `Pump()` with `await UniTask.SwitchToMainThread()` inside an async consumer, but a per-frame drain keeps beat ordering simple and costs nothing when the queue is empty.
- Beat callbacks fire when FMOD's timeline crosses the beat, slightly ahead of the audible output by the mixer buffer latency. Sync visuals to the callback frame and allow a frame of slack. Do not schedule gameplay for exact sample positions from Unity.
- Domain reload off (Enter Play Mode Options): the static delegate field survives, which is fine. Do not keep instances or handles in other statics across Play sessions. See `csharp-unity` for resetting statics with `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`.

## Main thread hand-off without a queue

When you only need the latest value (current bar, last marker), use a `volatile int` or `Interlocked.Exchange` into a field and read it in `Update`. Use a queue when every beat matters, for example when gameplay counts beats.

## Programmer sounds (dialogue, audio tables)

A programmer instrument in a Studio event asks the game which sound to play. It is mainly used for dialogue backed by a (localized) audio table, so one template event serves thousands of lines.

1. Create the instance, pin the line key (`GCHandle.Alloc(key)`), `setUserData`, `setCallback(DialogueCallback)`, then `start()` and `release()`.
2. On `CREATE_PROGRAMMER_SOUND`: read the key from user data, then call `RuntimeManager.StudioSystem.getSoundInfo(key, out SOUND_INFO info)`. Next call `RuntimeManager.CoreSystem.createSound(info.name_or_data, MODE.LOOP_NORMAL | MODE.CREATECOMPRESSEDSAMPLE | MODE.NONBLOCKING | info.mode, ref info.exinfo, out FMOD.Sound sound)`. Then write `sound.handle` and `info.subsoundindex` into `PROGRAMMER_SOUND_PROPERTIES` and `Marshal.StructureToPtr` it back.
3. On `DESTROY_PROGRAMMER_SOUND`: `new FMOD.Sound(props.sound).release()`.
4. On `DESTROYED`: free the key handle.
5. The same IL2CPP rules apply: static callback, `MonoPInvokeCallback`, delegate in a static field.

Localized audio tables build one bank per locale (for example `Dialogue_EN.bank`). Load exactly one at a time. To switch language, unload the current one and then load the next. With Load Banks set to Specified, list at most one locale bank.
