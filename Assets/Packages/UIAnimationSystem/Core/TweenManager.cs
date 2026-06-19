using System;
using System.Collections.Generic;
using UnityEngine;

namespace UIToolkit.Animation
{
    /// <summary>
    /// Central runner for all tweens. Automatically creates a hidden GameObject
    /// and updates active tweens each frame. No manual setup needed.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class TweenManager : MonoBehaviour
    {
        static TweenManager _instance;
        static bool _quitting;

        readonly List<ITweenable> _active = new List<ITweenable>();
        readonly List<ITweenable> _toAdd = new List<ITweenable>();

        // completion callbacks (used by the async/await awaiter)
        readonly List<ITweenable> _waitTweens = new List<ITweenable>();
        readonly List<Action> _waitCallbacks = new List<Action>();

        public static TweenManager Instance
        {
            get
            {
                if (_quitting) return null;
                if (_instance == null)
                {
                    var go = new GameObject("[TweenManager]");
                    _instance = go.AddComponent<TweenManager>();
                    DontDestroyOnLoad(go);
                    go.hideFlags = HideFlags.HideAndDontSave;
                }
                return _instance;
            }
        }

        public static void Register(ITweenable t)
        {
            if (Instance == null) return;
            Instance._toAdd.Add(t);
        }

        void Update()
        {
            if (_toAdd.Count > 0)
            {
                _active.AddRange(_toAdd);
                _toAdd.Clear();
            }

            float dt = Time.deltaTime;
            float unscaledDt = Time.unscaledDeltaTime;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var t = _active[i];
                float delta = t.IgnoreTimeScale ? unscaledDt : dt;
                bool done = t.Update(delta);
                if (done || t.State == TweenState.Killed || t.State == TweenState.Completed)
                    _active.RemoveAt(i);
            }

            // fire any completion callbacks whose tween has finished
            for (int i = _waitTweens.Count - 1; i >= 0; i--)
            {
                var t = _waitTweens[i];
                if (t == null || t.State == TweenState.Completed || t.State == TweenState.Killed)
                {
                    var cb = _waitCallbacks[i];
                    _waitTweens.RemoveAt(i);
                    _waitCallbacks.RemoveAt(i);
                    cb?.Invoke();
                }
            }
        }

        // Invoke a callback once the tween completes/dies. Used by the awaiter so
        // 'await element.FadeIn();' resumes on the main thread when it finishes.
        public void WhenComplete(ITweenable t, Action callback)
        {
            if (callback == null) return;
            if (t == null || t.State == TweenState.Completed || t.State == TweenState.Killed) { callback(); return; }
            _waitTweens.Add(t);
            _waitCallbacks.Add(callback);
        }

        /// <summary>Kill all tweens, or only those matching an id.</summary>
        public void KillAll(object id = null, bool complete = false)
        {
            // Include queued-but-not-yet-active tweens so a kill issued the same
            // frame as Register is honored.
            if (_toAdd.Count > 0)
            {
                _active.AddRange(_toAdd);
                _toAdd.Clear();
            }

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (id == null || id.Equals(_active[i].Id))
                    _active[i].Kill(complete);
            }
        }

        void OnApplicationQuit() => _quitting = true;
    }
}
