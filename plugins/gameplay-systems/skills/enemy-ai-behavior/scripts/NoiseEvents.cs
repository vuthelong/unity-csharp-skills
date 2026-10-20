using System;
using UnityEngine;

public readonly struct NoiseEvent
{
    public NoiseEvent(Vector3 position, float radius, PerceptionTarget source)
    {
        this.Position = position;
        this.Radius = radius;
        this.Source = source;
    }

    public Vector3 Position { get; }
    public float Radius { get; }
    public PerceptionTarget Source { get; }
}

public static class NoiseEvents
{
    #region Fields

    public static event Action<NoiseEvent> Emitted;

    #endregion

    #region Emission

    public static void Emit(Vector3 position, float radius, PerceptionTarget source = null)
    {
        if (radius <= 0f) return;
        Emitted?.Invoke(new NoiseEvent(position, radius, source));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Emitted = null;

    #endregion
}
