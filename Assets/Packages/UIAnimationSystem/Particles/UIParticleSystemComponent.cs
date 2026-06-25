using UnityEngine;
using UnityEngine.UIElements;
using UIToolkit.Animation.Timeline;

namespace UIToolkit.Animation.Particles
{
    // Drop-in MonoBehaviour to run a particle system inside a UI Toolkit panel.
    // Add to the GameObject that hosts the panel (Panel Renderer on 6.5+, or
    // UIDocument on older versions - resolved via UIPanel). Multiple are fine.
    [AddComponentMenu("UI Toolkit/UI Particle System")]
    public class UIParticleSystemComponent : MonoBehaviour
    {
        [Tooltip("Particle preset asset.")]
        public ParticleSystemConfig config;

        [Tooltip("Name of the host element in UXML where particles render. Empty = root.")]
        public string hostElementName = "";

        [Tooltip("Use unscaled time (ignore Time.timeScale).")]
        public bool ignoreTimeScale = false;

        ParticleEmitter _emitter;

        void OnEnable()
        {
            if (config == null) { Debug.LogWarning("[UIParticleSystem] No config assigned."); return; }
            UIPanel.WhenReady(gameObject, root =>
            {
                VisualElement host = string.IsNullOrEmpty(hostElementName)
                    ? root
                    : root.Q<VisualElement>(hostElementName) ?? root;

                // ensure host clips and is positioned for absolute children
                host.style.overflow = Overflow.Hidden;

                _emitter = new ParticleEmitter(config, host) { IgnoreTimeScale = ignoreTimeScale };
                if (!config.playOnStart) _emitter.Stop();
                TweenManager.Register(_emitter);
            });
        }

        void OnDisable() => _emitter?.Kill();

        // ----- runtime control -----
        public void Play() => _emitter?.Play();
        public void Pause() => _emitter?.Pause();
        public void Stop(bool clear = false) => _emitter?.Stop(clear);
        public void Emit(int count) => _emitter?.Emit(count);
        public int AliveCount => _emitter != null ? _emitter.AliveCount : 0;
    }

    // Code-first API for spawning particle systems on any element.
    public static class ParticleExtensions
    {
        public static ParticleEmitter SpawnParticles(this VisualElement host, ParticleSystemConfig config, bool ignoreTimeScale = false)
        {
            host.style.overflow = Overflow.Hidden;
            var emitter = new ParticleEmitter(config, host) { IgnoreTimeScale = ignoreTimeScale };
            TweenManager.Register(emitter);
            return emitter;
        }

        // One-shot burst that auto-cleans once all particles die, regardless of the
        // config's loop setting.
        public static ParticleEmitter Burst(this VisualElement host, ParticleSystemConfig config, int count)
        {
            var emitter = host.SpawnParticles(config);
            emitter.SetCompleteWhenEmpty();
            emitter.Stop();        // no continuous emission
            emitter.Emit(count);   // single burst
            return emitter;
        }

        // Spawn a built-in preset by kind, no asset needed:
        //   host.SpawnPreset(ParticlePresets.Kind.Sparkle);
        public static ParticleEmitter SpawnPreset(this VisualElement host, ParticlePresets.Kind kind, bool ignoreTimeScale = false)
            => host.SpawnParticles(ParticlePresets.Create(kind), ignoreTimeScale);

        // One-shot burst of a built-in preset:
        //   host.BurstPreset(ParticlePresets.Kind.Confetti, 80);
        public static ParticleEmitter BurstPreset(this VisualElement host, ParticlePresets.Kind kind, int count)
            => host.Burst(ParticlePresets.Create(kind), count);
    }
}
