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

        // Particle systems bound to specific host elements (by name).
        public List<SceneParticleBinding> particles = new List<SceneParticleBinding>();

        // Event-driven triggers: play a clip when an element gets an interaction
        // (click / hold / hover / ...). Separate from the Play Order (which is
        // start-time); these fire whenever the event happens during gameplay.
        public List<InteractionTrigger> triggers = new List<InteractionTrigger>();

        [Serializable]
        public class InteractionTrigger
        {
            public string elementName = "";
            public UITrigger trigger = UITrigger.Click;
            public UIAnimationClip clip;
            public TriggerPlatform platform = TriggerPlatform.Both;
            public int loops = 1;                 // 1 = once, 0 = forever, N = N times
            public LoopType loopType = LoopType.Restart;
            [Min(0.05f)] public float holdSeconds = 0.5f;  // for the Hold trigger

            // Anti-spam: when on, re-firing while the clip still plays is ignored
            // (it must finish first). When off, re-firing restarts it (one instance).
            public bool ignoreWhilePlaying = true;
            [Min(0f)] public float cooldown = 0f;          // min seconds between fires
        }

        // The single ordered playlist of clips (the "Play Order"). Steps play in
        // order; each step sets its own repeat. This is the one place clips are
        // scheduled. For on-demand playback from code use UIAnimation.Play(name, root).
        public List<SequenceStep> sequence = new List<SequenceStep>();
        [Tooltip("Play the sequence automatically when the scene director enables.")]
        public bool playSequenceOnStart = true;

        [Tooltip("Trace the runtime playback to the Console (Setup > Debug > Log).")]
        public bool debugLog = false;

        [Serializable]
        public class SequenceStep
        {
            public string id = "step";
            public UIAnimationClip clip;              // optional: clip to play
            public string rootElementName = "";       // empty = document root
            [Min(0f)] public float delay = 0f;        // wait before this step
            public int loops = 1;                     // 1 = once, 0 = repeat forever, N = N times
            public LoopType loopType = LoopType.Restart;
            [Tooltip("On = start after the previous step finishes. Off = play together with the previous step.")]
            public bool waitForPrevious = true;

            // Optional: a step can also (or instead) fire a particle effect at its
            // start time, so particles live on the same timeline as clips.
            public Particles.ParticleSystemConfig particle;
            public string particleHost = "";          // element to host particles (empty = root)
            public int particleBurst = 0;             // 0 = continuous, N>0 = one-shot burst of N
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
