using System;
using TMPro;
using UnityEngine;
using UITween;

namespace PersianUGUI
{
    /// <summary>
    /// PersianText
    /// ============
    /// این کامپوننت رو روی هر آبجکتی که TMP_Text داره قرار بده. متن خام
    /// فارسی رو با PersianTextEngine پردازش می‌کنه و نمایش می‌ده.
    ///
    /// قابلیت‌های این نسخه:
    ///   ۱) تراز خودکار راست‌چین (Auto Right Align)
    ///   ۲) فرمت خودکار مبلغ (Auto Format Numbers) - فقط کافیه تیکش رو
    ///      بزنی؛ هر عدد خامی که مستقیم توی متن خام نوشته باشی (بدون
    ///      کاما، بدون هیچ Placeholder‌ی) خودش با جداکننده‌ی هزارگان
    ///      گروه‌بندی می‌شه. نیازی به فیلد جدا یا نشونه‌ی خاصی نیست.
    ///
    ///      مثال: توی Source Text بنویس "مبلغ: 65000 تومان" و تیک
    ///      "فرمت خودکار مبلغ" رو بزن -> نتیجه: "مبلغ: 65,000 تومان"
    ///
    ///      اعدادی که خودت از قبل جداکننده گذاشتی (مثلاً کد ملی با خط
    ///      تیره، یا شماره‌ای که خودت با کاما نوشتی) دست‌نخورده می‌مونن،
    ///      چون فقط بلوک‌های رقم خالص فرمت می‌شن.
    ///
    /// استفاده در کد:
    ///     GetComponent&lt;PersianText&gt;().SetText($"موجودی: {balance} تومان");
    ///
    /// نکته راه‌اندازی: Font Asset باید Atlas Population Mode = Dynamic
    /// با Source Font File متصل باشه تا گلیف‌های shaped رندر بشن.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    [ExecuteAlways]
    [AddComponentMenu("UI/Persian Text")]
    public class PersianText : MonoBehaviour
    {
        [Tooltip("متن خام فارسی/عربی - همون‌طوری که معمولاً می‌نویسی. تگ‌های Rich Text TMP هم پشتیبانی می‌شن.")]
        [TextArea(2, 6)]
        [SerializeField] private string sourceText;

        [Header("تراز")]
        [Tooltip("چیدمان افقی TMP رو خودکار روی راست‌چین قرار بده")]
        [SerializeField] private bool autoRightAlign = true;

        [Header("عدد و مبلغ")]
        [Tooltip("ارقام لاتین (0-9) به ارقام فارسی (۰-۹) تبدیل بشن")]
        [SerializeField] private bool convertDigitsToPersian = false;

        [Tooltip("هر بلوک عدد خالص (بدون جداکننده‌ی دستی) که توی متن پیدا بشه، خودکار با جداکننده‌ی هزارگان گروه‌بندی می‌شه. مثلاً 65000 -> 65,000")]
        [SerializeField] private bool autoFormatNumbers = false;

        [Tooltip("جداکننده‌ی هزارگان برای فرمت خودکار")]
        [SerializeField] private char thousandsSeparator = ',';

        private TMP_Text _tmp;

        public string SourceText
        {
            get => sourceText;
            set { sourceText = value; Apply(); }
        }

        public bool ConvertDigitsToPersian
        {
            get => convertDigitsToPersian;
            set { convertDigitsToPersian = value; Apply(); }
        }

        public bool AutoFormatNumbers
        {
            get => autoFormatNumbers;
            set { autoFormatNumbers = value; Apply(); }
        }

        private void Awake()
        {
            CacheAndFixComponent();
            Apply();
        }

        private void OnEnable()
        {
            CacheAndFixComponent();
            Apply();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                CacheAndFixComponent();
                Apply();
            }
        }
#endif

        /// <summary>
        /// رفرنس TMP رو کش می‌کنه، Enable RTL Editor داخلی TMP رو خاموش
        /// می‌کنه (چون با shaping دستی تداخل داره) و تراز افقی رو در
        /// صورت نیاز راست‌چین می‌کنه.
        /// </summary>
        private void CacheAndFixComponent()
        {
            if (_tmp == null) _tmp = GetComponent<TMP_Text>();
            if (_tmp == null) return;

            if (_tmp.isRightToLeftText)
            {
                _tmp.isRightToLeftText = false;
#if UNITY_EDITOR
                Debug.LogWarning($"[PersianText] گزینه‌ی Enable RTL Editor روی '{_tmp.name}' خاموش شد چون با shaping این کامپوننت تداخل داشت.", _tmp);
#endif
            }

            if (autoRightAlign && _tmp.horizontalAlignment != HorizontalAlignmentOptions.Right)
                _tmp.horizontalAlignment = HorizontalAlignmentOptions.Right;
        }

        /// <summary>متن خام جدید رو ست و اعمال می‌کنه.</summary>
        public void SetText(string newRawText)
        {
            sourceText = newRawText;
            Apply();
        }

        private void Apply()
        {
            if (_tmp == null) _tmp = GetComponent<TMP_Text>();
            if (_tmp == null || string.IsNullOrEmpty(sourceText)) return;

            _tmp.text = PersianTextEngine.Process(sourceText, CurrentOptions());

            if (autoRightAlign && _tmp.horizontalAlignment != HorizontalAlignmentOptions.Right)
                _tmp.horizontalAlignment = HorizontalAlignmentOptions.Right;
        }

        private PersianTextEngine.Options CurrentOptions() => new PersianTextEngine.Options
        {
            ConvertDigitsToPersian = convertDigitsToPersian,
            AutoFormatNumbers = autoFormatNumbers,
            ThousandsSeparator = thousandsSeparator
        };

        /// <summary>
        /// افکتِ تایپ‌رایتر، مخصوصِ متنِ فارسی/عربیِ شکل‌داده‌شده — برخلافِ تایپ‌رایترِ ساده‌ی
        /// روی رشته‌ی خام، اینجا واحدِ آشکارسازی «خوشه‌ی منطقی» است (نه کاراکترِ خام)، پس:
        ///   - جهتِ آشکارسازی طبیعی و همسو با خواندنِ فارسی است (از راستِ جمله شروع می‌شود)
        ///   - حروف هیچ‌وقت با یک اتصالِ آویزانِ نیمه‌کاره نمایش داده نمی‌شوند
        /// </summary>
        public Tween PlayTypewriter(float duration, Action onComplete = null)
        {
            if (_tmp == null) _tmp = GetComponent<TMP_Text>();
            if (_tmp == null)
            {
                UITweenDebug.LogError($"«{name}»: PlayTypewriter صدا زده شد ولی TMP_Text پیدا نشد!");
                return null;
            }
            if (string.IsNullOrEmpty(sourceText))
            {
                UITweenDebug.LogError($"«{name}»: PlayTypewriter صدا زده شد ولی sourceText خالی است! (SetText یا فیلد Source Text را در Inspector پر کنید)");
                return null;
            }

            var options = CurrentOptions();
            int totalClusters = PersianTextEngine.GetClusterCount(sourceText, options);
            UITweenDebug.Log($"«{name}»: PlayTypewriter شروع شد. sourceText=\"{sourceText}\", totalClusters={totalClusters}, duration={duration}");

            if (totalClusters == 0)
                UITweenDebug.LogWarning($"«{name}»: totalClusters صفر شد! یعنی Tokenize چیزی برای این متن پیدا نکرد — همون‌جا مشکل است.");

            _tmp.text = string.Empty;

            var t = DOVirtual.Int(0, totalClusters, duration,
                count => _tmp.text = PersianTextEngine.ProcessPartial(sourceText, options, count));

            if (onComplete != null) t.OnComplete(() => onComplete());
            return t;
        }

        /// <summary>
        /// نسخه‌ی کلمه‌به‌کلمه‌ی همان افکت — برای متن‌های بلندتر خواناتره چون هر بار یک
        /// کلمه‌ی کاملاً شکل‌گرفته (نه یک حرفِ تنها) اضافه می‌شود.
        /// </summary>
        public Tween PlayWordByWord(float duration, Action onComplete = null)
        {
            if (_tmp == null) _tmp = GetComponent<TMP_Text>();
            if (_tmp == null || string.IsNullOrEmpty(sourceText)) return null;

            var options = CurrentOptions();
            var boundaries = PersianTextEngine.GetWordBoundaryClusterCounts(sourceText, options);
            _tmp.text = string.Empty;

            if (boundaries.Length == 0)
            {
                _tmp.text = PersianTextEngine.Process(sourceText, options);
                return DOVirtual.DelayedCall(0f, () => onComplete?.Invoke());
            }

            var t = DOVirtual.Int(0, boundaries.Length, duration, wordIndex =>
            {
                int idx = Mathf.Clamp(wordIndex, 1, boundaries.Length) - 1;
                int clusterCount = wordIndex <= 0 ? 0 : boundaries[idx];
                _tmp.text = PersianTextEngine.ProcessPartial(sourceText, options, clusterCount);
            });

            if (onComplete != null) t.OnComplete(() => onComplete());
            return t;
        }
    }
}