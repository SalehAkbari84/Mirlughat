using UnityEngine;

namespace UITween
{
    /// <summary>
    /// سیستم لاگِ تشخیصی برای پیداکردن اینکه چرا یک انیمیشن اجرا نمی‌شود.
    /// پیش‌فرض روشن است تا همین الان بتوانید مشکل را پیدا کنید؛ بعد از رفع مشکل،
    /// با یک خط خاموشش کنید تا Console شلوغ نشود:
    ///     UITweenDebug.Enabled = false;
    /// </summary>
    public static class UITweenDebug
    {
        public static bool Enabled = true;

        public static void Log(string msg)
        {
            if (!Enabled) return;
            Debug.Log($"<color=#4FC3F7>[UITween]</color> {msg}");
        }

        public static void LogWarning(string msg)
        {
            if (!Enabled) return;
            Debug.LogWarning($"<color=#FFB300>[UITween ⚠]</color> {msg}");
        }

        /// <summary>خطاها همیشه چاپ می‌شوند، حتی اگر Enabled خاموش باشد — چون این‌ها واقعاً مشکل‌دارند</summary>
        public static void LogError(string msg)
        {
            Debug.LogError($"<color=#FF5252>[UITween ✗]</color> {msg}");
        }

        public static void LogSuccess(string msg)
        {
            if (!Enabled) return;
            Debug.Log($"<color=#66BB6A>[UITween ✓]</color> {msg}");
        }
    }
}
