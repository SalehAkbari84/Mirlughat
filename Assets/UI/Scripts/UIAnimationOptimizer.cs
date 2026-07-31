using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// جایگزین USS transition برای toggle button ها.
/// به جای اینکه Unity هر فریم layout dirty کنه،
/// مستقیم transform رو interpolate می‌کنه (بدون layout recalc).
/// </summary>
public class UIAnimationOptimizer : MonoBehaviour
{
    // ===== در هر coroutine فقط یه تاپل ساده =====
    private readonly struct TweenJob
    {
        public readonly VisualElement Target;
        public readonly StyleColor    FromColor;
        public readonly StyleColor    ToColor;
        public readonly float         FromTranslateX; // percent
        public readonly float         ToTranslateX;
        public readonly float         Duration;

        public TweenJob(VisualElement t, Color from, Color to,
                        float fromX, float toX, float dur)
        {
            Target         = t;
            FromColor      = new StyleColor(from);
            ToColor        = new StyleColor(to);
            FromTranslateX = fromX;
            ToTranslateX   = toX;
            Duration       = dur;
        }
    }

    // صف انیمیشن‌ها — هیچ allocation جدیدی در حین اجرا نداریم
    private readonly Queue<TweenJob> _queue = new(8);
    private bool _running;

    // ===== رنگ‌های از پیش parse شده (نه هر بار parse) =====
    private static readonly Color GreenColor;
    private static readonly Color RedColor;

    static UIAnimationOptimizer()
    {
        ColorUtility.TryParseHtmlString("#22C55E", out GreenColor);
        ColorUtility.TryParseHtmlString("#EF4444", out RedColor);
    }

    // ===== API عمومی =====

    /// <summary>
    /// یه toggle animation برای یه button و handle اضافه کن.
    /// </summary>
    public void AnimateToggle(Button button, Image handle, bool newState,
                              float duration = 0.2f)
    {
        if (button == null || handle == null) return;

        Color fromColor = newState ? RedColor   : GreenColor;
        Color toColor   = newState ? GreenColor : RedColor;
        float fromX     = newState ? 0f         : 124f;
        float toX       = newState ? 124f        : 0f;

        // اگه انیمیشن قبلی برای همین المنت در صف داره cancel می‌کنیم (آخری می‌بره)
        var job = new TweenJob(button, fromColor, toColor, fromX, toX, duration);
        _queue.Enqueue(job);

        // job مربوط به handle
        var handleJob = new TweenJob(handle,
            fromColor, toColor, fromX, toX, duration);
        _queue.Enqueue(handleJob);

        if (!_running)
            StartCoroutine(ProcessQueue());
    }

    // ===== پردازش صف =====
    private IEnumerator ProcessQueue()
    {
        _running = true;

        while (_queue.Count > 0)
        {
            var job = _queue.Dequeue();

            // handle و button رو جدا tween کن
            if (job.Target is Button btn)
                yield return StartCoroutine(TweenColor(btn, job.FromColor.value,
                    job.ToColor.value, job.Duration));
            else if (job.Target is Image img)
                yield return StartCoroutine(TweenTranslate(img, job.FromTranslateX,
                    job.ToTranslateX, job.Duration));
        }

        _running = false;
    }

    // ===== Tween توابع داخلی =====

    private static IEnumerator TweenColor(VisualElement el,
        Color from, Color to, float duration)
    {
        float t = 0;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            el.style.backgroundColor = Color.Lerp(from, to, EaseInOut(t));
            yield return null;
        }
        el.style.backgroundColor = to;
    }

    private static IEnumerator TweenTranslate(VisualElement el,
        float fromX, float toX, float duration)
    {
        float t = 0;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            float x = Mathf.Lerp(fromX, toX, EaseInOut(t));
            el.style.translate = new Translate(new Length(x, LengthUnit.Percent), 0, 0);
            yield return null;
        }
        el.style.translate = new Translate(new Length(toX, LengthUnit.Percent), 0, 0);
    }

    private static float EaseInOut(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t); // smoothstep
    }
}
