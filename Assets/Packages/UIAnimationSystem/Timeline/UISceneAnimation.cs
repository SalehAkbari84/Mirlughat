using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UIToolkit.Animation.Particles;

namespace UIToolkit.Animation.Timeline
{
    // Central asset that ties a real UXML layout to a set of animation clips and
    // particle bindings. The window loads the UXML (with its USS) for a true
    // preview, then lets you attach animations/particles to elements by name.
    //
    // Create -> UI Toolkit -> Scene Animation
    [CreateAssetMenu(fileName = "NewUIScene", menuName = "UI Toolkit/Scene Animation")]
    public class UISceneAnimation : ScriptableObject
    {
        [Tooltip("The UXML layout this scene animates. Its USS is rendered for preview.")]
        public VisualTreeAsset uxml;

        [Tooltip("Optional extra style sheets to apply on top of the UXML's own styles.")]
        public List<StyleSheet> additionalStyleSheets = new List<StyleSheet>();

        [Tooltip("Editor-only preview canvas size.")]
        public Vector2 previewSize = new Vector2(1920, 1080);
        public Color previewBackground = new Color(0.1f, 0.1f, 0.12f, 1f);

        // Animation clips that play against this layout. Each clip targets
        // elements by name (multi-element clips).
        public List<SceneClipBinding> clips = new List<SceneClipBinding>();

        // Particle systems bound to specific host elements (by name).
        public List<SceneParticleBinding> particles = new List<SceneParticleBinding>();

        [Serializable]
        public class SceneClipBinding
        {
            public string id = "clip";
            public UIAnimationClip clip;
            public PlayTrigger trigger = PlayTrigger.Manual;
            public string rootElementName = ""; // empty = document root
        }

        [Serializable]
        public class SceneParticleBinding
        {
            public string id = "particles";
            public ParticleSystemConfig particleConfig;
            public string hostElementName = "";
            public bool playOnStart = true;
        }
    }
}
