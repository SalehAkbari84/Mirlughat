using System;
using UnityEngine;

namespace UIToolkit.Animation.Particles
{
    // ----- SHAPE / SPAWN POSITION -----
    public enum EmitterShape { Point, Circle, Rectangle, Edge }

    [Serializable]
    public class ShapeModule : ParticleModule
    {
        public EmitterShape shape = EmitterShape.Point;
        public Vector2 origin = Vector2.zero;     // offset from emitter center
        public float radius = 40f;                // for Circle
        public Vector2 boxSize = new Vector2(120, 60); // for Rectangle
        public bool fromEdgeOnly = false;         // circle/rect surface vs edge

        public override void OnSpawn(Particle p, in ParticleContext ctx)
        {
            Vector2 center = new Vector2(ctx.emitterSize.x * 0.5f, ctx.emitterSize.y * 0.5f) + origin;
            switch (shape)
            {
                case EmitterShape.Point:
                    p.position = center;
                    break;
                case EmitterShape.Circle:
                {
                    float ang = (float)ctx.rng.NextDouble() * Mathf.PI * 2f;
                    float r = fromEdgeOnly ? radius : radius * Mathf.Sqrt((float)ctx.rng.NextDouble());
                    p.position = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                    break;
                }
                case EmitterShape.Rectangle:
                {
                    float rx = ((float)ctx.rng.NextDouble() - 0.5f) * boxSize.x;
                    float ry = ((float)ctx.rng.NextDouble() - 0.5f) * boxSize.y;
                    p.position = center + new Vector2(rx, ry);
                    break;
                }
                case EmitterShape.Edge:
                {
                    float rx = ((float)ctx.rng.NextDouble() - 0.5f) * boxSize.x;
                    p.position = center + new Vector2(rx, 0f);
                    break;
                }
            }
        }
        public override string DisplayName => "Shape";
    }

    // ----- INITIAL VELOCITY -----
    [Serializable]
    public class VelocityModule : ParticleModule
    {
        public float minAngle = 0f;      // degrees, 0 = right, 90 = up
        public float maxAngle = 360f;
        public float minSpeed = 60f;
        public float maxSpeed = 140f;
        public Vector2 constantAdd = Vector2.zero; // extra constant velocity

        public override void OnSpawn(Particle p, in ParticleContext ctx)
        {
            float a = Mathf.Lerp(minAngle, maxAngle, (float)ctx.rng.NextDouble()) * Mathf.Deg2Rad;
            float s = Mathf.Lerp(minSpeed, maxSpeed, (float)ctx.rng.NextDouble());
            // screen-space: y grows downward in UI Toolkit, so invert y for intuitive up.
            p.velocity = new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * s + constantAdd;
        }
        public override string DisplayName => "Velocity";
    }

    // ----- LIFETIME -----
    [Serializable]
    public class LifetimeModule : ParticleModule
    {
        public float minLifetime = 0.8f;
        public float maxLifetime = 1.6f;
        public override void OnSpawn(Particle p, in ParticleContext ctx)
        {
            p.lifetime = Mathf.Lerp(minLifetime, maxLifetime, (float)ctx.rng.NextDouble());
        }
        public override string DisplayName => "Lifetime";
    }

    // ----- SIZE -----
    [Serializable]
    public class SizeModule : ParticleModule
    {
        public float minStartSize = 10f;
        public float maxStartSize = 22f;
        public bool sizeOverLifetime = true;
        public AnimationCurve sizeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        public override void OnSpawn(Particle p, in ParticleContext ctx)
        {
            p.startSize = Mathf.Lerp(minStartSize, maxStartSize, (float)ctx.rng.NextDouble());
            p.size = p.startSize;
        }
        public override void OnUpdate(Particle p, in ParticleContext ctx)
        {
            if (sizeOverLifetime)
                p.size = p.startSize * Mathf.Max(0f, sizeCurve.Evaluate(p.NormalizedAge));
        }
        public override string DisplayName => "Size";
    }

    // ----- COLOR -----
    [Serializable]
    public class ColorModule : ParticleModule
    {
        public Gradient gradient = DefaultGradient();
        public bool useGradientOverLifetime = true;
        public Color startColor = Color.white;

        public override void OnSpawn(Particle p, in ParticleContext ctx)
        {
            p.startColor = useGradientOverLifetime ? gradient.Evaluate(0f) : startColor;
            p.color = p.startColor;
        }
        public override void OnUpdate(Particle p, in ParticleContext ctx)
        {
            if (useGradientOverLifetime)
                p.color = gradient.Evaluate(p.NormalizedAge);
        }
        public override string DisplayName => "Color";

        static Gradient DefaultGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.5f, 0.7f, 1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            return g;
        }
    }

    // ----- GRAVITY / FORCE -----
    [Serializable]
    public class GravityModule : ParticleModule
    {
        public Vector2 gravity = new Vector2(0f, 200f); // px/s^2, +y = down
        public float drag = 0f;                          // 0 = none, higher = more slowdown

        public override void OnUpdate(Particle p, in ParticleContext ctx)
        {
            p.velocity += gravity * ctx.deltaTime;
            if (drag > 0f)
                p.velocity *= Mathf.Clamp01(1f - drag * ctx.deltaTime);
        }
        public override string DisplayName => "Gravity / Force";
    }

    // ----- ATTRACTOR -----
    [Serializable]
    public class AttractorModule : ParticleModule
    {
        public Vector2 point = new Vector2(0.5f, 0.5f); // normalized within the emitter (0..1)
        public float strength = 220f;                   // pull acceleration
        public float maxRadius = 0f;                    // 0 = affect everywhere, else only within radius

        public override void OnUpdate(Particle p, in ParticleContext ctx)
        {
            Vector2 target = new Vector2(ctx.emitterSize.x * point.x, ctx.emitterSize.y * point.y);
            Vector2 dir = target - p.position;
            float dist = dir.magnitude;
            if (maxRadius > 0f && dist > maxRadius) return;
            if (dist > 0.001f) p.velocity += (dir / dist) * strength * ctx.deltaTime;
        }
        public override string DisplayName => "Attractor";
    }

    // ----- NOISE / TURBULENCE -----
    [Serializable]
    public class NoiseModule : ParticleModule
    {
        public float strength = 60f;
        public float frequency = 0.01f;   // spatial scale of the noise field

        public override void OnUpdate(Particle p, in ParticleContext ctx)
        {
            float nx = Mathf.PerlinNoise(p.position.x * frequency, p.position.y * frequency + p.seed) - 0.5f;
            float ny = Mathf.PerlinNoise(p.position.y * frequency + 100f, p.position.x * frequency + p.seed) - 0.5f;
            p.velocity += new Vector2(nx, ny) * (strength * 2f) * ctx.deltaTime;
        }
        public override string DisplayName => "Noise / Turbulence";
    }

    // ----- ROTATION -----
    [Serializable]
    public class RotationModule : ParticleModule
    {
        public float minAngularVelocity = -180f; // deg/s
        public float maxAngularVelocity = 180f;
        public bool alignToVelocity = false;

        public override void OnSpawn(Particle p, in ParticleContext ctx)
        {
            p.angularVelocity = Mathf.Lerp(minAngularVelocity, maxAngularVelocity, (float)ctx.rng.NextDouble());
        }
        public override void OnUpdate(Particle p, in ParticleContext ctx)
        {
            if (alignToVelocity && p.velocity.sqrMagnitude > 0.001f)
                p.rotation = Mathf.Atan2(p.velocity.y, p.velocity.x) * Mathf.Rad2Deg;
            else
                p.rotation += p.angularVelocity * ctx.deltaTime;
        }
        public override string DisplayName => "Rotation";
    }
}
