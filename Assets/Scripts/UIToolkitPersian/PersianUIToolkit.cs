// ============================================================================
//  Persian Text Support for Unity UI Toolkit  —  بدون هیچ پکیج خارجی
// ============================================================================
//
//  این فایل کاملاً مستقل (self-contained) است و فقط از UnityEngine و
//  UnityEngine.UIElements استفاده می‌کند. هیچ DLL یا پکیج بیرونی لازم نیست.
//
//  چه کاری انجام می‌دهد؟
//   1) PersianTextShaper:
//      - شکل‌دهی حروف فارسی/عربی (Glyph Shaping) طبق جدول استاندارد
//        Unicode Arabic Presentation Forms (هر حرف بسته به موقعیتش در کلمه
//        به یکی از ۴ شکل isolated / initial / medial / final تبدیل می‌شود)
//      - لیگاتور «لا» (Lam-Alef) به‌صورت خاص مدیریت می‌شود
//      - بازچینی بصری (Visual Reordering) برای نمایش صحیح RTL در موتورهایی
//        که bidi ندارند (مثل UI Toolkit) — بدون به‌هم‌ریختن اعداد/متن انگلیسی
//
//   2) PersianLabel / PersianTextField / PersianButton:
//      - VisualElement های آماده که خودشان متن را Fix می‌کنند و راست‌چین
//        نمایش می‌دهند.
//
//  مراحل راه‌اندازی:
//   1) یک فونت فارسی (مثلاً Vazirmatn) را به پروژه اضافه کنید.
//   2) از مسیر Window > TextMeshPro > Font Asset Creator یک Font Asset
//      (SDF) از آن بسازید. *** مهم ***: در بخش Character Set باید علاوه بر
//      حروف اصلی فارسی (U+0600-06FF)، رنج‌های زیر هم پوشش داده شوند چون
//      شکل‌های شکل‌دهی‌شده از همین رنج‌ها هستند:
//         U+FB50-FDFF  (Arabic Presentation Forms-A)
//         U+FE70-FEFF  (Arabic Presentation Forms-B)
//      اگر این رنج‌ها در فونت اسد نباشند، حروف به جای گلیف صحیح، علامت
//      missing-glyph (□) نشان داده می‌شوند.
//   3) این فایل را در پوشه Scripts بگذارید.
//   4) به‌جای Label از PersianLabel استفاده کنید، یا روی Label معمولی
//      PersianTextHelper.SetPersianText(label, "متن شما") را صدا بزنید.
// ============================================================================

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace PersianUI
{
    // ════════════════════════════════════════════════════════════════════
    //  هسته اصلی: PersianTextShaper
    //  شکل‌دهی حروف + بازچینی RTL — بدون هیچ وابستگی خارجی
    // ════════════════════════════════════════════════════════════════════
    public static class PersianTextShaper
    {
        // ──────────────────────────────────────────────────────────────
        //  نوع پیوند حروف عربی/فارسی طبق Unicode Joining_Type
        //  Dual    : از هر دو طرف وصل می‌شود (بیشتر حروف)
        //  Right   : فقط از سمت راست (یعنی به حرف قبلی) وصل می‌شود
        //            مثل ا، د، ذ، ر، ز، ژ، و
        //  NonJoin : به هیچ‌کدام وصل نمی‌شود (همزه تنها: ء)
        //  Trans   : شفاف - در زنجیره پیوند نادیده گرفته می‌شود (اعراب)
        // ──────────────────────────────────────────────────────────────
        private enum JoinType { Dual, Right, NonJoin, Transparent, None }

        // هر ورودی: { Isolated, Final, Initial, Medial }
        // برای حروف Right-joining فقط دو خانه اول استفاده می‌شود.
        // برای NonJoin فقط خانه اول استفاده می‌شود.
        private static readonly Dictionary<char, char[]> Forms = new()
        {
            ['\u0621'] = new[] { '\uFE80', '\uFE80', '\uFE80', '\uFE80' }, // ء  همزه (NonJoin)
            ['\u0622'] = new[] { '\uFE81', '\uFE82', '\u0000', '\u0000' }, // آ  (Right)
            ['\u0623'] = new[] { '\uFE83', '\uFE84', '\u0000', '\u0000' }, // أ  (Right)
            ['\u0624'] = new[] { '\uFE85', '\uFE86', '\u0000', '\u0000' }, // ؤ  (Right)
            ['\u0625'] = new[] { '\uFE87', '\uFE88', '\u0000', '\u0000' }, // إ  (Right)
            ['\u0626'] = new[] { '\uFE89', '\uFE8A', '\uFE8B', '\uFE8C' }, // ئ  (Dual)
            ['\u0627'] = new[] { '\uFE8D', '\uFE8E', '\u0000', '\u0000' }, // ا  الف (Right)
            ['\u0628'] = new[] { '\uFE8F', '\uFE90', '\uFE91', '\uFE92' }, // ب
            ['\u0629'] = new[] { '\uFE93', '\uFE94', '\u0000', '\u0000' }, // ة  (Right)
            ['\u062A'] = new[] { '\uFE95', '\uFE96', '\uFE97', '\uFE98' }, // ت
            ['\u062B'] = new[] { '\uFE99', '\uFE9A', '\uFE9B', '\uFE9C' }, // ث
            ['\u062C'] = new[] { '\uFE9D', '\uFE9E', '\uFE9F', '\uFEA0' }, // ج
            ['\u062D'] = new[] { '\uFEA1', '\uFEA2', '\uFEA3', '\uFEA4' }, // ح
            ['\u062E'] = new[] { '\uFEA5', '\uFEA6', '\uFEA7', '\uFEA8' }, // خ
            ['\u062F'] = new[] { '\uFEA9', '\uFEAA', '\u0000', '\u0000' }, // د  (Right)
            ['\u0630'] = new[] { '\uFEAB', '\uFEAC', '\u0000', '\u0000' }, // ذ  (Right)
            ['\u0631'] = new[] { '\uFEAD', '\uFEAE', '\u0000', '\u0000' }, // ر  (Right)
            ['\u0632'] = new[] { '\uFEAF', '\uFEB0', '\u0000', '\u0000' }, // ز  (Right)
            ['\u0633'] = new[] { '\uFEB1', '\uFEB2', '\uFEB3', '\uFEB4' }, // س
            ['\u0634'] = new[] { '\uFEB5', '\uFEB6', '\uFEB7', '\uFEB8' }, // ش
            ['\u0635'] = new[] { '\uFEB9', '\uFEBA', '\uFEBB', '\uFEBC' }, // ص
            ['\u0636'] = new[] { '\uFEBD', '\uFEBE', '\uFEBF', '\uFEC0' }, // ض
            ['\u0637'] = new[] { '\uFEC1', '\uFEC2', '\uFEC3', '\uFEC4' }, // ط
            ['\u0638'] = new[] { '\uFEC5', '\uFEC6', '\uFEC7', '\uFEC8' }, // ظ
            ['\u0639'] = new[] { '\uFEC9', '\uFECA', '\uFECB', '\uFECC' }, // ع
            ['\u063A'] = new[] { '\uFECD', '\uFECE', '\uFECF', '\uFED0' }, // غ
            ['\u0640'] = new[] { '\u0640', '\u0640', '\u0640', '\u0640' }, // ـ  کشیده (Dual)
            ['\u0641'] = new[] { '\uFED1', '\uFED2', '\uFED3', '\uFED4' }, // ف
            ['\u0642'] = new[] { '\uFED5', '\uFED6', '\uFED7', '\uFED8' }, // ق
            ['\u0643'] = new[] { '\uFED9', '\uFEDA', '\uFEDB', '\uFEDC' }, // ك  کاف عربی
            ['\u0644'] = new[] { '\uFEDD', '\uFEDE', '\uFEDF', '\uFEE0' }, // ل
            ['\u0645'] = new[] { '\uFEE1', '\uFEE2', '\uFEE3', '\uFEE4' }, // م
            ['\u0646'] = new[] { '\uFEE5', '\uFEE6', '\uFEE7', '\uFEE8' }, // ن
            ['\u0647'] = new[] { '\uFEE9', '\uFEEA', '\uFEEB', '\uFEEC' }, // ه
            ['\u0648'] = new[] { '\uFEED', '\uFEEE', '\u0000', '\u0000' }, // و  (Right)
            ['\u0649'] = new[] { '\uFEEF', '\uFEF0', '\u0000', '\u0000' }, // ى  الف مقصوره (Right)
            ['\u064A'] = new[] { '\uFEF1', '\uFEF2', '\uFEF3', '\uFEF4' }, // ي  یای عربی

            // ── حروف ویژه فارسی ──
            ['\u067E'] = new[] { '\uFB56', '\uFB57', '\uFB58', '\uFB59' }, // پ
            ['\u0686'] = new[] { '\uFB7A', '\uFB7B', '\uFB7C', '\uFB7D' }, // چ
            ['\u0698'] = new[] { '\uFB8A', '\uFB8B', '\u0000', '\u0000' }, // ژ  (Right)
            ['\u06A9'] = new[] { '\uFB8E', '\uFB8F', '\uFB90', '\uFB91' }, // ک
            ['\u06AF'] = new[] { '\uFB92', '\uFB93', '\uFB94', '\uFB95' }, // گ
            ['\u06CC'] = new[] { '\uFBFC', '\uFBFD', '\uFBFE', '\uFBFF' }, // ی  یای فارسی
        };

        // اعراب/علائم شفاف - در محاسبه پیوند نادیده گرفته می‌شوند
        private static readonly HashSet<char> TransparentChars = new()
        {
            '\u064B', '\u064C', '\u064D', '\u064E', '\u064F',
            '\u0650', '\u0651', '\u0652', '\u0670'
        };

        // لیگاتور لام + الف  →  { Isolated, Final }
        private static readonly Dictionary<char, (char iso, char fin)> LamAlefLigatures = new()
        {
            ['\u0627'] = ('\uFEFB', '\uFEFC'), // ل + ا  → لا
            ['\u0622'] = ('\uFEF5', '\uFEF6'), // ل + آ  → لآ
            ['\u0623'] = ('\uFEF7', '\uFEF8'), // ل + أ  → لأ
            ['\u0625'] = ('\uFEF9', '\uFEFA'), // ل + إ  → لإ
        };

        private const char LAM = '\u0644';

        private static bool IsPersianOrArabicDigit(char c) =>
            (c >= '\u06F0' && c <= '\u06F9') || (c >= '\u0660' && c <= '\u0669');

        private static JoinType GetJoinType(char c)
        {
            if (TransparentChars.Contains(c)) return JoinType.Transparent;
            if (!Forms.TryGetValue(c, out var f)) return JoinType.None;
            if (c == '\u0621') return JoinType.NonJoin;
            return (f[2] == '\u0000' && f[3] == '\u0000') ? JoinType.Right : JoinType.Dual;
        }

        // آیا حرف در ایندکس i به حرف قبلی (در ترتیب منطقی) وصل می‌شود؟
        private static bool JoinsPrev(string s, int i)
        {
            var t = GetJoinType(s[i]);
            if (t != JoinType.Dual && t != JoinType.Right) return false;

            int j = i - 1;
            while (j >= 0 && GetJoinType(s[j]) == JoinType.Transparent) j--;
            if (j < 0) return false;

            return GetJoinType(s[j]) == JoinType.Dual;
        }

        // آیا حرف در ایندکس i به حرف بعدی (در ترتیب منطقی) وصل می‌شود؟
        private static bool JoinsNext(string s, int i)
        {
            var t = GetJoinType(s[i]);
            if (t != JoinType.Dual) return false;

            int j = i + 1;
            while (j < s.Length && GetJoinType(s[j]) == JoinType.Transparent) j++;
            if (j >= s.Length) return false;

            var nt = GetJoinType(s[j]);
            return nt == JoinType.Dual || nt == JoinType.Right;
        }

        private static char GetForm(char c, bool joinsPrev, bool joinsNext)
        {
            var forms = Forms[c];
            var t = GetJoinType(c);

            switch (t)
            {
                case JoinType.NonJoin:
                    return forms[0];
                case JoinType.Right:
                    return joinsPrev ? forms[1] : forms[0];
                default: // Dual
                    if (joinsPrev && joinsNext) return forms[3]; // medial
                    if (joinsPrev) return forms[1];               // final
                    if (joinsNext) return forms[2];               // initial
                    return forms[0];                              // isolated
            }
        }

        /// <summary>
        /// مرحله ۱: شکل‌دهی حروف فارسی/عربی به شکل‌های Presentation Form
        /// مناسب موقعیتشان در کلمه (ابتدا/وسط/انتها/تکی).
        /// ترتیب منطقی رشته حفظ می‌شود — فقط خود کاراکترها تغییر می‌کنند.
        /// </summary>
        public static string Shape(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var sb = new StringBuilder(input.Length);
            int n = input.Length;
            int i = 0;

            while (i < n)
            {
                char c = input[i];

                // لیگاتور «لا»: ل بلافاصله قبل از یکی از حالت‌های الف
                if (c == LAM && i + 1 < n && LamAlefLigatures.TryGetValue(input[i + 1], out var lig))
                {
                    bool lamJoinsPrev = JoinsPrev(input, i); // لام Dual است، همان قانون
                    sb.Append(lamJoinsPrev ? lig.fin : lig.iso);
                    i += 2;
                    continue;
                }

                var joinType = GetJoinType(c);

                if (joinType == JoinType.None || joinType == JoinType.Transparent)
                {
                    sb.Append(c);
                    i++;
                    continue;
                }

                bool jp = JoinsPrev(input, i);
                bool jn = JoinsNext(input, i);
                sb.Append(GetForm(c, jp, jn));
                i++;
            }

            return sb.ToString();
        }

        // آیا این کاراکتر باید در جریان نمایش RTL قرار بگیرد؟
        // (حروف فارسی/عربی شکل‌دهی‌شده یا خام - به‌جز اعداد)
        private static bool IsRtlChar(char c)
        {
            if (c >= '\uFB50' && c <= '\uFDFF') return true; // Presentation Forms-A
            if (c >= '\uFE70' && c <= '\uFEFF') return true; // Presentation Forms-B
            if (c >= '\u0600' && c <= '\u06FF')
                return !IsPersianOrArabicDigit(c);
            return false;
        }

        /// <summary>
        /// مرحله ۲: بازچینی بصری (Visual Reordering) برای رندر صحیح در
        /// موتوری که bidi واقعی ندارد و فقط چپ‌به‌راست رسم می‌کند.
        ///
        /// روش: بخش‌های LTR (انگلیسی/اعداد) ابتدا به‌صورت محلی معکوس
        /// می‌شوند، سپس کل رشته معکوس می‌شود. نتیجه‌ی نهایی این است که
        /// کلمات فارسی به ترتیب صحیح RTL ظاهر می‌شوند، در حالی که اعداد و
        /// متن انگلیسی داخل آن‌ها خوانا و به ترتیب درست باقی می‌مانند.
        ///
        /// ورودی باید از قبل توسط Shape() پردازش شده باشد.
        /// </summary>
        public static string ReorderForDisplay(string shaped)
        {
            if (string.IsNullOrEmpty(shaped)) return shaped;

            var chars = shaped.ToCharArray();
            int n = chars.Length;

            // معکوس کردن بخش‌های پیوسته‌ی غیر-RTL (انگلیسی/اعداد/علائم)
            int i = 0;
            while (i < n)
            {
                if (!IsRtlChar(chars[i]))
                {
                    int j = i;
                    while (j < n && !IsRtlChar(chars[j])) j++;
                    System.Array.Reverse(chars, i, j - i);
                    i = j;
                }
                else
                {
                    i++;
                }
            }

            // معکوس کردن کل رشته
            System.Array.Reverse(chars);
            return new string(chars);
        }

        /// <summary>
        /// متد اصلی: شکل‌دهی + بازچینی در یک مرحله.
        /// همین یک متد کافی است — متن فارسی خام را بدهید، متن قابل‌نمایش
        /// در UI Toolkit برمی‌گردد.
        /// </summary>
        public static string Fix(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return ReorderForDisplay(Shape(input));
        }

        // ──────────────────────────────────────────────────────────────
        //  ابزارهای کمکی برای اعداد
        // ──────────────────────────────────────────────────────────────
        private static readonly char[] PersianDigits =
            { '۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹' };

        /// <summary>تبدیل اعداد لاتین ۰-۹ به اعداد فارسی ۰-۹</summary>
        public static string ToPersianDigits(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var sb = new StringBuilder(input.Length);
            foreach (char c in input)
                sb.Append(c >= '0' && c <= '9' ? PersianDigits[c - '0'] : c);
            return sb.ToString();
        }

        /// <summary>تبدیل اعداد فارسی ۰-۹ به اعداد لاتین ۰-۹ (مثلاً برای پارس کردن ورودی کاربر)</summary>
        public static string ToLatinDigits(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var sb = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (c >= '\u06F0' && c <= '\u06F9') sb.Append((char)('0' + (c - '\u06F0')));
                else if (c >= '\u0660' && c <= '\u0669') sb.Append((char)('0' + (c - '\u0660')));
                else sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>آیا رشته حاوی حرف فارسی/عربی است؟</summary>
        public static bool ContainsPersian(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
            {
                if (c >= '\u0600' && c <= '\u06FF') return true;
                if (c >= '\uFB50' && c <= '\uFDFF') return true;
                if (c >= '\uFE70' && c <= '\uFEFF') return true;
            }
            return false;
        }
    }


    // ════════════════════════════════════════════════════════════════════
    //  PersianLabel  —  جایگزین Label برای متن فارسی
    // ════════════════════════════════════════════════════════════════════
    public class PersianLabel : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<PersianLabel, UxmlTraits> { }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlStringAttributeDescription _text =
                new() { name = "text", defaultValue = "" };

            private readonly UxmlFloatAttributeDescription _fontSize =
                new() { name = "font-size", defaultValue = 16f };

            private readonly UxmlBoolAttributeDescription _convertDigits =
                new() { name = "convert-digits", defaultValue = false };

            public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
                { get { yield break; } }

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                var label = (PersianLabel)ve;
                label.ConvertDigitsToPersian = _convertDigits.GetValueFromBag(bag, cc);
                label.FontSize = _fontSize.GetValueFromBag(bag, cc);
                label.Text = _text.GetValueFromBag(bag, cc);
            }
        }

        private readonly Label _innerLabel;
        private string _rawText = "";

        /// <summary>متن فارسی خام - همین را ست کنید، خودش Fix می‌شود</summary>
        public string Text
        {
            get => _rawText;
            set
            {
                _rawText = value ?? "";
                Refresh();
            }
        }

        /// <summary>اعداد لاتین درون متن به اعداد فارسی تبدیل شوند؟</summary>
        public bool ConvertDigitsToPersian { get; set; } = false;

        public float FontSize
        {
            get => _innerLabel.style.fontSize.value.value;
            set => _innerLabel.style.fontSize = value;
        }

        public Color TextColor
        {
            set => _innerLabel.style.color = value;
        }

        public PersianLabel()
        {
            // کانتینر به صورت RTL: محتوا از راست شروع می‌شود
            style.flexDirection = FlexDirection.RowReverse;

            _innerLabel = new Label();
            _innerLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            _innerLabel.style.flexGrow = 1;
            _innerLabel.style.whiteSpace = WhiteSpace.Normal;

            Add(_innerLabel);
        }

        public PersianLabel(string text) : this()
        {
            Text = text;
        }

        private void Refresh()
        {
            if (_innerLabel == null) return;
            string source = ConvertDigitsToPersian
                ? PersianTextShaper.ToPersianDigits(_rawText)
                : _rawText;
            _innerLabel.text = PersianTextShaper.Fix(source);
        }

        public void AddLabelClass(string className) => _innerLabel.AddToClassList(className);
        public void RemoveLabelClass(string className) => _innerLabel.RemoveFromClassList(className);
    }


    // ════════════════════════════════════════════════════════════════════
    //  PersianTextField  —  فیلد ورودی RTL
    // ════════════════════════════════════════════════════════════════════
    //  نکته: شکل‌دهی زنده هنگام تایپ کمی پیچیده است (چون مکان‌نمای کاربر
    //  هم باید جابه‌جا شود). این کلاس مقدار خام کاربر را نگه می‌دارد و فقط
    //  برای *نمایش* (مثلاً پیش‌نمایش) از Fix استفاده می‌کند.
    // ════════════════════════════════════════════════════════════════════
    public class PersianTextField : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<PersianTextField, UxmlTraits> { }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlStringAttributeDescription _label =
                new() { name = "label", defaultValue = "" };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                var field = (PersianTextField)ve;
                field.Label = _label.GetValueFromBag(bag, cc);
            }
        }

        private readonly TextField _textField;

        /// <summary>متن خام تایپ‌شده توسط کاربر (بدون شکل‌دهی)</summary>
        public string Value
        {
            get => _textField.value;
            set => _textField.value = value ?? "";
        }

        /// <summary>متن آماده برای نمایش (شکل‌دهی + بازچینی RTL)</summary>
        public string DisplayValue => PersianTextShaper.Fix(_textField.value);

        public string Label
        {
            set => _textField.label = value;
        }

        public event System.Action<string> OnTextChanged;

        public PersianTextField()
        {
            _textField = new TextField();
            _textField.style.flexGrow = 1;

            var inputElement = _textField.Q(className: TextField.inputUssClassName);
            if (inputElement != null)
                inputElement.style.unityTextAlign = TextAnchor.MiddleRight;

            // برچسب (label) را هم راست‌چین کن
            var labelElement = _textField.Q<Label>();
            if (labelElement != null)
                labelElement.style.unityTextAlign = TextAnchor.MiddleRight;

            _textField.RegisterValueChangedCallback(evt => OnTextChanged?.Invoke(evt.newValue));

            Add(_textField);
        }

        public void Clear() => _textField.value = "";
    }


    // ════════════════════════════════════════════════════════════════════
    //  PersianButton  —  دکمه با متن فارسی
    // ════════════════════════════════════════════════════════════════════
    public class PersianButton : Button
    {
        public new class UxmlFactory : UxmlFactory<PersianButton, UxmlTraits> { }

        public new class UxmlTraits : Button.UxmlTraits
        {
            private readonly UxmlStringAttributeDescription _persianText =
                new() { name = "persian-text", defaultValue = "دکمه" };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                var btn = (PersianButton)ve;
                btn.PersianText = _persianText.GetValueFromBag(bag, cc);
            }
        }

        private string _rawText = "";

        public string PersianText
        {
            get => _rawText;
            set
            {
                _rawText = value ?? "";
                text = PersianTextShaper.Fix(_rawText);
            }
        }

        public PersianButton()
        {
            style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        public PersianButton(string persianText, System.Action onClick = null) : this()
        {
            PersianText = persianText;
            if (onClick != null) clicked += onClick;
        }
    }


    // ════════════════════════════════════════════════════════════════════
    //  نمونه استفاده
    // ════════════════════════════════════════════════════════════════════
    /*
    using UnityEngine;
    using UnityEngine.UIElements;
    using PersianUI;

    public class PersianUIExample : MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;

        private void OnEnable()
        {
            var root = _uiDocument.rootVisualElement;

            // روش ۱: PersianLabel
            var title = new PersianLabel("سلام دنیا!");
            title.FontSize = 24;
            title.TextColor = Color.white;
            root.Add(title);

            // روش ۲: روی Label معمولیِ ساخته‌شده در UXML
            var label = root.Q<Label>("scoreLabel");
            label.style.unityTextAlign = TextAnchor.MiddleRight;
            label.text = PersianTextShaper.Fix("امتیاز: " + score);

            // روش ۳: PersianButton
            var btn = new PersianButton("شروع بازی", OnStartClicked);
            root.Add(btn);

            // روش ۴: PersianTextField برای ورودی کاربر
            var input = new PersianTextField();
            input.Label = "نام کاربری";
            input.OnTextChanged += text => Debug.Log($"ورودی: {text}");
            root.Add(input);

            // فقط شکل‌دهی متن، بدون VisualElement خاص:
            string fixedText = PersianTextShaper.Fix("متن دلخواه شما");
        }

        private void OnStartClicked() => Debug.Log("بازی شروع شد");
    }
    */
}
