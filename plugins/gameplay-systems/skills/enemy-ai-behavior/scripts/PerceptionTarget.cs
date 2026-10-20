using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PerceptionTarget : MonoBehaviour
{
    #region Fields

    private static readonly List<PerceptionTarget> ActiveTargets = new();

    [SerializeField] private Transform aimPoint;
    [SerializeField] private bool isPlayer = true;
    [SerializeField, Range(0f, 1f)] private float visibility = 1f;

    #endregion

    #region Properties

    public static IReadOnlyList<PerceptionTarget> Active => ActiveTargets;

    public Vector3 AimPosition => this.aimPoint != null ? this.aimPoint.position : transform.position + Vector3.up;
    public bool IsPlayer => this.isPlayer;
    public bool IsTargetable { get; set; } = true;

    public float Visibility
    {
        get => this.visibility;
        set => this.visibility = Mathf.Clamp01(value);
    }

    #endregion

    #region Unity Lifecycle

    private void OnEnable() => ActiveTargets.Add(this);

    private void OnDisable() => ActiveTargets.Remove(this);

    #endregion

    #region Registry

    public static bool TryGetNearestPlayerSqrDistance(Vector3 position, out float sqrDistance)
    {
        sqrDistance = float.MaxValue;
        var found = false;
        for (var i = 0; i < ActiveTargets.Count; i++)
        {
            var target = ActiveTargets[i];
            if (!target.isPlayer) continue;

            var sqr = (target.transform.position - position).sqrMagnitude;
            if (sqr >= sqrDistance) continue;

            sqrDistance = sqr;
            found = true;
        }

        return found;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => ActiveTargets.Clear();

    #endregion
}
