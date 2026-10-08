# Timeline API

Namespaces: `UnityEngine.Timeline` (assets, tracks, clips), `UnityEngine.Playables` (`PlayableDirector`, `PlayableAsset`, `PlayableBehaviour`).

## Build a cutscene asset (Editor)

```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public static class CutsceneBuilder
{
    [MenuItem("Tools/Timeline/Build Intro")]
    public static void BuildIntro()
    {
        const string path = "Assets/Cutscenes/Intro.playable";
        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        AssetDatabase.CreateAsset(timeline, path);
        timeline.editorSettings.frameRate = 30;

        var animTrack = timeline.CreateTrack<AnimationTrack>(null, "Hero");
        var heroClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/HeroIntro.anim");
        TimelineClip anim = animTrack.CreateClip(heroClip);
        anim.start = 0;

        var titleTrack = timeline.CreateTrack<ActivationTrack>(null, "TitleCard");
        titleTrack.postPlaybackState = ActivationTrack.PostPlaybackState.Inactive;
        TimelineClip show = titleTrack.CreateClip<ActivationPlayableAsset>();
        show.start = 1.0;
        show.duration = 2.5;
        show.displayName = "Show Title";

        var audioTrack = timeline.CreateTrack<AudioTrack>(null, "Music");
        var music = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Intro.ogg");
        audioTrack.CreateClip(music).start = 0;

        var signalTrack = timeline.CreateTrack<SignalTrack>(null, "Events");
        var endSignal = AssetDatabase.LoadAssetAtPath<SignalAsset>("Assets/Cutscenes/IntroEnded.signal");
        var emitter = signalTrack.CreateMarker<SignalEmitter>(anim.end);
        emitter.asset = endSignal;
        emitter.emitOnce = true;

        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();
    }
}
```

`SignalAsset` files are created with `ScriptableObject.CreateInstance<SignalAsset>()` + `AssetDatabase.CreateAsset(..., "X.signal")`.

## Bind a director

```csharp
public static void Bind(PlayableDirector director, TimelineAsset timeline, Animator hero, GameObject titleCard, AudioSource music, SignalReceiver receiver)
{
    director.playableAsset = timeline;
    director.playOnAwake = false;
    director.extrapolationMode = DirectorWrapMode.None;

    foreach (var track in timeline.GetOutputTracks())
    {
        switch (track.name)
        {
            case "Hero": director.SetGenericBinding(track, hero); break;
            case "TitleCard": director.SetGenericBinding(track, titleCard); break;
            case "Music": director.SetGenericBinding(track, music); break;
            case "Events": director.SetGenericBinding(track, receiver); break;
        }
    }
}
```

In the Editor, call `EditorSceneManager.MarkSceneDirty(director.gameObject.scene)` after binding.

## Signal receiver reactions

```csharp
var reaction = new UnityEngine.Events.UnityEvent();
reaction.AddListener(OnIntroEnded);
receiver.AddReaction(endSignal, reaction);
```

Reactions added at runtime with `AddListener` are not serialized; for persistent wiring use the Inspector or `UnityEditor.Events.UnityEventTools.AddPersistentListener` on the reaction before `AddReaction`.

Alternatively implement `INotificationReceiver` on a MonoBehaviour bound to the track:

```csharp
public class CutsceneEvents : MonoBehaviour, INotificationReceiver
{
    public void OnNotify(Playable origin, INotification notification, object context)
    {
        if (notification is SignalEmitter e && e.asset != null)
            Debug.Log($"Signal {e.asset.name} at {e.time:F2}s");
    }
}
```

## Runtime playback controller

```csharp
using System;
using UnityEngine;
using UnityEngine.Playables;

[RequireComponent(typeof(PlayableDirector))]
public class CutscenePlayer : MonoBehaviour
{
    public event Action Finished;
    PlayableDirector director;

    void Awake()
    {
        director = GetComponent<PlayableDirector>();
        director.stopped += OnStopped;
    }

    void OnDestroy() => director.stopped -= OnStopped;

    public void Play()
    {
        director.time = 0;
        director.Play();
    }

    public void Skip()
    {
        if (director.state != PlayState.Playing) return;
        director.time = director.duration;
        director.Evaluate();
        director.Stop();
    }

    void OnStopped(PlayableDirector d) => Finished?.Invoke();
}
```

With `extrapolationMode = None`, `stopped` fires automatically at the end. With `Hold`, it never fires on its own.

## Control track clip (exposed reference)

```csharp
var controlTrack = timeline.CreateTrack<ControlTrack>(null, "FX");
TimelineClip clip = controlTrack.CreateClip<ControlPlayableAsset>();
var asset = (ControlPlayableAsset)clip.asset;
asset.sourceGameObject.exposedName = UnityEditor.GUID.Generate().ToString();
director.SetReferenceValue(asset.sourceGameObject.exposedName, explosionFx);
```

The same pattern applies to `CinemachineShot.VirtualCamera`:

```csharp
var cmTrack = timeline.CreateTrack<Unity.Cinemachine.CinemachineTrack>(null, "Cameras");
director.SetGenericBinding(cmTrack, brain);
TimelineClip shotClip = cmTrack.CreateDefaultClip();
var shot = (Unity.Cinemachine.CinemachineShot)shotClip.asset;
shot.VirtualCamera.exposedName = UnityEditor.GUID.Generate().ToString();
director.SetReferenceValue(shot.VirtualCamera.exposedName, closeUpCam);
```

## Custom clip and track

```csharp
using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public class LightIntensityBehaviour : PlayableBehaviour
{
    public float intensity = 1f;
}

[Serializable]
public class LightIntensityClip : PlayableAsset, ITimelineClipAsset
{
    public LightIntensityBehaviour template = new();
    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        => ScriptPlayable<LightIntensityBehaviour>.Create(graph, template);
}

public class LightIntensityMixer : PlayableBehaviour
{
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (playerData is not Light light) return;
        float total = 0f;
        int count = playable.GetInputCount();
        for (int i = 0; i < count; i++)
        {
            float w = playable.GetInputWeight(i);
            var input = (ScriptPlayable<LightIntensityBehaviour>)playable.GetInput(i);
            total += input.GetBehaviour().intensity * w;
        }
        light.intensity = total;
    }
}

[TrackColor(1f, 0.85f, 0.2f)]
[TrackClipType(typeof(LightIntensityClip))]
[TrackBindingType(typeof(Light))]
public class LightIntensityTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        => ScriptPlayable<LightIntensityMixer>.Create(graph, inputCount);
}
```

Store the original value on first `ProcessFrame` and restore it in `OnPlayableDestroy` if the track should not leave the scene modified after preview.

## Useful members

| Need | API |
|---|---|
| All tracks (flattened) | `timeline.GetOutputTracks()` |
| Root tracks | `timeline.GetRootTracks()` |
| Clips on a track | `track.GetClips()` |
| Markers | `track.GetMarkers()`, `timeline.markerTrack` (`CreateMarkerTrack()`) |
| Delete | `timeline.DeleteTrack(track)`, `track.DeleteClip(clip)`, `track.DeleteMarker(marker)` |
| Mute / lock | `track.muted`, `track.locked` |
| Clip blending | `clip.easeInDuration`, `clip.easeOutDuration`, `clip.blendInDuration` (overlap) |
| Clip speed / trim | `clip.timeScale`, `clip.clipIn` |
| Duration | `timeline.duration`, `timeline.durationMode` / `fixedDuration` |
| Animation track offsets | `animTrack.trackOffset` (`ApplyTransformOffsets`, `ApplySceneOffsets`, `Auto`), `position`, `rotation` |
| Avatar mask per track | `animTrack.avatarMask`, `applyAvatarMask` |
