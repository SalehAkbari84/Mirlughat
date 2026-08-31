// ============================================================================
//  GameSecurity Toolkit  v2.0  —  بدون پکیج خارجی، همه پلتفرم‌ها
// ============================================================================
//
//  پشتیبانی: Android / iOS / PC / Mac / WebGL  (همه پلتفرم‌های Unity)
//
//  فقط از namespace های استاندارد .NET استفاده می‌کند:
//  System, System.IO, System.Security.Cryptography, System.Text,
//  System.Threading.Tasks, UnityEngine
//  بخشی از خودِ Unity هستند — هیچ DLL یا پکیج بیرونی لازم نیست.
//
//  تغییرات نسخه ۲.۰ نسبت به نسخه قبلی:
//   ✓ حذف کامل lockToDevice — دیگه deviceUniqueIdentifier استفاده نمیشه
//     (چون روی iOS بعد از پاک کردن اپ عوض میشه و روی Android به signing key
//      وابسته‌ست. استفاده از اون می‌توانست باعث از دست رفتن سیو بازیکن بشه)
//   ✓ اضافه شدن SaveAsync / LoadAsync — PBKDF2 روی ترد جدا اجرا می‌شود
//     تا گیم‌پلی هیچ‌وقت freeze نکند
//   ✓ اضافه شدن Atomic Write — فایل اول به .tmp نوشته می‌شود بعد rename
//     می‌شود؛ یعنی اگر گوشی وسط ذخیره خاموش شود، سیو قبلی دست نخورده می‌ماند
//   ✓ اضافه شدن Auto-Backup — کنار هر .sav یک .bak هم ذخیره می‌شود؛
//     اگر فایل اصلی دستکاری یا خراب شود، از بکاپ بارگذاری می‌شود
//   ✓ API ساده‌تر — دیگه نیازی به پارامتر اضافه نیست
//   ✓ اضافه شدن Editor Menu برای ساختن EmbeddedSecret جدید
//
//  ── تنها کاری که باید انجام دهید ──
//   این فایل را در پوشه Scripts پروژه بریزید، EmbeddedSecret را تغییر دهید
//   (از منوی Tools > GameSecurity > Generate Secret)، و از SecureSave
//   در کدتان استفاده کنید. همین. هیچ کار دیگری لازم نیست.
// ============================================================================

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameSecurity
{
    // ════════════════════════════════════════════════════════════════════
    //  CryptoCore  —  هسته‌ی رمزنگاری AES-256-CBC + HMAC-SHA256 + PBKDF2
    // ════════════════════════════════════════════════════════════════════
    public static class CryptoCore
    {
        // ── ثابت‌های ساختار فایل رمزنگاری‌شده ──
        // قالب باینری: [Salt:16] [IV:16] [Ciphertext:N] [HMAC:32]
        private const int SaltSize         = 16;
        private const int IvSize           = 16;
        private const int KeySize          = 32;  // 256-bit
        private const int HmacSize         = 32;  // SHA-256 output
        private const int MinEncryptedSize = SaltSize + IvSize + HmacSize + 1;

        // ── تعداد تکرار PBKDF2 ──
        // عمداً کُند است. هر چی بیشتر، brute-force سخت‌تر.
        // روی گوشی‌های متوسط: ~20-80ms — قابل‌قبول برای عملیات ذخیره/بارگذاری.
        // اگه روی گوشی‌های خیلی ضعیف کند شد، به 50_000 کاهش بده.
        private const int Pbkdf2Iterations = 100_000;

        // ── از یک پسورد + Salt، دو کلید کاملاً جدا تولید می‌کند ──
        // کلید رمزنگاری AES و کلید HMAC باید همیشه جدا باشند
        private static (byte[] encKey, byte[] hmacKey) DeriveKeys(string password, byte[] salt)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(
                password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256);

            byte[] combined  = pbkdf2.GetBytes(KeySize * 2);
            byte[] encKey    = new byte[KeySize];
            byte[] hmacKey   = new byte[KeySize];
            Buffer.BlockCopy(combined, 0,       encKey,   0, KeySize);
            Buffer.BlockCopy(combined, KeySize, hmacKey,  0, KeySize);
            return (encKey, hmacKey);
        }

        // محاسبه‌ی HMAC روی چند بخش پیوسته بدون کپی غیرضروری
        private static byte[] ComputeHmac(byte[] key, byte[] salt, byte[] iv, byte[] cipher)
        {
            using var hmac = new HMACSHA256(key);
            hmac.TransformBlock(salt,   0, salt.Length,   null, 0);
            hmac.TransformBlock(iv,     0, iv.Length,     null, 0);
            hmac.TransformFinalBlock(cipher, 0, cipher.Length);
            return hmac.Hash;
        }

        // مقایسه‌ی زمان‌ثابت — جلوگیری از Timing Attack
        private static bool ConstantTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private static byte[] SecureRandom(int length)
        {
            byte[] buf = new byte[length];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(buf);
            return buf;
        }

        /// <summary>
        /// رمزنگاری آرایه‌ی بایت. خروجی: [Salt|IV|Ciphertext|HMAC]
        /// هر بار با Salt/IV تصادفی جدید — خروجی هر بار متفاوت است.
        /// این متد Thread-Safe است و روی background thread قابل فراخوانی است.
        /// </summary>
        public static byte[] EncryptBytes(byte[] plainBytes, string password)
        {
            if (plainBytes == null) throw new ArgumentNullException(nameof(plainBytes));
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));

            byte[] salt = SecureRandom(SaltSize);
            var (encKey, hmacKey) = DeriveKeys(password, salt);

            using var aes = Aes.Create();
            aes.KeySize  = 256;
            aes.Key      = encKey;
            aes.Mode     = CipherMode.CBC;
            aes.Padding  = PaddingMode.PKCS7;
            aes.GenerateIV();
            byte[] iv = aes.IV;

            byte[] cipherBytes;
            using (var encryptor = aes.CreateEncryptor())
            using (var ms = new MemoryStream())
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            {
                cs.Write(plainBytes, 0, plainBytes.Length);
                cs.FlushFinalBlock();
                cipherBytes = ms.ToArray();
            }

            byte[] hmac = ComputeHmac(hmacKey, salt, iv, cipherBytes);

            // ترکیب نهایی: Salt | IV | Ciphertext | HMAC
            byte[] output = new byte[SaltSize + IvSize + cipherBytes.Length + HmacSize];
            int pos = 0;
            Buffer.BlockCopy(salt,        0, output, pos, SaltSize);      pos += SaltSize;
            Buffer.BlockCopy(iv,          0, output, pos, IvSize);        pos += IvSize;
            Buffer.BlockCopy(cipherBytes, 0, output, pos, cipherBytes.Length); pos += cipherBytes.Length;
            Buffer.BlockCopy(hmac,        0, output, pos, HmacSize);
            return output;
        }

        /// <summary>
        /// رمزگشایی. ابتدا HMAC بررسی می‌شود — اگر فایل دستکاری شده باشد
        /// یا پسورد اشتباه باشد، CryptographicException پرتاب می‌شود.
        /// این متد Thread-Safe است و روی background thread قابل فراخوانی است.
        /// </summary>
        public static byte[] DecryptBytes(byte[] encryptedData, string password)
        {
            if (encryptedData == null || encryptedData.Length < MinEncryptedSize)
                throw new CryptographicException("داده رمزنگاری‌شده نامعتبر یا ناقص است.");
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException(nameof(password));

            // جدا کردن بخش‌ها
            byte[] salt  = new byte[SaltSize];
            byte[] iv    = new byte[IvSize];
            int cipherLen = encryptedData.Length - SaltSize - IvSize - HmacSize;
            byte[] cipher = new byte[cipherLen];
            byte[] rxHmac = new byte[HmacSize];

            int pos = 0;
            Buffer.BlockCopy(encryptedData, pos, salt,   0, SaltSize);  pos += SaltSize;
            Buffer.BlockCopy(encryptedData, pos, iv,     0, IvSize);    pos += IvSize;
            Buffer.BlockCopy(encryptedData, pos, cipher, 0, cipherLen); pos += cipherLen;
            Buffer.BlockCopy(encryptedData, pos, rxHmac, 0, HmacSize);

            var (encKey, hmacKey) = DeriveKeys(password, salt);

            // ── اول HMAC بررسی می‌شود، بعد رمزگشایی (Encrypt-then-MAC) ──
            byte[] exHmac = ComputeHmac(hmacKey, salt, iv, cipher);
            if (!ConstantTimeEquals(exHmac, rxHmac))
                throw new CryptographicException("HMAC mismatch — فایل دستکاری شده یا پسورد نادرست است.");

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Key     = encKey;
            aes.IV      = iv;
            aes.Mode    = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var dec = aes.CreateDecryptor();
            using var ms  = new MemoryStream(cipher);
            using var cs  = new CryptoStream(ms, dec, CryptoStreamMode.Read);
            using var res = new MemoryStream();
            cs.CopyTo(res);
            return res.ToArray();
        }

        // ── کمک‌کننده‌های رشته‌ای ──
        public static string EncryptString(string plainText, string password)
            => Convert.ToBase64String(EncryptBytes(Encoding.UTF8.GetBytes(plainText ?? ""), password));

        public static string DecryptString(string cipherBase64, string password)
            => Encoding.UTF8.GetString(DecryptBytes(Convert.FromBase64String(cipherBase64), password));
    }


    // ════════════════════════════════════════════════════════════════════
    //  ProjectSecret  —  کلید مخصوص پروژه (یک‌بار تنظیم، همیشه کار می‌کند)
    // ════════════════════════════════════════════════════════════════════
    public static class ProjectSecret
    {
        // ══════════════════════════════════════════════════════════════
        //  ⚠️  تنها چیزی که باید در این فایل تغییر دهید:
        //
        //  این آرایه را برای هر پروژه‌ی جدید عوض کنید.
        //  از منوی ادیتور: Tools > GameSecurity > Generate New Secret
        //  خروجی Console را کپی کنید و اینجا جایگزین کنید.
        //
        //  ❌ هرگز پس از انتشار بازی این مقدار را عوض نکنید —
        //     سیو تمام بازیکنان باطل می‌شود.
        // ══════════════════════════════════════════════════════════════
        private static readonly byte[] EmbeddedSecret =
        {
            0xC2, 0xB2, 0x73, 0xE9, 0x0C, 0x76, 0xBE, 0x9B, 
            0x45, 0xF8, 0x4C, 0x5D, 0x2D, 0x51, 0x90, 0x54, 
            0x58, 0x6D, 0x03, 0xF4, 0x4C, 0xB5, 0x0A, 0xBE, 
            0xCB, 0x42, 0xA9, 0xE5, 0x4B, 0xCD, 0xE6, 0xE3, 
        };

        // Cache — یک‌بار محاسبه می‌شود، بقیه وقت‌ها همان رشته برگردانده می‌شود
        private static string _cachedPassword;

        /// <summary>
        /// پسورد پروژه — portable (بدون قفل به دستگاه)
        /// روی همه پلتفرم‌ها و بعد از هر reinstall یکسان است.
        /// </summary>
        public static string GetPassword()
        {
            if (_cachedPassword != null) return _cachedPassword;

            // EmbeddedSecret را با شناسه‌ی Bundle/Package مخلوط می‌کنیم.
            // این شناسه در طول عمر بازی ثابت است (com.yourcompany.yourgame)
            // و به سخت‌افزار دستگاه وابسته نیست — پس بعد از reinstall هم
            // همان مقدار را می‌دهد.
            byte[] bundleBytes = Encoding.UTF8.GetBytes(Application.identifier);
            byte[] combined    = new byte[EmbeddedSecret.Length + bundleBytes.Length];
            Buffer.BlockCopy(EmbeddedSecret, 0, combined, 0,                    EmbeddedSecret.Length);
            Buffer.BlockCopy(bundleBytes,    0, combined, EmbeddedSecret.Length, bundleBytes.Length);

            _cachedPassword = Convert.ToBase64String(combined);
            return _cachedPassword;
        }

        /// <summary>
        /// کش را پاک می‌کند — نیازی به صدا زدن دستی نیست.
        /// فقط برای تست Unit Test استفاده کنید.
        /// </summary>
        internal static void ClearCache() => _cachedPassword = null;

        /// <summary>
        /// یک آرایه‌ی تصادفی ۳۲ بایتی تولید می‌کند.
        /// نتیجه را در Console می‌بینید — کپی کنید و
        /// جایگزین EmbeddedSecret کنید.
        /// </summary>
        public static void PrintNewSecret()
        {
            byte[] random = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(random);

            var sb = new StringBuilder();
            sb.AppendLine("// این را کپی کنید و جایگزین EmbeddedSecret کنید:");
            sb.AppendLine("private static readonly byte[] EmbeddedSecret =");
            sb.AppendLine("{");
            for (int i = 0; i < random.Length; i += 8)
            {
                sb.Append("    ");
                for (int j = i; j < Math.Min(i + 8, random.Length); j++)
                    sb.Append($"0x{random[j]:X2}, ");
                sb.AppendLine();
            }
            sb.AppendLine("};");
            Debug.Log(sb.ToString());
        }

#if UNITY_EDITOR
        [MenuItem("Tools/GameSecurity/Generate New Secret")]
        private static void GenerateFromMenu() => PrintNewSecret();

        [MenuItem("Tools/GameSecurity/Show Save Folder")]
        private static void OpenSaveFolder()
        {
            string path = Application.persistentDataPath;
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }
#endif
    }


    // ════════════════════════════════════════════════════════════════════
    //  SecureSave  —  API اصلی برای ذخیره و بارگذاری
    // ════════════════════════════════════════════════════════════════════
    //
    //  ویژگی‌ها:
    //   • Atomic Write: فایل به .tmp نوشته می‌شود، بعد rename — اگر گوشی
    //     وسط ذخیره خاموش شود، سیو قبلی دست‌نخورده می‌ماند.
    //   • Auto-Backup: کنار هر .sav یک .bak ذخیره می‌شود — اگر فایل
    //     اصلی دستکاری شود، از بکاپ بارگذاری می‌شود.
    //   • Async: SaveAsync/LoadAsync روی ترد جدا اجرا می‌شوند تا
    //     گیم‌پلی freeze نکند.
    // ════════════════════════════════════════════════════════════════════
    public static class SecureSave
    {
        // ── رویدادها ──

        /// <summary>هنگامی که فایل سیو دستکاری‌شده تشخیص داده شود صدا می‌زند</summary>
        public static event Action<string> OnTamperDetected;

        /// <summary>هنگامی که بارگذاری از بکاپ انجام شود صدا می‌زند (کلید سیو)</summary>
        public static event Action<string> OnRestoredFromBackup;

        /// <summary>هنگامی که ذخیره موفقیت‌آمیز باشد صدا می‌زند (کلید سیو)</summary>
        public static event Action<string> OnSaveSuccess;


        // ── مسیرها ──
        private static string SavePath(string key)   => Path.Combine(Application.persistentDataPath, key + ".sav");
        private static string BackupPath(string key) => Path.Combine(Application.persistentDataPath, key + ".bak");
        private static string TempPath(string key)   => Path.Combine(Application.persistentDataPath, key + ".tmp");


        // ════════════════════════════════════════════════════════════════
        //  SAVE — ذخیره
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// ذخیره‌ی synchronous (blocking).
        /// برای Auto-Save یا ذخیره‌ی وسط گیم‌پلی از SaveAsync استفاده کنید.
        /// مناسب برای: دکمه‌ی "ذخیره"، صفحه‌ی Game Over، چک‌پوینت.
        /// </summary>
        public static void Save<T>(string key, T data)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            string json      = JsonUtility.ToJson(data);  // باید روی Main Thread باشد
            string password  = ProjectSecret.GetPassword();
            string savePath  = SavePath(key);
            string backPath  = BackupPath(key);
            string tempPath  = TempPath(key);

            WriteAtomic(key, json, password, savePath, backPath, tempPath);
        }

        /// <summary>
        /// ذخیره‌ی asynchronous (non-blocking) — PBKDF2 روی ترد جدا اجرا می‌شود.
        /// گیم‌پلی هیچ‌وقت freeze نمی‌کند.
        /// مناسب برای: Auto-Save هر N ثانیه.
        ///
        /// استفاده:
        ///   await SecureSave.SaveAsync("playerSave", data);
        ///   // یا بدون await:
        ///   _ = SecureSave.SaveAsync("playerSave", data);
        /// </summary>
        public static async Task SaveAsync<T>(string key, T data)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            // این سه مورد باید روی Main Thread خوانده شوند
            string json     = JsonUtility.ToJson(data);
            string password = ProjectSecret.GetPassword();
            string savePath = SavePath(key);
            string backPath = BackupPath(key);
            string tempPath = TempPath(key);

            // رمزنگاری و نوشتن فایل روی ترد جدا
            await Task.Run(() => WriteAtomic(key, json, password, savePath, backPath, tempPath));
        }


        // ════════════════════════════════════════════════════════════════
        //  LOAD — بارگذاری
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// بارگذاری synchronous (blocking).
        /// برای Auto-Load یا بارگذاری پس‌زمینه از LoadAsync استفاده کنید.
        /// اگر فایل وجود نداشته باشد: defaultValue برمی‌گردد.
        /// اگر فایل اصلی دستکاری شده باشد: از بکاپ بارگذاری می‌شود.
        /// اگر هر دو دستکاری شده باشند: OnTamperDetected صدا می‌زند و
        /// defaultValue برمی‌گردد — بازی کرش نمی‌کند.
        /// </summary>
        public static T Load<T>(string key, T defaultValue = default)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            string password = ProjectSecret.GetPassword();
            return ReadWithFallback<T>(key, password, defaultValue);
        }

        /// <summary>
        /// بارگذاری asynchronous (non-blocking) — روی ترد جدا اجرا می‌شود.
        /// مناسب برای: لودینگ صفحه، Splash Screen.
        ///
        /// استفاده:
        ///   var data = await SecureSave.LoadAsync("playerSave", new SaveData());
        /// </summary>
        public static async Task<T> LoadAsync<T>(string key, T defaultValue = default)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            string password = ProjectSecret.GetPassword();
            return await Task.Run(() => ReadWithFallback<T>(key, password, defaultValue));
        }


        // ════════════════════════════════════════════════════════════════
        //  ابزارهای کمکی عمومی
        // ════════════════════════════════════════════════════════════════

        /// <summary>آیا فایل سیوی با این کلید وجود دارد؟</summary>
        public static bool Exists(string key) => File.Exists(SavePath(key));

        /// <summary>حذف فایل سیو و بکاپ آن</summary>
        public static void Delete(string key)
        {
            TryDelete(SavePath(key));
            TryDelete(BackupPath(key));
            TryDelete(TempPath(key));
        }

        /// <summary>
        /// رمزنگاری دستی یک رشته‌ی دلخواه
        /// (مثلاً برای ذخیره در PlayerPrefs یا ارسال به سرور)
        /// </summary>
        public static string EncryptString(string plainText)
            => CryptoCore.EncryptString(plainText, ProjectSecret.GetPassword());

        /// <summary>رمزگشایی دستی — خروجی EncryptString را ورودی می‌گیرد</summary>
        public static string DecryptString(string cipherBase64)
            => CryptoCore.DecryptString(cipherBase64, ProjectSecret.GetPassword());


        // ════════════════════════════════════════════════════════════════
        //  پیاده‌سازی داخلی
        // ════════════════════════════════════════════════════════════════

        // Atomic Write + Auto-Backup
        private static void WriteAtomic(string key, string json, string password,
                                        string savePath, string backPath, string tempPath)
        {
            try
            {
                byte[] encrypted = CryptoCore.EncryptBytes(Encoding.UTF8.GetBytes(json), password);

                // ۱. اول به .tmp بنویس (اگر قطع شد، .sav دست نمیخوره)
                File.WriteAllBytes(tempPath, encrypted);

                // ۲. سیو قبلی را به .bak تبدیل کن
                if (File.Exists(savePath))
                {
                    TryDelete(backPath);
                    File.Move(savePath, backPath);
                }

                // ۳. .tmp را به .sav تبدیل کن
                File.Move(tempPath, savePath);

                OnSaveSuccess?.Invoke(key);
            }
            catch (Exception ex)
            {
                TryDelete(tempPath);
                Debug.LogError($"[SecureSave] خطا در ذخیره '{key}': {ex.Message}");
                throw;
            }
        }

        // بارگذاری با fallback خودکار به بکاپ
        private static T ReadWithFallback<T>(string key, string password, T defaultValue)
        {
            string savePath = SavePath(key);
            string backPath = BackupPath(key);

            // فایل اصلاً وجود ندارد
            if (!File.Exists(savePath) && !File.Exists(backPath))
                return defaultValue;

            // تلاش اول: فایل اصلی
            if (File.Exists(savePath))
            {
                var (result, ok) = TryReadFile<T>(savePath, password);
                if (ok) return result;

                // فایل اصلی خراب یا دستکاری شده — بکاپ را امتحان کن
                Debug.LogWarning($"[SecureSave] فایل اصلی '{key}' معتبر نیست، از بکاپ بارگذاری می‌شود.");
            }

            // تلاش دوم: بکاپ
            if (File.Exists(backPath))
            {
                var (result, ok) = TryReadFile<T>(backPath, password);
                if (ok)
                {
                    OnRestoredFromBackup?.Invoke(key);
                    return result;
                }
            }

            // هر دو خراب یا دستکاری شده
            OnTamperDetected?.Invoke(key);
            Debug.LogWarning($"[SecureSave] هر دو فایل سیو '{key}' معتبر نیستند. مقدار پیش‌فرض برگردانده می‌شود.");
            return defaultValue;
        }

        private static (T result, bool success) TryReadFile<T>(string path, string password)
        {
            try
            {
                byte[] encrypted = File.ReadAllBytes(path);
                byte[] plain     = CryptoCore.DecryptBytes(encrypted, password);
                string json      = Encoding.UTF8.GetString(plain);
                T obj            = JsonUtility.FromJson<T>(json);
                return (obj, true);
            }
            catch (CryptographicException)
            {
                return (default, false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SecureSave] خطای غیرمنتظره در خواندن '{path}': {ex.Message}");
                return (default, false);
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* نادیده بگیر */ }
        }
    }


    // ════════════════════════════════════════════════════════════════════
    //  ObfuscationRng  —  PRNG سریع داخلی (xorshift64)
    //  Thread-Safe نیست — عمداً، چون متغیرها همیشه رو یک ترد استفاده می‌شن
    // ════════════════════════════════════════════════════════════════════
    internal static class ObfuscationRng
    {
        private static ulong _state;

        static ObfuscationRng()
        {
            byte[] seed = new byte[8];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(seed);
            _state = BitConverter.ToUInt64(seed, 0);
            if (_state == 0) _state = 0xDEADBEEFA5A5A5A5UL;
        }

        public static int NextInt()
        {
            _state ^= _state << 13;
            _state ^= _state >> 7;
            _state ^= _state << 17;
            return unchecked((int)_state);
        }

        public static long NextLong()
        {
            _state ^= _state << 13;
            _state ^= _state >> 7;
            _state ^= _state << 17;
            return unchecked((long)_state);
        }

        public static byte NextByte() => (byte)(NextInt() & 0xFF);
    }


    // ════════════════════════════════════════════════════════════════════
    //  Obscured Types  —  متغیرهایی که مقدار خامشان هرگز در RAM نیست
    // ════════════════════════════════════════════════════════════════════
    //
    //  چون struct هستند، Reobscure() فقط روی field کار می‌کند، نه property:
    //      public ObscuredInt gold;            // ✓ درست
    //      public ObscuredInt Gold { get; set; } // ✗ Reobscure کار نمی‌کند

    [Serializable]
    public struct ObscuredInt : IEquatable<ObscuredInt>
    {
        private int _cipher;
        private int _key;

        public ObscuredInt(int value)
        {
            _key    = ObfuscationRng.NextInt();
            _cipher = value ^ _key;
        }

        public int Value
        {
            get => _cipher ^ _key;
            set
            {
                int k   = ObfuscationRng.NextInt();
                _cipher = value ^ k;
                _key    = k;
            }
        }

        /// <summary>
        /// بدون تغییر مقدار منطقی، بایت‌های خام حافظه را عوض می‌کند.
        /// هر ۳-۵ ثانیه برای متغیرهای حساس صدا بزنید.
        /// </summary>
        public void Reobscure()
        {
            int cur = Value;
            int k   = ObfuscationRng.NextInt();
            _cipher = cur ^ k;
            _key    = k;
        }

        public static implicit operator int(ObscuredInt o)       => o.Value;
        public static implicit operator ObscuredInt(int v)       => new ObscuredInt(v);
        public static ObscuredInt operator +(ObscuredInt a, ObscuredInt b) => new ObscuredInt(a.Value + b.Value);
        public static ObscuredInt operator -(ObscuredInt a, ObscuredInt b) => new ObscuredInt(a.Value - b.Value);
        public static ObscuredInt operator *(ObscuredInt a, ObscuredInt b) => new ObscuredInt(a.Value * b.Value);
        public static ObscuredInt operator +(ObscuredInt a, int b)         => new ObscuredInt(a.Value + b);
        public static ObscuredInt operator -(ObscuredInt a, int b)         => new ObscuredInt(a.Value - b);
        public static ObscuredInt operator *(ObscuredInt a, int b)         => new ObscuredInt(a.Value * b);
        public static ObscuredInt operator ++(ObscuredInt a)               => new ObscuredInt(a.Value + 1);
        public static ObscuredInt operator --(ObscuredInt a)               => new ObscuredInt(a.Value - 1);
        public static bool operator ==(ObscuredInt a, ObscuredInt b)       => a.Value == b.Value;
        public static bool operator !=(ObscuredInt a, ObscuredInt b)       => a.Value != b.Value;
        public static bool operator  >(ObscuredInt a, ObscuredInt b)       => a.Value  > b.Value;
        public static bool operator  <(ObscuredInt a, ObscuredInt b)       => a.Value  < b.Value;
        public static bool operator >=(ObscuredInt a, ObscuredInt b)       => a.Value >= b.Value;
        public static bool operator <=(ObscuredInt a, ObscuredInt b)       => a.Value <= b.Value;
        public bool Equals(ObscuredInt other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ObscuredInt o && Equals(o);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
    }


    [Serializable]
    public struct ObscuredFloat : IEquatable<ObscuredFloat>
    {
        private int _cipher; // بیت‌های float به صورت int ذخیره می‌شود
        private int _key;

        public ObscuredFloat(float value)
        {
            _key    = ObfuscationRng.NextInt();
            _cipher = F2I(value) ^ _key;
        }

        public float Value
        {
            get => I2F(_cipher ^ _key);
            set { int k = ObfuscationRng.NextInt(); _cipher = F2I(value) ^ k; _key = k; }
        }

        public void Reobscure()
        {
            float cur = Value;
            int k = ObfuscationRng.NextInt();
            _cipher = F2I(cur) ^ k;
            _key = k;
        }

        private static int   F2I(float f) => BitConverter.ToInt32(BitConverter.GetBytes(f), 0);
        private static float I2F(int i)   => BitConverter.ToSingle(BitConverter.GetBytes(i), 0);

        public static implicit operator float(ObscuredFloat o)          => o.Value;
        public static implicit operator ObscuredFloat(float v)          => new ObscuredFloat(v);
        public static ObscuredFloat operator +(ObscuredFloat a, ObscuredFloat b) => new ObscuredFloat(a.Value + b.Value);
        public static ObscuredFloat operator -(ObscuredFloat a, ObscuredFloat b) => new ObscuredFloat(a.Value - b.Value);
        public static ObscuredFloat operator *(ObscuredFloat a, ObscuredFloat b) => new ObscuredFloat(a.Value * b.Value);
        public static ObscuredFloat operator +(ObscuredFloat a, float b)         => new ObscuredFloat(a.Value + b);
        public static ObscuredFloat operator -(ObscuredFloat a, float b)         => new ObscuredFloat(a.Value - b);
        public static ObscuredFloat operator *(ObscuredFloat a, float b)         => new ObscuredFloat(a.Value * b);
        public bool Equals(ObscuredFloat other) => Mathf.Approximately(Value, other.Value);
        public override bool Equals(object obj) => obj is ObscuredFloat o && Equals(o);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
    }


    [Serializable]
    public struct ObscuredLong : IEquatable<ObscuredLong>
    {
        private long _cipher;
        private long _key;

        public ObscuredLong(long value)
        {
            _key    = ObfuscationRng.NextLong();
            _cipher = value ^ _key;
        }

        public long Value
        {
            get => _cipher ^ _key;
            set { long k = ObfuscationRng.NextLong(); _cipher = value ^ k; _key = k; }
        }

        public void Reobscure()
        {
            long cur = Value;
            long k   = ObfuscationRng.NextLong();
            _cipher  = cur ^ k;
            _key     = k;
        }

        public static implicit operator long(ObscuredLong o)           => o.Value;
        public static implicit operator ObscuredLong(long v)           => new ObscuredLong(v);
        public static ObscuredLong operator +(ObscuredLong a, long b)  => new ObscuredLong(a.Value + b);
        public static ObscuredLong operator -(ObscuredLong a, long b)  => new ObscuredLong(a.Value - b);
        public static ObscuredLong operator ++(ObscuredLong a)         => new ObscuredLong(a.Value + 1);
        public static ObscuredLong operator --(ObscuredLong a)         => new ObscuredLong(a.Value - 1);
        public bool Equals(ObscuredLong other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ObscuredLong o && Equals(o);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
    }


    [Serializable]
    public struct ObscuredBool : IEquatable<ObscuredBool>
    {
        private ObscuredInt _inner;

        public ObscuredBool(bool value)   { _inner = new ObscuredInt(value ? 1 : 0); }

        public bool Value
        {
            get => _inner.Value != 0;
            set => _inner.Value = value ? 1 : 0;
        }

        public void Reobscure() => _inner.Reobscure();

        public static implicit operator bool(ObscuredBool o)     => o.Value;
        public static implicit operator ObscuredBool(bool v)     => new ObscuredBool(v);
        public static bool operator ==(ObscuredBool a, ObscuredBool b) => a.Value == b.Value;
        public static bool operator !=(ObscuredBool a, ObscuredBool b) => a.Value != b.Value;
        public bool Equals(ObscuredBool other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ObscuredBool o && Equals(o);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
    }


    [Serializable]
    public struct ObscuredString : IEquatable<ObscuredString>
    {
        private byte[] _cipher;
        private byte[] _key;

        public ObscuredString(string value)
        {
            byte[] plain = Encoding.UTF8.GetBytes(value ?? "");
            _key    = MakeKey(plain.Length);
            _cipher = Xor(plain, _key);
        }

        public string Value
        {
            get => _cipher == null ? "" : Encoding.UTF8.GetString(Xor(_cipher, _key));
            set
            {
                byte[] plain = Encoding.UTF8.GetBytes(value ?? "");
                _key    = MakeKey(plain.Length);
                _cipher = Xor(plain, _key);
            }
        }

        public void Reobscure()
        {
            byte[] plain = Encoding.UTF8.GetBytes(Value);
            _key    = MakeKey(plain.Length);
            _cipher = Xor(plain, _key);
        }

        private static byte[] MakeKey(int len)
        {
            byte[] k = new byte[len];
            for (int i = 0; i < len; i++) k[i] = ObfuscationRng.NextByte();
            return k;
        }

        private static byte[] Xor(byte[] data, byte[] key)
        {
            byte[] res = new byte[data.Length];
            for (int i = 0; i < data.Length; i++) res[i] = (byte)(data[i] ^ key[i]);
            return res;
        }

        public static implicit operator string(ObscuredString o)     => o.Value;
        public static implicit operator ObscuredString(string v)     => new ObscuredString(v);
        public bool Equals(ObscuredString other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ObscuredString o && Equals(o);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value;
    }


    // ════════════════════════════════════════════════════════════════════
    //  نمونه استفاده کامل
    // ════════════════════════════════════════════════════════════════════
    /*

    using UnityEngine;
    using System.Threading.Tasks;
    using GameSecurity;

    // ── ساختار داده‌ی سیو ──
    [System.Serializable]
    public class SaveData
    {
        public int   gold;
        public int   level;
        public float totalPlayTime;
        public string playerName;
    }


    // ── مدیریت متغیرهای بازی با Obscured Types ──
    public class PlayerStats : MonoBehaviour
    {
        // به‌جای int/float/bool معمولی:
        public ObscuredInt   gold         = 100;
        public ObscuredInt   diamonds     = 0;
        public ObscuredFloat health       = 100f;
        public ObscuredBool  isInvincible = false;

        private float _timer;

        void Update()
        {
            // هر ۳ ثانیه بایت‌های RAM رو عوض کن
            _timer += Time.deltaTime;
            if (_timer > 3f)
            {
                gold.Reobscure();
                diamonds.Reobscure();
                health.Reobscure();
                _timer = 0f;
            }
        }

        // استفاده دقیقاً مثل int/float/bool معمولی:
        public void AddGold(int amount)     => gold    += amount;
        public void TakeDamage(float dmg)   => health  -= dmg;
        public bool CanAfford(int price)    => gold    >= price;
        public bool IsDead()                => health  <= 0f;
    }


    // ── مدیریت ذخیره / بارگذاری ──
    public class SaveManager : MonoBehaviour
    {
        private void OnEnable()
        {
            // رویدادها — ثبت‌نام کنید تا مطلع شوید
            SecureSave.OnTamperDetected    += key => Debug.LogWarning($"سیو دستکاری شده: {key}");
            SecureSave.OnRestoredFromBackup += key => Debug.Log($"از بکاپ بارگذاری شد: {key}");
            SecureSave.OnSaveSuccess        += key => Debug.Log($"ذخیره موفق: {key}");
        }

        // ── ذخیره معمولی (مثلاً دکمه‌ی سیو یا صفحه‌ی پایان) ──
        public void SaveGame(SaveData data)
        {
            SecureSave.Save("playerSave", data);
        }

        // ── ذخیره async (مثلاً Auto-Save هر ۳۰ ثانیه) ──
        public async void AutoSave(SaveData data)
        {
            await SecureSave.SaveAsync("playerSave", data);
            // بعد از اینجا گیم‌پلی هیچ وقت قطع نشد
        }

        // ── بارگذاری معمولی ──
        public SaveData LoadGame()
        {
            return SecureSave.Load("playerSave", new SaveData());
            // اگه فایل نبود: SaveData خالی برمیگرده
            // اگه دستکاری شده بود: از بکاپ برمیگرده
            // اگه هر دو خراب بودن: SaveData خالی برمیگرده + رویداد OnTamperDetected
        }

        // ── بارگذاری async (مثلاً صفحه‌ی Loading) ──
        public async Task<SaveData> LoadGameAsync()
        {
            return await SecureSave.LoadAsync("playerSave", new SaveData());
        }

        // ── رمزنگاری دستی یک مقدار (مثلاً برای PlayerPrefs) ──
        public void SaveHighScore(int score)
        {
            string encrypted = SecureSave.EncryptString(score.ToString());
            PlayerPrefs.SetString("HighScore", encrypted);
        }

        public int LoadHighScore()
        {
            string raw = PlayerPrefs.GetString("HighScore", "");
            if (string.IsNullOrEmpty(raw)) return 0;
            try   { return int.Parse(SecureSave.DecryptString(raw)); }
            catch { return 0; }
        }
    }

    */
}