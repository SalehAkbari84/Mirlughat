using System.Collections.Generic;
using UnityEngine;

namespace UIToolkit.Animation.Particles
{
    // Ready-made particle presets for common UI effects. Build one and either
    // host.SpawnParticles(config) at runtime or save it as an asset in the editor.
    public static class ParticlePresets
    {
        public enum Kind { Confetti, Sparkle, Smoke, Burst, Fireworks, Trail }

        public static string DisplayName(Kind k)
        {
            switch (k)
            {
                case Kind.Confetti: return "Confetti";
                case Kind.Sparkle: return "Sparkle";
                case Kind.Smoke: return "Smoke";
                case Kind.Burst: return "Burst";
                case Kind.Fireworks: return "Fireworks";
                case Kind.Trail: return "Trail";
                default: return k.ToString();
            }
        }

        public static System.Array AllKinds => System.Enum.GetValues(typeof(Kind));

        public static ParticleSystemConfig Create(Kind kind)
        {
            var c = ScriptableObject.CreateInstance<ParticleSystemConfig>();
            c.name = DisplayName(kind);
            c.modules = new List<ParticleModule>();

            switch (kind)
            {
                case Kind.Confetti:
                    c.loop = false; c.duration = 2.5f; c.maxParticles = 300; c.emissionRate = 0f;
                    c.visualKind = ParticleVisualKind.Square;
                    c.bursts.Add(new ParticleSystemConfig.Burst { time = 0f, count = 90 });
                    c.modules.Add(new ShapeModule { shape = EmitterShape.Point });
                    c.modules.Add(new VelocityModule { minAngle = 55f, maxAngle = 125f, minSpeed = 220f, maxSpeed = 420f });
                    c.modules.Add(new LifetimeModule { minLifetime = 1.2f, maxLifetime = 2.2f });
                    c.modules.Add(new SizeModule { minStartSize = 8f, maxStartSize = 16f, sizeOverLifetime = false });
                    c.modules.Add(new ColorModule { useGradientOverLifetime = true, gradient = Rainbow() });
                    c.modules.Add(new GravityModule { gravity = new Vector2(0f, 420f), drag = 0.3f });
                    c.modules.Add(new RotationModule { minAngularVelocity = -360f, maxAngularVelocity = 360f });
                    break;

                case Kind.Sparkle:
                    c.loop = true; c.maxParticles = 200; c.emissionRate = 35f;
                    c.visualKind = ParticleVisualKind.Circle; c.additiveHint = true;
                    c.modules.Add(new ShapeModule { shape = EmitterShape.Rectangle, boxSize = new Vector2(200f, 120f) });
                    c.modules.Add(new VelocityModule { minAngle = 0f, maxAngle = 360f, minSpeed = 6f, maxSpeed = 30f });
                    c.modules.Add(new LifetimeModule { minLifetime = 0.5f, maxLifetime = 1.2f });
                    c.modules.Add(new SizeModule { minStartSize = 2f, maxStartSize = 7f, sizeOverLifetime = true, sizeCurve = PopFade() });
                    c.modules.Add(new ColorModule { useGradientOverLifetime = true, gradient = WhiteFade() });
                    break;

                case Kind.Smoke:
                    c.loop = true; c.maxParticles = 120; c.emissionRate = 12f;
                    c.visualKind = ParticleVisualKind.Circle;
                    c.modules.Add(new ShapeModule { shape = EmitterShape.Circle, radius = 14f });
                    c.modules.Add(new VelocityModule { minAngle = 80f, maxAngle = 100f, minSpeed = 18f, maxSpeed = 46f });
                    c.modules.Add(new LifetimeModule { minLifetime = 2f, maxLifetime = 3.5f });
                    c.modules.Add(new SizeModule { minStartSize = 18f, maxStartSize = 34f, sizeOverLifetime = true, sizeCurve = Grow() });
                    c.modules.Add(new ColorModule { useGradientOverLifetime = true, gradient = SmokeGradient() });
                    c.modules.Add(new GravityModule { gravity = new Vector2(0f, -10f), drag = 0.4f });
                    break;

                case Kind.Burst:
                    c.loop = false; c.duration = 1.5f; c.maxParticles = 200; c.emissionRate = 0f;
                    c.visualKind = ParticleVisualKind.Circle;
                    c.bursts.Add(new ParticleSystemConfig.Burst { time = 0f, count = 50 });
                    c.modules.Add(new ShapeModule { shape = EmitterShape.Point });
                    c.modules.Add(new VelocityModule { minAngle = 0f, maxAngle = 360f, minSpeed = 120f, maxSpeed = 280f });
                    c.modules.Add(new LifetimeModule { minLifetime = 0.5f, maxLifetime = 1f });
                    c.modules.Add(new SizeModule { minStartSize = 6f, maxStartSize = 12f, sizeOverLifetime = true, sizeCurve = Fade() });
                    c.modules.Add(new ColorModule { useGradientOverLifetime = true, gradient = WhiteFade() });
                    c.modules.Add(new GravityModule { gravity = new Vector2(0f, 160f), drag = 0.5f });
                    break;

                case Kind.Fireworks:
                    c.loop = true; c.maxParticles = 400; c.emissionRate = 0f;
                    c.visualKind = ParticleVisualKind.Circle; c.additiveHint = true;
                    c.bursts.Add(new ParticleSystemConfig.Burst { time = 0f, count = 70 });
                    c.bursts.Add(new ParticleSystemConfig.Burst { time = 1.2f, count = 70 });
                    c.modules.Add(new ShapeModule { shape = EmitterShape.Point });
                    c.modules.Add(new VelocityModule { minAngle = 0f, maxAngle = 360f, minSpeed = 160f, maxSpeed = 320f });
                    c.modules.Add(new LifetimeModule { minLifetime = 0.8f, maxLifetime = 1.6f });
                    c.modules.Add(new SizeModule { minStartSize = 4f, maxStartSize = 9f, sizeOverLifetime = true, sizeCurve = Fade() });
                    c.modules.Add(new ColorModule { useGradientOverLifetime = true, gradient = Rainbow() });
                    c.modules.Add(new GravityModule { gravity = new Vector2(0f, 120f), drag = 0.6f });
                    break;

                case Kind.Trail:
                    c.loop = true; c.maxParticles = 80; c.emissionRate = 22f;
                    c.visualKind = ParticleVisualKind.Circle; c.additiveHint = true;
                    c.trail = true; c.trailLength = 10; c.trailWidthScale = 0.6f;
                    c.modules.Add(new ShapeModule { shape = EmitterShape.Point });
                    c.modules.Add(new VelocityModule { minAngle = 0f, maxAngle = 360f, minSpeed = 40f, maxSpeed = 95f });
                    c.modules.Add(new LifetimeModule { minLifetime = 0.8f, maxLifetime = 1.5f });
                    c.modules.Add(new SizeModule { minStartSize = 4f, maxStartSize = 8f, sizeOverLifetime = true, sizeCurve = Fade() });
                    c.modules.Add(new ColorModule { useGradientOverLifetime = true, gradient = WhiteFade() });
                    break;
            }

            return c;
        }

        // ----- gradients / curves -----
        static Gradient Rainbow()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.3f, 0.3f), 0f),
                    new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0.25f),
                    new GradientColorKey(new Color(0.3f, 0.9f, 0.4f), 0.5f),
                    new GradientColorKey(new Color(0.3f, 0.6f, 1f), 0.75f),
                    new GradientColorKey(new Color(0.8f, 0.4f, 1f), 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        static Gradient WhiteFade()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.8f, 0.9f, 1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        static Gradient SmokeGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(0.55f, 0.55f, 0.58f), 0f), new GradientColorKey(new Color(0.3f, 0.3f, 0.32f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.45f, 0.2f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        static AnimationCurve Fade() => AnimationCurve.Linear(0f, 1f, 1f, 0f);
        static AnimationCurve Grow() => AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.4f);
        static AnimationCurve PopFade()
        {
            var c = new AnimationCurve();
            c.AddKey(0f, 0f);
            c.AddKey(0.3f, 1f);
            c.AddKey(1f, 0f);
            return c;
        }
    }
}
