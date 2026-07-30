// ============================================================
//  SafeAreaManager.cs  —  v4.0  "Smart Edition"
//  Unity 6.5+  |  فقط PanelRenderer
//
//  سیستم‌های یکپارچه:
//   ① Safe Area         — مارجین خودکار برای notch / home bar
//   ② Smart UI Scaler   — تشخیص خودکار دستگاه + انتخاب پروفایل
//   ③ Event System      — رویداد برای هر تغییر scale / orientation
//   ④ Debug Overlay     — نمایش اطلاعات لحظه‌ای در build
//
//  استفاده: فقط این کامپوننت رو کنار PanelRenderer بذار.
//  هیچ تنظیمی لازم نیست — خودکار شناسایی می‌کنه.
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

// ── تایپ‌های کمکی ────────────────────────────────────────────

public enum UIScalerMode
{
    /// <summary>دستگاه شناسایی می‌شه و بهترین پروفایل انتخاب می‌شه</summary>
    Auto,
    /// <summary>رزولوشن و match رو خودت تعیین می‌کنی</summary>
    Manual
}

[Serializable]
public class DeviceProfile
{
    [Tooltip("نام نمایشی این پروفایل")]
    public string profileName = "Profile";

    [Tooltip("حداکثر قطر صفحه (اینچ) برای استفاده از این پروفایل")]
    public float maxDiagonalInches = 6.5f;

    [Tooltip("رزولوشن طراحی — portrait پیشنهاد: 1080×1920")]
    public Vector2Int referenceResolution = new(1080, 1920);

    public PanelScreenMatchMode matchMode = PanelScreenMatchMode.MatchWidthOrHeight;

    [Tooltip("✅ توصیه‌شده: match رو بر اساس نسبت ابعاد واقعی حساب کن")]
    public bool useSmartMatch = true;

    [Range(0f, 1f)]
    [Tooltip("فقط اگر useSmartMatch خاموشه")]
    public float match = 0.5f;
}

// ════════════════════════════════════════════════════════════
//  SafeAreaManager — MonoBehaviour اصلی
// ════════════════════════════════════════════════════════════

[RequireComponent(typeof(PanelRenderer))]
[AddComponentMenu("UI Toolkit/Safe Area Manager")]
[DisallowMultipleComponent]
public class SafeAreaManager : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════
    //  ① Safe Area
    // ══════════════════════════════════════════════════════════

    [Header("① Safe Area")]
    [SerializeField] private bool collapseMargins = true;
    [SerializeField] private bool forceOrientationCheck = true;
    [Space(3)]
    [SerializeField] private bool excludeLeft;
    [SerializeField] private bool excludeRight;
    [SerializeField] private bool excludeTop;
    [SerializeField] private bool excludeBottom;
    [Space(3)]
    [SerializeField] private bool excludeTvos;

    // ══════════════════════════════════════════════════════════
    //  ② Smart UI Scaler
    // ══════════════════════════════════════════════════════════

    [Header("② Smart UI Scaler")]
    [SerializeField] private bool enableScaler = true;

    [SerializeField] private UIScalerMode scalerMode = UIScalerMode.Auto;

    // ── Auto Mode ──────────────────────────────────────────
    [Tooltip("پروفایل‌های سفارشی — اگر خالی باشه از پروفایل‌های پیش‌فرض استفاده می‌شه.\n" +
             "ترتیب مهمه: از کوچک‌ترین maxDiagonal به بزرگ‌ترین.")]
    [SerializeField] private DeviceProfile[] customProfiles = Array.Empty<DeviceProfile>();

    // ── Manual Mode ────────────────────────────────────────
    [Tooltip("فقط در حالت Manual")]
    [SerializeField] private Vector2Int manualReferenceResolution = new(1080, 1920);
    [SerializeField]
    private PanelScreenMatchMode manualMatchMode =
                         PanelScreenMatchMode.MatchWidthOrHeight;
    [Tooltip("✅ محاسبه خودکار match در Manual هم")]
    [SerializeField] private bool manualSmartMatch = true;
    [Range(0f, 1f)]
    [SerializeField] private float manualMatch = 0.5f;
    [SerializeField] private bool autoSwapInLandscape = true;

    // ── Extra ──────────────────────────────────────────────
    [Header("③ Extra")]
    [Tooltip("ضریب مقیاس اضافه — مثلاً 1.2 برای دسترسی‌پذیری\n" +
             "1 = بدون تغییر")]
    [Range(0.5f, 3f)]
    [SerializeField] private float extraScale = 1f;

    [Tooltip("نمایش اطلاعات debug در Game view (فقط Editor و Development Build)")]
    [SerializeField] private bool showDebugOverlay;

    // ══════════════════════════════════════════════════════════
    //  Events — رویدادهای عمومی
    // ══════════════════════════════════════════════════════════

    /// <summary>هر بار scale تغییر کنه: scale فعلی رو می‌ده</summary>
    public event Action<float> OnScaleChanged;

    /// <summary>چرخش یا تغییر رزولوشن</summary>
    public event Action<ScreenOrientation> OnOrientationChanged;

    /// <summary>فقط Auto mode: وقتی پروفایل عوض بشه</summary>
    public event Action<string> OnProfileChanged;

    // ══════════════════════════════════════════════════════════
    //  وضعیت داخلی
    // ══════════════════════════════════════════════════════════

    private PanelRenderer _pr;
    private PanelSettings _runtimeSettings;
    private SafeAreaElement _safeArea;

    private Vector2Int _lastResolution;
    private ScreenOrientation _lastOrientation;
    private string _activeProfileName = "-";
    private float _currentScale;

    // ── پروفایل‌های پیش‌فرض (Auto mode) ─────────────────────
    // قطر صفحه به اینچ — بر اساس آمار واقعی بازار
    private static readonly DeviceProfile[] BuiltinProfiles =
    {
        new() { profileName = "Compact Phone",
                maxDiagonalInches  = 5.0f,
                referenceResolution = new(1080, 1920),
                useSmartMatch = true },

        new() { profileName = "Phone",
                maxDiagonalInches  = 6.5f,
                referenceResolution = new(1080, 1920),
                useSmartMatch = true },

        new() { profileName = "Large Phone / Phablet",
                maxDiagonalInches  = 7.5f,
                referenceResolution = new(1080, 2400),
                useSmartMatch = true },

        new() { profileName = "Small Tablet",
                maxDiagonalInches  = 9.0f,
                referenceResolution = new(1536, 2048),
                useSmartMatch = true },

        new() { profileName = "Tablet",
                maxDiagonalInches  = 13.0f,
                referenceResolution = new(1536, 2048),
                useSmartMatch = true,
                match = 0.5f },

        new() { profileName = "Desktop / Large Screen",
                maxDiagonalInches  = float.MaxValue,
                referenceResolution = new(1920, 1080),
                matchMode = PanelScreenMatchMode.MatchWidthOrHeight,
                useSmartMatch = true,
                match = 0.5f },
    };

    // ──────────────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Awake()
    {
        _pr = GetComponent<PanelRenderer>();
        _lastResolution = new Vector2Int(Screen.width, Screen.height);
        _lastOrientation = Screen.orientation;

        if (enableScaler)
            SetupScaler();
    }

    private void OnEnable() => _pr.RegisterUIReloadCallback(OnUIReload);
    private void OnDisable() => _pr.UnregisterUIReloadCallback(OnUIReload);

    private void Update()
    {
        if (!enableScaler || _runtimeSettings == null) return;

        var currentRes = new Vector2Int(Screen.width, Screen.height);
        var currentOri = Screen.orientation;

        // تشخیص تغییر رزولوشن (مثلاً resize پنجره در desktop)
        if (currentRes != _lastResolution)
        {
            _lastResolution = currentRes;
            ApplyScalerSettings();
            OnOrientationChanged?.Invoke(currentOri);
            return;
        }

        // تشخیص تغییر چرخش
        if (currentOri != _lastOrientation)
        {
            _lastOrientation = currentOri;
            ApplyScalerSettings();
            OnOrientationChanged?.Invoke(currentOri);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying) return;

        if (enableScaler && _runtimeSettings != null)
            ApplyScalerSettings();

        if (_safeArea != null)
        {
            ApplySafeAreaConfig();
            _safeArea.Refresh();
        }
    }
#endif

    #endregion

    // ──────────────────────────────────────────────────────────
    #region ② Scaler Core

    private void SetupScaler()
    {
        var original = _pr.panelSettings;
        if (original == null)
        {
            Debug.LogWarning("[SafeAreaManager] PanelSettings null — scaler فعال نشد.", this);
            return;
        }

        _runtimeSettings = Instantiate(original);
        _runtimeSettings.name = original.name + "_Runtime";
        _pr.panelSettings = _runtimeSettings;

        ApplyScalerSettings();
    }

    private void ApplyScalerSettings()
    {
        if (_runtimeSettings == null) return;

        _runtimeSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;

        Vector2Int refRes;
        PanelScreenMatchMode matchMode;
        float matchValue;

        if (scalerMode == UIScalerMode.Auto)
        {
            // ── حالت Auto: پروفایل مناسب انتخاب می‌شه ────────
            float diagonal = GetDiagonalInches();
            var profile = SelectProfile(diagonal);

            refRes = profile.referenceResolution;
            matchMode = profile.matchMode;
            matchValue = profile.useSmartMatch
                        ? CalculateSmartMatch(refRes)
                        : profile.match;

            // اطلاع‌رسانی تغییر پروفایل
            if (profile.profileName != _activeProfileName)
            {
                _activeProfileName = profile.profileName;
                OnProfileChanged?.Invoke(_activeProfileName);
            }
        }
        else
        {
            // ── حالت Manual ────────────────────────────────────
            refRes = manualReferenceResolution;
            matchMode = manualMatchMode;
            matchValue = manualSmartMatch
                        ? CalculateSmartMatch(refRes)
                        : manualMatch;
            _activeProfileName = "Manual";
        }

        // اعمال چرخش (عرض/ارتفاع عوض می‌شن)
        if (autoSwapInLandscape && IsLandscape())
            refRes = new Vector2Int(refRes.y, refRes.x);

        // اعمال extraScale — مقیاس reference تنظیم می‌شه
        // (reference کوچک‌تر = UI بزرگ‌تر روی صفحه)
        if (!Mathf.Approximately(extraScale, 1f))
        {
            refRes = new Vector2Int(
                Mathf.RoundToInt(refRes.x / extraScale),
                Mathf.RoundToInt(refRes.y / extraScale)
            );
        }

        _runtimeSettings.referenceResolution = refRes;
        _runtimeSettings.screenMatchMode = matchMode;
        _runtimeSettings.match = matchValue;

        // محاسبه scale فعلی برای event
        float newScale = ComputeCurrentScale(refRes, matchValue);
        if (!Mathf.Approximately(newScale, _currentScale))
        {
            _currentScale = newScale;
            OnScaleChanged?.Invoke(_currentScale);
        }
    }

    // ──────────────────────────────────────────────────────────
    #region Smart Calculations

    /// <summary>
    /// match ایده‌آل رو بر اساس نسبت ابعاد صفحه vs reference حساب می‌کنه.
    /// اگه صفحه عریض‌تر از reference باشه → match نزدیک به 1 (height)
    /// اگه صفحه کشیده‌تر باشه → match نزدیک به 0 (width)
    /// </summary>
    private static float CalculateSmartMatch(Vector2Int refRes)
    {
        if (Screen.width <= 0 || Screen.height <= 0) return 0.5f;

        float screenAspect = (float)Screen.width / Screen.height;
        float refAspect = (float)refRes.x / refRes.y;

        // لگاریتم پایه 4: اختلاف 2x در aspect → ±0.5 تغییر در match
        float bias = Mathf.Log(screenAspect / refAspect, 4f);
        return Mathf.Clamp01(0.5f + bias);
    }

    /// <summary>قطر فیزیکی صفحه به اینچ</summary>
    private static float GetDiagonalInches()
    {
        float dpi = Screen.dpi > 0 ? Screen.dpi : 160f;
        float w = Screen.width / dpi;
        float h = Screen.height / dpi;
        return Mathf.Sqrt(w * w + h * h);
    }

    /// <summary>انتخاب پروفایل مناسب بر اساس قطر صفحه</summary>
    private DeviceProfile SelectProfile(float diagonalInches)
    {
        var profiles = customProfiles is { Length: > 0 }
                       ? customProfiles
                       : BuiltinProfiles;

        return profiles
               .OrderBy(p => p.maxDiagonalInches)
               .FirstOrDefault(p => diagonalInches <= p.maxDiagonalInches)
               ?? profiles.Last();
    }

    /// <summary>
    /// scale فعلی رو تخمین می‌زنه (برای event و debug)
    /// از همان فرمول لگاریتمی Unity استفاده می‌کنه.
    /// </summary>
    private static float ComputeCurrentScale(Vector2Int refRes, float matchValue)
    {
        if (refRes.x <= 0 || refRes.y <= 0) return 1f;
        const float logBase = 2f;
        float logW = Mathf.Log((float)Screen.width / refRes.x, logBase);
        float logH = Mathf.Log((float)Screen.height / refRes.y, logBase);
        return Mathf.Pow(logBase, Mathf.Lerp(logW, logH, matchValue));
    }

    private static bool IsLandscape() =>
        Screen.width > Screen.height;

    #endregion

    #endregion

    // ──────────────────────────────────────────────────────────
    #region ① Safe Area — PanelRenderer Callback

    private void OnUIReload(PanelRenderer pr, VisualElement root)
    {
        if (_safeArea != null && _safeArea.parent == root)
        {
            ApplySafeAreaConfig();
            _safeArea.Refresh();
            return;
        }

        _safeArea = new SafeAreaElement();
        ApplySafeAreaConfig();

        var children = new List<VisualElement>(root.childCount);
        for (int i = 0; i < root.childCount; i++)
            children.Add(root[i]);

        root.Clear();
        foreach (var child in children)
            _safeArea.Add(child);

        root.Add(_safeArea);
    }

    private void ApplySafeAreaConfig()
    {
        if (_safeArea == null) return;
        _safeArea.CollapseMargins = collapseMargins;
        _safeArea.ExcludeLeft = excludeLeft;
        _safeArea.ExcludeRight = excludeRight;
        _safeArea.ExcludeTop = excludeTop;
        _safeArea.ExcludeBottom = excludeBottom;
        _safeArea.ExcludeTvos = excludeTvos;
        _safeArea.ForceOrientationCheck = forceOrientationCheck;
    }

    #endregion

    // ──────────────────────────────────────────────────────────
    #region ④ Debug Overlay

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private GUIStyle _debugStyle;

    private void OnGUI()
    {
        if (!showDebugOverlay || _runtimeSettings == null) return;

        _debugStyle ??= new GUIStyle(GUI.skin.box)
        {
            fontSize = Mathf.Max(12, Screen.height / 70),
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(10, 10, 8, 8),
        };
        _debugStyle.normal.textColor = Color.white;

        float diagonal = GetDiagonalInches();
        var safeRect = Screen.safeArea;

        string info =
            $"<b>[SafeAreaManager]</b>\n" +
            $"Screen  :  {Screen.width} × {Screen.height}  @  {Screen.dpi:F0} dpi\n" +
            $"Diagonal:  {diagonal:F2}\"  |  Orientation: {Screen.orientation}\n" +
            $"Profile :  {_activeProfileName}\n" +
            $"Ref Res :  {_runtimeSettings.referenceResolution.x} × " +
                        $"{_runtimeSettings.referenceResolution.y}\n" +
            $"Match   :  {_runtimeSettings.match:F3}  |  Scale ≈ {_currentScale:F3}\n" +
            $"SafeArea:  L{safeRect.xMin:F0}  R{Screen.width - safeRect.xMax:F0}  " +
                        $"T{Screen.height - safeRect.yMax:F0}  B{safeRect.yMin:F0}";

        float w = Mathf.Min(Screen.width * 0.5f, 480);
        float h = _debugStyle.fontSize * 9f;
        GUI.Box(new Rect(12, 12, w, h), info, _debugStyle);
    }
#endif

    #endregion

    // ──────────────────────────────────────────────────────────
    #region Public API

    /// <summary>اطلاعات وضعیت فعلی (برای debug یا رابط تنظیمات)</summary>
    public (string profile, float scale, Vector2Int refRes, float match) GetStatus() =>
    (
        _activeProfileName,
        _currentScale,
        _runtimeSettings?.referenceResolution ?? default,
        _runtimeSettings?.match ?? 0
    );

    /// <summary>extraScale رو در runtime تغییر بده (مثلاً دکمه دسترسی‌پذیری)</summary>
    public void SetExtraScale(float scale)
    {
        extraScale = Mathf.Clamp(scale, 0.5f, 3f);
        ApplyScalerSettings();
    }

    /// <summary>دسترسی به PanelSettings runtime (read-only توصیه می‌شه)</summary>
    public PanelSettings RuntimeSettings => _runtimeSettings;

    /// <summary>دسترسی به SafeAreaElement داخلی</summary>
    public SafeAreaElement SafeArea => _safeArea;

    #endregion
}


// ════════════════════════════════════════════════════════════
//  SafeAreaElement  —  VisualElement محاسبه safe area
// ════════════════════════════════════════════════════════════

public class SafeAreaElement : VisualElement
{
    public bool CollapseMargins { get; set; } = true;
    public bool ExcludeLeft { get; set; }
    public bool ExcludeRight { get; set; }
    public bool ExcludeTop { get; set; }
    public bool ExcludeBottom { get; set; }
    public bool ExcludeTvos { get; set; }
    public bool ForceOrientationCheck { get; set; }

    private readonly VisualElement _content;
    public override VisualElement contentContainer => _content;

    private ScreenOrientation _lastOrientation;
    private IVisualElementScheduledItem _poller;

    public SafeAreaElement()
    {
        name = "safe-area-root";
        pickingMode = PickingMode.Ignore;

        style.position = Position.Absolute;
        style.top = 0; style.bottom = 0;
        style.left = 0; style.right = 0;

        _content = new VisualElement
        {
            name = "safe-area-content-container",
            pickingMode = PickingMode.Ignore,
        };
        _content.style.flexGrow = 1;
        _content.style.flexShrink = 0;
        hierarchy.Add(_content);

        RegisterCallback<AttachToPanelEvent>(OnAttach);
        RegisterCallback<DetachFromPanelEvent>(OnDetach);
        RegisterCallback<GeometryChangedEvent>(_ => Apply());

        _poller = schedule.Execute(PollOrientation).Every(250).StartingIn(0);
        _poller.Pause();
    }

    private void OnAttach(AttachToPanelEvent _)
    {
        if (ForceOrientationCheck) { _lastOrientation = Screen.orientation; _poller?.Resume(); }
        Apply();
    }

    private void OnDetach(DetachFromPanelEvent _) => _poller?.Pause();

    private void PollOrientation()
    {
        if (panel == null) return;
        if (((int)_lastOrientation ^ (int)Screen.orientation) is 3 or 7) Apply();
        _lastOrientation = Screen.orientation;
    }

    public void Refresh() => Apply();

    private void Apply()
    {
        try
        {
            var sa = GetSafeArea();
            var mrg = GetMargin();

            if (CollapseMargins)
            {
                _content.style.marginLeft = Mathf.Max(mrg.L, sa.L) - mrg.L;
                _content.style.marginRight = Mathf.Max(mrg.R, sa.R) - mrg.R;
                _content.style.marginTop = Mathf.Max(mrg.T, sa.T) - mrg.T;
                _content.style.marginBottom = Mathf.Max(mrg.B, sa.B) - mrg.B;
            }
            else
            {
                _content.style.marginLeft = sa.L;
                _content.style.marginRight = sa.R;
                _content.style.marginTop = sa.T;
                _content.style.marginBottom = sa.B;
            }
        }
        catch (InvalidCastException) { }
        catch (Exception e) { Debug.LogWarning($"[SafeAreaElement] {e.Message}"); }
    }

    private (float L, float R, float T, float B) GetSafeArea()
    {
        var rect = Screen.safeArea;
        var lt = RuntimePanelUtils.ScreenToPanel(panel,
                       new Vector2(rect.xMin, Screen.height - rect.yMax));
        var rb = RuntimePanelUtils.ScreenToPanel(panel,
                       new Vector2(Screen.width - rect.xMax, rect.yMin));
#if UNITY_TVOS
        if (ExcludeTvos) return default;
#endif
        return (
            ExcludeLeft ? 0 : lt.x,
            ExcludeRight ? 0 : rb.x,
            ExcludeTop ? 0 : lt.y,
            ExcludeBottom ? 0 : rb.y
        );
    }

    private (float L, float R, float T, float B) GetMargin() => (
        resolvedStyle.marginLeft,
        resolvedStyle.marginRight,
        resolvedStyle.marginTop,
        resolvedStyle.marginBottom
    );
}
