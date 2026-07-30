using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIToolkit.Animation.Timeline
{
    // Play animation clips by name from gameplay code.
    //
    //   UIAnimation.Play("MyIntro", rootVisualElement);
    //
    // A clip is found by, in order:
    //   1. an explicit Register call, then
    //   2. Resources/UIAnimations/name.asset  (the folder the editor saves to).
    //
    // So any clip you create with the Scene Animator (saved under
    // Assets/Resources/UIAnimations) is callable by its asset name with no setup.
    public static class UIAnimation
    {
        // Resources sub-path used as a fallback location for individual clips.
        public const string ResourcesFolder = "UISA/Animations";

        static readonly Dictionary<string, UIAnimationClip> _registry = new Dictionary<string, UIAnimationClip>();
        static bool _librariesLoaded;

        // Auto-register every clip listed in any UIAnimationLibrary under Resources.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoLoadLibraries()
        {
            _librariesLoaded = false;
            EnsureLibraries();
        }

        static void EnsureLibraries()
        {
            if (_librariesLoaded) return;
            _librariesLoaded = true;
            var libs = Resources.LoadAll<UIAnimationLibrary>("");
            foreach (var lib in libs)
            {
                if (lib == null) continue;
                foreach (var clip in lib.clips)
                    if (clip != null) _registry[clip.name] = clip;
            }
        }

        // Register a clip under its own asset name.
        public static void Register(UIAnimationClip clip)
        {
            if (clip != null) _registry[clip.name] = clip;
        }

        // Register a clip under a custom name.
        public static void Register(string name, UIAnimationClip clip)
        {
            if (clip != null && !string.IsNullOrEmpty(name)) _registry[name] = clip;
        }

        public static bool Unregister(string name) => _registry.Remove(name);
        public static void Clear() => _registry.Clear();

        // Look up a clip by name (registry first, then Resources). Caches the result.
        public static UIAnimationClip Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            EnsureLibraries();
            if (_registry.TryGetValue(name, out var cached) && cached != null) return cached;

            var loaded = Resources.Load<UIAnimationClip>(ResourcesFolder + "/" + name);
            if (loaded != null) _registry[name] = loaded;
            return loaded;
        }

        public static bool Has(string name) => Get(name) != null;

        // Play a named clip on a root element. Named targets inside the clip are
        // resolved under this root. Returns the player (or null if not found).
        public static ClipPlayer Play(string name, VisualElement root)
        {
            var clip = Get(name);
            if (clip == null)
            {
                Debug.LogWarning($"[UIAnimation] No clip named '{name}'. Save it under Resources/{ResourcesFolder} or call UIAnimation.Register.");
                return null;
            }
            if (root == null)
            {
                Debug.LogWarning($"[UIAnimation] Cannot play '{name}': root element is null.");
                return null;
            }
            return clip.Play(root);
        }
    }
}
