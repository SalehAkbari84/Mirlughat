using TMPro;
using UnityEngine;
using UITween;

namespace PersianUGUI
{
    /// <summary>
    /// این کامپوننت رو کنارِ PersianText روی همون آبجکت بذارید. چون رابطِ IUIAnimated رو
    /// پیاده‌سازی می‌کنه، دقیقاً مثلِ UIAnimatedElement توی innerElements پنل‌های
    /// AnimatedPanelController یا GameEventEffect جا می‌گیره — یعنی می‌تونید متنِ فارسی
    /// رو هم عضوِ همون نمایشِ آبشاریِ باز/بسته‌شدنِ پنل کنید، بدون نوشتنِ کدِ اضافه.
    /// </summary>
    [RequireComponent(typeof(PersianText))]
    [AddComponentMenu("UI/Persian Animated Text")]
    public class PersianAnimatedText : MonoBehaviour, IUIAnimated
    {
        public enum RevealStyle
        {
            /// <summary>بدون انیمیشن — متن آنی و کامل نمایش داده می‌شود</summary>
            Instant,
            /// <summary>حرف‌به‌حرف (خوشه‌به‌خوشه) با رعایتِ جهتِ درستِ فارسی</summary>
            Typewriter,
            /// <summary>کلمه‌به‌کلمه — برای متنِ طولانی خواناتره</summary>
            WordByWord
        }

        [Header("استایل نمایش")]
        [SerializeField] private RevealStyle style = RevealStyle.Typewriter;
        [SerializeField] private float duration = 1.5f;

        [Header("استایل مخفی‌شدن")]
        [SerializeField] private float hideDuration = 0.25f;

        [Header("اجرای خودکار")]
        [Tooltip("اگر روشن باشد، همون ابتدای بازی خودکار Show() اجرا می‌شود")]
        [SerializeField] private bool playOnStart = false;

        PersianText persianText;
        TMP_Text tmp;
        float defaultAlpha = 1f;
        bool alphaCaptured;

        void Awake()
        {
            persianText = GetComponent<PersianText>();
            tmp = GetComponent<TMP_Text>();
        }

        void Start()
        {
            if (playOnStart) Show();
        }

        void EnsureRefs()
        {
            if (persianText == null) persianText = GetComponent<PersianText>();
            if (tmp == null) tmp = GetComponent<TMP_Text>();
            if (!alphaCaptured && tmp != null)
            {
                defaultAlpha = tmp.color.a;
                alphaCaptured = true;
            }
        }

        void SetAlpha(float a)
        {
            if (tmp == null) return;
            var c = tmp.color;
            tmp.color = new Color(c.r, c.g, c.b, a);
        }

        /// <summary>نمایش با استایلِ انتخاب‌شده (Typewriter/WordByWord/Instant) — با تاخیرِ اختیاری برای نمایشِ آبشاری</summary>
        public Tween Show(float delay = 0f)
        {
            EnsureRefs();
            UITweenDebug.Log($"«{name}»: PersianAnimatedText.Show() صدا زده شد (style={style}, delay={delay}, defaultAlpha={defaultAlpha})");

            gameObject.SetActive(true);
            SetAlpha(defaultAlpha); // اگر قبلاً Hide شده بود، شفافیت رو برمی‌گردونه

            Tween t;
            switch (style)
            {
                case RevealStyle.Typewriter:
                    t = persianText.PlayTypewriter(duration);
                    break;
                case RevealStyle.WordByWord:
                    t = persianText.PlayWordByWord(duration);
                    break;
                default:
                    persianText.SetText(persianText.SourceText);
                    t = DOVirtual.DelayedCall(0f, () => { });
                    break;
            }

            t?.SetDelay(delay);
            return t;
        }

        /// <summary>مخفی‌کردنِ متن با محو تدریجی، و در پایان غیرفعال‌کردنِ GameObject</summary>
        public Tween Hide(float delay = 0f)
        {
            EnsureRefs();

            if (tmp == null)
            {
                gameObject.SetActive(false);
                return DOVirtual.DelayedCall(0f, () => { });
            }

            var t = tmp.DOFade(0f, hideDuration);
            t.SetDelay(delay);
            t.OnComplete(() => gameObject.SetActive(false));
            return t;
        }

        /// <summary>پنهان‌کردنِ فوری (بدون انیمیشن) — برای آماده‌سازی در ابتدای بازی</summary>
        public void SnapToHidden()
        {
            EnsureRefs();
            SetAlpha(0f);
            gameObject.SetActive(false);
        }

        /// <summary>اجرای انیمیشن با یک متنِ کاملاً جدید (به‌جای متنِ تنظیم‌شده در Inspector)</summary>
        public Tween Play(string newText)
        {
            EnsureRefs();
            persianText.SourceText = newText;
            return Show();
        }

        // ==================== نسخه‌های void، مخصوص اتصال از Button/EventTrigger ====================
        // یونیتی توی دراپ‌داونِ OnClick()/EventTrigger فقط متدهای void رو نشون می‌ده؛ چون Show/Hide/Play
        // باید Tween برگردونن (برای زنجیره‌کردنِ SetDelay/.OnComplete از کد)، این‌جا با متدهای void
        // بدونِ آرگومان یه راهِ مستقیم برای اتصال از Inspector هم گذاشتیم.

        /// <summary>معادلِ void برای Show() — برای اتصال از Button/EventTrigger</summary>
        public void ShowFromButton() => Show();

        /// <summary>معادلِ void برای Hide() — برای اتصال از Button/EventTrigger</summary>
        public void HideFromButton() => Hide();
    }
}