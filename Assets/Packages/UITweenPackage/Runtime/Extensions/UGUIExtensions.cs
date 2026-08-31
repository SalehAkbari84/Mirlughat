using System;
using UnityEngine;
using UnityEngine.UI;

namespace UITween
{
    /// <summary>
    /// تمام متدهای DOxxx برای اجزای UGUI و Transform.
    /// کافیست using UITween; بنویسید و مستقیم روی هر کامپوننت صدا بزنید.
    /// </summary>
    public static class UGUIExtensions
    {
        // ==================== RectTransform ====================

        public static Tween DOAnchorPos(this RectTransform rt, Vector2 to, float duration, bool isRelative = false)
        {
            var from = rt.anchoredPosition;
            var target = isRelative ? from + to : to;
            return UITweenManager.Track(ValueTween<Vector2>.Get(rt, from, target, duration, v => rt.anchoredPosition = v, Vector2.LerpUnclamped));
        }

        /// <summary>می‌پرد به مقدار "from" و سپس به مقدار فعلیِ آبجکت برمی‌گردد — عالی برای انیمیشن ورود</summary>
        public static Tween DOAnchorPosFrom(this RectTransform rt, Vector2 from, float duration)
        {
            var to = rt.anchoredPosition;
            return UITweenManager.Track(ValueTween<Vector2>.Get(rt, from, to, duration, v => rt.anchoredPosition = v, Vector2.LerpUnclamped));
        }

        public static Tween DOAnchorPosX(this RectTransform rt, float toX, float duration)
        {
            var from = rt.anchoredPosition;
            return rt.DOAnchorPos(new Vector2(toX, from.y), duration);
        }

        public static Tween DOAnchorPosY(this RectTransform rt, float toY, float duration)
        {
            var from = rt.anchoredPosition;
            return rt.DOAnchorPos(new Vector2(from.x, toY), duration);
        }

        public static Tween DOSizeDelta(this RectTransform rt, Vector2 to, float duration)
        {
            var from = rt.sizeDelta;
            return UITweenManager.Track(ValueTween<Vector2>.Get(rt, from, to, duration, v => rt.sizeDelta = v, Vector2.LerpUnclamped));
        }

        public static Tween DOAnchorMin(this RectTransform rt, Vector2 to, float duration)
        {
            var from = rt.anchorMin;
            return UITweenManager.Track(ValueTween<Vector2>.Get(rt, from, to, duration, v => rt.anchorMin = v, Vector2.LerpUnclamped));
        }

        public static Tween DOAnchorMax(this RectTransform rt, Vector2 to, float duration)
        {
            var from = rt.anchorMax;
            return UITweenManager.Track(ValueTween<Vector2>.Get(rt, from, to, duration, v => rt.anchorMax = v, Vector2.LerpUnclamped));
        }

        public static Tween DOPivot(this RectTransform rt, Vector2 to, float duration)
        {
            var from = rt.pivot;
            return UITweenManager.Track(ValueTween<Vector2>.Get(rt, from, to, duration, v => rt.pivot = v, Vector2.LerpUnclamped));
        }

        /// <summary>لرزش پنل روی anchoredPosition — برای خطاها، ضربه، هشدار</summary>
        public static Tween DOShakeAnchorPos(this RectTransform rt, float duration, float strength = 20f, int vibrato = 10)
            => UITweenManager.Track(new ShakeTween2D(rt, () => rt.anchoredPosition, v => rt.anchoredPosition = v, duration, strength, vibrato));

        // ==================== Transform (۲بعدی و ۳بعدی) ====================

        public static Tween DOMove(this Transform tr, Vector3 to, float duration, bool isRelative = false)
        {
            var from = tr.position;
            var target = isRelative ? from + to : to;
            return UITweenManager.Track(ValueTween<Vector3>.Get(tr, from, target, duration, v => tr.position = v, Vector3.LerpUnclamped));
        }

        public static Tween DOLocalMove(this Transform tr, Vector3 to, float duration, bool isRelative = false)
        {
            var from = tr.localPosition;
            var target = isRelative ? from + to : to;
            return UITweenManager.Track(ValueTween<Vector3>.Get(tr, from, target, duration, v => tr.localPosition = v, Vector3.LerpUnclamped));
        }

        public static Tween DOScale(this Transform tr, Vector3 to, float duration)
        {
            var from = tr.localScale;
            return UITweenManager.Track(ValueTween<Vector3>.Get(tr, from, to, duration, v => tr.localScale = v, Vector3.LerpUnclamped));
        }

        public static Tween DOScale(this Transform tr, float to, float duration) => tr.DOScale(Vector3.one * to, duration);

        /// <summary>می‌پرد به مقیاسِ "from" و سپس به مقیاس فعلی برمی‌گردد — عالی برای Pop-in</summary>
        public static Tween DOScaleFrom(this Transform tr, float from, float duration)
        {
            var to = tr.localScale;
            return UITweenManager.Track(ValueTween<Vector3>.Get(tr, Vector3.one * from, to, duration, v => tr.localScale = v, Vector3.LerpUnclamped));
        }

        public static Tween DORotate(this Transform tr, Vector3 toEuler, float duration)
        {
            var from = tr.localEulerAngles;
            return UITweenManager.Track(ValueTween<Vector3>.Get(tr, from, toEuler, duration, v => tr.localEulerAngles = v, Vector3.LerpUnclamped));
        }

        public static Sequence DOPunchScale(this Transform tr, float strength, float duration)
        {
            var baseScale = tr.localScale;
            var peak = baseScale * (1f + strength);

            Tween grow = ValueTween<Vector3>.Get(tr, baseScale, peak, duration * 0.5f, v => tr.localScale = v, Vector3.LerpUnclamped);
            grow.SetEase(Ease.OutQuad);
            Tween shrink = ValueTween<Vector3>.Get(tr, peak, baseScale, duration * 0.5f, v => tr.localScale = v, Vector3.LerpUnclamped);
            shrink.SetEase(Ease.InQuad);

            var seq = new Sequence(tr);
            seq.Append(grow).Append(shrink);
            return UITweenManager.Track(seq);
        }

        public static Tween DOShakePosition(this Transform tr, float duration, float strength = 0.5f, int vibrato = 10)
            => UITweenManager.Track(new ShakeTween3D(tr, () => tr.position, v => tr.position = v, duration, strength, vibrato));

        public static Tween DOShakeLocalPosition(this Transform tr, float duration, float strength = 0.5f, int vibrato = 10)
            => UITweenManager.Track(new ShakeTween3D(tr, () => tr.localPosition, v => tr.localPosition = v, duration, strength, vibrato));

        public static Tween DOShakeScale(this Transform tr, float duration, float strength = 0.15f, int vibrato = 10)
            => UITweenManager.Track(new ShakeTween3D(tr, () => tr.localScale, v => tr.localScale = v, duration, strength, vibrato));

        public static Tween DOShakeRotation(this Transform tr, float duration, float strength = 15f, int vibrato = 10)
            => UITweenManager.Track(new ShakeTween3D(tr, () => tr.localEulerAngles, v => tr.localEulerAngles = v, duration, strength, vibrato));

        // ==================== CanvasGroup ====================

        public static Tween DOFade(this CanvasGroup cg, float to, float duration)
        {
            float from = cg.alpha;
            return UITweenManager.Track(ValueTween<float>.Get(cg, from, to, duration, v => cg.alpha = v, Mathf.LerpUnclamped));
        }

        public static Tween DOFadeFrom(this CanvasGroup cg, float from, float duration)
        {
            float to = cg.alpha;
            return UITweenManager.Track(ValueTween<float>.Get(cg, from, to, duration, v => cg.alpha = v, Mathf.LerpUnclamped));
        }

        // ==================== Graphic (Image, RawImage, Text, ...) ====================

        public static Tween DOColor(this Graphic g, Color to, float duration)
        {
            var from = g.color;
            return UITweenManager.Track(ValueTween<Color>.Get(g, from, to, duration, v => g.color = v, Color.LerpUnclamped));
        }

        public static Tween DOColorFrom(this Graphic g, Color from, float duration)
        {
            var to = g.color;
            return UITweenManager.Track(ValueTween<Color>.Get(g, from, to, duration, v => g.color = v, Color.LerpUnclamped));
        }

        public static Tween DOFade(this Graphic g, float to, float duration)
        {
            var from = g.color;
            return g.DOColor(new Color(from.r, from.g, from.b, to), duration);
        }

        // ==================== Image ====================

        public static Tween DOFillAmount(this Image img, float to, float duration)
        {
            float from = img.fillAmount;
            return UITweenManager.Track(ValueTween<float>.Get(img, from, to, duration, v => img.fillAmount = v, Mathf.LerpUnclamped));
        }

        // ==================== Text (تایپ‌رایتر و رنگ) ====================

        /// <summary>افکت تایپ‌رایتر: کاراکترها یکی‌یکی نمایش داده می‌شوند</summary>
        public static Tween DOText(this Text text, string toText, float duration)
        {
            var t = ValueTween<int>.Get(text, 0, toText.Length, duration,
                count => text.text = toText.Substring(0, count),
                (a, b, tt) => Mathf.RoundToInt(Mathf.Lerp(a, b, tt)));
            t.SetEase(Ease.Linear);
            return UITweenManager.Track(t);
        }

        /// <summary>مثل تایپ‌رایتر معمولی، ولی یک نشانگرِ چشمک‌نزن (مثلاً "|") انتهای متنِ نوشته‌نشده نمایش داده می‌شود</summary>
        public static Tween DOTypewriterCursor(this Text text, string toText, float duration, char cursor = '|')
        {
            var t = ValueTween<int>.Get(text, 0, toText.Length, duration, count =>
            {
                string revealed = toText.Substring(0, count);
                text.text = count < toText.Length ? revealed + cursor : revealed;
            }, (a, b, tt) => Mathf.RoundToInt(Mathf.Lerp(a, b, tt)));
            t.SetEase(Ease.Linear);
            return UITweenManager.Track(t);
        }

        /// <summary>به‌جای کاراکتر، کلمه‌به‌کلمه نمایان می‌شود — برای متن‌های طولانی خواناتر است</summary>
        public static Tween DOWordByWord(this Text text, string toText, float duration)
        {
            var words = toText.Split(' ');
            var t = ValueTween<int>.Get(text, 0, words.Length, duration, count =>
            {
                text.text = string.Join(" ", words, 0, Mathf.Clamp(count, 0, words.Length));
            }, (a, b, tt) => Mathf.RoundToInt(Mathf.Lerp(a, b, tt)));
            t.SetEase(Ease.Linear);
            return UITweenManager.Track(t);
        }

        /// <summary>
        /// افکتِ «رمزگشایی» (شبیه فیلم‌های هکری) — کاراکترهای هنوز آشکارنشده با کاراکترهای
        /// تصادفی جایگزین می‌شوند تا نوبتِ آشکارشدنِ واقعی‌شان برسد.
        /// </summary>
        public static Tween DOScrambleReveal(this Text text, string toText, float duration,
            string scrambleCharset = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")
        {
            var rnd = new System.Random();
            var buffer = new char[toText.Length];

            var t = ValueTween<float>.Get(text, 0f, 1f, duration, progress =>
            {
                int revealCount = Mathf.FloorToInt(progress * toText.Length);
                for (int i = 0; i < toText.Length; i++)
                {
                    buffer[i] = i < revealCount ? toText[i] : scrambleCharset[rnd.Next(scrambleCharset.Length)];
                }
                text.text = new string(buffer);
            }, Mathf.LerpUnclamped);
            t.SetEase(Ease.Linear);
            return UITweenManager.Track(t);
        }

        /// <summary>شمارشِ عددیِ صحیح (مثلاً امتیاز از ۰ تا ۱۰۰۰) با فرمتِ دلخواه (پیش‌فرض جداکننده‌ی هزارگان)</summary>
        public static Tween DOCountUp(this Text text, int from, int to, float duration, string format = "N0")
        {
            var t = ValueTween<int>.Get(text, from, to, duration,
                v => text.text = v.ToString(format),
                (a, b, tt) => Mathf.RoundToInt(Mathf.Lerp(a, b, tt)));
            return UITweenManager.Track(t);
        }

        /// <summary>شمارشِ عددیِ اعشاری (مثلاً درصد یا پول) با فرمتِ دلخواه</summary>
        public static Tween DOCountUp(this Text text, float from, float to, float duration, string format = "F1")
        {
            var t = ValueTween<float>.Get(text, from, to, duration,
                v => text.text = v.ToString(format), Mathf.LerpUnclamped);
            return UITweenManager.Track(t);
        }

        // ==================== Slider / ScrollRect / LayoutElement ====================

        public static Tween DOValue(this Slider slider, float to, float duration)
        {
            float from = slider.value;
            return UITweenManager.Track(ValueTween<float>.Get(slider, from, to, duration, v => slider.value = v, Mathf.LerpUnclamped));
        }

        public static Tween DONormalizedPos(this ScrollRect sr, Vector2 to, float duration)
        {
            var from = sr.normalizedPosition;
            return UITweenManager.Track(ValueTween<Vector2>.Get(sr, from, to, duration, v => sr.normalizedPosition = v, Vector2.LerpUnclamped));
        }

        public static Tween DOPreferredHeight(this LayoutElement le, float to, float duration)
        {
            float from = le.preferredHeight;
            return UITweenManager.Track(ValueTween<float>.Get(le, from, to, duration, v => le.preferredHeight = v, Mathf.LerpUnclamped));
        }

        public static Tween DOPreferredWidth(this LayoutElement le, float to, float duration)
        {
            float from = le.preferredWidth;
            return UITweenManager.Track(ValueTween<float>.Get(le, from, to, duration, v => le.preferredWidth = v, Mathf.LerpUnclamped));
        }

        // ==================== ابزارهای عمومی ====================

        /// <summary>ساخت یک Sequence جدید متصل به این Component</summary>
        public static Sequence DOSequence(this Component owner) => UITweenManager.Track(new Sequence(owner));

        /// <summary>همان DOSequence بالا، برای زمانی که مستقیم روی یک GameObject صدا زده می‌شود</summary>
        public static Sequence DOSequence(this GameObject owner) => UITweenManager.Track(new Sequence(owner.transform));

        /// <summary>کشتن تمام تویین‌های فعالِ متصل به این Component</summary>
        public static void DOKill(this Component target, bool complete = false)
        {
            UITweenManager.Instance?.KillAll(target, complete);
        }

        /// <summary>همان DOKill بالا، برای زمانی که مستقیم روی یک GameObject صدا زده می‌شود</summary>
        public static void DOKill(this GameObject target, bool complete = false)
        {
            UITweenManager.Instance?.KillAll(target.transform, complete);
        }

        /// <summary>
        /// اجرای دوره‌ای یک تابع (مثلاً "هر ۲ ثانیه یک‌بار")، وابسته به عمر این Component —
        /// اگر خود owner نابود شود، تایمر خودکار متوقف می‌شود. معادل تمیزتر InvokeRepeating.
        /// </summary>
        public static Tween DOSchedule(this Component owner, float interval, Action callback, int loops = -1, bool callImmediately = false)
            => DOVirtual.RepeatingCall(interval, callback, loops, owner, callImmediately);
    }
}
