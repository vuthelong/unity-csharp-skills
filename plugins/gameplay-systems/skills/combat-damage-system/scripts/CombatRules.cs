using UnityEngine;

[CreateAssetMenu(fileName = "CombatRules", menuName = "Combat/Combat Rules")]
public class CombatRules : ScriptableObject
{
    #region Fields

    [SerializeField] private bool friendlyFire;
    [SerializeField, Range(0f, 1f)] private float friendlyFireMultiplier = 0.5f;
    [SerializeField] private bool selfDamage = true;

    #endregion

    #region Properties

    public bool FriendlyFire
    {
        get => this.friendlyFire;
        set => this.friendlyFire = value;
    }

    #endregion

    #region Rules

    public bool TryScale(CombatTeam attacker, CombatTeam victim, bool isSelf, ref float amount)
    {
        if (isSelf) return this.selfDamage;
        if (attacker == CombatTeam.Neutral || victim == CombatTeam.Neutral || attacker != victim) return true;
        if (!this.friendlyFire) return false;

        amount *= this.friendlyFireMultiplier;
        return amount > 0f;
    }

    #endregion
}
