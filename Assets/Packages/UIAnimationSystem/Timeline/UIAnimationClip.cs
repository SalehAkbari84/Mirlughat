using System;
using System.Collections.Generic;
using UnityEngine;

namespace UIToolkit.Animation.Timeline
{
    // A single keyframe: time + value + easing toward the next key.
    [Serializable]
    public class UIKeyframe
    {
        public float time;
        public float floatValue;
        public Color colorValue = Color.white;
        public Ease ease = Ease.OutQuad;
        public bool useCurve;
        public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);

        public UIKeyframe() { }
        public UIKeyframe(float t, float v, Ease e = Ease.OutQuad) { time = t; floatValue = v; ease = e; }
        public UIKeyframe(float t, Color c, Ease e = Ease.OutQuad) { time = t; colorValue = c; ease = e; }

        public UIKeyframe Clone()
        {
            return new UIKeyframe
            {
                time = time, floatValue = floatValue, colorValue = colorValue,
                ease = ease, useCurve = useCurve,
                curve = new AnimationCurve(curve.keys)
            };
        }
    }

    // All keyframes for one property of one element.
    [Serializable]
    public class PropertyTrack
    {
        public AnimatableProperty property;
        public List<UIKeyframe> keys = new List<UIKeyframe>();

        public PropertyValueType ValueType => PropertyMeta.TypeOf(property);

        public void SortKeys() => keys.Sort((a, b) => a.time.CompareTo(b.time));

        public float Duration
        {
            get { float m = 0f; foreach (var k in keys) if (k.time > m) m = k.time; return m; }
        }

        public float SampleFloat(float t)
        {
            if (keys.Count == 0) return 0f;
            if (keys.Count == 1) return keys[0].floatValue;
            if (t <= keys[0].time) return keys[0].floatValue;
            if (t >= keys[keys.Count - 1].time) return keys[keys.Count - 1].floatValue;
            for (int i = 0; i < keys.Count - 1; i++)
            {
                var a = keys[i]; var b = keys[i + 1];
                if (t >= a.time && t <= b.time)
                {
                    float span = b.time - a.time;
                    float local = span <= 0f ? 0f : (t - a.time) / span;
                    float eased = a.useCurve ? Easing.Evaluate(a.curve, local) : Easing.Evaluate(a.ease, local);
                    return Mathf.LerpUnclamped(a.floatValue, b.floatValue, eased);
                }
            }
            return keys[keys.Count - 1].floatValue;
        }

        public Color SampleColor(float t)
        {
            if (keys.Count == 0) return Color.white;
            if (keys.Count == 1) return keys[0].colorValue;
            if (t <= keys[0].time) return keys[0].colorValue;
            if (t >= keys[keys.Count - 1].time) return keys[keys.Count - 1].colorValue;
            for (int i = 0; i < keys.Count - 1; i++)
            {
                var a = keys[i]; var b = keys[i + 1];
                if (t >= a.time && t <= b.time)
                {
                    float span = b.time - a.time;
                    float local = span <= 0f ? 0f : (t - a.time) / span;
                    float eased = a.useCurve ? Easing.Evaluate(a.curve, local) : Easing.Evaluate(a.ease, local);
                    return Color.LerpUnclamped(a.colorValue, b.colorValue, eased);
                }
            }
            return keys[keys.Count - 1].colorValue;
        }
    }

    // All property tracks for one target element (referenced by name).
    [Serializable]
    public class ElementTrack
    {
        public string elementName = "element";
        public bool expanded = true;
        public List<PropertyTrack> properties = new List<PropertyTrack>();

        public float Duration
        {
            get { float m = 0f; foreach (var p in properties) if (p.Duration > m) m = p.Duration; return m; }
        }

        public PropertyTrack GetOrCreate(AnimatableProperty prop)
        {
            foreach (var p in properties) if (p.property == prop) return p;
            var t = new PropertyTrack { property = prop };
            properties.Add(t);
            return t;
        }
    }

    // A named event fired at a time during playback (synced to gameplay/SFX).
    [Serializable]
    public class ClipEvent
    {
        public float time;
        public string name = "event";
    }

    // A full animation clip. Can target multiple elements at once.
    // Create -> UI Toolkit -> Animation Clip
    [CreateAssetMenu(fileName = "NewUIAnimClip", menuName = "UI Toolkit/Animation Clip")]
    public class UIAnimationClip : ScriptableObject
    {
        public List<ElementTrack> elements = new List<ElementTrack>();

        public bool loop;
        public LoopType loopType = LoopType.Restart;
        [Min(0)] public int loops = 1; // 0 = infinite when loop is on
        public bool ignoreTimeScale;
        [Min(0.01f)] public float playbackSpeed = 1f;

        [Tooltip("When on, float values are added on top of each element's pose at play time (relative animation).")]
        public bool relative;

        // Timed events raised during playback (ClipPlayer.OnEvent).
        public List<ClipEvent> events = new List<ClipEvent>();

        public float Duration
        {
            get { float m = 0f; foreach (var e in elements) if (e.Duration > m) m = e.Duration; return m; }
        }

        public ElementTrack GetOrCreateElement(string name)
        {
            foreach (var e in elements) if (e.elementName == name) return e;
            var t = new ElementTrack { elementName = name };
            elements.Add(t);
            return t;
        }

        public void RemoveElement(string name) => elements.RemoveAll(e => e.elementName == name);
    }
}
