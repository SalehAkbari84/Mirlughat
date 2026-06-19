using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIToolkit.Animation.Timeline
{
    // Plays a multi-element clip. Resolves each named ElementTrack under a root
    // VisualElement, then drives all their property tracks. Registers itself in
    // TweenManager so it stays in sync with the rest of the system.
    public sealed class ClipPlayer : ITweenable
    {
        readonly UIAnimationClip _clip;
        readonly VisualElement _root;
        readonly Dictionary<string, VisualElement> _resolved = new Dictionary<string, VisualElement>();

        float _elapsed;
        int _loopsDone;
        bool _isForward = true;
        bool _started;
        float _lastEventT = -1f;

        readonly Dictionary<PropertyTrack, float> _baseFloat = new Dictionary<PropertyTrack, float>();

        Action _onComplete;
        Action _onStepComplete;

        // Raised when playback crosses a clip event's time. Argument is event name.
        public event Action<string> OnEvent;

        public TweenState State { get; private set; } = TweenState.Pending;
        public bool IgnoreTimeScale => _clip != null && _clip.ignoreTimeScale;
        public object Id { get; private set; }

        public float TotalDuration
        {
            get
            {
                if (_clip == null) return 0f;
                float once = _clip.Duration / Mathf.Max(0.01f, _clip.playbackSpeed);
                int count = (_clip.loop && _clip.loops > 0) ? _clip.loops : 1;
                return once * count;
            }
        }

        // root is the element under which the named targets are searched.
        public ClipPlayer(UIAnimationClip clip, VisualElement root)
        {
            _clip = clip;
            _root = root;
            Resolve();
            CaptureBase();
            // Sample frame 0 immediately so elements start at their initial pose
            // instead of flashing their default pose for one frame.
            Sample(0f);
        }

        // For relative clips: remember each tracked float's starting value so we can
        // add the keyed values on top of the element's existing pose.
        void CaptureBase()
        {
            _baseFloat.Clear();
            if (_clip == null || !_clip.relative) return;
            foreach (var el in _clip.elements)
            {
                if (!_resolved.TryGetValue(el.elementName, out var ve) || ve == null) continue;
                foreach (var pt in el.properties)
                    if (pt.ValueType == PropertyValueType.Float)
                        _baseFloat[pt] = PropertyBinder.ReadFloat(ve, pt.property);
            }
        }

        void Resolve()
        {
            _resolved.Clear();
            if (_clip == null || _root == null) return;
            foreach (var el in _clip.elements)
            {
                if (string.IsNullOrEmpty(el.elementName)) continue;
                VisualElement target = el.elementName == _root.name
                    ? _root
                    : _root.Q<VisualElement>(el.elementName);
                if (target != null) _resolved[el.elementName] = target;
                else Debug.LogWarning($"[ClipPlayer] Element '{el.elementName}' not found under '{_root.name}'.");
            }
        }

        public ClipPlayer SetId(object id) { Id = id; return this; }
        public ClipPlayer OnComplete(Action cb) { _onComplete += cb; return this; }
        public ClipPlayer OnStepComplete(Action cb) { _onStepComplete += cb; return this; }

        public void Pause() { if (State == TweenState.Running) State = TweenState.Paused; }
        public void Play()  { if (State == TweenState.Paused)  State = TweenState.Running; }

        public void Reset()
        {
            _elapsed = 0f; _loopsDone = 0; _isForward = true; _started = false;
            State = TweenState.Pending;
        }

        public void Kill(bool complete = false)
        {
            if (State == TweenState.Killed || State == TweenState.Completed) return;
            if (complete) { Sample(_clip != null ? _clip.Duration : 0f); _onComplete?.Invoke(); }
            State = TweenState.Killed;
        }

        // Seek to an absolute time (seconds) and apply that pose. Optionally pauses
        // playback so gameplay code can scrub a clip. Time is clamped to the clip.
        public void Seek(float time, bool pause = false)
        {
            if (_clip == null) return;
            float dur = _clip.Duration;
            _elapsed = Mathf.Clamp(time, 0f, dur);
            _isForward = true;
            Sample(_elapsed);
            if (pause && State == TweenState.Running) State = TweenState.Paused;
        }

        // Apply all element/property values at absolute time t. Used by the editor preview too.
        public void Sample(float t)
        {
            if (_clip == null) return;
            foreach (var el in _clip.elements)
            {
                if (!_resolved.TryGetValue(el.elementName, out var ve) || ve == null) continue;
                foreach (var pt in el.properties)
                {
                    if (pt.keys.Count == 0) continue;
                    if (pt.ValueType == PropertyValueType.Color)
                    {
                        PropertyBinder.ApplyColor(ve, pt.property, pt.SampleColor(t));
                    }
                    else
                    {
                        float v = pt.SampleFloat(t);
                        if (_clip.relative && _baseFloat.TryGetValue(pt, out var b)) v += b;
                        PropertyBinder.ApplyFloat(ve, pt.property, v);
                    }
                }
            }
        }

        public bool Update(float deltaTime)
        {
            if (State == TweenState.Killed || State == TweenState.Completed) return true;
            if (State == TweenState.Paused) return false;
            if (_clip == null || _root == null) { State = TweenState.Killed; return true; }

            if (!_started) { _started = true; State = TweenState.Running; }

            float dur = _clip.Duration;
            if (dur <= 0f) { Sample(0f); State = TweenState.Completed; _onComplete?.Invoke(); return true; }

            _elapsed += deltaTime * _clip.playbackSpeed;
            float t = Mathf.Clamp(_elapsed, 0f, dur);
            Sample(_isForward ? t : dur - t);

            if (OnEvent != null && _clip.events != null && _clip.events.Count > 0)
            {
                FireEvents(_lastEventT, t);
                _lastEventT = t;
            }

            if (_elapsed >= dur)
            {
                _loopsDone++;
                _onStepComplete?.Invoke();

                bool infinite = _clip.loop && _clip.loops <= 0;
                bool more = _clip.loop && (infinite || _loopsDone < _clip.loops);
                if (!more)
                {
                    State = TweenState.Completed;
                    _onComplete?.Invoke();
                    return true;
                }
                _elapsed = 0f;
                _lastEventT = -1f;
                if (_clip.loopType == LoopType.Yoyo) _isForward = !_isForward;
            }
            return false;
        }

        void FireEvents(float prev, float cur)
        {
            foreach (var ev in _clip.events)
                if (ev.time > prev && ev.time <= cur)
                    OnEvent?.Invoke(ev.name);
        }
    }
}
