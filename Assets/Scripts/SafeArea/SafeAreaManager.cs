// ============================================================
//  SafeAreaManager.cs  —  v3.0
//  Unity 6.5+  |  فقط PanelRenderer
//
//  ویژگی‌ها:
//   ① Safe Area  — مارجین خودکار برای notch / home indicator
//   ② UI Scaler  — نگه داشتن ساختار طراحی روی هر رزولوشن
//
//  استفاده: کامپوننت رو روی همون GameObject که PanelRenderer
//           داره بذار — هیچ کد اضافه‌ای لازم نیست.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(PanelRenderer))]
[AddComponentMenu("UI Toolkit/Safe Area Manager")]
[DisallowMultipleComponent]
public class SafeAreaManager : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════
    //  ① بخش Safe Area
    // ══════════════════════════════════════════════════════════

    [Header("① Safe Area")]
    [Tooltip("مارجین container با safe area ادغام بشه.\n" +
             "اگر margin > safe area باشه، margin استفاده می‌شه.")]
    [SerializeField] private bool collapseMargins = true;

    [Tooltip("چرخش صفحه هر ۲۵۰ms بررسی بشه.")]
    [SerializeField] private bool forceOrientationCheck = true;

    [Space(4)]
    [SerializeField] private bool excludeLeft;
    [SerializeField] private bool excludeRight;
    [SerializeField] private bool excludeTop;
    [SerializeField] private bool excludeBottom;

    [Space(4)]
    [Tooltip("روی tvOS کل safe area نادیده گرفته بشه.")]
    [SerializeField] private bool excludeTvos;

    // ══════════════════════════════════════════════════════════
    //  ② بخش UI Scaler
    // ══════════════════════════════════════════════════════════

    [Header("② UI Scaler  (Scale with Screen Size)")]
    [Tooltip("روشن: UI روی همه رزولوشن‌ها scale خودکار می‌شه.\n" +
             "خاموش: PanelSettings دست نخورده می‌مونه.")]
    [SerializeField] private bool enableScaler = true;

    [Tooltip("رزولوشنی که UI برای اون طراحی شده.\n" +
             "مثال: 1080 × 1920 برای موبایل portrait\n" +
             "      1920 × 1080 برای landscape / desktop")]
    [SerializeField] private Vector2Int referenceResolution = new(1080, 1920);

    [Tooltip("نحوه تطبیق با صفحه:\n" +
             "  MatchWidthOrHeight — ترکیبی از عرض و ارتفاع (توصیه‌شده)\n" +
             "  Expand             — فضای خالی اضافه می‌شه (هیچ‌چیزی clip نمی‌شه)\n" +
             "  Shrink             — محتوا کوچک می‌شه تا چیزی clip نشه")]
    [SerializeField]
    private PanelScreenMatchMode screenMatchMode =
                         PanelScreenMatchMode.MatchWidthOrHeight;

    [Tooltip("فقط در حالت MatchWidthOrHeight:\n" +
             "  0 = فقط عرض رو match کن\n" +
             "  1 = فقط ارتفاع رو match کن\n" +
             "  0.5 = تعادل (پیشنهاد برای موبایل)")]
    [Range(0f, 1f)]
    [SerializeField] private float match = 0.5f;

    [Tooltip("در حالت Landscape، عرض و ارتفاع reference رو خودکار عوض کن.")]
    [SerializeField] private bool autoSwapInLandscape = true;

    // ══════════════════════════════════════════════════════════
    //  وضعیت داخلی
    // ══════════════════════════════════════════════════════════

    private PanelRenderer _pr;
    private PanelSettings _runtimeSettings;   // کپی runtime از asset اصلی
    private SafeAreaElement _safeArea;
    private ScreenOrientation _lastOrientation;

    // ──────────────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Awake()
    {
        _pr = GetComponent<PanelRenderer>();
        _lastOrientation = Screen.orientation;

        if (enableScaler)
            SetupScaler();
    }

    private void OnEnable()
    {
        _pr.RegisterUIReloadCallback(OnUIReload);
    }

    private void OnDisable()
    {
        _pr.UnregisterUIReloadCallback(OnUIReload);
    }

    private void Update()
    {
        // بررسی سبک‌وزن تغییر جهت (فقط وقتی scaler روشنه)
        if (!enableScaler || _runtimeSettings == null) return;
        if (!autoSwapInLandscape) return;

        var current = Screen.orientation;
        if (current == _lastOrientation) return;

        // تغییر بین portrait ↔ landscape
        bool wasLandscape = IsLandscape(_lastOrientation);
        bool nowLandscape = IsLandscape(current);
        _lastOrientation = current;

        if (wasLandscape != nowLandscape)
            ApplyScalerSettings();   // reference resolution عوض می‌شه
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
    #region ② Scaler  —  PanelSettings Runtime Clone

    /// <summary>
    /// یه کپی runtime از PanelSettings asset می‌سازه و روی PanelRenderer
    /// ست می‌کنه. Asset اصلی دست نخورده می‌مونه.
    /// </summary>
    private void SetupScaler()
    {
        var original = _pr.panelSettings;
        if (original == null)
        {
            Debug.LogWarning("[SafeAreaManager] PanelSettings null هست — scaler فعال نشد.", this);
            return;
        }

        // ساخت کپی runtime  (نه تغییر asset اصلی)
        _runtimeSettings = Object.Instantiate(original);
        _runtimeSettings.name = original.name + "_Runtime";
        _pr.panelSettings = _runtimeSettings;

        ApplyScalerSettings();
    }

    /// <summary>
    /// تنظیمات scale رو روی کپی runtime اعمال می‌کنه.
    /// هر بار که orientation تغییر کنه هم صدا زده می‌شه.
    /// </summary>
    private void ApplyScalerSettings()
    {
        if (_runtimeSettings == null) return;

        _runtimeSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;

        // تعیین reference resolution با توجه به جهت فعلی صفحه
        var res = referenceResolution;
        if (autoSwapInLandscape && IsLandscape(Screen.orientation))
            res = new Vector2Int(res.y, res.x);   // عرض و ارتفاع عوض می‌شن

        _runtimeSettings.referenceResolution = res;
        _runtimeSettings.screenMatchMode = screenMatchMode;
        _runtimeSettings.match = match;
    }

    private static bool IsLandscape(ScreenOrientation o) =>
        o is ScreenOrientation.LandscapeLeft or ScreenOrientation.LandscapeRight;

    #endregion

    // ──────────────────────────────────────────────────────────
    #region ① Safe Area  —  PanelRenderer Callback

    private void OnUIReload(PanelRenderer pr, VisualElement root)
    {
        // اگر SafeArea قبلاً توی همین root هست، فقط config رو بروز کن
        if (_safeArea != null && _safeArea.parent == root)
        {
            ApplySafeAreaConfig();
            _safeArea.Refresh();
            return;
        }

        _safeArea = new SafeAreaElement();
        ApplySafeAreaConfig();

        // انتقال تمام فرزندان فعلی root به SafeArea
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
    #region Public API

    /// <summary>
    /// تنظیم Scaler از طریق کد در runtime.
    /// </summary>
    public void SetScaler(
        Vector2Int resolution,
        PanelScreenMatchMode matchMode = PanelScreenMatchMode.MatchWidthOrHeight,
        float matchValue = 0.5f)
    {
        referenceResolution = resolution;
        screenMatchMode = matchMode;
        match = matchValue;
        ApplyScalerSettings();
    }

    /// <summary>
    /// تنظیم Safe Area از طریق کد در runtime.
    /// </summary>
    public void SetSafeArea(
        bool collapseMargins = true,
        bool excludeLeft = false,
        bool excludeRight = false,
        bool excludeTop = false,
        bool excludeBottom = false,
        bool excludeTvos = false,
        bool forceOrientationCheck = true)
    {
        this.collapseMargins = collapseMargins;
        this.excludeLeft = excludeLeft;
        this.excludeRight = excludeRight;
        this.excludeTop = excludeTop;
        this.excludeBottom = excludeBottom;
        this.excludeTvos = excludeTvos;
        this.forceOrientationCheck = forceOrientationCheck;
        ApplySafeAreaConfig();
        _safeArea?.Refresh();
    }

    /// <summary>دسترسی مستقیم به SafeAreaElement داخلی</summary>
    public SafeAreaElement SafeArea => _safeArea;

    /// <summary>دسترسی به کپی runtime از PanelSettings</summary>
    public PanelSettings RuntimeSettings => _runtimeSettings;

    #endregion
}


// ════════════════════════════════════════════════════════════
//  SafeAreaElement  —  VisualElement مشترک
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
        style.top = 0;
        style.bottom = 0;
        style.left = 0;
        style.right = 0;

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
        RegisterCallback<GeometryChangedEvent>(OnGeometry);

        _poller = schedule.Execute(PollOrientation).Every(250).StartingIn(0);
        _poller.Pause();
    }

    private void OnAttach(AttachToPanelEvent _)
    {
        if (ForceOrientationCheck) { _lastOrientation = Screen.orientation; _poller?.Resume(); }
        Apply();
    }

    private void OnDetach(DetachFromPanelEvent _) => _poller?.Pause();
    private void OnGeometry(GeometryChangedEvent _) => Apply();

    private void PollOrientation()
    {
        if (panel == null) return;
        if (((int)_lastOrientation ^ (int)Screen.orientation) is 3 or 7)
            Apply();
        _lastOrientation = Screen.orientation;
    }

    public void Refresh() => Apply();

    private void Apply()
    {
        try
        {
            var sa = SafeAreaOffset();
            var mrg = MarginOffset();

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
        catch (System.InvalidCastException) { }
        catch (System.Exception e) { Debug.LogWarning($"[SafeAreaElement] {e.Message}"); }
    }

    private Quad SafeAreaOffset()
    {
        var rect = Screen.safeArea;
        var lt = RuntimePanelUtils.ScreenToPanel(panel,
                       new Vector2(rect.xMin, Screen.height - rect.yMax));
        var rb = RuntimePanelUtils.ScreenToPanel(panel,
                       new Vector2(Screen.width - rect.xMax, rect.yMin));
#if UNITY_TVOS
        if (ExcludeTvos) return default;
#endif
        return new Quad
        {
            L = ExcludeLeft ? 0 : lt.x,
            R = ExcludeRight ? 0 : rb.x,
            T = ExcludeTop ? 0 : lt.y,
            B = ExcludeBottom ? 0 : rb.y,
        };
    }

    private Quad MarginOffset() => new Quad
    {
        L = resolvedStyle.marginLeft,
        R = resolvedStyle.marginRight,
        T = resolvedStyle.marginTop,
        B = resolvedStyle.marginBottom,
    };

    private struct Quad { public float L, R, T, B; }
}