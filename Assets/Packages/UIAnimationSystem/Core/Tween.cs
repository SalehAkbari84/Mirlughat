using System;
using UnityEngine;

namespace UIToolkit.Animation
{
    public enum LoopType { Restart, Yoyo, Incremental }
    public enum TweenState { Pending, Running, Paused, Completed, Killed }

    /// <summary>Base contract for anything playable on the timeline (Tween or Sequence).</summary>
    public interface ITweenable
    {
        bool Update(float deltaTime);   // returns true when finished
        void Reset();
        float TotalDuration { get; }
        TweenState State { get; }
        void Kill(bool complete = false);

        // Exposed on the interface so the manager never needs reflection to read them.
        object Id { get; }
        bool IgnoreTimeScale { get; }
    }

    /// <summary>
    /// A generic tween over a value of type T. The core of the animation engine.
    /// </summary>
    public sealed class Tween<T> : ITweenable
    {
        readonly Func<T> _getter;
        readonly Action<T> _setter;
        readonly Func<T, T, float, T> _lerp;

        T _start;
        T _end;
        bool _hasExplicitStart;

        float _duration;
        float _delay;
        float _elapsed;
        float _delayElapsed;

        Ease _ease = Ease.OutQuad;
        AnimationCurve _curve;

        int _loops = 1;
        int _loopsDone;
        LoopType _loopType = LoopType.Restart;
        bool _isForward = true;

        bool _ignoreTimeScale;

        Action _onStart;
        Action<float> _onUpdate;
        Action _onComplete;
        Action _onStepComplete;
        Action _onKill;

        bool _started;

        public TweenState State { get; private set; } = TweenState.Pending;
        public float TotalDuration => _delay + _duration * (_loops < 0 ? 1 : _loops);
        public object Id { get; private set; }

        public Tween(Func<T> getter, Action<T> setter, T end, float duration, Func<T, T, float, T> lerp)
        {
            _getter = getter;
            _setter = setter;
            _end = end;
            _duration = Mathf.Max(0.0001f, duration);
            _lerp = lerp;
        }

        // ---------- Fluent API ----------
        public Tween<T> From(T startValue) { _start = startValue; _hasExplicitStart = true; return this; }
        public Tween<T> SetEase(Ease ease) { _ease = ease; _curve = null; return this; }
        public Tween<T> SetEase(AnimationCurve curve) { _curve = curve; return this; }
        public Tween<T> SetDelay(float delay) { _delay = Mathf.Max(0f, delay); return this; }
        public Tween<T> SetLoops(int loops, LoopType type = LoopType.Restart) { _loops = loops; _loopType = type; return this; }
        public Tween<T> SetId(object id) { Id = id; return this; }
        public Tween<T> SetIgnoreTimeScale(bool ignore = true) { _ignoreTimeScale = ignore; return this; }

        public Tween<T> OnStart(Action cb) { _onStart += cb; return this; }
        public Tween<T> OnUpdate(Action<float> cb) { _onUpdate += cb; return this; }
        public Tween<T> OnComplete(Action cb) { _onComplete += cb; return this; }
        public Tween<T> OnStepComplete(Action cb) { _onStepComplete += cb; return this; }
        public Tween<T> OnKill(Action cb) { _onKill += cb; return this; }

        public bool IgnoreTimeScale => _ignoreTimeScale;

        // ---------- Control ----------
        public void Pause() { if (State == TweenState.Running) State = TweenState.Paused; }
        public void Play() { if (State == TweenState.Paused) State = TweenState.Running; }
        public void Restart() { Reset(); State = TweenState.Running; }

        public void Reset()
        {
            _elapsed = 0f;
            _delayElapsed = 0f;
            _loopsDone = 0;
            _isForward = true;
            _started = false;
            State = TweenState.Pending;
        }

        public void Kill(bool complete = false)
        {
            if (State == TweenState.Killed || State == TweenState.Completed) return;
            if (complete) { ApplyProgress(1f); _onComplete?.Invoke(); }
            State = TweenState.Killed;
            _onKill?.Invoke();
        }

        void EnsureStart()
        {
            if (_started) return;
            if (!_hasExplicitStart && _getter != null) _start = _getter();
            _started = true;
            State = TweenState.Running;
            _onStart?.Invoke();
        }

        void ApplyProgress(float linearT)
        {
            float eased = _curve != null ? Easing.Evaluate(_curve, linearT) : Easing.Evaluate(_ease, linearT);
            _setter(_lerp(_start, _end, eased));
            _onUpdate?.Invoke(linearT);
        }

        public bool Update(float deltaTime)
        {
            if (State == TweenState.Killed || State == TweenState.Completed) return true;
            if (State == TweenState.Paused) return false;

            // delay
            if (_delayElapsed < _delay)
            {
                _delayElapsed += deltaTime;
                if (_delayElapsed < _delay) return false;
                deltaTime = _delayElapsed - _delay;
            }

            EnsureStart();

            _elapsed += deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            float displayT = _isForward ? t : 1f - t;
            ApplyProgress(displayT);

            if (t >= 1f)
            {
                _loopsDone++;
                _onStepComplete?.Invoke();

                bool infinite = _loops < 0;
                if (!infinite && _loopsDone >= _loops)
                {
                    State = TweenState.Completed;
                    _onComplete?.Invoke();
                    return true;
                }

                // Carry the overshoot into the next loop so we do not lose time
                // at each boundary (prevents drift over many loops).
                _elapsed = Mathf.Max(0f, _elapsed - _duration);
                switch (_loopType)
                {
                    case LoopType.Yoyo: _isForward = !_isForward; break;
                    case LoopType.Incremental:
                        // Extend the range by one delta: newEnd = end + (end - start).
                        // Must be computed before overwriting _start.
                        T nextEnd = _lerp(_start, _end, 2f);
                        _start = _end;
                        _end = nextEnd;
                        break;
                }
            }
            return false;
        }
    }
}
