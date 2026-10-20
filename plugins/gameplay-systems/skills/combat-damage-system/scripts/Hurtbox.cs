using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Hurtbox : MonoBehaviour
{
    #region Fields

    [SerializeField] private Health owner;
    [SerializeField] private float damageMultiplier = 1f;

    #endregion

    #region Properties

    public Health Owner => this.owner;
    public float DamageMultiplier => this.damageMultiplier;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (this.owner == null) this.owner = GetComponentInParent<Health>();
        if (this.owner != null) return;

        Debug.LogError($"{nameof(Hurtbox)} on {name} has no {nameof(Health)} owner.", this);
        enabled = false;
    }

    #endregion

    #region Hits

    public float ApplyHit(in DamageInfo info)
    {
        if (!enabled) return 0f;

        return this.owner.ApplyDamage(info.WithAmount(info.Amount * this.damageMultiplier));
    }

    #endregion
}
