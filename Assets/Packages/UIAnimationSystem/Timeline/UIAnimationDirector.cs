using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIToolkit.Animation.Timeline
{
    public enum PlayTrigger { OnEnable, OnStart, Manual }

    // One animation entry on the director. Each entry pairs a clip with a
    // trigger and an optional sub-root element name under which the clip's
    // element tracks are resolved.
    [Serializable]
    public class AnimationEntry
    {
        public string id = "anim";
        public UIAnimationClip clip;
        public PlayTrigger trigger = PlayTrigger.Manual;
        [Tooltip("Optional. Name of a sub-element to use as the resolve root. Empty = document root.")]
        public string rootElementName = "";
        public bool playOnAwakeIfEnable = true;

        [NonSerialized] public ClipPlayer runtimePlayer;
    }

    // Central animation controller. Put ONE of these on the GameObject that hosts
    // the UI Toolkit panel (Panel Renderer on 6.5+, or UIDocument on older - via
    // UIPanel). Add as many AnimationEntry items as you like.
    [AddComponentMenu("UI Toolkit/UI Animation Director")]
    public class UIAnimationDirector : MonoBehaviour
    {
        public List<AnimationEntry> animations = new List<AnimationEntry>();

        VisualElement _root;   // cached panel root once ready

        void OnEnable()
        {
            UIPanel.WhenReady(gameObject, root =>
            {
                _root = root;
                foreach (var a in animations)
                    if (a.trigger == PlayTrigger.OnEnable || a.trigger == PlayTrigger.OnStart)
                        PlayInternal(a);
            });
        }

        // Play by id (call from code, UnityEvents, or UI buttons).
        public ClipPlayer Play(string id)
        {
            var entry = animations.Find(a => a.id == id);
            if (entry == null) { Debug.LogWarning($"[Director] No animation with id '{id}'."); return null; }
            return PlayInternal(entry);
        }

        public void Stop(string id)
        {
            var entry = animations.Find(a => a.id == id);
            entry?.runtimePlayer?.Kill();
        }

        public void StopAll()
        {
            foreach (var a in animations) a.runtimePlayer?.Kill();
        }

        ClipPlayer PlayInternal(AnimationEntry entry)
        {
            if (entry.clip == null) { Debug.LogWarning($"[Director] Entry '{entry.id}' has no clip."); return null; }
            if (_root == null) return null;

            VisualElement resolveRoot = string.IsNullOrEmpty(entry.rootElementName)
                ? _root
                : _root.Q<VisualElement>(entry.rootElementName) ?? _root;

            entry.runtimePlayer?.Kill();
            entry.runtimePlayer = entry.clip.Play(resolveRoot);
            return entry.runtimePlayer;
        }
    }
}
