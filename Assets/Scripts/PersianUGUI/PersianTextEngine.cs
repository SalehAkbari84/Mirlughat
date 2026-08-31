using System.Collections.Generic;
using System.Text;

namespace PersianUGUI
{
    /// <summary>
    /// PersianTextEngine
    /// ===================
    /// موتور کامل تبدیل متن خام فارسی/عربی به متنی که TextMeshPro بتونه
    /// درست نمایشش بده (چسباندن صحیح حروف + چیدمان راست‌به‌چپ).
    ///
    /// این نسخه، برخلاف اسکریپت‌های اولیه، متن رو به واحدهای منطقی
    /// (Cluster) تقسیم می‌کنه تا موارد زیر رو درست هندل کنه:
    ///   - تگ‌های Rich Text خود TMP (مثل &lt;color=red&gt;...&lt;/color&gt;)
    ///   - اعراب/حرکت‌گذاری عربی (که نباید اتصال حروف رو بشکنن)
    ///   - بلوک‌های عدد/لاتین (که باید به‌صورت یکپارچه خونده بشن)
    ///   - لیگاتور اجباری لام+الف
    ///
    /// استفاده:
    ///     string shaped = PersianTextEngine.Process("قیمت: 65,000 <color=red>تومان</color>");
    ///
    /// نکته: خروجی شامل کاراکترهایی از بازه‌ی Unicode Arabic Presentation
    /// Forms هست. Font Asset باید Dynamic باشه و Source Font File متصل
    /// داشته باشه تا این گلیف‌ها در زمان اجرا ساخته بشن.
    /// </summary>
    public static class PersianTextEngine
    {
        public struct Options
        {
            /// <summary>تبدیل ارقام لاتین (0-9) به ارقام فارسی (۰-۹)</summary>
            public bool ConvertDigitsToPersian;

            /// <summary>
            /// درج خودکار جداکننده‌ی هزارگان برای بلوک‌های عددی خالص (فقط
            /// رقم، بدون جداکننده‌ی دستی از قبل و بدون حرف لاتین قاطی).
            /// مناسب برای نمایش مبلغ بدون نیاز به تایپ دستی کاما.
            /// مثال: 65000 -> 65,000
            /// </summary>
            public bool AutoFormatNumbers;

            /// <summary>کاراکتر جداکننده‌ی هزارگان برای فرمت خودکار (پیش‌فرض کاما لاتین)</summary>
            public char ThousandsSeparator;

            public static Options Default => new Options
            {
                ConvertDigitsToPersian = false,
                AutoFormatNumbers = false,
                ThousandsSeparator = ','
            };
        }

        // ---------------------------------------------------------------
        // جدول حروف
        // ---------------------------------------------------------------
        private enum JoinType { Dual, RightOnly, NoneJoin }

        private class Forms
        {
            public readonly JoinType Type;
            public readonly char Isolated, Initial, Medial, Final;
            public Forms(JoinType type, char iso, char init = default, char med = default, char final = default)
            {
                Type = type;
                Isolated = iso;
                Initial = init == default ? iso : init;
                Final = final == default ? iso : final;
                Medial = med == default ? Final : med;
            }
        }

        private static readonly Dictionary<char, Forms> Table = new Dictionary<char, Forms>
        {
            { 'ء', new Forms(JoinType.NoneJoin, '\uFE80') },
            { 'آ', new Forms(JoinType.RightOnly, '\uFE81', final: '\uFE82') },
            { 'أ', new Forms(JoinType.RightOnly, '\uFE83', final: '\uFE84') },
            { 'ؤ', new Forms(JoinType.RightOnly, '\uFE85', final: '\uFE86') },
            { 'إ', new Forms(JoinType.RightOnly, '\uFE87', final: '\uFE88') },
            { 'ئ', new Forms(JoinType.Dual, '\uFE89', '\uFE8B', '\uFE8C', '\uFE8A') },
            { 'ا', new Forms(JoinType.RightOnly, '\uFE8D', final: '\uFE8E') },
            { 'ب', new Forms(JoinType.Dual, '\uFE8F', '\uFE91', '\uFE92', '\uFE90') },
            { 'پ', new Forms(JoinType.Dual, '\uFB56', '\uFB58', '\uFB59', '\uFB57') },
            { 'ت', new Forms(JoinType.Dual, '\uFE95', '\uFE97', '\uFE98', '\uFE96') },
            { 'ث', new Forms(JoinType.Dual, '\uFE99', '\uFE9B', '\uFE9C', '\uFE9A') },
            { 'ج', new Forms(JoinType.Dual, '\uFE9D', '\uFE9F', '\uFEA0', '\uFE9E') },
            { 'چ', new Forms(JoinType.Dual, '\uFB7A', '\uFB7C', '\uFB7D', '\uFB7B') },
            { 'ح', new Forms(JoinType.Dual, '\uFEA1', '\uFEA3', '\uFEA4', '\uFEA2') },
            { 'خ', new Forms(JoinType.Dual, '\uFEA5', '\uFEA7', '\uFEA8', '\uFEA6') },
            { 'د', new Forms(JoinType.RightOnly, '\uFEA9', final: '\uFEAA') },
            { 'ذ', new Forms(JoinType.RightOnly, '\uFEAB', final: '\uFEAC') },
            { 'ر', new Forms(JoinType.RightOnly, '\uFEAD', final: '\uFEAE') },
            { 'ز', new Forms(JoinType.RightOnly, '\uFEAF', final: '\uFEB0') },
            { 'ژ', new Forms(JoinType.RightOnly, '\uFB8A', final: '\uFB8B') },
            { 'س', new Forms(JoinType.Dual, '\uFEB1', '\uFEB3', '\uFEB4', '\uFEB2') },
            { 'ش', new Forms(JoinType.Dual, '\uFEB5', '\uFEB7', '\uFEB8', '\uFEB6') },
            { 'ص', new Forms(JoinType.Dual, '\uFEB9', '\uFEBB', '\uFEBC', '\uFEBA') },
            { 'ض', new Forms(JoinType.Dual, '\uFEBD', '\uFEBF', '\uFEC0', '\uFEBE') },
            { 'ط', new Forms(JoinType.Dual, '\uFEC1', '\uFEC3', '\uFEC4', '\uFEC2') },
            { 'ظ', new Forms(JoinType.Dual, '\uFEC5', '\uFEC7', '\uFEC8', '\uFEC6') },
            { 'ع', new Forms(JoinType.Dual, '\uFEC9', '\uFECB', '\uFECC', '\uFECA') },
            { 'غ', new Forms(JoinType.Dual, '\uFECD', '\uFECF', '\uFED0', '\uFECE') },
            { 'ف', new Forms(JoinType.Dual, '\uFED1', '\uFED3', '\uFED4', '\uFED2') },
            { 'ق', new Forms(JoinType.Dual, '\uFED5', '\uFED7', '\uFED8', '\uFED6') },
            { 'ک', new Forms(JoinType.Dual, '\uFB8E', '\uFB90', '\uFB91', '\uFB8F') },
            { 'ك', new Forms(JoinType.Dual, '\uFEDB', '\uFEDD', '\uFEDE', '\uFEDC') },
            { 'گ', new Forms(JoinType.Dual, '\uFB92', '\uFB94', '\uFB95', '\uFB93') },
            { 'ل', new Forms(JoinType.Dual, '\uFEDD', '\uFEDF', '\uFEE0', '\uFEDE') },
            { 'م', new Forms(JoinType.Dual, '\uFEE1', '\uFEE3', '\uFEE4', '\uFEE2') },
            { 'ن', new Forms(JoinType.Dual, '\uFEE5', '\uFEE7', '\uFEE8', '\uFEE6') },
            { 'و', new Forms(JoinType.RightOnly, '\uFEED', final: '\uFEEE') },
            { 'ه', new Forms(JoinType.Dual, '\uFEE9', '\uFEEB', '\uFEEC', '\uFEEA') },
            { 'ة', new Forms(JoinType.RightOnly, '\uFE93', final: '\uFE94') },
            { 'ی', new Forms(JoinType.Dual, '\uFBFC', '\uFBFE', '\uFBFF', '\uFBFD') },
            { 'ي', new Forms(JoinType.Dual, '\uFEF1', '\uFEF3', '\uFEF4', '\uFEF2') },
            { 'ى', new Forms(JoinType.RightOnly, '\uFEEF', final: '\uFEF0') },
        };

        private static readonly Dictionary<char, (char iso, char final)> LamAlef = new Dictionary<char, (char, char)>
        {
            { 'ا', ('\uFEFB', '\uFEFC') },
            { 'آ', ('\uFEF5', '\uFEF6') },
            { 'أ', ('\uFEF7', '\uFEF8') },
            { 'إ', ('\uFEF9', '\uFEFA') },
        };

        // اعراب/حرکت‌گذاری عربی: باید به حرف قبلی بچسبن و اتصال رو نشکنن
        private static bool IsDiacritic(char c) =>
            (c >= '\u064B' && c <= '\u0652') || c == '\u0670' || c == '\u0640' /* tatweel */;

        // ارقام لاتین (0-9)، فارسی (۰-۹ / U+06F0-U+06F9) و عربی (٠-٩ / U+0660-U+0669)
        // هر سه باید به‌عنوان "رقم" شناسایی بشن، وگرنه اگه عدد با ارقام فارسی
        // نوشته شده باشه، به‌عنوان بلوک عدد اتمی در نظر گرفته نمی‌شه و در
        // بازآرایی RTL تک‌تک ارقامش معکوس می‌شن.
        private static bool IsDigit(char c) =>
            (c >= '0' && c <= '9') ||
            (c >= '\u06F0' && c <= '\u06F9') ||
            (c >= '\u0660' && c <= '\u0669');
        private static bool IsLatin(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        private static bool IsNumberSeparator(char c) =>
            c == ',' || c == '.' || c == '٬' || c == '٫' || c == '،' ||
            c == ':' || c == '/' || c == '%' || c == '-' || c == '+';

        /// <summary>
        /// یک رشته‌ی خالص از ارقام (بدون جداکننده) رو با جداکننده‌ی هزارگان
        /// گروه‌بندی می‌کنه. برخلاف FormatThousands، مستقیم روی کاراکترها کار
        /// می‌کنه (نه روی عدد عددی)، پس با ارقام فارسی/عربی هم مشکلی نداره
        /// و محدودیت overflow اعداد بزرگ رو هم نداره.
        /// </summary>
        private static string GroupRawDigits(string digits, char separator)
        {
            int len = digits.Length;
            if (len <= 3) return digits;

            var sb = new StringBuilder(len + len / 3);
            int firstGroupLen = len % 3;
            if (firstGroupLen == 0) firstGroupLen = 3;

            sb.Append(digits, 0, firstGroupLen);
            for (int i = firstGroupLen; i < len; i += 3)
            {
                sb.Append(separator);
                sb.Append(digits, i, 3);
            }

            return sb.ToString();
        }

        private const char PersianZero = '۰';
        private static char ToPersianDigit(char latinDigit) => (char)(PersianZero + (latinDigit - '0'));

        /// <summary>
        /// عدد رو با جداکننده‌ی هزارگان فرمت می‌کنه، مستقل از تنظیمات زبان
        /// سیستم/دستگاه (بر خلاف value.ToString("N0") که به Culture دستگاه
        /// وابسته‌ست و ممکنه جای دیگه‌ای خروجی رو عوض کنه).
        /// ساختار همیشه ثابت می‌مونه: هر ۳ رقم از سمت راست، یک جداکننده.
        /// مثال: FormatThousands(65000) => "65,000"
        /// </summary>
        public static string FormatThousands(long value, char separator = ',')
        {
            bool isNegative = value < 0;
            string digits = System.Math.Abs(value).ToString();

            var sb = new StringBuilder(digits.Length + digits.Length / 3);
            int firstGroupLen = digits.Length % 3;
            if (firstGroupLen == 0) firstGroupLen = 3;

            sb.Append(digits, 0, firstGroupLen);
            for (int i = firstGroupLen; i < digits.Length; i += 3)
            {
                sb.Append(separator);
                sb.Append(digits, i, 3);
            }

            return (isNegative ? "-" : string.Empty) + sb;
        }

        // ---------------------------------------------------------------
        // ساختار خوشه (Cluster)
        // ---------------------------------------------------------------
        private enum ClusterKind { PersianLetter, LtrRun, Tag, Other }

        private class Cluster
        {
            public ClusterKind Kind;
            public string Emit;          // متن نهایی این خوشه (بدون تغییر ترتیب داخلی)
            public char BaseLetter;      // فقط برای PersianLetter: حرف پایه (پیش از shaping)
            public JoinType JoinType;    // فقط برای PersianLetter
        }

        // کش ساده برای جلوگیری از پردازش تکراری متن‌های ثابت
        private static readonly Dictionary<string, string> _cache = new Dictionary<string, string>();
        private const int MaxCacheEntries = 256;

        /// <summary>
        /// نقطه‌ی ورودی اصلی. متن خام رو shape و reorder می‌کنه.
        /// </summary>
        public static string Process(string input, Options options = default)
        {
            if (string.IsNullOrEmpty(input)) return input;

            string cacheKey = $"{(options.ConvertDigitsToPersian ? 1 : 0)}{(options.AutoFormatNumbers ? 1 : 0)}{options.ThousandsSeparator}|{input}";
            if (_cache.TryGetValue(cacheKey, out var cached))
                return cached;

            var clusters = Tokenize(input, options);
            ShapeClusters(clusters);

            // معکوس‌سازی کلی برای نمایش RTL - هر خوشه به‌صورت اتمی جابه‌جا می‌شه
            // ولی ترتیب داخلی کاراکترهای خودش دست‌نخورده می‌مونه.
            clusters.Reverse();

            var sb = new StringBuilder(input.Length + 8);
            foreach (var c in clusters) sb.Append(c.Emit);
            string result = sb.ToString();

            if (_cache.Count >= MaxCacheEntries) _cache.Clear();
            _cache[cacheKey] = result;

            return result;
        }

        // ---------------------------------------------------------------
        // مرحله ۱: تبدیل رشته به خوشه‌ها
        // ---------------------------------------------------------------
        private static List<Cluster> Tokenize(string input, Options options)
        {
            var clusters = new List<Cluster>(input.Length);
            int i = 0;
            int len = input.Length;

            while (i < len)
            {
                char c = input[i];

                // --- تگ Rich Text: <...> ---
                if (c == '<')
                {
                    int close = input.IndexOf('>', i + 1);
                    if (close != -1)
                    {
                        clusters.Add(new Cluster { Kind = ClusterKind.Tag, Emit = input.Substring(i, close - i + 1) });
                        i = close + 1;
                        continue;
                    }
                }

                // --- بلوک عدد/لاتین (شامل جداکننده‌ها) ---
                if (IsDigit(c) || IsLatin(c))
                {
                    int start = i;
                    bool sawSeparator = false;
                    bool sawLatin = false;

                    while (i < len && (IsDigit(input[i]) || IsLatin(input[i]) ||
                           (IsNumberSeparator(input[i]) && i + 1 < len && (IsDigit(input[i + 1]) || IsLatin(input[i + 1])))))
                    {
                        if (IsLatin(input[i])) sawLatin = true;
                        if (IsNumberSeparator(input[i])) sawSeparator = true;
                        i++;
                    }

                    string raw = input.Substring(start, i - start);

                    // فرمت خودکار: فقط وقتی بلوک، رقم خالصه (بدون جداکننده‌ی
                    // دستی و بدون حرف لاتین قاطی‌شده مثل "v2" یا کد پستی و ...)
                    if (options.AutoFormatNumbers && !sawSeparator && !sawLatin)
                        raw = GroupRawDigits(raw, options.ThousandsSeparator == default ? ',' : options.ThousandsSeparator);

                    if (options.ConvertDigitsToPersian)
                    {
                        var conv = new StringBuilder(raw.Length);
                        foreach (var ch in raw)
                            conv.Append((ch >= '0' && ch <= '9') ? ToPersianDigit(ch) : ch);
                        raw = conv.ToString();
                    }

                    clusters.Add(new Cluster { Kind = ClusterKind.LtrRun, Emit = raw });
                    continue;
                }

                // --- حرف پایه فارسی/عربی ---
                if (Table.ContainsKey(c))
                {
                    // بررسی لیگاتور لام+الف (با عبور شفاف از تگ‌ها، نه اعراب چون بین لام و الف اعراب بی‌معنیه)
                    if (c == 'ل' && i + 1 < len && LamAlef.ContainsKey(input[i + 1]))
                    {
                        clusters.Add(new Cluster { Kind = ClusterKind.PersianLetter, BaseLetter = c, JoinType = JoinType.Dual, Emit = null });
                        clusters[clusters.Count - 1].Emit = "LAM_ALEF:" + input[i + 1]; // نشانه‌گذاری موقت، در Shape جایگزین می‌شه
                        i += 2;
                        continue;
                    }

                    // جمع‌آوری اعراب همراه (اگر بود) - این‌ها به همین خوشه می‌چسبن
                    int diacriticEnd = i + 1;
                    while (diacriticEnd < len && IsDiacritic(input[diacriticEnd])) diacriticEnd++;
                    string trailingDiacritics = diacriticEnd > i + 1 ? input.Substring(i + 1, diacriticEnd - i - 1) : string.Empty;

                    var forms = Table[c];
                    clusters.Add(new Cluster
                    {
                        Kind = ClusterKind.PersianLetter,
                        BaseLetter = c,
                        JoinType = forms.Type,
                        Emit = trailingDiacritics // موقتاً فقط اعراب رو نگه می‌داریم، حرف shaped بعداً prepend می‌شه
                    });
                    i = diacriticEnd;
                    continue;
                }

                // --- هر چیز دیگه (فاصله، علامت، ایموجی و ...) ---
                clusters.Add(new Cluster { Kind = ClusterKind.Other, Emit = c.ToString() });
                i++;
            }

            return clusters;
        }

        // ---------------------------------------------------------------
        // مرحله ۲: تعیین شکل صحیح هر حرف فارسی بر اساس همسایه‌ها
        // ---------------------------------------------------------------
        private static void ShapeClusters(List<Cluster> clusters)
        {
            for (int idx = 0; idx < clusters.Count; idx++)
            {
                var cur = clusters[idx];
                if (cur.Kind != ClusterKind.PersianLetter) continue;

                // لیگاتور لام+الف
                if (cur.Emit != null && cur.Emit.StartsWith("LAM_ALEF:"))
                {
                    char alefVariant = cur.Emit[9];
                    var lig = LamAlef[alefVariant];
                    bool prevConnects = HasJoiningNeighbor(clusters, idx, -1);
                    cur.Emit = (prevConnects ? lig.final : lig.iso).ToString();
                    continue;
                }

                var forms = Table[cur.BaseLetter];
                string diacritics = cur.Emit; // اعراب همراه (اگه بود)

                bool hasPrevJoin = forms.Type != JoinType.NoneJoin && HasJoiningNeighbor(clusters, idx, -1);
                bool hasNextJoin = forms.Type == JoinType.Dual && HasJoiningNeighbor(clusters, idx, +1);

                char shapedChar;
                if (forms.Type == JoinType.NoneJoin) shapedChar = forms.Isolated;
                else if (hasPrevJoin && hasNextJoin) shapedChar = forms.Medial;
                else if (hasPrevJoin) shapedChar = forms.Final;
                else if (hasNextJoin) shapedChar = forms.Initial;
                else shapedChar = forms.Isolated;

                cur.Emit = shapedChar + diacritics;
            }
        }

        /// <summary>
        /// بررسی می‌کنه آیا در جهت داده‌شده (±1) یک حرف فارسی قابل‌اتصال وجود داره،
        /// با عبور شفاف (transparent) از تگ‌های Rich Text.
        /// </summary>
        private static bool HasJoiningNeighbor(List<Cluster> clusters, int index, int direction)
        {
            int i = index + direction;
            while (i >= 0 && i < clusters.Count)
            {
                var c = clusters[i];
                if (c.Kind == ClusterKind.Tag) { i += direction; continue; } // شفاف - رد شو

                if (c.Kind != ClusterKind.PersianLetter) return false; // عدد/لاتین/فاصله/علامت -> اتصال قطع می‌شه

                // همسایه یک حرف فارسیه. برای join شدن باید بتونه به این سمت وصل بشه.
                if (c.Emit != null && c.Emit.StartsWith("LAM_ALEF:"))
                    return direction < 0; // لیگاتور لام-الف فقط از سمت راست (قبل) قابل اتصاله، بعدش نه

                var neighborForms = Table[c.BaseLetter];

                if (direction < 0)
                    // همسایه‌ی قبلی: باید بتونه به بعدی (یعنی به ما) وصل بشه => باید Dual باشه
                    return neighborForms.Type == JoinType.Dual;
                else
                    // همسایه‌ی بعدی: کافیه بتونه از قبل (یعنی از ما) وصل بگیره => Dual یا RightOnly
                    return neighborForms.Type != JoinType.NoneJoin;
            }
            return false;
        }

        /// <summary>پاک کردن کش (مثلاً بعد از تغییر زبان یا تنظیمات)</summary>
        public static void ClearCache() => _cache.Clear();

        // ---------------------------------------------------------------
        // پشتیبانی از آشکارسازیِ تدریجی (برای افکت‌های تایپ‌رایتر/کلمه‌به‌کلمه)
        // ---------------------------------------------------------------

        /// <summary>
        /// تعداد «خوشه»های منطقیِ متن (نه تعداد کاراکترهای خام) — واحدِ درستی که باید
        /// برای آشکارسازیِ تدریجی (تایپ‌رایتر) بر اساسش پیش برید، نه تعداد کاراکتر.
        /// </summary>
        public static int GetClusterCount(string input, Options options = default)
        {
            if (string.IsNullOrEmpty(input)) return 0;
            return Tokenize(input, options).Count;
        }

        /// <summary>
        /// پردازشِ متن تا فقط اولین N خوشه (به ترتیبِ منطقیِ تایپ‌شدن، یعنی از سمتِ راستِ
        /// جمله) نمایش داده بشه. برخلافِ برش‌دادنِ سطحیِ رشته‌ی shaped، اینجا شکل‌دهیِ حروف
        /// دوباره فقط روی همین زیرمجموعه انجام می‌شه — یعنی آخرین حرفِ نمایش‌داده‌شده هیچ‌وقت
        /// شکلِ «منتظرِ اتصالِ بعدی» نمی‌گیره، چون از دیدِ Shaper اصلاً همسایه‌ی بعدی وجود نداره.
        /// همین باعث می‌شه هم جهتِ آشکارسازی طبیعی باشه (از راست به چپ) هم اتصالِ حروف تمیز بمونه.
        /// </summary>
        public static string ProcessPartial(string input, Options options, int visibleClusterCount)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var all = Tokenize(input, options);
            int count = visibleClusterCount < 0 ? 0 : (visibleClusterCount > all.Count ? all.Count : visibleClusterCount);

            var visible = all.GetRange(0, count);
            ShapeClusters(visible); // با دیدِ محدود به همین زیرلیست، بدون همسایه‌ی خیالی

            visible.Reverse();
            var sb = new StringBuilder();
            foreach (var c in visible) sb.Append(c.Emit);
            return sb.ToString();
        }

        /// <summary>
        /// فهرستِ «تعداد خوشه‌ی تجمعی تا پایانِ هر کلمه» — برای افکتِ کلمه‌به‌کلمه.
        /// یعنی اگر ProcessPartial را با یکی از این اعداد صدا بزنید، دقیقاً یک کلمه‌ی
        /// کامل (نه نصفه) بیشتر آشکار می‌شود.
        /// </summary>
        public static int[] GetWordBoundaryClusterCounts(string input, Options options = default)
        {
            if (string.IsNullOrEmpty(input)) return new int[0];

            var clusters = Tokenize(input, options);
            var boundaries = new List<int>();

            for (int i = 0; i < clusters.Count; i++)
            {
                if (clusters[i].Kind == ClusterKind.Other && clusters[i].Emit == " ")
                    boundaries.Add(i); // تا درست قبل از این فاصله، یک کلمه کامل شده
            }

            if (boundaries.Count == 0 || boundaries[boundaries.Count - 1] != clusters.Count)
                boundaries.Add(clusters.Count); // آخرین کلمه (که فاصله‌ای بعدش نیست)

            return boundaries.ToArray();
        }
    }
}