using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIToolkit.Animation.Particles
{
    // Core engine. Pools VisualElement particles inside a host element and
    // simulates them via the config's module list. Driven by TweenManager so it
    // ticks in sync with the rest of the animation system (no per-system MonoBehaviour).
    public sealed class ParticleEmitter : ITweenable
    {
        readonly ParticleSystemConfig _config;
        readonly VisualElement _host;
        readonly List<Particle> _pool = new List<Particle>();
        readonly System.Random _rng;

        float _emitAccumulator;
        float _elapsed;
        bool _emitting = true;
        bool _completeWhenEmpty;
        int _aliveCount;

        // mesh rendering: draw all particles with one element instead of one VE each
        readonly bool _mesh;
        readonly bool _trail;
        VisualElement _meshSurface;

        public TweenState State { get; private set; } = TweenState.Pending;
        public float TotalDuration => _config != null && !_config.loop ? _config.duration : float.PositiveInfinity;
        public bool IgnoreTimeScale { get; set; }
        public object Id { get; private set; }
        public int AliveCount => _aliveCount;

        public ParticleEmitter(ParticleSystemConfig config, VisualElement host, int? seed = null)
        {
            _config = config;
            _host = host;
            _rng = new System.Random(seed ?? Environment.TickCount);
            // Mesh path supports the simple solid shapes; image/custom fall back.
            // Trails also require the mesh path.
            bool wantMesh = config != null && (config.meshRenderer || config.trail);
            _mesh = wantMesh
                    && (config.visualKind == ParticleVisualKind.Circle || config.visualKind == ParticleVisualKind.Square);
            _trail = _mesh && config.trail;
            Prewarm();
        }

        public ParticleEmitter SetId(object id) { Id = id; return this; }

        // When set, the emitter completes (and is removed from the manager) once it
        // has stopped emitting and all live particles have died. Used by one-shot
        // bursts so they auto-clean even when the config has loop = true.
        public ParticleEmitter SetCompleteWhenEmpty(bool value = true) { _completeWhenEmpty = value; return this; }

        void Prewarm()
        {
            if (_config == null || _host == null) return;
            if (_mesh)
            {
                _meshSurface = new VisualElement
                {
                    pickingMode = PickingMode.Ignore,
                    style = { position = Position.Absolute, left = 0, top = 0, right = 0, bottom = 0 }
                };
                _meshSurface.generateVisualContent += DrawParticles;
                _host.Add(_meshSurface);
            }

            int cap = Mathf.Max(1, _config.maxParticles);
            for (int i = 0; i < cap; i++)
                _pool.Add(CreatePooled());

            foreach (var b in _config.bursts) b.fired = false;

            if (_config.warmup > 0f)
            {
                const float step = 1f / 60f;
                float remaining = _config.warmup;
                while (remaining > 0f) { Step(Mathf.Min(step, remaining)); remaining -= step; }
            }
        }

        Particle CreatePooled()
        {
            var p = new Particle();
            p.ResetState();
            if (_trail)
                p.trail = new Vector2[Mathf.Max(2, _config.trailLength)];
            if (!_mesh)
            {
                p.visual = ParticleVisualFactory.Create(_config);
                p.visual.style.display = DisplayStyle.None;
                p.visual.pickingMode = PickingMode.Ignore;
                _host.Add(p.visual);
            }
            return p;
        }

        Particle GetDead()
        {
            for (int i = 0; i < _pool.Count; i++)
                if (!_pool[i].alive) return _pool[i];
            // pool exhausted; if under cap (it shouldn't exceed), grow, else null
            if (_pool.Count < _config.maxParticles)
            {
                var np = CreatePooled();
                _pool.Add(np);
                return np;
            }
            return null;
        }

        // ----- public control -----
        public void Play() { _emitting = true; if (State == TweenState.Paused) State = TweenState.Running; }
        public void Pause() { if (State == TweenState.Running) State = TweenState.Paused; }
        public void Stop(bool clear = false)
        {
            _emitting = false;
            if (clear) KillAllParticles();
        }
        public void Emit(int count)
        {
            for (int i = 0; i < count; i++) SpawnOne();
        }

        public void Reset()
        {
            _elapsed = 0f; _emitAccumulator = 0f; _emitting = true; State = TweenState.Pending;
            KillAllParticles();
            if (_config != null)
                foreach (var b in _config.bursts) b.fired = false;
        }

        public void Kill(bool complete = false)
        {
            RemoveVisuals();
            State = TweenState.Killed;
        }

        void KillAllParticles()
        {
            foreach (var p in _pool)
            {
                p.alive = false;
                if (p.visual != null) p.visual.style.display = DisplayStyle.None;
            }
            _aliveCount = 0;
        }

        // Detach all pooled visuals from the host. Used when a one-shot burst is
        // done so it leaves no hidden elements behind.
        void RemoveVisuals()
        {
            foreach (var p in _pool)
                p.visual?.RemoveFromHierarchy();
            _meshSurface?.RemoveFromHierarchy();
            _meshSurface = null;
            _pool.Clear();
            _aliveCount = 0;
        }

        // ----- simulation -----
        public bool Update(float deltaTime)
        {
            if (State == TweenState.Killed) return true;
            if (State == TweenState.Paused) return false;
            if (_config == null || _host == null) { State = TweenState.Killed; return true; }
            if (State == TweenState.Pending) State = TweenState.Running;

            Step(deltaTime);

            // completion: no particles left AND either a non-looping system that is
            // past its duration, or a one-shot burst that has stopped emitting.
            bool durationDone = !_config.loop && _elapsed >= _config.duration;
            bool burstDone = _completeWhenEmpty && !_emitting;
            if (_aliveCount == 0 && (durationDone || burstDone))
            {
                State = TweenState.Completed;
                if (_completeWhenEmpty) RemoveVisuals();
                return true;
            }
            return false;
        }

        void Step(float dt)
        {
            _elapsed += dt;

            Vector2 size = new Vector2(
                Mathf.Max(1f, _host.resolvedStyle.width),
                Mathf.Max(1f, _host.resolvedStyle.height));

            var ctx = new ParticleContext { emitterSize = size, deltaTime = dt, rng = _rng };

            bool withinEmission = _config.loop || _elapsed <= _config.duration;

            // continuous emission
            if (_emitting && withinEmission && _config.emissionRate > 0f)
            {
                _emitAccumulator += _config.emissionRate * dt;
                while (_emitAccumulator >= 1f)
                {
                    SpawnOne();
                    _emitAccumulator -= 1f;
                }
            }

            // bursts
            if (_emitting && withinEmission)
            {
                foreach (var b in _config.bursts)
                {
                    if (!b.fired && _elapsed >= b.time)
                    {
                        Emit(b.count);
                        b.fired = true;
                    }
                }
            }

            // simulate live particles
            _aliveCount = 0;
            var modules = _config.modules;
            for (int i = 0; i < _pool.Count; i++)
            {
                var p = _pool[i];
                if (!p.alive) continue;

                p.age += dt;
                if (p.age >= p.lifetime)
                {
                    p.alive = false;
                    if (p.visual != null) p.visual.style.display = DisplayStyle.None;
                    continue;
                }

                for (int m = 0; m < modules.Count; m++)
                {
                    var mod = modules[m];
                    if (mod != null && mod.enabled) mod.OnUpdate(p, in ctx);
                }

                p.position += p.velocity * dt;
                if (_trail) p.PushTrail(p.position);
                ApplyVisual(p);
                _aliveCount++;
            }

            if (_mesh && _meshSurface != null) _meshSurface.MarkDirtyRepaint();
        }

        void SpawnOne()
        {
            var p = GetDead();
            if (p == null) return;

            p.ResetState();
            p.alive = true;
            p.seed = (float)_rng.NextDouble();

            Vector2 size = new Vector2(
                Mathf.Max(1f, _host.resolvedStyle.width),
                Mathf.Max(1f, _host.resolvedStyle.height));
            var ctx = new ParticleContext { emitterSize = size, deltaTime = 0f, rng = _rng };

            var modules = _config.modules;
            for (int m = 0; m < modules.Count; m++)
            {
                var mod = modules[m];
                if (mod != null && mod.enabled) mod.OnSpawn(p, in ctx);
            }

            if (p.visual != null) p.visual.style.display = DisplayStyle.Flex;
            ApplyVisual(p);
        }

        void ApplyVisual(Particle p)
        {
            if (_mesh || p.visual == null) return;   // mesh path draws in DrawParticles
            var s = p.visual.style;
            float half = p.size * 0.5f;
            s.width = p.size;
            s.height = p.size;
            s.left = p.position.x - half;
            s.top = p.position.y - half;
            s.opacity = p.color.a;
            s.rotate = new Rotate(new Angle(p.rotation, AngleUnit.Degree));

            if (_config.visualKind == ParticleVisualKind.Image)
                s.unityBackgroundImageTintColor = p.color;
            else
                s.backgroundColor = p.color;
        }

        // Draw every live particle with a single mesh via Painter2D.
        void DrawParticles(MeshGenerationContext mgc)
        {
            var p2d = mgc.painter2D;
            bool square = _config.visualKind == ParticleVisualKind.Square;
            for (int i = 0; i < _pool.Count; i++)
            {
                var p = _pool[i];
                if (!p.alive) continue;

                // fading ribbon behind the particle
                if (_trail && p.trailCount >= 2)
                {
                    int len = p.trail.Length;
                    for (int s = 0; s < p.trailCount - 1; s++)
                    {
                        int iA = ((p.trailHead - p.trailCount + s) % len + len) % len;
                        int iB = (iA + 1) % len;
                        float tNorm = s / (float)(p.trailCount - 1); // 0 = oldest, 1 = newest
                        var col = p.color; col.a *= tNorm;
                        p2d.strokeColor = col;
                        p2d.lineWidth = Mathf.Max(1f, p.size * _config.trailWidthScale * tNorm);
                        p2d.BeginPath();
                        p2d.MoveTo(p.trail[iA]);
                        p2d.LineTo(p.trail[iB]);
                        p2d.Stroke();
                    }
                }

                float r = p.size * 0.5f;
                p2d.fillColor = p.color;
                p2d.BeginPath();
                if (square)
                {
                    p2d.MoveTo(new Vector2(p.position.x - r, p.position.y - r));
                    p2d.LineTo(new Vector2(p.position.x + r, p.position.y - r));
                    p2d.LineTo(new Vector2(p.position.x + r, p.position.y + r));
                    p2d.LineTo(new Vector2(p.position.x - r, p.position.y + r));
                    p2d.ClosePath();
                }
                else
                {
                    p2d.Arc(p.position, r, new Angle(0f), new Angle(360f));
                }
                p2d.Fill();
            }
        }
    }
}
