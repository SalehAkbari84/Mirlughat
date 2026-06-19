using UnityEngine;
using UnityEngine.UIElements;
using UIToolkit.Animation.Particles;

namespace UIToolkit.Animation.Timeline
{
    // Runtime player for a whole UISceneAnimation. Put on the GameObject with the
    // UIDocument that uses the same UXML. Plays bound clips and particle systems.
    [AddComponentMenu("UI Toolkit/UI Scene Director")]
    [RequireComponent(typeof(UIDocument))]
    public class UISceneDirector : MonoBehaviour
    {
        public UISceneAnimation scene;

        UIDocument _doc;

        void Awake() => _doc = GetComponent<UIDocument>();

        void OnEnable()
        {
            if (scene == null) return;
            if (_doc == null) _doc = GetComponent<UIDocument>();
            var root = _doc != null ? _doc.rootVisualElement : null;
            if (root == null) return;

            root.schedule.Execute(() =>
            {
                // clips
                foreach (var c in scene.clips)
                {
                    if (c.clip == null) continue;
                    if (c.trigger == PlayTrigger.OnEnable || c.trigger == PlayTrigger.OnStart)
                    {
                        VisualElement r = string.IsNullOrEmpty(c.rootElementName)
                            ? root : root.Q<VisualElement>(c.rootElementName) ?? root;
                        c.clip.Play(r);
                    }
                }

                // particles
                foreach (var p in scene.particles)
                {
                    if (p.particleConfig == null || !p.playOnStart) continue;
                    VisualElement host = string.IsNullOrEmpty(p.hostElementName)
                        ? root : root.Q<VisualElement>(p.hostElementName) ?? root;
                    host.SpawnParticles(p.particleConfig);
                }
            });
        }

        // Play a single clip binding by id (from code or UI buttons).
        public ClipPlayer PlayClip(string id)
        {
            if (scene == null || _doc == null) return null;
            var root = _doc.rootVisualElement;
            var c = scene.clips.Find(x => x.id == id);
            if (c == null || c.clip == null) return null;
            VisualElement r = string.IsNullOrEmpty(c.rootElementName)
                ? root : root.Q<VisualElement>(c.rootElementName) ?? root;
            return c.clip.Play(r);
        }

        public ParticleEmitter PlayParticles(string id)
        {
            if (scene == null || _doc == null) return null;
            var root = _doc.rootVisualElement;
            var p = scene.particles.Find(x => x.id == id);
            if (p == null || p.particleConfig == null) return null;
            VisualElement host = string.IsNullOrEmpty(p.hostElementName)
                ? root : root.Q<VisualElement>(p.hostElementName) ?? root;
            return host.SpawnParticles(p.particleConfig);
        }
    }
}
