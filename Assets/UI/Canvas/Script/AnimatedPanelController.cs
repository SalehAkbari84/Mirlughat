using UnityEngine;
using UITween;

/// <summary>
/// کنترلرِ عمومیِ باز/بسته‌شدنِ پنل. فقط GameObject‌ها رو بکشید — لازم نیست نگران باشید
/// دقیقاً کدوم کامپوننت روی اون آبجکته؛ خودش با GetComponent&lt;IUIAnimated&gt;() پیدا می‌کنه
/// (چه UIAnimatedElement باشه، چه PersianAnimatedText، چه هر کامپوننتِ سفارشیِ دیگه).
/// </summary>
public class AnimatedPanelController : MonoBehaviour
{
    [Header("بخش‌های اصلی (فقط GameObject رو بکشید)")]
    [Tooltip("اختیاری — اگر این پنل بک‌گراندِ تیره‌کننده ندارد، خالی بگذارید")]
    [SerializeField] private GameObject backgroundElement;
    [SerializeField] private GameObject panelElement;

    [Header("بخش‌های داخلی پنل (فقط GameObject رو بکشید، به هر تعداد که خواستید)")]
    [SerializeField] private GameObject[] innerElements;

    [Header("زمان‌بندی آبشاری")]
    [SerializeField] private float staggerStep = 0.07f;

    [Header("رفتار خروج")]
    [SerializeField] private float exitOverlap = 0.25f;

    [Header("صدا (اختیاری، سطح کل پنل — جدا از صدای خودِ هر المان)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;

    [Header("پارتیکل (اختیاری، سطح کل پنل)")]
    [SerializeField] private ParticleSystem[] openParticles;
    [SerializeField] private ParticleSystem[] closeParticles;

    public bool IsOpen { get; private set; }

    /// <summary>پیداکردنِ خودکارِ کامپوننتِ انیمیت‌شونده روی این GameObject، مهم نیست دقیقاً کدومه</summary>
    IUIAnimated GetAnimated(GameObject go)
    {
        if (go == null) return null;
        var animated = go.GetComponent<IUIAnimated>();
        if (animated == null)
            UITweenDebug.LogError($"«{name}»: روی «{go.name}» هیچ کامپوننتی که رابطِ IUIAnimated رو پیاده کرده باشه (مثل UIAnimatedElement یا PersianAnimatedText) پیدا نشد.");
        return animated;
    }

    void Start()
    {
        var panel = GetAnimated(panelElement);
        if (panelElement != null && panel == null)
            UITweenDebug.LogError($"«{name}»: panelElement معتبر نیست! Open/Close کار نخواهد کرد.");
        else if (panelElement == null)
            UITweenDebug.LogError($"«{name}»: فیلد panelElement توی Inspector خالی است!");

        GetAnimated(backgroundElement)?.SnapToHidden();
        panel?.SnapToHidden();

        foreach (var go in innerElements)
            GetAnimated(go)?.SnapToHidden();
    }

    public void Open()
    {
        UITweenDebug.Log($"«{name}»: Open() صدا زده شد. IsOpen={IsOpen}");
        if (IsOpen) return;
        IsOpen = true;

        PlayParticles(openParticles);
        PlaySound(openSound);

        GetAnimated(backgroundElement)?.Show();

        var panel = GetAnimated(panelElement);
        if (panel != null)
            panel.Show().OnComplete(ShowInnerElements);
        else
            ShowInnerElements();
    }

    public void Close()
    {
        UITweenDebug.Log($"«{name}»: Close() صدا زده شد. IsOpen={IsOpen}");
        if (!IsOpen) return;
        IsOpen = false;

        PlayParticles(closeParticles);
        PlaySound(closeSound);

        HideInnerElements();
        GetAnimated(panelElement)?.Hide(exitOverlap);
        GetAnimated(backgroundElement)?.Hide(exitOverlap);
    }

    public void Toggle()
    {
        UITweenDebug.Log($"«{name}»: Toggle() صدا زده شد. IsOpen={IsOpen}");
        if (IsOpen) Close();
        else Open();
    }

    void PlayParticles(ParticleSystem[] particles)
    {
        var targetGo = panelElement != null ? panelElement : gameObject;
        var fitRect = targetGo.GetComponent<RectTransform>();
        VFXHelper.PlayParticles(particles, targetGo.transform.position, fitRect);
    }

    void PlaySound(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
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

    void HideInnerElements()
    {
        float delay = 0f;
        foreach (var go in innerElements)
        {
            var animated = GetAnimated(go);
            if (animated == null) continue;
            animated.Hide(delay);
            delay += staggerStep * 0.5f;
        }
    }
}