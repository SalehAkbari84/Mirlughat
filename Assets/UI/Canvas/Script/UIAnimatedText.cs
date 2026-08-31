using UnityEngine;
using UnityEngine.UI;
using UITween;

/// <summary>
/// کامپوننتی آماده برای انیمیت‌کردنِ خودکارِ متن — دقیقاً مثل UIAnimatedElement ولی
/// مخصوصِ محتوای متنی. فقط استایل رو از Dropdown انتخاب کنید و Play() رو صدا بزنید
/// (یا از طریق دکمه/EventTrigger/Trigger دیگری وصلش کنید).
/// </summary>
[RequireComponent(typeof(Text))]
public class UIAnimatedText : MonoBehaviour
{
    public enum TextRevealStyle
    {
        /// <summary>کاراکترها یکی‌یکی نمایش داده می‌شوند</summary>
        Typewriter,
        /// <summary>مثل تایپ‌رایتر، با یک نشانگرِ "|" در انتهای متنِ نوشته‌نشده</summary>
        TypewriterCursor,
        /// <summary>افکتِ رمزگشاییِ سبکِ هکری — کاراکترهای تصادفی تا رسیدنِ نوبتِ آشکارشدن</summary>
        ScrambleReveal,
        /// <summary>به‌جای کاراکتر، کلمه‌به‌کلمه نمایان می‌شود — برای متن‌های طولانی خواناتره</summary>
        WordByWord
    }

    [Header("استایل")]
    [SerializeField] private TextRevealStyle style = TextRevealStyle.Typewriter;
    [SerializeField] private float duration = 1.2f;

    [Header("متنِ مقصد")]
    [Tooltip("خالی بگذارید تا همون متنی که الان توی کامپوننتِ Text نوشته شده به‌عنوان مقصد استفاده بشه")]
    [TextArea]
    [SerializeField] private string overrideText;

    Text text;
    string targetText;

    void Awake()
    {
        text = GetComponent<Text>();
        targetText = string.IsNullOrEmpty(overrideText) ? text.text : overrideText;
    }

    /// <summary>شروعِ انیمیشنِ متن با استایلِ انتخاب‌شده</summary>
    public Tween Play()
    {
        text.text = "";
        switch (style)
        {
            case TextRevealStyle.Typewriter: return text.DOText(targetText, duration);
            case TextRevealStyle.TypewriterCursor: return text.DOTypewriterCursor(targetText, duration);
            case TextRevealStyle.ScrambleReveal: return text.DOScrambleReveal(targetText, duration);
            case TextRevealStyle.WordByWord: return text.DOWordByWord(targetText, duration);
            default: return text.DOText(targetText, duration);
        }
    }

    /// <summary>شروعِ انیمیشن با یک متنِ کاملاً جدید (به‌جای متنِ تنظیم‌شده در Inspector)</summary>
    public Tween Play(string newText)
    {
        targetText = newText;
        return Play();
    }
}
