using UnityEngine;

public enum DamageType
{
    Physical,
    Fire,
    Frost,
    Poison,
    Fall,
    True
}

public enum CombatTeam
{
    Neutral,
    Players,
    Enemies
}

public readonly struct DamageInfo
{
    public DamageInfo(float amount, DamageType type, GameObject source, CombatTeam sourceTeam, Vector3 point, Vector3 direction, bool ignoresInvulnerability = false)
    {
        this.Amount = amount;
        this.Type = type;
        this.Source = source;
        this.SourceTeam = sourceTeam;
        this.Point = point;
        this.Direction = direction;
        this.IgnoresInvulnerability = ignoresInvulnerability;
    }

    public float Amount { get; }
    public DamageType Type { get; }
    public GameObject Source { get; }
    public CombatTeam SourceTeam { get; }
    public Vector3 Point { get; }
    public Vector3 Direction { get; }
    public bool IgnoresInvulnerability { get; }

    public DamageInfo WithAmount(float amount) => new(amount, Type, Source, SourceTeam, Point, Direction, IgnoresInvulnerability);

    public DamageInfo WithHit(Vector3 point, Vector3 direction) => new(Amount, Type, Source, SourceTeam, point, direction, IgnoresInvulnerability);
}
