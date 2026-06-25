using UnityEngine;
using UnityEngine.UIElements;
using UIToolkit.Animation.Particles;

namespace UIToolkit.Animation.Timeline
{
    // Runtime player for a whole UISceneAnimation. Put it on the GameObject that
    // hosts the UI Toolkit panel (Panel Renderer on Unity 6.5+, or UIDocument on
    // older versions - auto-detected via UIPanel). Plays the Play Order, bound
    // particles, and interaction triggers.
    [AddComponentMenu("UI Toolkit/UI Scene Director")]
    public class UISceneDirector : MonoBehaviour
    {
        public UISceneAnimation scene;

        VisualElement _root;   // cached panel root once ready

        void OnEnable()
        {
            if (scene == null) { Debug.LogWarning("[UISceneDirector] No scene asset assigned.", this); return; }

            UILog.Enabled = scene.debugLog;   // Setup > Debug > Log
            UILog.Log($"Director.OnEnable on '{name}'. Waiting for the UI panel host...");

            UIPanel.WhenReady(gameObject, root =>
            {
                _root = root;
                if (root == null) { UILog.Warn("root was null."); return; }
                UILog.Log($"Root ready: '{root.name}', children={root.childCount}.");

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
                UILog.Log($"Particles spawned on start: {spawned}.");

                // ordered playlist (Play Order)
                if (scene.playSequenceOnStart)
                {
                    UILog.Log($"playSequenceOnStart = true; sequence steps = {(scene.sequence != null ? scene.sequence.Count : 0)}. Playing.");
                    UISequenceRunner.Play(scene, root);
                }
                else UILog.Log("playSequenceOnStart = false (Play Order will NOT auto-play).");

                // interaction triggers (click / hold / hover / ...)
                WireTriggers(root);

                bool willPlay =
                    (scene.playSequenceOnStart && scene.sequence != null && scene.sequence.Exists(s => s.clip != null || s.particle != null))
                    || (scene.particles != null && scene.particles.Exists(p => p.playOnStart && p.particleConfig != null))
                    || (scene.triggers != null && scene.triggers.Count > 0);
                if (!willPlay)
                    Debug.LogWarning("[UISceneDirector] Nothing is scheduled to play. Add clips to Play Order and tick 'Play sequence on start', add Interaction Triggers, or call PlaySequence()/UIAnimation.Play from code.", this);
            });
        }

        // Hook up the event-driven interaction triggers. Pointer events cover both
        // mouse and touch; the platform filter lets you make some desktop- or
        // mobile-only (e.g. hover only makes sense on desktop).
        void WireTriggers(VisualElement root)
        {
            if (scene.triggers == null) return;
            bool mobile = Application.isMobilePlatform;
            int wired = 0;
            foreach (var tr in scene.triggers)
            {
                if (tr == null || tr.clip == null || string.IsNullOrEmpty(tr.elementName)) continue;
                if (!PlatformMatches(tr.platform, mobile)) continue;

                var el = root.Q<VisualElement>(tr.elementName);
                if (el == null) { UILog.Warn($"Trigger element '{tr.elementName}' not found in UXML."); continue; }

                el.PlayOnClip(tr.trigger, tr.clip, tr.loops, tr.loopType,
                    tr.ignoreWhilePlaying, tr.cooldown, tr.holdSeconds);
                wired++;
            }
            UILog.Log($"Interaction triggers wired: {wired}.");
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

        // Play the scene's Play Order from code/UI buttons. Uses the cached root,
        // which is set once the panel is ready (after OnEnable).
        public Sequence PlaySequence()
        {
            if (scene == null || _root == null) return null;
            return UISequenceRunner.Play(scene, _root);
        }

        public ParticleEmitter PlayParticles(string id)
        {
            if (scene == null || _root == null) return null;
            var p = scene.particles.Find(x => x.id == id);
            if (p == null || p.particleConfig == null) return null;
            VisualElement host = string.IsNullOrEmpty(p.hostElementName)
                ? _root : _root.Q<VisualElement>(p.hostElementName) ?? _root;
            return host.SpawnParticles(p.particleConfig);
        }
    }
}
