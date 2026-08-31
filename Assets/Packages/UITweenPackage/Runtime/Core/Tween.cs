using System;
using System.Collections.Generic;
using UnityEngine;

namespace UITween
{
    public enum LoopType
    {
        Restart,
        Yoyo
    }

    /// <summary>تنظیمات سراسری پکیج</summary>
    public static class UITweenSettings
    {
        public static Ease DefaultEase = Ease.OutQuad;

        /// <summary>
        /// حداکثر deltaTime مجاز در هر فریم برای پیشروی تویین‌ها. اگر فریمی به هر دلیلی
        /// (بارگذاری صحنه، گیرکردن لحظه‌ای — مخصوصاً روی موبایل) خیلی طول بکشد، بدون این سقف
        /// یک deltaTime بزرگ می‌تواند باعث شود انیمیشن یک‌مرتبه به انتها بپرد به‌جای اجرای تدریجی.
        /// </summary>
        public static float MaxDeltaTime = 0.1f;
    }

    /// <summary>کلاس پایه‌ی همه‌ی تویین‌ها. متدهای زنجیره‌ای (Fluent API) اینجا تعریف شده‌اند.</summary>
    public abstract class Tween
    {
        internal bool killed;
        internal bool started;
        internal bool isPlaying = true;

        internal float delay;
        internal float delayElapsed;
        internal float duration;
        internal float elapsed;

        internal Ease ease = UITweenSettings.DefaultEase;
        internal AnimationCurve customCurve;

        internal int loops = 1;
        internal LoopType loopType = LoopType.Restart;
        internal int completedLoops;
        internal bool useUnscaledTime;

        internal UnityEngine.Object safetyTarget;

        internal Action onStartCb;
        internal Action onUpdateCb;
        internal Action onCompleteCb;
        internal Action onKillCb;
        internal Action onLoopCb;

        internal abstract void Evaluate(float easedT);
        internal virtual void FlipDirection() { }

        /// <summary>بازگرداندن اشیاء قابل استفاده‌ی مجدد به Pool (برای کاهش GC). پیش‌فرض خالی است.</summary>
        internal virtual void ReturnToPool() { }

        internal bool TargetAlive() => safetyTarget != null;

        /// <summary>محاسبه‌ی مقدار نهایی Ease شده (اولویت با منحنی سفارشی، در غیر این‌صورت Ease enum)</summary>
        internal float EvaluateEase(float t01) => customCurve != null ? customCurve.Evaluate(t01) : Easing.Evaluate(ease, t01);

        internal void ResetState()
        {
            killed = false;
            started = false;
            isPlaying = true;
            delay = 0f;
            delayElapsed = 0f;
            duration = 0f;
            elapsed = 0f;
            ease = UITweenSettings.DefaultEase;
            customCurve = null;
            loops = 1;
            loopType = LoopType.Restart;
            completedLoops = 0;
            useUnscaledTime = false;
            onStartCb = onUpdateCb = onCompleteCb = onKillCb = onLoopCb = null;
        }

        /// <summary>تعیین نوع منحنی حرکتی</summary>
        public Tween SetEase(Ease e) { ease = e; customCurve = null; return this; }

        /// <summary>تعیین یک منحنی سفارشی (AnimationCurve) به‌جای Ease استاندارد — قدرتمندتر از هر Ease از پیش‌تعریف‌شده</summary>
        public Tween SetEase(AnimationCurve curve) { customCurve = curve; return this; }

        /// <summary>تاخیر قبل از شروع (ثانیه)</summary>
        public Tween SetDelay(float seconds) { delay = Mathf.Max(0f, seconds); return this; }

        /// <summary>تعداد تکرار. -1 یعنی بی‌نهایت.</summary>
        public Tween SetLoops(int count, LoopType type = LoopType.Restart) { loops = count; loopType = type; return this; }

        /// <summary>مستقل از Time.timeScale اجرا شود (برای منوی پاز و صفحات UI که باید حتی هنگام توقف بازی انیمیت شوند)</summary>
        public Tween SetUpdate(bool unscaledTime) { useUnscaledTime = unscaledTime; return this; }

        public Tween OnStart(Action callback) { onStartCb += callback; return this; }
        public Tween OnUpdate(Action callback) { onUpdateCb += callback; return this; }
        public Tween OnComplete(Action callback) { onCompleteCb += callback; return this; }
        public Tween OnKill(Action callback) { onKillCb += callback; return this; }

        /// <summary>این Callback بعد از پایان هر دور تکرار (Loop) صدا زده می‌شود — پایه‌ی ساخت تایمرهای زمان‌بندی‌شده مثل "هر ۳ ثانیه یک‌بار"</summary>
        public Tween OnLoop(Action callback) { onLoopCb += callback; return this; }

        public void Play() => isPlaying = true;
        public void Pause() => isPlaying = false;
        public void TogglePause() => isPlaying = !isPlaying;

        /// <summary>توقف کامل. اگر complete=true باشد، مقدار نهایی اعمال و OnComplete صدا زده می‌شود.</summary>
        public void Kill(bool complete = false)
        {
            if (killed) return;
            if (complete)
            {
                Evaluate(EvaluateEase(1f));
                onCompleteCb?.Invoke();
            }
            killed = true;
            onKillCb?.Invoke();
        }

        /// <summary>ریست و شروع دوباره از ابتدا</summary>
        public void Restart()
        {
            elapsed = 0f;
            delayElapsed = 0f;
            completedLoops = 0;
            started = false;
            killed = false;
            isPlaying = true;
        }
    }

    /// <summary>
    /// تویین عمومیِ Pool-شده برای هر نوع مقدار (float، Vector2/3، Color، int و...).
    /// برای کاهش GC Allocation، نمونه‌های استفاده‌شده بازیافت می‌شوند.
    /// </summary>
    public sealed class ValueTween<T> : Tween
    {
        static readonly Stack<ValueTween<T>> pool = new Stack<ValueTween<T>>();

        T startVal;
        T endVal;
        Action<T> setter;
        Func<T, T, float, T> lerp;

        ValueTween() { }

        /// <summary>گرفتن یک نمونه از Pool (یا ساخت نمونه‌ی جدید) و مقداردهی اولیه</summary>
        public static ValueTween<T> Get(UnityEngine.Object target, T from, T to, float dur, Action<T> setter, Func<T, T, float, T> lerp)
        {
            var tw = pool.Count > 0 ? pool.Pop() : new ValueTween<T>();
            tw.ResetState();
            tw.safetyTarget = target;
            tw.startVal = from;
            tw.endVal = to;
            tw.duration = Mathf.Max(0f, dur);
            tw.setter = setter;
            tw.lerp = lerp;
            return tw;
        }

        internal override void Evaluate(float easedT) => setter(lerp(startVal, endVal, easedT));

        internal override void FlipDirection() => (startVal, endVal) = (endVal, startVal);

        internal override void ReturnToPool()
        {
            setter = null;
            lerp = null;
            safetyTarget = null;
            pool.Push(this);
        }
    }
}
