using UnityEngine;
using TMPro;

namespace UITween
{
    /// <summary>
    /// اکستنشن‌های TextMeshPro. این فایل داخل یک اسمبلیِ جدا قرار دارد و فقط وقتی
    /// پکیج TextMeshPro در پروژه نصب باشد کامپایل می‌شود. اگر TMPro ندارید،
    /// کل پوشه‌ی Optional_TMP را حذف کنید تا خطای کامپایل نگیرید.
    /// </summary>
    public static class TMPExtensions
    {
        public static Tween DOColor(this TMP_Text txt, Color to, float duration)
        {
            var from = txt.color;
            return UITweenManager.Track(ValueTween<Color>.Get(txt, from, to, duration, v => txt.color = v, Color.LerpUnclamped));
        }

        public static Tween DOFade(this TMP_Text txt, float to, float duration)
        {
            var from = txt.color;
            return txt.DOColor(new Color(from.r, from.g, from.b, to), duration);
        }

        public static Tween DOText(this TMP_Text txt, string toText, float duration)
        {
            var t = ValueTween<int>.Get(txt, 0, toText.Length, duration,
                count => txt.text = toText.Substring(0, count),
                (a, b, tt) => Mathf.RoundToInt(Mathf.Lerp(a, b, tt)));
            t.SetEase(Ease.Linear);
            return UITweenManager.Track(t);
        }

        /// <summary>مثل تایپ‌رایتر معمولی، ولی یک نشانگرِ چشمک‌نزن انتهای متنِ نوشته‌نشده نمایش داده می‌شود</summary>
        public static Tween DOTypewriterCursor(this TMP_Text txt, string toText, float duration, char cursor = '|')
        {
            var t = ValueTween<int>.Get(txt, 0, toText.Length, duration, count =>
            {
                string revealed = toText.Substring(0, count);
                txt.text = count < toText.Length ? revealed + cursor : revealed;
            }, (a, b, tt) => Mathf.RoundToInt(Mathf.Lerp(a, b, tt)));
            t.SetEase(Ease.Linear);
            return UITweenManager.Track(t);
        }

        /// <summary>به‌جای کاراکتر، کلمه‌به‌کلمه نمایان می‌شود</summary>
        public static Tween DOWordByWord(this TMP_Text txt, string toText, float duration)
        {
            var words = toText.Split(' ');
            var t = ValueTween<int>.Get(txt, 0, words.Length, duration, count =>
            {
                txt.text = string.Join(" ", words, 0, Mathf.Clamp(count, 0, words.Length));
            }, (a, b, tt) => Mathf.RoundToInt(Mathf.Lerp(a, b, tt)));
            t.SetEase(Ease.Linear);
            return UITweenManager.Track(t);
        }

        /// <summary>افکتِ «رمزگشایی» — کاراکترهای هنوز آشکارنشده با کاراکترهای تصادفی جایگزین می‌شوند</summary>
        public static Tween DOScrambleReveal(this TMP_Text txt, string toText, float duration,
            string scrambleCharset = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")
        {
            var rnd = new System.Random();
            var buffer = new char[toText.Length];

            var t = ValueTween<float>.Get(txt, 0f, 1f, duration, progress =>
            {
                int revealCount = Mathf.FloorToInt(progress * toText.Length);
                for (int i = 0; i < toText.Length; i++)
                {
                    buffer[i] = i < revealCount ? toText[i] : scrambleCharset[rnd.Next(scrambleCharset.Length)];
                }
                txt.text = new string(buffer);
            }, Mathf.LerpUnclamped);
            t.SetEase(Ease.Linear);
            return UITweenManager.Track(t);
        }

        /// <summary>شمارشِ عددیِ صحیح با فرمتِ دلخواه</summary>
        public static Tween DOCountUp(this TMP_Text txt, int from, int to, float duration, string format = "N0")
        {
            var t = ValueTween<int>.Get(txt, from, to, duration,
                v => txt.text = v.ToString(format),
                (a, b, tt) => Mathf.RoundToInt(Mathf.Lerp(a, b, tt)));
            return UITweenManager.Track(t);
        }

        /// <summary>شمارشِ عددیِ اعشاری با فرمتِ دلخواه</summary>
        public static Tween DOCountUp(this TMP_Text txt, float from, float to, float duration, string format = "F1")
        {
            var t = ValueTween<float>.Get(txt, from, to, duration,
                v => txt.text = v.ToString(format), Mathf.LerpUnclamped);
            return UITweenManager.Track(t);
        }

        public static Tween DOFontSize(this TMP_Text txt, float to, float duration)
        {
            float from = txt.fontSize;
            return UITweenManager.Track(ValueTween<float>.Get(txt, from, to, duration, v => txt.fontSize = v, Mathf.LerpUnclamped));
        }
    }
}
