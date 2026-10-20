using System;
using UnityEngine;

public readonly struct DamageEvent
{
    public DamageEvent(Health target, DamageInfo info, float applied, bool lethal)
    {
        this.Target = target;
        this.Info = info;
        this.Applied = applied;
        this.Lethal = lethal;
    }

    public Health Target { get; }
    public DamageInfo Info { get; }
    public float Applied { get; }
    public bool Lethal { get; }
}

[CreateAssetMenu(fileName = "DamageEventChannel", menuName = "Combat/Damage Event Channel")]
public class DamageEventChannel : ScriptableObject
{
    #region Fields

    public event Action<DamageEvent> Raised;

    #endregion

    #region Channel

    public void Raise(in DamageEvent damageEvent) => Raised?.Invoke(damageEvent);

    #endregion
}
