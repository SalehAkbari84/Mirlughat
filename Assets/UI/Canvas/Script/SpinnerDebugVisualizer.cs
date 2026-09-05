using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ابزار دیباگِ نمایشیِ چرخ‌ونه.
/// این اسکریپت رو روی همون آبجکتی که "wheelVisual" هست بذار (یا یه فرزند خالی زیرش).
///
/// نکته‌ی مهم فنی: ساخت/حذف GameObject هرگز مستقیم داخل OnValidate انجام نمی‌شه
/// (چون باعث تلنبار شدن و لگ می‌شه)، بلکه فقط یه فلگ ست می‌کنه و ساخت واقعی
/// با یه فریم تاخیر (delayCall توی ادیتور) انجام می‌شه.
/// </summary>
[ExecuteAlways]
public class SpinnerDebugVisualizer : MonoBehaviour
{
    [System.Serializable]
    public class DebugSegment
    {
        [Tooltip("اندازه‌ی زاویه‌ای این بخش بر حسب درجه. مجموع همه‌ی بخش‌ها لازم نیست دقیقاً 360 بشه، خودت تنظیمش کن")]
        [Range(1f, 359f)]
        public float angleSize = 45f;

        [HideInInspector] public float startAngle;
        [HideInInspector] public float centerAngle;
    }

    [Header("رفرنس‌ها")]
    [Tooltip("همون آبجکتی که در واقع می‌چرخه (wheelVisual / spinner-background)")]
    [SerializeField] private RectTransform wheelVisual;

    [Tooltip("لیبل و تعداد سکه‌ی هر بخش از اینجا خونده میشه (نه از خود این اسکریپت)")]
    [SerializeField] private SpinnerWheelController wheelController;

    [Header("بخش‌ها")]
    [SerializeField] private DebugSegment[] segments = new DebugSegment[8];

    [Header("ظاهر خط‌های جداکننده")]
    [SerializeField] private bool showLines = true;
    [SerializeField] private Color lineColor = Color.red;
    [SerializeField] private float lineThickness = 4f;
    [Range(0.1f, 1f)]
    [SerializeField] private float lineLengthRatio = 1f;

    [Header("ظاهر متن هر بخش")]
    [SerializeField] private bool showLabels = true;
    [Tooltip("فونت TMP که گلیف فارسی/عربی داره (همون فونتی که توی PersianUGUI استفاده می‌کنی). اگه خالی بمونه از فونت پیش‌فرض TMP استفاده میشه که فارسی نداره و مربع خالی نشون میده")]
    [SerializeField] private TMP_FontAsset labelFont;
    [SerializeField] private int fontSize = 28;
    [SerializeField] private Color textColor = Color.yellow;
    [Range(0.1f, 1f)]
    [SerializeField] private float textRadiusRatio = 0.6f;

    [Header("نمایش در بازی نهایی")]
    [Tooltip("خاموش بذار تا این ابزار توی بیلد نهایی اصلاً ساخته نشه")]
    [SerializeField] private bool showAtRuntimeBuild = false;

    private const string ContainerName = "__SpinnerDebugOverlay__";
    private Transform container;
    private bool rebuildQueued;

    private void OnEnable() => QueueRebuild();
    private void OnDisable() => CleanupAllContainers();
    private void OnDestroy() => CleanupAllContainers();
    private void OnValidate() => QueueRebuild();
    private void OnTransformParentChanged() => QueueRebuild();

    // در Play Mode از Update برای اعمال تغییرات استفاده می‌کنیم (delayCall فقط ادیتوره)
    private void Update()
    {
        if (rebuildQueued && Application.isPlaying)
        {
            rebuildQueued = false;
            Rebuild();
        }
    }

    private void QueueRebuild()
    {
        if (rebuildQueued) return;
        rebuildQueued = true;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall += () =>
            {
                // آبجکت ممکنه تا اون موقع حذف شده باشه
                if (this == null) return;
                rebuildQueued = false;
                Rebuild();
            };
        }
#endif
    }

    private void Rebuild()
    {
        CleanupAllContainers();

        if (wheelVisual == null || segments == null || segments.Length == 0)
            return;

        if (Application.isPlaying && !showAtRuntimeBuild)
            return;

        container = new GameObject(ContainerName, typeof(RectTransform)).transform;
        var containerRect = (RectTransform)container;
        containerRect.SetParent(wheelVisual, false);
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = Vector2.zero;
        containerRect.SetAsLastSibling();

        float radius = Mathf.Min(wheelVisual.rect.width, wheelVisual.rect.height) * 0.5f;
        float cumulative = 0f;

        for (int i = 0; i < segments.Length; i++)
        {
            var seg = segments[i];
            seg.startAngle = cumulative;
            seg.centerAngle = cumulative + seg.angleSize * 0.5f;
            cumulative += seg.angleSize;

            if (showLines) CreateDividerLine(seg.startAngle, radius, i);
            if (showLabels) CreateLabel(seg, radius, i);
        }

        // خط پایانیِ بخش آخر (لبه‌ی انتهایی که با هیچ خط دیگه‌ای پوشش داده نمی‌شه).
        // بدون این خط، تغییر Angle Size بخش آخر هیچ اثر قابل‌مشاهده‌ای نداره.
        if (showLines) CreateDividerLine(cumulative, radius, segments.Length);
    }

    private void CreateDividerLine(float angleDeg, float radius, int index)
    {
        var go = new GameObject($"Line_{index}", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(container, false);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(lineThickness, radius * lineLengthRatio);
        rt.localEulerAngles = new Vector3(0f, 0f, -angleDeg);

        var img = go.GetComponent<Image>();
        img.color = lineColor;
        img.raycastTarget = false;
    }

    private void CreateLabel(DebugSegment seg, float radius, int index)
    {
        var go = new GameObject($"Label_{index}", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(container, false);
        rt.sizeDelta = new Vector2(radius * 0.9f, 60f);

        float angleRad = seg.centerAngle * Mathf.Deg2Rad;
        Vector2 pos = new Vector2(
            radius * textRadiusRatio * Mathf.Sin(angleRad),
            radius * textRadiusRatio * Mathf.Cos(angleRad));
        rt.anchoredPosition = pos;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (labelFont != null) tmp.font = labelFont;
        tmp.text = BuildLabelText(index);
        tmp.fontSize = fontSize;
        tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
    }

    private string BuildLabelText(int index)
    {
        if (wheelController == null) return string.Empty;

        var controllerSegments = wheelController.Segments;
        if (controllerSegments == null || index >= controllerSegments.Length || controllerSegments[index] == null)
            return string.Empty;

        return controllerSegments[index].label ?? string.Empty;
    }

    /// <summary>
    /// پاک‌سازی مطمئن: هر چند تا آبجکتِ باقی‌مونده از قبل (حتی از باگ قبلی) هم پیدا و حذف می‌کنه،
    /// نه فقط یکی. اگه صحنه‌ات همین الان پر از خطوط تلنبارشده‌ست، همین کافیه که پاکش کنه.
    /// </summary>
    [ContextMenu("Cleanup Now")]
    public void CleanupAllContainers()
    {
        container = null;

        if (wheelVisual == null) return;

        for (int i = wheelVisual.childCount - 1; i >= 0; i--)
        {
            var child = wheelVisual.GetChild(i);
            if (child == null) continue;
            if (child.name != ContainerName) continue;

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }
}