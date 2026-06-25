// ============================================================================
//  GameSecurity Toolkit  —  بدون هیچ پکیج خارجی، قابل استفاده در همه پروژه‌ها
// ============================================================================
//
//  این فایل فقط از namespace های استاندارد .NET استفاده می‌کند:
//  System, System.IO, System.Security.Cryptography, System.Text
//  این‌ها بخشی از خودِ .NET/Unity هستند — نه یک Asset/Package دانلودی.
//  هیچ DLL یا کتابخانه‌ی بیرونی لازم نیست.
//
//  این فایل دو لایه‌ی محافظتی مستقل می‌دهد:
//
//   ① رمزنگاری فایل سیو (CryptoCore + SecureSave)
//      AES-256-CBC  +  HMAC-SHA256 (Encrypt-then-MAC)  +  PBKDF2 (100,000 iteration)
//      - هر بار رمزنگاری، Salt و IV تصادفی جدید تولید می‌شود (رمزنگاری deterministic نیست)
//      - کلید نهایی به دستگاه (deviceUniqueIdentifier) متصل می‌شود، یعنی فایل سیو
//        کپی‌شده به دستگاه دیگر باز نمی‌شود
//      - اگر فایل دستکاری شود، بررسی HMAC شکست می‌خورد و قبل از Decrypt تشخیص
//        داده می‌شود (یعنی مهاجم حتی نمی‌تواند مقدار را با امتحان‌وخطا حدس بزند)
//
//   ② مخفی‌سازی متغیرهای در حال اجرا در RAM (ObscuredInt / Float / Bool / Long / String)
//      مقدار واقعی هرگز به‌صورت خام در حافظه ذخیره نمی‌شود؛ با یک کلید XOR
//      تصادفی که در هر تغییر مقدار عوض می‌شود نگه‌داری می‌شود. این باعث می‌شود
//      اسکن حافظه با Cheat Engine (روش "Exact Value" یا "Unchanged Value")
//      عملاً بی‌فایده شود، چون بایت‌های خام هیچ‌وقت با مقدار منطقی یکی نیستند
//      و حتی اگر مقدار عوض نشود هم می‌توانید با ()Reobscure بایت‌ها را عوض کنید.
//
//  نحوه استفاده‌ی مجدد در پروژه‌های بعدی:
//   - این فایل را در پوشه Scripts پروژه‌ی جدید کپی کنید.
//   - حتماً مقدار EmbeddedSecret در کلاس ProjectSecret را عوض کنید (پایین‌تر
//     توضیح داده شده) تا هر پروژه کلید مخصوص به خودش را داشته باشد — اگر همه
//     پروژه‌ها از یک کلید استفاده کنند، کرک یکی یعنی کرک همه.
//   - بقیه‌ی کد (CryptoCore / SecureSave / Obscured Types) نیازی به تغییر ندارد.
// ============================================================================

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace GameSecurity
{
    // ════════════════════════════════════════════════════════════════════
    //  CryptoCore  —  هسته‌ی رمزنگاری AES-256 + HMAC-SHA256 + PBKDF2
    // ════════════════════════════════════════════════════════════════════
    public static class CryptoCore
    {
        private const int SaltSize = 16;
        private const int IvSize = 16;
        private const int KeySize = 32;     // 256-bit برای AES و برای HMAC
        private const int HmacSize = 32;    // خروجی SHA-256
        private const int Pbkdf2Iterations = 100_000; // کند عمدی، برای سخت‌کردن brute-force

        // ──────────────────────────────────────────────────────────────
        //  از یک Salt + Password، دو کلید جدا تولید می‌کند: یکی برای
        //  رمزنگاری AES و یکی برای HMAC. استفاده از یک کلید برای هر دو کار
        //  ضعف امنیتی شناخته‌شده‌ای است؛ این تابع آن را کاملاً جدا می‌کند.
        // ──────────────────────────────────────────────────────────────
        private static (byte[] encKey, byte[] hmacKey) DeriveKeys(string password, byte[] salt)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256);
            byte[] combined = pbkdf2.GetBytes(KeySize * 2);

            byte[] encKey = new byte[KeySize];
            byte[] hmacKey = new byte[KeySize];
            Buffer.BlockCopy(combined, 0, encKey, 0, KeySize);
            Buffer.BlockCopy(combined, KeySize, hmacKey, 0, KeySize);
            return (encKey, hmacKey);
        }

        private static byte[] ComputeHmac(byte[] key, params byte[][] parts)
        {
            using var hmac = new HMACSHA256(key);
            using var ms = new MemoryStream();
            foreach (var part in parts) ms.Write(part, 0, part.Length);
            ms.Position = 0;
            return hmac.ComputeHash(ms);
        }

        // مقایسه‌ی زمان‌ثابت — جلوگیری از Timing Attack روی بررسی HMAC
        private static bool ConstantTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private static byte[] SecureRandomBytes(int length)
        {
            byte[] bytes = new byte[length];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return bytes;
        }

        /// <summary>
        /// رمزنگاری بایت‌های خام. خروجی شامل: Salt | IV | Ciphertext | HMAC
        /// هر بار اجرا، خروجی متفاوتی می‌دهد حتی برای ورودی یکسان (به‌خاطر
        /// Salt/IV تصادفی) — این یعنی مهاجم نمی‌تواند با مقایسه‌ی دو فایل سیو
        /// مشابه، الگو پیدا کند.
        /// </summary>
        public static byte[] EncryptBytes(byte[] plainBytes, string password)
        {
            byte[] salt = SecureRandomBytes(SaltSize);
            var (encKey, hmacKey) = DeriveKeys(password, salt);

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Key = encKey;
            aes.GenerateIV();
            byte[] iv = aes.IV;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            byte[] cipherBytes;
            using (var encryptor = aes.CreateEncryptor())
            using (var ms = new MemoryStream())
            {
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    cs.Write(plainBytes, 0, plainBytes.Length);
                    cs.FlushFinalBlock();
                }
                cipherBytes = ms.ToArray();
            }

            byte[] hmac = ComputeHmac(hmacKey, salt, iv, cipherBytes);

            using var output = new MemoryStream();
            output.Write(salt, 0, salt.Length);
            output.Write(iv, 0, iv.Length);
            output.Write(cipherBytes, 0, cipherBytes.Length);
            output.Write(hmac, 0, hmac.Length);
            return output.ToArray();
        }

        /// <summary>
        /// رمزگشایی. اگر داده دستکاری شده باشد یا پسورد اشتباه باشد،
        /// CryptographicException پرتاب می‌شود — قبل از اینکه اصلاً تلاشی
        /// برای Decrypt واقعی انجام شود (بررسی HMAC اول انجام می‌شود).
        /// </summary>
        public static byte[] DecryptBytes(byte[] encryptedData, string password)
        {
            if (encryptedData == null || encryptedData.Length < SaltSize + IvSize + HmacSize)
                throw new CryptographicException("داده رمزنگاری‌شده نامعتبر یا ناقص است.");

            byte[] salt = new byte[SaltSize];
            byte[] iv = new byte[IvSize];
            int cipherLength = encryptedData.Length - SaltSize - IvSize - HmacSize;
            byte[] cipherBytes = new byte[cipherLength];
            byte[] receivedHmac = new byte[HmacSize];

            Buffer.BlockCopy(encryptedData, 0, salt, 0, SaltSize);
            Buffer.BlockCopy(encryptedData, SaltSize, iv, 0, IvSize);
            Buffer.BlockCopy(encryptedData, SaltSize + IvSize, cipherBytes, 0, cipherLength);
            Buffer.BlockCopy(encryptedData, SaltSize + IvSize + cipherLength, receivedHmac, 0, HmacSize);

            var (encKey, hmacKey) = DeriveKeys(password, salt);

            byte[] expectedHmac = ComputeHmac(hmacKey, salt, iv, cipherBytes);
            if (!ConstantTimeEquals(expectedHmac, receivedHmac))
                throw new CryptographicException("داده دستکاری شده یا رمز عبور نادرست است (HMAC mismatch).");

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Key = encKey;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(cipherBytes);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var result = new MemoryStream();
            cs.CopyTo(result);
            return result.ToArray();
        }

        public static string EncryptString(string plainText, string password)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText ?? "");
            return Convert.ToBase64String(EncryptBytes(plainBytes, password));
        }

        public static string DecryptString(string cipherTextBase64, string password)
        {
            byte[] encrypted = Convert.FromBase64String(cipherTextBase64);
            byte[] plainBytes = DecryptBytes(encrypted, password);
            return Encoding.UTF8.GetString(plainBytes);
        }
    }


    // ════════════════════════════════════════════════════════════════════
    //  ProjectSecret  —  مدیریت کلید مخصوص هر پروژه
    // ════════════════════════════════════════════════════════════════════
    public static class ProjectSecret
    {
        // ⚠️ مهم‌ترین خط این فایل ⚠️
        // این آرایه را برای *هر پروژه‌ی جدید* با یک مقدار تصادفی جدید عوض کنید.
        // برای ساخت یک آرایه‌ی تصادفی جدید: یک‌بار متد PrintNewSecretArray() را
        // از یک اسکریپت موقت یا از منوی زیر صدا بزنید و خروجی Console را اینجا
        // جایگزین کنید، بعد آن متد را پاک یا غیرفعال کنید.
        //
        //      [MenuItem("Tools/GameSecurity/Generate New Project Secret")]
        //
        private static readonly byte[] EmbeddedSecret =
        {
            0x4F, 0x3A, 0x9C, 0x12, 0x88, 0xAB, 0x77, 0x01,
            0xDE, 0xFA, 0x56, 0x23, 0x90, 0x6C, 0x1B, 0xE4,
            0x3D, 0x82, 0x0F, 0x55, 0x99, 0xC1, 0x44, 0x2E,
            0x71, 0xB6, 0xA0, 0x18, 0x5D, 0x9E, 0x33, 0x67
        };

        /// <summary>
        /// رمز نهایی پروژه: ترکیب کلید Embedded با شناسه‌ی یکتای دستگاه.
        /// نتیجه این است که حتی اگر فایل سیو رمزنگاری‌شده را عیناً به یک
        /// دستگاه دیگر کپی کنند، چون deviceUniqueIdentifier فرق دارد،
        /// HMAC verify شکست می‌خورد و فایل قابل خواندن نیست.
        /// </summary>
        public static string GetProjectPassword()
        {
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            byte[] deviceBytes = Encoding.UTF8.GetBytes(deviceId);

            byte[] combined = new byte[EmbeddedSecret.Length + deviceBytes.Length];
            Buffer.BlockCopy(EmbeddedSecret, 0, combined, 0, EmbeddedSecret.Length);
            Buffer.BlockCopy(deviceBytes, 0, combined, EmbeddedSecret.Length, deviceBytes.Length);

            return Convert.ToBase64String(combined);
        }

        /// <summary>
        /// رمز عبور بدون قفل به دستگاه — برای مواردی که می‌خواهید کاربر
        /// بتواند سیو را بین دستگاه‌های خودش جابه‌جا کند (مثلاً با Cloud Save).
        /// </summary>
        public static string GetPortablePassword()
        {
            return Convert.ToBase64String(EmbeddedSecret);
        }

        /// <summary>
        /// یک‌بار اجرا کنید تا یک کلید ۳۲ بایتی واقعاً تصادفی تولید شود.
        /// خروجی را در Console می‌بینید؛ آن را کپی کرده و جایگزین
        /// EmbeddedSecret در بالای همین کلاس کنید.
        /// </summary>
        public static void PrintNewSecretArray()
        {
            byte[] random = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(random);

            var sb = new StringBuilder();
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
    }


    // ════════════════════════════════════════════════════════════════════
    //  SecureSave  —  لایه‌ی راحت برای ذخیره/بارگذاری داده‌ی رمزنگاری‌شده
    // ════════════════════════════════════════════════════════════════════
    public static class SecureSave
    {
        /// <summary>وقتی فایلی دستکاری‌شده تشخیص داده شود، این event صدا زده می‌شود</summary>
        public static event Action<string> OnTamperDetected;

        private static string GetFilePath(string key) =>
            Path.Combine(Application.persistentDataPath, key + ".sav");

        /// <summary>
        /// ذخیره‌ی یک آبجکت [Serializable] به‌صورت رمزنگاری‌شده.
        /// </summary>
        public static void Save<T>(string key, T data, bool lockToDevice = true)
        {
            string json = JsonUtility.ToJson(data);
            string password = lockToDevice
                ? ProjectSecret.GetProjectPassword()
                : ProjectSecret.GetPortablePassword();

            byte[] encrypted = CryptoCore.EncryptBytes(Encoding.UTF8.GetBytes(json), password);
            File.WriteAllBytes(GetFilePath(key), encrypted);
        }

        /// <summary>
        /// بارگذاری. اگر فایل وجود نداشته باشد defaultValue برمی‌گردد.
        /// اگر فایل دستکاری شده باشد، OnTamperDetected فراخوانی می‌شود و
        /// defaultValue برمی‌گردد (بازی کرش نمی‌کند).
        /// </summary>
        public static T Load<T>(string key, T defaultValue = default, bool lockToDevice = true)
        {
            string path = GetFilePath(key);
            if (!File.Exists(path)) return defaultValue;

            try
            {
                byte[] encrypted = File.ReadAllBytes(path);
                string password = lockToDevice
                    ? ProjectSecret.GetProjectPassword()
                    : ProjectSecret.GetPortablePassword();

                byte[] plainBytes = CryptoCore.DecryptBytes(encrypted, password);
                string json = Encoding.UTF8.GetString(plainBytes);
                return JsonUtility.FromJson<T>(json);
            }
            catch (CryptographicException)
            {
                OnTamperDetected?.Invoke(key);
                return defaultValue;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"SecureSave: خطا در بارگذاری '{key}': {ex.Message}");
                return defaultValue;
            }
        }

        public static bool Exists(string key) => File.Exists(GetFilePath(key));

        public static void Delete(string key)
        {
            if (Exists(key)) File.Delete(GetFilePath(key));
        }
    }


    // ════════════════════════════════════════════════════════════════════
    //  ObfuscationRng  —  PRNG سریع داخلی برای کلیدهای مخفی‌سازی حافظه
    //  (نیازی به امنیت رمزنگارانه ندارد، فقط باید غیرقابل‌پیش‌بینی و سریع
    //   باشد چون ممکن است هر فریم چندین‌بار صدا زده شود)
    // ════════════════════════════════════════════════════════════════════
    internal static class ObfuscationRng
    {
        private static ulong _state;

        static ObfuscationRng()
        {
            byte[] seed = new byte[8];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(seed);
            _state = BitConverter.ToUInt64(seed, 0);
            if (_state == 0) _state = 0xA5A5A5A5DEADBEEF;
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
    //  Obscured Types  —  متغیرهایی که مقدار خامشان هرگز در RAM ذخیره
    //  نمی‌شود، برای خنثی‌کردن اسکن حافظه با ابزارهایی مثل Cheat Engine
    // ════════════════════════════════════════════════════════════════════
    //
    //  نکته‌ی مهم: چون این‌ها struct هستند (نه class)، اگر آن‌ها را به‌صورت
    //  Property تعریف کنید (نه Field عمومی)، نمی‌توانید مستقیماً
    //  ()Reobscure را روی نتیجه‌ی Property صدا بزنید. برای استفاده از
    //  Reobscure حتماً از Field عمومی استفاده کنید:
    //      public ObscuredInt gold;          // درست ✓
    //      public ObscuredInt Gold { get; set; }   // Reobscure روی این کار نمی‌کند ✗
    // ════════════════════════════════════════════════════════════════════

    [Serializable]
    public struct ObscuredInt : IEquatable<ObscuredInt>
    {
        private int _cipher;
        private int _key;

        public ObscuredInt(int value)
        {
            _key = ObfuscationRng.NextInt();
            _cipher = value ^ _key;
        }

        public int Value
        {
            get => _cipher ^ _key;
            set
            {
                int newKey = ObfuscationRng.NextInt();
                _cipher = value ^ newKey;
                _key = newKey;
            }
        }

        /// <summary>
        /// بدون تغییر مقدار منطقی، بایت‌های خام در حافظه را عوض می‌کند.
        /// برای متغیرهای خیلی حساس (مثل سکه/الماس)، این را هر چند ثانیه
        /// یک‌بار از یک Update/Coroutine صدا بزنید تا اسکن نوع
        /// "Unchanged Value" در Cheat Engine هم بی‌فایده شود.
        /// </summary>
        public void Reobscure()
        {
            int current = Value;
            int newKey = ObfuscationRng.NextInt();
            _cipher = current ^ newKey;
            _key = newKey;
        }

        public static implicit operator int(ObscuredInt o) => o.Value;
        public static implicit operator ObscuredInt(int v) => new ObscuredInt(v);

        public static ObscuredInt operator +(ObscuredInt a, ObscuredInt b) => new ObscuredInt(a.Value + b.Value);
        public static ObscuredInt operator -(ObscuredInt a, ObscuredInt b) => new ObscuredInt(a.Value - b.Value);
        public static ObscuredInt operator +(ObscuredInt a, int b) => new ObscuredInt(a.Value + b);
        public static ObscuredInt operator -(ObscuredInt a, int b) => new ObscuredInt(a.Value - b);
        public static ObscuredInt operator ++(ObscuredInt a) => new ObscuredInt(a.Value + 1);
        public static ObscuredInt operator --(ObscuredInt a) => new ObscuredInt(a.Value - 1);

        public bool Equals(ObscuredInt other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ObscuredInt o && Equals(o);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
    }


    [Serializable]
    public struct ObscuredFloat : IEquatable<ObscuredFloat>
    {
        private int _cipher; // بیت‌های float به‌صورت int نگه‌داری می‌شود
        private int _key;

        public ObscuredFloat(float value)
        {
            _key = ObfuscationRng.NextInt();
            _cipher = FloatToBits(value) ^ _key;
        }

        public float Value
        {
            get => BitsToFloat(_cipher ^ _key);
            set
            {
                int newKey = ObfuscationRng.NextInt();
                _cipher = FloatToBits(value) ^ newKey;
                _key = newKey;
            }
        }

        public void Reobscure()
        {
            float current = Value;
            int newKey = ObfuscationRng.NextInt();
            _cipher = FloatToBits(current) ^ newKey;
            _key = newKey;
        }

        private static int FloatToBits(float f) => BitConverter.ToInt32(BitConverter.GetBytes(f), 0);
        private static float BitsToFloat(int i) => BitConverter.ToSingle(BitConverter.GetBytes(i), 0);

        public static implicit operator float(ObscuredFloat o) => o.Value;
        public static implicit operator ObscuredFloat(float v) => new ObscuredFloat(v);

        public static ObscuredFloat operator +(ObscuredFloat a, ObscuredFloat b) => new ObscuredFloat(a.Value + b.Value);
        public static ObscuredFloat operator -(ObscuredFloat a, ObscuredFloat b) => new ObscuredFloat(a.Value - b.Value);
        public static ObscuredFloat operator +(ObscuredFloat a, float b) => new ObscuredFloat(a.Value + b);
        public static ObscuredFloat operator -(ObscuredFloat a, float b) => new ObscuredFloat(a.Value - b);

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
            _key = ObfuscationRng.NextLong();
            _cipher = value ^ _key;
        }

        public long Value
        {
            get => _cipher ^ _key;
            set
            {
                long newKey = ObfuscationRng.NextLong();
                _cipher = value ^ newKey;
                _key = newKey;
            }
        }

        public void Reobscure()
        {
            long current = Value;
            long newKey = ObfuscationRng.NextLong();
            _cipher = current ^ newKey;
            _key = newKey;
        }

        public static implicit operator long(ObscuredLong o) => o.Value;
        public static implicit operator ObscuredLong(long v) => new ObscuredLong(v);

        public bool Equals(ObscuredLong other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ObscuredLong o && Equals(o);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
    }


    [Serializable]
    public struct ObscuredBool : IEquatable<ObscuredBool>
    {
        private ObscuredInt _inner;

        public ObscuredBool(bool value) { _inner = new ObscuredInt(value ? 1 : 0); }

        public bool Value
        {
            get => _inner.Value != 0;
            set => _inner.Value = value ? 1 : 0;
        }

        public void Reobscure() => _inner.Reobscure();

        public static implicit operator bool(ObscuredBool o) => o.Value;
        public static implicit operator ObscuredBool(bool v) => new ObscuredBool(v);

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
            _key = GenerateKey(plain.Length);
            _cipher = Xor(plain, _key);
        }

        public string Value
        {
            get => _cipher == null ? "" : Encoding.UTF8.GetString(Xor(_cipher, _key));
            set
            {
                byte[] plain = Encoding.UTF8.GetBytes(value ?? "");
                _key = GenerateKey(plain.Length);
                _cipher = Xor(plain, _key);
            }
        }

        public void Reobscure()
        {
            string current = Value;
            byte[] plain = Encoding.UTF8.GetBytes(current);
            _key = GenerateKey(plain.Length);
            _cipher = Xor(plain, _key);
        }

        private static byte[] GenerateKey(int length)
        {
            byte[] key = new byte[length];
            for (int i = 0; i < length; i++) key[i] = ObfuscationRng.NextByte();
            return key;
        }

        private static byte[] Xor(byte[] data, byte[] key)
        {
            byte[] result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++) result[i] = (byte)(data[i] ^ key[i]);
            return result;
        }

        public static implicit operator string(ObscuredString o) => o.Value;
        public static implicit operator ObscuredString(string v) => new ObscuredString(v);

        public bool Equals(ObscuredString other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ObscuredString o && Equals(o);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value;
    }


    // ════════════════════════════════════════════════════════════════════
    //  نمونه استفاده
    // ════════════════════════════════════════════════════════════════════
    /*
    using UnityEngine;
    using GameSecurity;

    // ── ۱) متغیرهای حساس بازی را به‌جای int/float/bool معمولی، Obscured کنید ──
    public class PlayerStats : MonoBehaviour
    {
        public ObscuredInt gold = 100;          // به‌جای: public int gold = 100;
        public ObscuredInt diamonds = 0;
        public ObscuredFloat health = 100f;
        public ObscuredBool isInvincible = false;

        private float _reobscureTimer;

        private void Update()
        {
            // هر ۳ ثانیه، بایت‌های حافظه را عوض کن (حتی اگر مقدار عوض نشده باشد)
            _reobscureTimer += Time.deltaTime;
            if (_reobscureTimer > 3f)
            {
                gold.Reobscure();
                diamonds.Reobscure();
                _reobscureTimer = 0f;
            }
        }

        public void AddGold(int amount) => gold += amount;   // مثل int معمولی کار می‌کند
        public bool CanAfford(int price) => gold >= price;   // implicit conversion خودکار
    }


    // ── ۲) ذخیره/بارگذاری امن داده‌ی بازی ──
    [System.Serializable]
    public class SaveData
    {
        public int gold;
        public int level;
        public string playerName;
    }

    public class SaveManager : MonoBehaviour
    {
        public void SaveGame(SaveData data)
        {
            SecureSave.Save("playerSave", data);   // رمزنگاری + قفل به دستگاه، خودکار
        }

        public SaveData LoadGame()
        {
            return SecureSave.Load("playerSave", new SaveData());
        }

        private void OnEnable()
        {
            SecureSave.OnTamperDetected += key =>
                Debug.LogWarning($"فایل سیو دستکاری شده بود: {key}");
        }
    }


    // ── ۳) رمزنگاری دستی یک رشته یا داده‌ی دلخواه ──
    string secret = CryptoCore.EncryptString("یک متن حساس", ProjectSecret.GetProjectPassword());
    string original = CryptoCore.DecryptString(secret, ProjectSecret.GetProjectPassword());
    */
}
