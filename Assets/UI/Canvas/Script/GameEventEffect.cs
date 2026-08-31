using UnityEngine;
using UITween;

/// <summary>
/// کامپوننتی عمومی و قابل‌استفاده‌ی مجدد برای «اتفاق‌های بازی» — چیزی فراتر از یک ساده
/// نمایش/مخفیِ پنل. مثال‌ها: صفحه‌ی برد بازی، Level Complete، دریافت جایزه، Combo بزرگ.
///
/// فقط GameObject‌ها رو بکشید — لازم نیست نگران باشید دقیقاً کدوم کامپوننتِ انیمیت‌شونده
/// روی اون آبجکته؛ خودش با GetComponent&lt;IUIAnimated&gt;() پیدا می‌کنه.
/// </summary>
public class GameEventEffect : MonoBehaviour
{
    [Header("پنل اصلیِ این رویداد (فقط GameObject رو بکشید)")]
    [Tooltip("پنلی که هنگام Trigger باز می‌شود (مثلاً کل صفحه‌ی «برنده شدید!»)")]
    [SerializeField] private GameObject mainPanel;

    [Header("المان‌های داخلی (فقط GameObject رو بکشید، اختیاری، به‌صورت آبشاری نمایش داده می‌شوند)")]
    [SerializeField] private GameObject[] innerElements;
    [SerializeField] private float staggerStep = 0.08f;

    [Header("پارتیکل‌های این رویداد (جدا از پارتیکل‌های خودِ المان — هم پارتیکل صحنه هم پریفب پشتیبانی می‌شود)")]
    [SerializeField] private ParticleSystem[] particles;
    [Tooltip("اگر خالی بماند، پارتیکل‌های پریفب روی موقعیتِ mainPanel ساخته می‌شوند")]
    [SerializeField] private Transform particleSpawnPoint;

    [Header("صدا")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip triggerSound;

    [Header("لرزشِ دوربین (اختیاری)")]
    [SerializeField] private bool shakeCamera;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float shakeStrength = 0.4f;
    [SerializeField] private float shakeDuration = 0.35f;

    [Header("بستنِ خودکار")]
    [Tooltip("اگر بزرگ‌تر از صفر باشد، بعد از این‌همه ثانیه خودکار Dismiss فراخوانی می‌شود")]
    [SerializeField] private float autoDismissAfter = 0f;

    public bool IsActive { get; private set; }

    void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
    }

    /// <summary>پیداکردنِ خودکارِ کامپوننتِ انیمیت‌شونده روی این GameObject، مهم نیست دقیقاً کدومه</summary>
    IUIAnimated GetAnimated(GameObject go)
    {
        if (go == null) return null;
        var animated = go.GetComponent<IUIAnimated>();
        if (animated == null)
            UITweenDebug.LogError($"«{name}»: روی «{go.name}» هیچ کامپوننتی که رابطِ IUIAnimated رو پیاده کرده باشه پیدا نشد.");
        return animated;
    }

    void Start()
    {
        GetAnimated(mainPanel)?.SnapToHidden();
        foreach (var go in innerElements)
            GetAnimated(go)?.SnapToHidden();
    }

    /// <summary>اجرای کاملِ رویداد: پنل + المان‌های داخلی + پارتیکل + صدا + لرزشِ دوربین</summary>
    public void Trigger()
    {
        if (IsActive) return;
        IsActive = true;

        UITweenDebug.Log($"«{name}»: GameEventEffect.Trigger() اجرا شد.");

        var spawnPoint = particleSpawnPoint != null ? particleSpawnPoint
            : (mainPanel != null ? mainPanel.transform : transform);
        var fitRect = mainPanel != null ? mainPanel.GetComponent<RectTransform>() : null;
        VFXHelper.PlayParticles(particles, spawnPoint.position, fitRect);

        if (triggerSound != null && audioSource != null)
            audioSource.PlayOneShot(triggerSound);

        if (shakeCamera && targetCamera != null)
            targetCamera.transform.DOShakePosition(shakeDuration, shakeStrength);

        var panel = GetAnimated(mainPanel);
        if (panel != null)
            panel.Show().OnComplete(ShowInnerElements);
        else
            ShowInnerElements();

        if (autoDismissAfter > 0f)
            DOVirtual.DelayedCall(autoDismissAfter, Dismiss);
    }

    /// <summary>بستنِ رویداد (پنل + المان‌های داخلی با انیمیشنِ خروج)</summary>
    public void Dismiss()
    {
        if (!IsActive) return;
        IsActive = false;

        UITweenDebug.Log($"«{name}»: GameEventEffect.Dismiss() اجرا شد.");

        float delay = 0f;
        foreach (var go in innerElements)
        {
            var animated = GetAnimated(go);
            if (animated == null) continue;
            animated.Hide(delay);
            delay += staggerStep * 0.5f;
        }

        GetAnimated(mainPanel)?.Hide(delay + 0.05f);
    }

    void ShowInnerElements()
    {
        float delay = 0f;
        foreach (var go in innerElements)
        {
            var animated = GetAnimated(go);
            if (animated == null) continue;
            animated.Show(delay);
            delay += staggerStep;
        }
    }
}