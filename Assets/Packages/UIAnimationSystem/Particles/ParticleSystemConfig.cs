using System;
using System.Collections.Generic;
using UnityEngine;

namespace UIToolkit.Animation.Particles
{
    public enum ParticleVisualKind { Circle, Square, Image, CustomClass }

    // Reusable particle preset. Create -> UI Toolkit -> Particle System.
    [CreateAssetMenu(fileName = "NewUIParticles", menuName = "UI Toolkit/Particle System")]
    public class ParticleSystemConfig : ScriptableObject
    {
        [Header("Emission")]
        [Min(0f)] public float emissionRate = 40f;     // particles per second
        [Min(0)] public int maxParticles = 500;
        public bool playOnStart = true;
        public bool loop = true;
        [Min(0f)] public float duration = 3f;          // used when loop = false
        [Min(0f)] public float warmup = 0f;            // simulate this many seconds at start

        [Header("Bursts (optional)")]
        public List<Burst> bursts = new List<Burst>();

        [Header("Visual")]
        public ParticleVisualKind visualKind = ParticleVisualKind.Circle;
        public Texture2D sprite;                        // for Image kind
        public string customUssClass = "ui-particle";   // for CustomClass kind
        public bool additiveHint = false;               // documented hint for USS blend

        [Tooltip("Draw all particles with a single mesh (Painter2D) instead of one VisualElement each. Scales to thousands of Circle/Square particles. Image/CustomClass kinds fall back to per-element.")]
        public bool meshRenderer = false;

        [Header("Trail (uses the mesh renderer)")]
        [Tooltip("Draw a fading ribbon behind each particle. Forces the mesh renderer.")]
        public bool trail = false;
        [Min(2)] public int trailLength = 8;          // number of history points
        [Min(0f)] public float trailWidthScale = 0.5f; // ribbon width relative to particle size

        [Header("Modules (order matters)")]
        [SerializeReference] public List<ParticleModule> modules = new List<ParticleModule>();

        [Serializable]
        public class Burst
        {
            public float time = 0f;     // seconds from start
            [Min(0)] public int count = 20;
            [NonSerialized] public bool fired;
        }

        // Sensible default modules when a fresh asset is created.
        void Reset()
        {
            modules = new List<ParticleModule>
            {
                new ShapeModule(),
                new VelocityModule(),
                new LifetimeModule(),
                new SizeModule(),
                new ColorModule(),
                new GravityModule { gravity = new Vector2(0f, 120f) },
                new RotationModule()
            };
        }
    }
}
