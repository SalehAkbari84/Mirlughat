using UITween;

/// <summary>
/// رابطِ مشترک برای هر کامپوننتی که بخواد در کنارِ UIAnimatedElement توی سیستمِ نمایش/مخفیِ
/// پنل‌ها (AnimatedPanelController, GameEventEffect) جا بگیره — چه خودِ UIAnimatedElement
/// باشه، چه PersianAnimatedText، چه هر کامپوننتِ سفارشیِ دیگه‌ای که بعداً بسازید.
///
/// با پیاده‌سازیِ همین سه متد، کامپوننتِ شما دقیقاً مثلِ بقیه توی innerElements جا می‌گیره
/// و آبشاری/هماهنگ با بقیه‌ی پنل نمایش داده می‌شه.
/// </summary>
public interface IUIAnimated
{
    /// <summary>نمایش با انیمیشن، با تاخیرِ اختیاری (برای نمایشِ آبشاری)</summary>
    Tween Show(float delay = 0f);

    /// <summary>مخفی‌کردن با انیمیشن، با تاخیرِ اختیاری</summary>
    Tween Hide(float delay = 0f);

    /// <summary>پنهان‌کردنِ فوری (بدون انیمیشن) — برای آماده‌سازیِ حالتِ اولیه در ابتدای بازی</summary>
    void SnapToHidden();
}
