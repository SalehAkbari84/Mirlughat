using UnityEngine;
using UnityEngine.UIElements;

namespace UIToolkit.Animation.Particles
{
    // Runtime state of a single particle. Pooled and reused; never allocated
    // per-frame after warmup. The visual is a real VisualElement so it can be
    // styled with USS or swapped for any custom element.
    public sealed class Particle
    {
        public VisualElement visual;

        public bool alive;
        public float age;
        public float lifetime;

        public Vector2 position;     // local px within the emitter surface
        public Vector2 velocity;     // px / second
        public float rotation;       // degrees
        public float angularVelocity;// degrees / second
        public float size;           // px (uniform)
        public float startSize;
        public Color color = Color.white;
        public Color startColor = Color.white;

        // Per-particle random seed so modules can vary deterministically.
        public float seed;

        // Trail history (ring buffer). Allocated by the emitter only in trail mode.
        public Vector2[] trail;
        public int trailHead;
        public int trailCount;

        public float NormalizedAge => lifetime <= 0f ? 1f : Mathf.Clamp01(age / lifetime);

        public void PushTrail(Vector2 pos)
        {
            if (trail == null || trail.Length == 0) return;
            trail[trailHead] = pos;
            trailHead = (trailHead + 1) % trail.Length;
            if (trailCount < trail.Length) trailCount++;
        }

        public void ResetState()
        {
            alive = false;
            age = 0f;
            lifetime = 1f;
            position = Vector2.zero;
            velocity = Vector2.zero;
            rotation = 0f;
            angularVelocity = 0f;
            size = startSize = 16f;
            color = startColor = Color.white;
            seed = Random.value;
            trailHead = 0;
            trailCount = 0;
        }
    }
}
