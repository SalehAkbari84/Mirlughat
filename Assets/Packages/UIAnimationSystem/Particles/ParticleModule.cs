using System;
using UnityEngine;

namespace UIToolkit.Animation.Particles
{
    // Context passed to modules so they can read emitter-wide info.
    public struct ParticleContext
    {
        public Vector2 emitterSize;   // px size of the emitter surface
        public float deltaTime;
        public System.Random rng;     // shared deterministic RNG (optional use)
    }

    // Base class for all particle behaviour modules. Add new behaviours by
    // subclassing this and adding it to the emitter's module list. Each module
    // can hook spawn (initialize a particle) and update (per-frame).
    //
    // Marked [Serializable] subclasses with [SerializeReference] in the system
    // config so the inspector can hold a polymorphic list.
    [Serializable]
    public abstract class ParticleModule
    {
        public bool enabled = true;

        // Called once when a particle is born.
        public virtual void OnSpawn(Particle p, in ParticleContext ctx) { }

        // Called every frame for each live particle.
        public virtual void OnUpdate(Particle p, in ParticleContext ctx) { }

        // Optional display name for editor/debug.
        public virtual string DisplayName => GetType().Name;
    }
}
