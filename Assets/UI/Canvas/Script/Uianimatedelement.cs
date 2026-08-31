using UnityEngine;
using UnityEngine.UI;
using UITween;

/// <summary>
/// کامپوننتی عمومی برای انیمیت‌کردن هر المان UGUI بین حالت «ثابت» (طراحیِ خودتان در Editor)
/// و حالت «انیمیشنی» (ورود/خروج). حالت ثابت هرگز توسط این اسکریپت دستکاری نمی‌شود؛ فقط در
/// زمان اجرا خوانده و به‌عنوان مقصدِ نهاییِ انیمیشن استفاده می‌شود.
///
/// از این نسخه به بعد، استایلِ نمایش و استایلِ مخفی‌شدن کاملاً مستقل از هم انتخاب می‌شوند
/// (مثلاً می‌تونید با ScaleIn ظاهر بشه ولی با SlideFromBottom خارج بشه)، و می‌شود پارتیکل و
/// صدا هم به هرکدوم وصل کرد.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class UIAnimatedElement : MonoBehaviour, IUIAnimated
{
    public enum TransitionStyle
    {
        // ساده
        FadeOnly,
        None, // بدون انیمیشن — فقط فعال/غیرفعال آنی

        // اسلاید از جهت‌های اصلی
        SlideFromLeft,
        SlideFromRight,
        SlideFromTop,
        SlideFromBottom,

        // اسلاید از گوشه‌ها
        SlideFromTopLeft,
        SlideFromTopRight,
        SlideFromBottomLeft,
        SlideFromBottomRight,

        // مقیاس
        ScaleIn,
        ZoomOut,
        PopBounce,
        ElasticIn,

        // فلت/فشرده (شبیه کارت یا پرده)
        FlipHorizontal,
        FlipVertical,
        SqueezeVertical,
        SqueezeHorizontal,
        Curtain,

        // چرخشی
        RotateIn,
        RotateInFromLeft,
        RotateInFromRight,
        Wobble,
        Spiral,

        // ترکیبی/جهشی
        BounceFromTop,
        BounceFromBottom,
        SlideFadeScale,

        // اسلایدِ بلند (فاصله‌ی دوبرابر — برای پنل‌های تمام‌صفحه)
        SlideFromFarLeft,
        SlideFromFarRight,
        SlideFromFarTop,
        SlideFromFarBottom,

        // انفجاری/نرم
        Explode,
        DropIn,
        RiseUpSoft,
        Peek,

        // کِشی/فنری
        JellySquish,
        StretchVertical,
        StretchHorizontal,
        Shutter,

        // چرخشیِ پیشرفته
        Vortex,
        Cartwheel,
        Tumble,
        WaveIn
    }

    [Header("استایل نمایش (Show)")]
    [SerializeField] private TransitionStyle showStyle = TransitionStyle.FadeOnly;
    [SerializeField] private float showDuration = 0.3f;
    [SerializeField] private Ease showEase = Ease.OutBack;

    [Header("استایل مخفی‌شدن (Hide) — کاملاً مستقل از نمایش")]
    [SerializeField] private TransitionStyle hideStyle = TransitionStyle.FadeOnly;
    [SerializeField] private float hideDuration = 0.25f;
    [SerializeField] private Ease hideEase = Ease.InCubic;

    [Header("فاصله‌ی اسلاید (برای استایل‌های Slide/Bounce/Spiral)")]
    [SerializeField] private float slideDistance = 300f;

    [Header("پارتیکل (اختیاری — هم پارتیکلِ صحنه هم پریفب پشتیبانی می‌شود)")]
    [Tooltip("این پارتیکل‌ها درست همون لحظه‌ای که Show() شروع می‌شه پخش می‌شوند")]
    [SerializeField] private ParticleSystem[] showParticles;
    [Tooltip("این پارتیکل‌ها درست همون لحظه‌ای که Hide() شروع می‌شه پخش می‌شوند")]
    [SerializeField] private ParticleSystem[] hideParticles;
    [Tooltip("اگر خالی بماند، پارتیکل‌های پریفب دقیقاً روی موقعیتِ همین المان ساخته می‌شوند")]
    [SerializeField] private Transform particleSpawnPoint;

    [Header("صدا (اختیاری)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip showSound;
    [SerializeField] private AudioClip hideSound;

    RectTransform rect;
    CanvasGroup canvasGroup;

    /// <summary>دسترسیِ عمومی به RectTransform — برای اینکه GameEventEffect/AnimatedPanelController بتوانند اندازه‌ی پارتیکل را با اندازه‌ی این المان تنظیم کنند</summary>
    public RectTransform Rect => rect;

    // ===== حالت ثابتِ طراحی‌شده در UGUI — فقط خوانده می‌شود =====
    Vector2 defaultAnchoredPos;
    Vector3 defaultScale;
    Vector3 defaultRotation;
    float defaultAlpha;
    bool captured;

    /// <summary>توصیفِ نقطه‌ی شروع/پایانِ یک استایل، نسبت به حالت ثابتِ طراحی‌شده</summary>
    struct StartState
    {
        public Vector2 posOffset;
        public Vector3 scaleMul;
        public float rotOffset;
        public Ease? easeOverride;

        public StartState(Vector2 pos, Vector3 scale, float rot, Ease? ease = null)
        {
            posOffset = pos; scaleMul = scale; rotOffset = rot; easeOverride = ease;
        }
    }

    // عمداً Start است نه Awake — یونیتی تضمین می‌کند همه‌ی Awake‌های صحنه (مثل SafeAreaManager
    // که ممکن است این المان را Reparent کند) قبل از هر Start‌ای اجرا شوند.
    void Start() => EnsureInit();

    void EnsureInit()
    {
        if (rect == null) rect = GetComponent<RectTransform>();

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
                UITweenDebug.Log($"«{name}»: CanvasGroup نداشت، خودکار اضافه شد.");
            }
        }

        if (!captured)
        {
            defaultAnchoredPos = rect.anchoredPosition;
            defaultScale = rect.localScale;
            defaultRotation = rect.localEulerAngles;
            defaultAlpha = canvasGroup.alpha;
            captured = true;
            UITweenDebug.Log($"«{name}»: حالتِ ثابت ضبط شد → pos={defaultAnchoredPos}, scale={defaultScale}, alpha={defaultAlpha}");
        }
    }

    /// <summary>محاسبه‌ی حالتِ «پنهان» بر اساس یک استایل مشخص — نسبت به حالت طراحی‌شده</summary>
    StartState GetStartState(TransitionStyle style)
    {
        float d = slideDistance;
        switch (style)
        {
            case TransitionStyle.FadeOnly: return new StartState(Vector2.zero, Vector3.one, 0f);
            case TransitionStyle.None: return new StartState(Vector2.zero, Vector3.one, 0f);

            case TransitionStyle.SlideFromLeft: return new StartState(new Vector2(-d, 0), Vector3.one, 0f);
            case TransitionStyle.SlideFromRight: return new StartState(new Vector2(d, 0), Vector3.one, 0f);
            case TransitionStyle.SlideFromTop: return new StartState(new Vector2(0, d), Vector3.one, 0f);
            case TransitionStyle.SlideFromBottom: return new StartState(new Vector2(0, -d), Vector3.one, 0f);

            case TransitionStyle.SlideFromTopLeft: return new StartState(new Vector2(-d, d), Vector3.one, 0f);
            case TransitionStyle.SlideFromTopRight: return new StartState(new Vector2(d, d), Vector3.one, 0f);
            case TransitionStyle.SlideFromBottomLeft: return new StartState(new Vector2(-d, -d), Vector3.one, 0f);
            case TransitionStyle.SlideFromBottomRight: return new StartState(new Vector2(d, -d), Vector3.one, 0f);

            case TransitionStyle.ScaleIn: return new StartState(Vector2.zero, Vector3.one * 0.7f, 0f);
            case TransitionStyle.ZoomOut: return new StartState(Vector2.zero, Vector3.one * 1.4f, 0f);
            case TransitionStyle.PopBounce: return new StartState(Vector2.zero, Vector3.one * 0.6f, 0f, Ease.OutBack);
            case TransitionStyle.ElasticIn: return new StartState(Vector2.zero, Vector3.one * 0.3f, 0f, Ease.OutElastic);

            case TransitionStyle.FlipHorizontal: return new StartState(Vector2.zero, new Vector3(0f, 1f, 1f), 0f);
            case TransitionStyle.FlipVertical: return new StartState(Vector2.zero, new Vector3(1f, 0f, 1f), 0f);
            case TransitionStyle.SqueezeVertical: return new StartState(Vector2.zero, new Vector3(1f, 0.1f, 1f), 0f);
            case TransitionStyle.SqueezeHorizontal: return new StartState(Vector2.zero, new Vector3(0.1f, 1f, 1f), 0f);
            case TransitionStyle.Curtain: return new StartState(Vector2.zero, new Vector3(1f, 0f, 1f), 0f, Ease.OutCubic);

            case TransitionStyle.RotateIn: return new StartState(Vector2.zero, Vector3.one, 180f);
            case TransitionStyle.RotateInFromLeft: return new StartState(new Vector2(-d, 0), Vector3.one, -45f);
            case TransitionStyle.RotateInFromRight: return new StartState(new Vector2(d, 0), Vector3.one, 45f);
            case TransitionStyle.Wobble: return new StartState(Vector2.zero, Vector3.one * 0.8f, 25f, Ease.OutElastic);
            case TransitionStyle.Spiral: return new StartState(new Vector2(-d, -d), Vector3.one * 0.4f, 180f, Ease.OutBack);

            case TransitionStyle.BounceFromTop: return new StartState(new Vector2(0, d), Vector3.one, 0f, Ease.OutBounce);
            case TransitionStyle.BounceFromBottom: return new StartState(new Vector2(0, -d), Vector3.one, 0f, Ease.OutBounce);
            case TransitionStyle.SlideFadeScale: return new StartState(new Vector2(0, -d * 0.5f), Vector3.one * 0.85f, 0f);

            case TransitionStyle.SlideFromFarLeft: return new StartState(new Vector2(-d * 2f, 0), Vector3.one, 0f);
            case TransitionStyle.SlideFromFarRight: return new StartState(new Vector2(d * 2f, 0), Vector3.one, 0f);
            case TransitionStyle.SlideFromFarTop: return new StartState(new Vector2(0, d * 2f), Vector3.one, 0f);
            case TransitionStyle.SlideFromFarBottom: return new StartState(new Vector2(0, -d * 2f), Vector3.one, 0f);

            case TransitionStyle.Explode: return new StartState(Vector2.zero, Vector3.one * 0.15f, 0f, Ease.OutExpo);
            case TransitionStyle.DropIn: return new StartState(new Vector2(0, d), Vector3.one, 15f, Ease.OutBounce);
            case TransitionStyle.RiseUpSoft: return new StartState(new Vector2(0, -d * 0.6f), Vector3.one, 0f, Ease.OutCubic);
            case TransitionStyle.Peek: return new StartState(new Vector2(0, -d * 0.15f), Vector3.one, 0f);

            case TransitionStyle.JellySquish: return new StartState(Vector2.zero, new Vector3(1.15f, 0.6f, 1f), 0f, Ease.OutElastic);
            case TransitionStyle.StretchVertical: return new StartState(Vector2.zero, new Vector3(1f, 0.15f, 1f), 0f, Ease.OutBack);
            case TransitionStyle.StretchHorizontal: return new StartState(Vector2.zero, new Vector3(0.15f, 1f, 1f), 0f, Ease.OutBack);
            case TransitionStyle.Shutter: return new StartState(Vector2.zero, new Vector3(1f, 0f, 1f), 8f, Ease.OutCubic);

            case TransitionStyle.Vortex: return new StartState(new Vector2(-d * 1.5f, -d * 1.5f), Vector3.one * 0.3f, 360f, Ease.OutBack);
            case TransitionStyle.Cartwheel: return new StartState(Vector2.zero, Vector3.one * 0.5f, 360f, Ease.OutBack);
            case TransitionStyle.Tumble: return new StartState(new Vector2(d, d), Vector3.one * 0.7f, -180f, Ease.OutBack);
            case TransitionStyle.WaveIn: return new StartState(new Vector2(0, -d * 0.3f), Vector3.one, 10f, Ease.OutElastic);

            default: return new StartState(Vector2.zero, Vector3.one, 0f);
        }
    }

    void PlayParticlesLocal(ParticleSystem[] particles)
    {
        var spawnPos = particleSpawnPoint != null ? particleSpawnPoint.position : transform.position;
        VFXHelper.PlayParticles(particles, spawnPos, rect);
    }

    void PlaySound(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }

    /// <summary>نمایش المان با انیمیشنِ ورود — همیشه دقیقاً به همان حالتی می‌رسد که در UGUI طراحی کرده‌اید</summary>
    public Tween Show(float extraDelay = 0f)
    {
        UITweenDebug.Log($"«{name}»: Show() صدا زده شد (style={showStyle}, delay={extraDelay})");
        EnsureInit();
        gameObject.SetActive(true);

        PlayParticlesLocal(showParticles);
        PlaySound(showSound);

        if (showStyle == TransitionStyle.None)
        {
            SnapToDefault();
            return DOVirtual.DelayedCall(0f, () => { });
        }

        var s = GetStartState(showStyle);
        Ease usedEase = s.easeOverride ?? showEase;

        canvasGroup.alpha = 0f;
        rect.localScale = Vector3.Scale(defaultScale, s.scaleMul);
        rect.anchoredPosition = defaultAnchoredPos + s.posOffset;
        rect.localEulerAngles = defaultRotation + new Vector3(0f, 0f, s.rotOffset);

        var seq = gameObject.DOSequence();
        seq.Join(canvasGroup.DOFade(defaultAlpha, showDuration).SetEase(Ease.OutQuad));
        seq.Join(rect.DOScale(defaultScale, showDuration).SetEase(usedEase));
        seq.Join(rect.DOAnchorPos(defaultAnchoredPos, showDuration).SetEase(usedEase));
        seq.Join(rect.DORotate(defaultRotation, showDuration).SetEase(usedEase));
        seq.SetDelay(extraDelay);
        return seq;
    }

    /// <summary>مخفی‌کردن المان با انیمیشنِ خروج (مستقل از استایل نمایش)، و در پایان غیرفعال‌کردنِ GameObject</summary>
    public Tween Hide(float extraDelay = 0f)
    {
        UITweenDebug.Log($"«{name}»: Hide() صدا زده شد (style={hideStyle}, delay={extraDelay})");
        EnsureInit();

        PlayParticlesLocal(hideParticles);
        PlaySound(hideSound);

        if (hideStyle == TransitionStyle.None)
        {
            gameObject.SetActive(false);
            return DOVirtual.DelayedCall(0f, () => { });
        }

        var s = GetStartState(hideStyle);
        Ease usedEase = s.easeOverride ?? hideEase;

        var seq = gameObject.DOSequence();
        seq.Join(canvasGroup.DOFade(0f, hideDuration).SetEase(Ease.InQuad));
        seq.Join(rect.DOScale(Vector3.Scale(defaultScale, s.scaleMul), hideDuration).SetEase(usedEase));
        seq.Join(rect.DOAnchorPos(defaultAnchoredPos + s.posOffset, hideDuration).SetEase(usedEase));
        seq.Join(rect.DORotate(defaultRotation + new Vector3(0f, 0f, s.rotOffset), hideDuration).SetEase(usedEase));
        seq.SetDelay(extraDelay);
        seq.OnComplete(() => gameObject.SetActive(false));
        return seq;
    }

    /// <summary>بازگرداندنِ فوری (بدون انیمیشن) به دقیقاً همان حالتِ طراحی‌شده در UGUI</summary>
    public void SnapToDefault()
    {
        EnsureInit();
        rect.anchoredPosition = defaultAnchoredPos;
        rect.localScale = defaultScale;
        rect.localEulerAngles = defaultRotation;
        canvasGroup.alpha = defaultAlpha;
    }

    /// <summary>
    /// مخفی‌کردنِ فوری (بدون هیچ انیمیشنی) و غیرفعال‌کردنِ GameObject — بر اساس استایلِ نمایش
    /// (چون این متد برای آماده‌سازیِ نقطه‌ی شروعِ Show در ابتدای بازی استفاده می‌شود).
    /// </summary>
    public void SnapToHidden()
    {
        UITweenDebug.Log($"«{name}»: SnapToHidden() صدا زده شد (style={showStyle})");
        EnsureInit();

        if (showStyle == TransitionStyle.None)
        {
            gameObject.SetActive(false);
            return;
        }

        var s = GetStartState(showStyle);
        canvasGroup.alpha = 0f;
        rect.localScale = Vector3.Scale(defaultScale, s.scaleMul);
        rect.anchoredPosition = defaultAnchoredPos + s.posOffset;
        rect.localEulerAngles = defaultRotation + new Vector3(0f, 0f, s.rotOffset);
        gameObject.SetActive(false);
    }

    void OnDisable()
    {
        UITweenDebug.Log($"«{name}»: OnDisable اجرا شد → DOKill صدا زده می‌شود.");
        gameObject.DOKill();
    }
}