using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UIToolkit.Animation.Particles;

namespace UIToolkit.Animation.Timeline
{
    // Runtime player for one or MORE UISceneAnimations. Put it on the GameObject
    // that hosts the UI Toolkit panel (Panel Renderer on Unity 6.5+, or UIDocument
    // on older - auto-detected via UIPanel). Plays each scene's Play Order, bound
    // particles, and interaction triggers.
    [AddComponentMenu("UI Toolkit/UI Scene Director")]
    public class UISceneDirector : MonoBehaviour
    {
        [Tooltip("One or more scene animations to play on this UI host.")]
        public List<UISceneAnimation> scenes = new List<UISceneAnimation>();

        VisualElement _root;   // cached panel root once ready

        void OnEnable()
        {
            if (scenes == null || scenes.Count == 0) { Debug.LogWarning("[UISceneDirector] No scene assets assigned.", this); return; }

            UILog.Enabled = scenes.Exists(s => s != null && s.debugLog);   // any scene with Debug > Log on
            UILog.Log($"Director.OnEnable on '{name}' with {scenes.Count} scene(s). Waiting for the UI panel host...");

            UIPanel.WhenReady(gameObject, root =>
            {
                _root = root;
                if (root == null) { UILog.Warn("root was null."); return; }
                UILog.Log($"Root ready: '{root.name}', children={root.childCount}.");

                foreach (var scene in scenes)
                    if (scene != null) PlayScene(scene, root);
            });
        }

        void PlayScene(UISceneAnimation scene, VisualElement root)
        {
            UILog.Log($"Playing scene '{scene.name}'.");

            // particles
            int spawned = 0;
            foreach (var p in scene.particles)
            {
                if (p.particleConfig == null || !p.playOnStart) continue;
                VisualElement host = string.IsNullOrEmpty(p.hostElementName)
                    ? root : root.Q<VisualElement>(p.hostElementName) ?? root;
                host.SpawnParticles(p.particleConfig);
                spawned++;
            }

            // ordered playlist (Play Order) - only the start section (empty = all)
            if (scene.playSequenceOnStart)
                UISequenceRunner.Play(scene, root, scene.startSection);

            // interaction triggers (click / hold / hover / ...)
            WireTriggers(scene, root);

            bool willPlay =
                (scene.playSequenceOnStart && scene.sequence != null && scene.sequence.Exists(s => s.clip != null || s.particle != null))
                || (scene.particles != null && scene.particles.Exists(p => p.playOnStart && p.particleConfig != null))
                || (scene.triggers != null && scene.triggers.Count > 0);
            if (!willPlay)
                Debug.LogWarning($"[UISceneDirector] Scene '{scene.name}' has nothing scheduled to play. Add clips to Play Order and tick 'Play sequence on start', add Interaction Triggers, or call from code.", this);
        }

        // Hook up the event-driven interaction triggers. Pointer events cover both
        // mouse and touch; the platform filter lets you make some desktop- or
        // mobile-only (e.g. hover only makes sense on desktop).
        void WireTriggers(UISceneAnimation scene, VisualElement root)
        {
            if (scene.triggers == null) return;
            bool mobile = Application.isMobilePlatform;
            foreach (var tr in scene.triggers)
            {
                if (tr == null || tr.clip == null || string.IsNullOrEmpty(tr.elementName)) continue;
                if (!PlatformMatches(tr.platform, mobile)) continue;

                var el = root.Q<VisualElement>(tr.elementName);
                if (el == null) { UILog.Warn($"Trigger element '{tr.elementName}' not found in UXML."); continue; }

                el.PlayOnClip(tr.trigger, tr.clip, tr.loops, tr.loopType,
                    tr.ignoreWhilePlaying, tr.cooldown, tr.holdSeconds);
            }
        }

        static bool PlatformMatches(TriggerPlatform p, bool mobile)
        {
            switch (p)
            {
                case TriggerPlatform.DesktopOnly: return !mobile;
                case TriggerPlatform.MobileOnly: return mobile;
                default: return true;
            }
        }

        // ----- manual control (from code / UI buttons) -----

        // Play the Play Order of every assigned scene.
        public void PlaySequence()
        {
            if (_root == null) return;
            foreach (var s in scenes) if (s != null) UISequenceRunner.Play(s, _root);
        }

        // Play the Play Order of one specific scene.
        public Sequence PlaySequence(UISceneAnimation scene)
        {
            if (scene == null || _root == null) return null;
            return UISequenceRunner.Play(scene, _root);
        }

        // Play a specific Play Order SECTION (searches all assigned scenes for it).
        public Sequence PlaySection(string section)
        {
            if (_root == null || string.IsNullOrEmpty(section)) return null;
            foreach (var scene in scenes)
            {
                if (scene == null || scene.sequence == null) continue;
                if (scene.sequence.Exists(s => s != null && s.section == section))
                    return UISequenceRunner.Play(scene, _root, section);
            }
            return null;
        }

        // Play a section from a specific scene.
        public Sequence PlaySection(UISceneAnimation scene, string section)
        {
            if (scene == null || _root == null) return null;
            return UISequenceRunner.Play(scene, _root, section);
        }

        // Spawn a particle binding by id, searching across all assigned scenes.
        public ParticleEmitter PlayParticles(string id)
        {
            if (_root == null) return null;
            foreach (var scene in scenes)
            {
                if (scene == null) continue;
                var p = scene.particles.Find(x => x.id == id);
                if (p == null || p.particleConfig == null) continue;
                VisualElement host = string.IsNullOrEmpty(p.hostElementName)
                    ? _root : _root.Q<VisualElement>(p.hostElementName) ?? _root;
                return host.SpawnParticles(p.particleConfig);
            }
            return null;
        }
    }
}
