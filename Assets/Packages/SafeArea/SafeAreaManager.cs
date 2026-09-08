using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UI.SafeArea
{
    /// <summary>
    /// SafeAreaManager
    /// این اسکریپت را روی RectTransform یک پنل (فرزند مستقیم Canvas و پدرِ کل UI بازی)
    /// قرار بده تا محتوای آن به‌صورت خودکار داخل Safe Area دستگاه محدود بماند.
    ///
    /// این نسخه کاملاً خودکار است:
    /// - اگر اشتباهاً روی خودِ Canvas اصلی سوار شود، خودش تشخیص می‌دهد و اصلاح می‌کند
    ///   (یک فرزند جدید به نام "SafeArea" می‌سازد، فرزندان قبلی را به آن منتقل می‌کند
    ///   و خودش را به آنجا جابه‌جا می‌کند).
    /// - بعد از هر اعمال تنظیمات، یک گزارش خودکار در Console چاپ می‌کند که با علامت
    ///   ✅ (همه‌چیز درست است) یا ❌ (مشکل پیدا شد + توضیح) مشخص می‌شود.
    /// - از canvas.pixelRect.size و رویداد OnRectTransformDimensionsChange استفاده می‌کند
    ///   (دقیق‌تر و بهینه‌تر از Update هر فریم).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Safe Area Manager")]
    public class SafeAreaManager : MonoBehaviour
    {
        [Header("حاشیه‌ی اضافه (پیکسل منطقی UI)")]
        [SerializeField] private int _marginLeft = 0;
        [SerializeField] private int _marginRight = 0;
        [SerializeField] private int _marginTop = 0;
        [SerializeField] private int _marginBottom = 0;

        [Header("محدود کردن اعمال Safe Area به جهت خاص (اختیاری)")]
        [SerializeField] private bool _applyOnTop = true;
        [SerializeField] private bool _applyOnBottom = true;
        [SerializeField] private bool _applyOnLeft = true;
        [SerializeField] private bool _applyOnRight = true;

        [Header("شبیه‌سازی برای تست در Editor")]
        [Tooltip("چون Screen.safeArea در Game View معمولی همیشه کل صفحه است، با فعال کردن این گزینه بدون Device Simulator هم می‌تونی نتیجه رو ببینی.")]
        [SerializeField] private bool _simulateInEditor = false;
        [SerializeField] private Vector4 _simulatedSafeAreaNormalized = new Vector4(0f, 0.05f, 0f, 0.08f);

        [Header("خودکارسازی")]
        [Tooltip("اگر فعال باشد و این کامپوننت اشتباهاً روی خودِ Canvas اصلی سوار شده باشد، خودش هر چیزی که لازم است را می‌سازد و اصلاح می‌کند.")]
        [SerializeField] private bool _autoFixHierarchy = true;

        [Tooltip("بعد از هر اعمال تنظیمات، یک گزارش کامل با نتیجه‌ی درست/غلط در Console چاپ می‌کند.")]
        [SerializeField] private bool _runDiagnostics = true;

        [Header("دیباگ تفصیلی (اختیاری)")]
        [SerializeField] private bool _verboseLogging = false;

        private RectTransform _rectTransform;
        private Canvas _canvas;
        private Rect _lastSafeArea = new Rect(0, 0, 0, 0);
        private Vector2Int _lastCanvasSize = new Vector2Int(0, 0);
        private bool _autoFixHandledThisInstance = false;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            _rectTransform = GetComponent<RectTransform>();

            if (TryAutoFixHierarchy())
            {
                return;
            }

            if (TryAutoRemoveIfDuplicate())
            {
                return;
            }

            _canvas = GetComponentInParent<Canvas>();
            ApplySafeArea(force: true);
        }

        /// <summary>
        /// اگر یک SafeAreaManager فعالِ دیگر بالاتر از این آبجکت (در همان Canvas) وجود داشته باشد،
        /// این نمونه اضافی و مضر است (باعث جمع‌شدنِ تصاعدی/Compounding می‌شود) و باید حذف شود.
        /// فقط یک SafeAreaManager — روی کانتینر بیرونی SafeArea — باید در هر Canvas فعال باشد.
        /// </summary>
        private bool TryAutoRemoveIfDuplicate()
        {
            if (!_autoFixHierarchy) return false;

            Transform t = transform.parent;
            while (t != null)
            {
                SafeAreaManager ancestor = t.GetComponent<SafeAreaManager>();
                if (ancestor != null && ancestor.enabled)
                {
                    Debug.LogWarning(
                        $"<color=#FF9800>[SafeAreaManager] 🔧 اصلاح خودکار: روی '{name}' یک SafeAreaManager تودرتو (داخل '{ancestor.name}') پیدا شد. " +
                        $"چون این باعث جمع‌شدن تصاعدی و به‌هم‌ریختگی چیدمان می‌شود، این نمونه‌ی اضافی به‌صورت خودکار حذف شد. " +
                        $"فقط باید یک SafeAreaManager — روی کانتینر بیرونی 'SafeArea' — در هر Canvas فعال باشد.</color>",
                        this);

                    _autoFixHandledThisInstance = true;
                    enabled = false;
                    SafeDestroySelf();
                    return true;
                }

                Canvas canvasHere = t.GetComponent<Canvas>();
                if (canvasHere != null && canvasHere.isRootCanvas) break; // فراتر از Canvas خودش نرو

                t = t.parent;
            }

            return false;
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_autoFixHandledThisInstance) return;
            ApplySafeArea(force: false);
        }

        /// <summary>در صورت نیاز از بیرون این متد را صدا بزن تا همه چیز دوباره محاسبه و بررسی شود.</summary>
        public void Refresh()
        {
            ApplySafeArea(force: true);
        }

        // ------------------------------------------------------------------
        // اصلاح خودکار: اگر روی خودِ Canvas اصلی سوار شده باشد
        // ------------------------------------------------------------------
        private bool TryAutoFixHierarchy()
        {
            if (!_autoFixHierarchy) return false;

            Canvas ownCanvas = GetComponent<Canvas>();
            if (ownCanvas == null || !ownCanvas.isRootCanvas) return false;

            // اگر قبلاً اصلاح شده (فرزند SafeArea از قبل وجود دارد)، فقط خودش را غیرفعال کن.
            Transform existing = transform.Find("SafeArea");
            if (existing != null && existing.GetComponent<SafeAreaManager>() != null)
            {
                Debug.Log($"<color=#2196F3>[SafeAreaManager] ℹ️ روی Canvas اصلی سوار است اما قبلاً اصلاح شده — از فرزند 'SafeArea' استفاده می‌شود.</color>", this);
                _autoFixHandledThisInstance = true;
                enabled = false;
                return true;
            }

            GameObject safeAreaGO = new GameObject("SafeArea", typeof(RectTransform));
            RectTransform safeAreaRT = safeAreaGO.GetComponent<RectTransform>();
            safeAreaGO.transform.SetParent(transform, false);
            safeAreaRT.anchorMin = Vector2.zero;
            safeAreaRT.anchorMax = Vector2.one;
            safeAreaRT.offsetMin = Vector2.zero;
            safeAreaRT.offsetMax = Vector2.zero;
            safeAreaRT.SetAsFirstSibling();

            // انتقال تمام فرزندان قبلی به داخل SafeArea جدید
            int movedCount = 0;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == safeAreaRT) continue;
                child.SetParent(safeAreaRT, false);
                movedCount++;
            }

            SafeAreaManager newManager = safeAreaGO.AddComponent<SafeAreaManager>();
            CopySettingsTo(newManager);

            Debug.LogWarning(
                $"<color=#FF9800>[SafeAreaManager] 🔧 اصلاح خودکار انجام شد روی '{name}':\n" +
                $"این کامپوننت روی خودِ Canvas اصلی سوار بود که باعث می‌شد هیچ تغییری قابل مشاهده نباشد.\n" +
                $"یک فرزند جدید به نام 'SafeArea' ساخته شد، {movedCount} فرزند قبلی به داخل آن منتقل شدند،\n" +
                $"و SafeAreaManager به‌صورت خودکار روی آن فعال شد. از این به بعد بقیه‌ی UI را زیر 'SafeArea' بساز.</color>",
                safeAreaGO);

            _autoFixHandledThisInstance = true;
            enabled = false;
            SafeDestroySelf();
            return true;
        }

        private void CopySettingsTo(SafeAreaManager other)
        {
            other._marginLeft = _marginLeft;
            other._marginRight = _marginRight;
            other._marginTop = _marginTop;
            other._marginBottom = _marginBottom;
            other._applyOnTop = _applyOnTop;
            other._applyOnBottom = _applyOnBottom;
            other._applyOnLeft = _applyOnLeft;
            other._applyOnRight = _applyOnRight;
            other._simulateInEditor = _simulateInEditor;
            other._simulatedSafeAreaNormalized = _simulatedSafeAreaNormalized;
            other._autoFixHierarchy = _autoFixHierarchy;
            other._runDiagnostics = _runDiagnostics;
            other._verboseLogging = _verboseLogging;
        }

        private void SafeDestroySelf()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.delayCall += () =>
                {
                    if (this != null) DestroyImmediate(this);
                };
                return;
            }
#endif
            Destroy(this);
        }

        // ------------------------------------------------------------------
        // اعمال Safe Area
        // ------------------------------------------------------------------
        private void ApplySafeArea(bool force)
        {
            if (_rectTransform == null) return;

            if (_canvas == null)
            {
                _canvas = GetComponentInParent<Canvas>();
                if (_canvas == null)
                {
                    RunDiagnostics(applied: false, extraIssue: "هیچ Canvas ای در والدین این آبجکت پیدا نشد.");
                    return;
                }
            }

            Rect safeArea = GetCurrentSafeArea();
            Vector2 canvasSize = _canvas.pixelRect.size;
            Vector2Int canvasSizeInt = new Vector2Int(Mathf.RoundToInt(canvasSize.x), Mathf.RoundToInt(canvasSize.y));

            bool changed = force || safeArea != _lastSafeArea || canvasSizeInt != _lastCanvasSize;
            if (!changed) return;

            _lastSafeArea = safeArea;
            _lastCanvasSize = canvasSizeInt;

            if (canvasSize.x <= 0f || canvasSize.y <= 0f) return;

            Vector2 inverseSize = new Vector2(1f, 1f) / canvasSize;
            Vector2 anchorMin = Vector2.Scale(safeArea.position, inverseSize);
            Vector2 anchorMax = Vector2.Scale(safeArea.position + safeArea.size, inverseSize);

            anchorMin.x += _marginLeft / canvasSize.x;
            anchorMin.y += _marginBottom / canvasSize.y;
            anchorMax.x -= _marginRight / canvasSize.x;
            anchorMax.y -= _marginTop / canvasSize.y;

            if (!_applyOnLeft) anchorMin.x = 0f;
            if (!_applyOnBottom) anchorMin.y = 0f;
            if (!_applyOnRight) anchorMax.x = 1f;
            if (!_applyOnTop) anchorMax.y = 1f;

            anchorMin.x = Mathf.Clamp(anchorMin.x, 0f, 1f);
            anchorMin.y = Mathf.Clamp(anchorMin.y, 0f, 1f);
            anchorMax.x = Mathf.Clamp(anchorMax.x, anchorMin.x, 1f);
            anchorMax.y = Mathf.Clamp(anchorMax.y, anchorMin.y, 1f);

            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;

            if (_verboseLogging)
            {
                Debug.Log($"[SafeAreaManager] '{name}' اعمال شد → " +
                          $"safeArea(px)={safeArea}  canvas={canvasSizeInt}  " +
                          $"anchorMin={anchorMin}  anchorMax={anchorMax}", this);
            }

            RunDiagnostics(applied: true, extraIssue: null, appliedAnchorMin: anchorMin, appliedAnchorMax: anchorMax, appliedSafeArea: safeArea, appliedCanvasSize: canvasSizeInt);
        }

        private Rect GetCurrentSafeArea()
        {
#if UNITY_EDITOR
            if (_simulateInEditor)
            {
                Vector2 canvasSize = _canvas != null ? _canvas.pixelRect.size : new Vector2(Screen.width, Screen.height);
                float left = _simulatedSafeAreaNormalized.x * canvasSize.x;
                float bottom = _simulatedSafeAreaNormalized.y * canvasSize.y;
                float right = canvasSize.x - (_simulatedSafeAreaNormalized.z * canvasSize.x);
                float top = canvasSize.y - (_simulatedSafeAreaNormalized.w * canvasSize.y);
                return new Rect(left, bottom, right - left, top - bottom);
            }
#endif
            return Screen.safeArea;
        }

        // ------------------------------------------------------------------
        // گزارش خودکار (تاییدیه یا فهرست مشکلات)
        // ------------------------------------------------------------------
        private void RunDiagnostics(bool applied, string extraIssue,
            Vector2 appliedAnchorMin = default, Vector2 appliedAnchorMax = default,
            Rect appliedSafeArea = default, Vector2Int appliedCanvasSize = default)
        {
            if (!_runDiagnostics) return;

            List<string> issues = new List<string>();
            List<string> infos = new List<string>();

            if (_rectTransform == null) issues.Add("RectTransform روی این آبجکت پیدا نشد.");
            if (_canvas == null) issues.Add("هیچ Canvas ای در والدین این آبجکت پیدا نشد — SafeArea باید زیر یک Canvas باشد.");

            Canvas ownCanvas = GetComponent<Canvas>();
            if (ownCanvas != null && ownCanvas.isRootCanvas)
                issues.Add("این کامپوننت روی خودِ Canvas اصلی سوار است.");

            if (_canvas != null && _canvas.renderMode == RenderMode.WorldSpace)
                infos.Add("Canvas در حالت World Space است؛ Safe Area معمولاً برای این حالت کاربرد ندارد.");

            if (applied)
            {
                bool anchorsAreFullScreen =
                    Mathf.Approximately(appliedAnchorMin.x, 0f) && Mathf.Approximately(appliedAnchorMin.y, 0f) &&
                    Mathf.Approximately(appliedAnchorMax.x, 1f) && Mathf.Approximately(appliedAnchorMax.y, 1f);

                if (anchorsAreFullScreen)
                {
#if UNITY_EDITOR
                    if (!_simulateInEditor)
                        infos.Add("Safe Area برابر کل صفحه است — طبیعی است اگر داخل Editor بدون Device Simulator هستی. برای تست، گزینه‌ی 'Simulate In Editor' را فعال کن.");
                    else
                        infos.Add("Safe Area برابر کل صفحه است (طبق مقادیر شبیه‌سازی‌شده‌ی فعلی).");
#else
                    infos.Add("Safe Area برابر کل صفحه است — یعنی این دستگاه بریدگی/ناچ ندارد.");
#endif
                }
            }
            else if (!string.IsNullOrEmpty(extraIssue))
            {
                issues.Add(extraIssue);
            }

            string infoBlock = infos.Count > 0 ? "\nℹ️ " + string.Join("\nℹ️ ", infos) : "";

            if (issues.Count == 0)
            {
                Debug.Log($"<color=#4CAF50>[SafeAreaManager] ✅ تاییدیه — روی '{name}' همه چیز درست تنظیم شده و Safe Area با موفقیت اعمال شد.</color>{infoBlock}", this);
            }
            else
            {
                Debug.LogWarning($"<color=#F44336>[SafeAreaManager] ❌ مشکل روی '{name}':\n- {string.Join("\n- ", issues)}</color>{infoBlock}", this);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            if (_autoFixHandledThisInstance) return;

            // تغییر anchorMin/anchorMax همین‌جا مجاز نیست چون داخلش پیام
            // OnRectTransformDimensionsChange فرستاده می‌شود و یونیتی فرستادن پیام
            // در حین OnValidate را اجازه نمی‌دهد. برای همین اجرای واقعی را به فریم بعد موکول می‌کنیم.
            EditorApplication.delayCall += DeferredValidateApply;
        }

        private void DeferredValidateApply()
        {
            EditorApplication.delayCall -= DeferredValidateApply;
            if (this == null) return; // آبجکت ممکن است تا آن زمان حذف شده باشد
            ApplySafeArea(force: true);
        }
#endif
    }
}