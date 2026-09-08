using UnityEngine;
using UnityEngine.UI;
using UITween;

/// <summary>
/// این کامپوننت رو روی هر GameObject خالی توی اولین صحنه‌ی بازی بذارید — نیازی به هیچ
/// تنظیمِ دیگه‌ای نیست، خودش یه Canvas و متن می‌سازه. مستقیم روی گوشی نشون می‌ده:
///   - FPS لحظه‌ای، میانگین، و کمینه
///   - زمانِ هر فریم (میلی‌ثانیه)
///   - تعدادِ تویینِ فعال (از UITweenManager)
///   - مصرفِ حافظه‌ی مدیریت‌شده (برای تشخیصِ افتِ فریم به‌خاطرِ GC)
///
/// هر وقت یه فریم به‌طرزِ محسوسی کند بود (پیش‌فرض: کندتر از ۳۰fps)، یه لاگِ هشدار هم
/// می‌زنه که دقیقاً همون لحظه چندتا تویین فعال بوده و حافظه چقدر تغییر کرده — این‌طوری
/// می‌فهمید افتِ فریم واقعاً به انیمیشن ربط داره یا به یه‌چیزِ دیگه (رندر/GC عمومی).
/// </summary>
public class PerformanceHUD : MonoBehaviour
{
    [SerializeField] private bool visible = true;
    [SerializeField] private float spikeThresholdMs = 33f; // کندتر از این یعنی زیر ۳۰fps افتاده
    [SerializeField] private int sampleCount = 60;

    Text label;
    float[] frameTimes;
    int frameIndex;
    float minFps = float.MaxValue;
    long lastGcMemory;

    void Awake()
    {
        frameTimes = new float[Mathf.Max(10, sampleCount)];
        BuildUI();
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("[PerformanceHUD Canvas]");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760; // همیشه رو همه‌چیزِ دیگه دیده بشه
        canvasGO.AddComponent<CanvasScaler>();

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(canvasGO.transform, false);
        label = labelGO.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 22;
        label.color = Color.green;
        label.alignment = TextAnchor.UpperLeft;

        var rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(10f, -10f);
        rect.sizeDelta = new Vector2(500f, 200f);

        canvasGO.SetActive(visible);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float ms = dt * 1000f;
        float fps = dt > 0f ? 1f / dt : 0f;

        frameTimes[frameIndex] = ms;
        frameIndex = (frameIndex + 1) % frameTimes.Length;
        minFps = Mathf.Min(minFps, fps);

        int activeTweens = UITweenManager.Instance != null ? UITweenManager.Instance.ActiveTweenCount : 0;
        long gcNow = System.GC.GetTotalMemory(false);
        long gcDelta = gcNow - lastGcMemory;

        // اگه این فریم به‌طرزِ محسوسی کند بود، یه لاگِ هشدار با جزئیاتِ لحظه‌ای بزن
        if (ms > spikeThresholdMs)
        {
            UITweenDebug.LogWarning(
                $"⚠ افتِ فریم: {ms:F1}ms (≈{fps:F0}fps) | تویینِ فعال: {activeTweens} | " +
                $"تغییرِ حافظه: {(gcDelta > 0 ? "+" : "")}{gcDelta / 1024f:F1}KB");
        }
        lastGcMemory = gcNow;

        if (label != null && visible)
        {
            float avgMs = Average(frameTimes);
            label.text =
                $"FPS: {fps:F0}  (avg {1000f / Mathf.Max(avgMs, 0.001f):F0}, min {minFps:F0})\n" +
                $"Frame: {ms:F1} ms\n" +
                $"Active Tweens: {activeTweens}\n" +
                $"GC Mem: {gcNow / 1024f / 1024f:F1} MB";
        }
    }

    float Average(float[] arr)
    {
        float sum = 0f;
        for (int i = 0; i < arr.Length; i++) sum += arr[i];
        return sum / arr.Length;
    }

    /// <summary>نمایش/مخفی‌کردنِ پنل از کد (مثلاً وصل به یه دکمه‌ی مخفیِ دیباگ)</summary>
    public void SetVisible(bool value)
    {
        visible = value;
        if (label != null) label.transform.parent.gameObject.SetActive(value);
    }
}
