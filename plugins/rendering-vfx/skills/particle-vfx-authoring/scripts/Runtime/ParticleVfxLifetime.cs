using System;
using UnityEngine;

namespace ParticleVfx
{
    [DisallowMultipleComponent]
    public sealed class ParticleVfxLifetime : MonoBehaviour
    {
        public static Action<GameObject> DespawnHandler;

        public float deathTimer = 1f;
        public bool unscaledTime;

        private float _remaining = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            DespawnHandler = null;
        }

        private void OnEnable()
        {
            _remaining = deathTimer;
        }

        private void OnDisable()
        {
            _remaining = -1f;
        }

        private void Update()
        {
            if (_remaining < 0f) return;
            _remaining -= unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (_remaining > 0f) return;
            _remaining = -1f;
            var handler = DespawnHandler;
            if (handler != null) handler(gameObject);
            else Destroy(gameObject);
        }
    }
}
