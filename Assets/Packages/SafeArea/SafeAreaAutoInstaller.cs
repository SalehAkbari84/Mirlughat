using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UI.SafeArea
{
    /// <summary>
    /// SafeAreaAutoInstaller
    /// این کلاس عامل کاملاً خودکارِ نصب SafeArea است — هیچ عامل انسانی لازم نیست:
    /// نه Drag & Drop دستی کامپوننت روی آبجکت، نه ساختن دستی فرزند SafeArea،
    /// نه جابه‌جا کردن دستی فرزندان زیر Canvas.
    ///
    /// نحوه‌ی کار:
    /// - در Editor: با هر تغییر در Hierarchy (حتی بدون فشردن Play)، تمام صحنه اسکن می‌شود.
    /// - در Play / Build واقعی: بلافاصله بعد از لود هر صحنه اجرا می‌شود.
    /// - برای هر Canvas ریشه (غیر از World Space) بررسی می‌کند: اگر از قبل فرزند
    ///   "SafeArea" مدیریت‌شده دارد، دست‌نخورده رهایش می‌کند (Idempotent — بدون تکرار کار).
    ///   در غیر این صورت، یک فرزند "SafeArea" می‌سازد، تمام فرزندان قبلیِ Canvas
    ///   را به داخل آن منتقل می‌کند و کامپوننت SafeAreaManager را روی آن فعال می‌کند.
    ///
    /// یعنی: کافی است این دو اسکریپت (SafeAreaAutoInstaller.cs و SafeAreaManager.cs)
    /// را در پروژه داشته باشی — هر Canvas ای که در هر صحنه‌ای بسازی، خودش تنظیم می‌شود.
    /// </summary>
    public static class SafeAreaAutoInstaller
    {
        private const string SafeAreaChildName = "SafeArea";
        private static bool _isProcessing = false;

#if UNITY_EDITOR
        static SafeAreaAutoInstaller()
        {
            EditorApplication.hierarchyChanged += OnHierarchyChangedInEditor;
            EditorApplication.delayCall += OnHierarchyChangedInEditor; // یک بار موقع باز شدن/کامپایل مجدد پروژه
        }

        private static void OnHierarchyChangedInEditor()
        {
            if (_isProcessing || Application.isPlaying) return;

            _isProcessing = true;
            try
            {
                InstallOnAllCanvases();
            }
            finally
            {
                _isProcessing = false;
            }
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnRuntimeSceneLoaded()
        {
            if (_isProcessing) return;

            _isProcessing = true;
            try
            {
                InstallOnAllCanvases();
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private static void InstallOnAllCanvases()
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
            foreach (Canvas canvas in canvases)
            {
                if (canvas == null) continue;
                if (!canvas.isRootCanvas) continue;              // فقط Canvas های ریشه
                if (canvas.renderMode == RenderMode.WorldSpace) continue; // Safe Area برای World Space کاربرد ندارد

                InstallOnCanvas(canvas);
            }
        }

        private static void InstallOnCanvas(Canvas canvas)
        {
            Transform canvasTransform = canvas.transform;
            Transform existing = canvasTransform.Find(SafeAreaChildName);

            // اگر قبلاً به‌درستی نصب شده، کاری نکن (جلوگیری از تکرار بی‌پایان کار)
            if (existing != null && existing.GetComponent<SafeAreaManager>() != null)
            {
                return;
            }

            GameObject safeAreaGO;
            RectTransform safeAreaRT;

            if (existing != null)
            {
                // فرزندی با این اسم هست ولی SafeAreaManager نداره؛ همونو تکمیل می‌کنیم.
                safeAreaGO = existing.gameObject;
                safeAreaRT = safeAreaGO.GetComponent<RectTransform>();
                if (safeAreaRT == null) safeAreaRT = safeAreaGO.AddComponent<RectTransform>();
            }
            else
            {
                safeAreaGO = new GameObject(SafeAreaChildName, typeof(RectTransform));
                safeAreaRT = safeAreaGO.GetComponent<RectTransform>();
                safeAreaGO.transform.SetParent(canvasTransform, false);
                safeAreaRT.anchorMin = Vector2.zero;
                safeAreaRT.anchorMax = Vector2.one;
                safeAreaRT.offsetMin = Vector2.zero;
                safeAreaRT.offsetMax = Vector2.zero;
                safeAreaRT.SetAsFirstSibling();

                // انتقال تمام فرزندان قبلیِ Canvas به داخل SafeArea جدید
                for (int i = canvasTransform.childCount - 1; i >= 0; i--)
                {
                    Transform child = canvasTransform.GetChild(i);
                    if (child == safeAreaRT) continue;
                    child.SetParent(safeAreaRT, false);
                }
            }

            if (safeAreaGO.GetComponent<SafeAreaManager>() == null)
            {
                safeAreaGO.AddComponent<SafeAreaManager>();
            }

            Debug.Log(
                $"<color=#4CAF50>[SafeAreaAutoInstaller] ✅ روی Canvas '{canvas.name}' کاملاً خودکار نصب و تنظیم شد " +
                $"— بدون هیچ کار دستی.</color>", canvas);
        }
    }
}