using System;
using System.Collections.Generic;
using UnityEngine;

namespace UIToolkit.Animation
{
    /// <summary>
    /// Compose tweens/sequences sequentially (Append), simultaneously (Join), or at a time (Insert).
    /// </summary>
    public sealed class Sequence : ITweenable
    {
        struct Entry
        {
            public ITweenable Tween;
            public float StartTime;     // start offset within the sequence
            public bool Started;
            public Action Callback;     // set by AppendCallback / InsertCallback
            public bool IsCallback;
            public bool CallbackFired;
        }

        readonly List<Entry> _entries = new List<Entry>();
        float _elapsed;
        float _cursor;          // running insert point used by Append
        float _duration;

        int _loops = 1;
        int _loopsDone;
        LoopType _loopType = LoopType.Restart;

        Action _onComplete;
        Action _onStart;
        Action _onStepComplete;
        Action _onKill;
        bool _started;
        bool _ignoreTimeScale;

        public TweenState State { get; private set; } = TweenState.Pending;
        public float TotalDuration => _duration * (_loops < 0 ? 1 : Mathf.Max(1, _loops));
        public bool IgnoreTimeScale => _ignoreTimeScale;
        public object Id { get; private set; }

        // ---------- Composition ----------
        /// <summary>Append a tween to run after the current end of the sequence.</summary>
        public Sequence Append(ITweenable t)
        {
            _entries.Add(new Entry { Tween = t, StartTime = _cursor });
            _cursor += t.TotalDuration;
            _duration = Mathf.Max(_duration, _cursor);
            return this;
        }

        /// <summary>Join a tween so it starts at the same time as the previous entry.</summary>
        public Sequence Join(ITweenable t)
        {
            float joinStart = _entries.Count > 0 ? _entries[_entries.Count - 1].StartTime : 0f;
            _entries.Add(new Entry { Tween = t, StartTime = joinStart });
            _duration = Mathf.Max(_duration, joinStart + t.TotalDuration);
            _cursor = Mathf.Max(_cursor, joinStart + t.TotalDuration);
            return this;
        }

        /// <summary>Insert a tween at an absolute time within the sequence.</summary>
        public Sequence Insert(float atTime, ITweenable t)
        {
            _entries.Add(new Entry { Tween = t, StartTime = atTime });
            _duration = Mathf.Max(_duration, atTime + t.TotalDuration);
            return this;
        }

        /// <summary>Append empty time (a gap) before the next entry.</summary>
        public Sequence AppendInterval(float interval) { _cursor += interval; _duration = Mathf.Max(_duration, _cursor); return this; }

        /// <summary>Append a callback that fires at the current end of the sequence.</summary>
        public Sequence AppendCallback(Action cb)
        {
            _entries.Add(new Entry { IsCallback = true, Callback = cb, StartTime = _cursor });
            return this;
        }

        public Sequence InsertCallback(float atTime, Action cb)
        {
            _entries.Add(new Entry { IsCallback = true, Callback = cb, StartTime = atTime });
            return this;
        }

        // ---------- Fluent ----------
        public Sequence SetLoops(int loops, LoopType type = LoopType.Restart) { _loops = loops; _loopType = type; return this; }
        public Sequence SetId(object id) { Id = id; return this; }
        public Sequence SetIgnoreTimeScale(bool ignore = true) { _ignoreTimeScale = ignore; return this; }
        public Sequence OnStart(Action cb) { _onStart += cb; return this; }
        public Sequence OnComplete(Action cb) { _onComplete += cb; return this; }
        public Sequence OnStepComplete(Action cb) { _onStepComplete += cb; return this; }
        public Sequence OnKill(Action cb) { _onKill += cb; return this; }

        // ---------- Control ----------
        public void Pause() { if (State == TweenState.Running) State = TweenState.Paused; }
        public void Play() { if (State == TweenState.Paused) State = TweenState.Running; }
        public void Restart() { Reset(); State = TweenState.Running; }

        public void Reset()
        {
            _elapsed = 0f;
            _loopsDone = 0;
            _started = false;
            State = TweenState.Pending;
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                e.Started = false;
                e.CallbackFired = false;
                e.Tween?.Reset();
                _entries[i] = e;
            }
        }

        public void Kill(bool complete = false)
        {
            if (State == TweenState.Killed || State == TweenState.Completed) return;
            if (complete)
            {
                foreach (var e in _entries) e.Tween?.Kill(true);
                _onComplete?.Invoke();
            }
            else
            {
                foreach (var e in _entries) e.Tween?.Kill(false);
            }
            State = TweenState.Killed;
            _onKill?.Invoke();
        }

        public bool Update(float deltaTime)
        {
            if (State == TweenState.Killed || State == TweenState.Completed) return true;
            if (State == TweenState.Paused) return false;

            if (!_started) { _started = true; State = TweenState.Running; _onStart?.Invoke(); }

            _elapsed += deltaTime;

            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];

                if (e.IsCallback)
                {
                    if (!e.CallbackFired && _elapsed >= e.StartTime)
                    {
                        e.Callback?.Invoke();
                        e.CallbackFired = true;
                        _entries[i] = e;
                    }
                    continue;
                }

                if (_elapsed < e.StartTime) continue;

                if (!e.Started)
                {
                    // First tick: advance only by how far past the start time we
                    // are, not the whole frame, so late entries don't jump ahead.
                    e.Started = true; _entries[i] = e;
                    float firstDelta = Mathf.Min(deltaTime, _elapsed - e.StartTime);
                    e.Tween.Update(Mathf.Max(0f, firstDelta));
                }
                else
                {
                    e.Tween.Update(deltaTime);
                }
            }

            if (_elapsed >= _duration)
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

                // Carry the overshoot so looping sequences don't drift.
                _elapsed = Mathf.Max(0f, _elapsed - _duration);
                for (int i = 0; i < _entries.Count; i++)
                {
                    var e = _entries[i];
                    e.Started = false;
                    e.CallbackFired = false;
                    e.Tween?.Reset();
                    _entries[i] = e;
                }
            }
            return false;
        }
    }
}
