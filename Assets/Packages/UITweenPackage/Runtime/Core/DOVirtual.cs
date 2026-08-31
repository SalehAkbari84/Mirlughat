using System;
using UnityEngine;

namespace UITween
{
    /// <summary>
    /// تویین‌های مستقل از هر شیءِ صحنه — برای انیمیت‌کردن مقادیر دلخواه (مثلاً شمارنده‌ها،
    /// منطق سفارشی، یا هر جایی که به المان UGUI مستقیم نیاز نیست).
    /// </summary>
    public static class DOVirtual
    {
        public static Tween Float(float from, float to, float duration, Action<float> onUpdate)
            => UITweenManager.Track(ValueTween<float>.Get(UITweenManager.Instance, from, to, duration, onUpdate, Mathf.LerpUnclamped));

        public static Tween Vector2(Vector2 from, Vector2 to, float duration, Action<Vector2> onUpdate)
            => UITweenManager.Track(ValueTween<Vector2>.Get(UITweenManager.Instance, from, to, duration, onUpdate, UnityEngine.Vector2.LerpUnclamped));

        public static Tween Vector3(Vector3 from, Vector3 to, float duration, Action<Vector3> onUpdate)
            => UITweenManager.Track(ValueTween<Vector3>.Get(UITweenManager.Instance, from, to, duration, onUpdate, UnityEngine.Vector3.LerpUnclamped));

        public static Tween Color(Color from, Color to, float duration, Action<Color> onUpdate)
            => UITweenManager.Track(ValueTween<Color>.Get(UITweenManager.Instance, from, to, duration, onUpdate, UnityEngine.Color.LerpUnclamped));

        public static Tween Int(int from, int to, float duration, Action<int> onUpdate)
            => UITweenManager.Track(ValueTween<int>.Get(UITweenManager.Instance, from, to, duration, onUpdate, (a, b, t) => Mathf.RoundToInt(Mathf.Lerp(a, b, t))));

        /// <summary>اجرای یک تابع بعد از یک تاخیر مشخص، بدون نیاز به Coroutine</summary>
        public static Tween DelayedCall(float delay, Action callback)
        {
            var t = ValueTween<float>.Get(UITweenManager.Instance, 0f, 0f, 0f, _ => { }, Mathf.LerpUnclamped);
            t.SetDelay(delay).OnComplete(callback);
            return UITweenManager.Track(t);
        }

        /// <summary>
        /// اجرای یک تابع به‌صورت دوره‌ای، مثلاً "هر ۳ ثانیه یک‌بار". جایگزین ساده و تمیز برای InvokeRepeating/Coroutine.
        /// </summary>
        /// <param name="interval">فاصله‌ی زمانی بین هر اجرا (ثانیه)</param>
        /// <param name="callback">تابعی که هر بار اجرا می‌شود</param>
        /// <param name="loops">تعداد دفعات تکرار. -1 یعنی تا زمانی که دستی Kill شود (بی‌نهایت)</param>
        /// <param name="owner">اگر یک Component بدهید، با نابودشدن آن این تایمر خودکار متوقف می‌شود؛ در غیر این‌صورت تا Kill دستی زنده می‌ماند</param>
        /// <param name="callImmediately">اگر true باشد، اولین اجرا بلافاصله (بدون صبر برای interval اول) انجام می‌شود</param>
        public static Tween RepeatingCall(float interval, Action callback, int loops = -1, UnityEngine.Object owner = null, bool callImmediately = false)
        {
            if (callImmediately) callback?.Invoke();

            var target = owner != null ? owner : UITweenManager.Instance;
            var t = ValueTween<float>.Get(target, 0f, 0f, Mathf.Max(0.01f, interval), _ => { }, Mathf.LerpUnclamped);
            t.SetEase(Ease.Linear);
            t.SetLoops(loops);
            t.OnLoop(callback);
            return UITweenManager.Track(t);
        }
    }
}
