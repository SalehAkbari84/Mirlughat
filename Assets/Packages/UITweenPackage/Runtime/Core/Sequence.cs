using System;
using System.Collections.Generic;
using UnityEngine;

namespace UITween
{
    /// <summary>
    /// زنجیره‌ای از تویین‌ها که می‌توانند پشت‌سرهم (Append) یا هم‌زمان (Join) اجرا شوند.
    /// مثال: seq.Append(a).Join(b).AppendInterval(0.5f).Append(c).OnComplete(...)
    /// </summary>
    public sealed class Sequence : Tween
    {
        class Entry
        {
            public Tween child;
            public float startTime;
            public bool started;
            public bool completed;
        }

        readonly List<Entry> entries = new List<Entry>();
        float cursor;

        /// <param name="owner">هر Component که به‌عنوان مرجعِ زنده‌بودن Sequence استفاده می‌شود</param>
        public Sequence(Component owner)
        {
            ease = Ease.Linear;
            safetyTarget = owner;
        }

        /// <summary>افزودن یک تویین به انتهای زنجیره (بعد از تمام‌شدن مورد قبلی اجرا می‌شود)</summary>
        public Sequence Append(Tween t)
        {
            UITweenManager.Untrack(t); // فقط خودِ Sequence باید این تویین را درایو کند
            entries.Add(new Entry { child = t, startTime = cursor });
            cursor += t.delay + t.duration;
            duration = Mathf.Max(duration, cursor);
            return this;
        }

        /// <summary>افزودن یک تویین هم‌زمان با آخرین موردِ اضافه‌شده</summary>
        public Sequence Join(Tween t)
        {
            UITweenManager.Untrack(t);
            float insertAt = entries.Count > 0 ? entries[entries.Count - 1].startTime : 0f;
            entries.Add(new Entry { child = t, startTime = insertAt });
            duration = Mathf.Max(duration, insertAt + t.delay + t.duration);
            return this;
        }

        /// <summary>افزودن یک وقفه‌ی خالی به زنجیره</summary>
        public Sequence AppendInterval(float seconds)
        {
            cursor += Mathf.Max(0f, seconds);
            duration = Mathf.Max(duration, cursor);
            return this;
        }

        /// <summary>افزودن یک Callback که در لحظه‌ی مشخصی از زنجیره صدا زده می‌شود</summary>
        public Sequence AppendCallback(Action callback)
        {
            var marker = ValueTween<float>.Get(safetyTarget, 0f, 0f, 0f, _ => { }, Mathf.LerpUnclamped);
            marker.OnComplete(callback);
            entries.Add(new Entry { child = marker, startTime = cursor });
            return this;
        }

        /// <summary>افزودن یک تویین در لحظه‌ی زمانی دقیق (ثانیه از ابتدای Sequence)</summary>
        public Sequence Insert(float atTime, Tween t)
        {
            UITweenManager.Untrack(t);
            entries.Add(new Entry { child = t, startTime = Mathf.Max(0f, atTime) });
            duration = Mathf.Max(duration, atTime + t.delay + t.duration);
            return this;
        }

        internal override void Evaluate(float easedT)
        {
            float absTime = easedT * duration;

            foreach (var e in entries)
            {
                float local = absTime - e.startTime - e.child.delay;
                if (local < 0f) continue;

                if (!e.started)
                {
                    e.started = true;
                    e.child.onStartCb?.Invoke();
                }

                float childDur = e.child.duration;
                float childT = childDur <= 0f ? 1f : Mathf.Clamp01(local / childDur);
                e.child.Evaluate(e.child.EvaluateEase(childT));

                if (childT >= 1f && !e.completed)
                {
                    e.completed = true;
                    e.child.onCompleteCb?.Invoke();
                }
            }
        }
    }
}
