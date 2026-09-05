using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// کنترلر چرخ‌ونه (Wheel of Fortune).
/// این اسکریپت رو باید روی یه ابجکت مدیریتی (مثلاً همون Spinner-Layout) بذاری.
///
/// نکته مهم در مورد ساختار: 
/// - wheelVisual باید همون آبجکتی باشه که واقعاً می‌چرخه (معمولاً spinner-background، 
///   چون هم بخش‌های رنگی و هم رینگ دایره‌های عدد سکه توش هستن و با هم می‌چرخن).
/// - spinner-border (قاب طلایی با جواهرها) و start-spinner-btn نباید بچرخن، 
///   پس اونا رو به wheelVisual اساین نکن؛ باید بیرون از اون، ثابت بمونن.
/// </summary>
public class SpinnerWheelController : MonoBehaviour
{
    [System.Serializable]
    public class SpinnerSegment
    {
        [Tooltip("فقط برای خوانایی توی اینسپکتور - تاثیری در منطق نداره")]
        public string label;

        [Tooltip("تعداد سکه‌ای که این خونه جایزه میده")]
        public int coinReward;

        [Tooltip("وزن احتمال این خونه. هرچی بیشتر باشه شانس بیشتری داره. مقدار پیش‌فرض 1 یعنی احتمال یکسان")]
        [Min(0.0001f)]
        public float weight = 1f;

        // این مقدار خودکار پر میشه، دستی تغییرش نده
        [HideInInspector] public float centerAngle;
    }

    [Header("رفرنس‌ها")]
    [Tooltip("آبجکتی که واقعاً باید بچرخه (معمولاً spinner-background)")]
    [SerializeField] private RectTransform wheelVisual;

    [Tooltip("دکمه‌ی شروع چرخش (start-spinner-btn)")]
    [SerializeField] private Button spinButton;

    [Header("خونه‌های چرخ‌ونه (به ترتیب ساعت‌گرد، از بالا شروع کن)")]
    [SerializeField] private SpinnerSegment[] segments = new SpinnerSegment[8];

    [Header("تنظیمات چرخش")]
    [Tooltip("چرخ‌ونه چند ثانیه بچرخه تا بایسته")]
    [SerializeField] private float spinDuration = 4f;

    [Tooltip("حداقل تعداد دور کامل قبل از توقف")]
    [SerializeField] private int minFullRotations = 5;

    [Tooltip("حداکثر تعداد دور کامل قبل از توقف")]
    [SerializeField] private int maxFullRotations = 8;

    [Tooltip("منحنی شتاب/کاهش سرعت چرخش (پیش‌فرض: شروع تند و کاهش تدریجی سرعت، شبیه یه چرخ واقعی)")]
    [SerializeField] private AnimationCurve spinEase = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 3.2f),
        new Keyframe(1f, 1f, 0f, 0f));

    [Header("لرزش نهایی (Settle Wobble)")]
    [Tooltip("بعد از رسیدن به خونه‌ی نهایی، یه کم بلغزه/بلرزه و بعد بایسته - انیمیشن رو خیلی طبیعی‌تر می‌کنه")]
    [SerializeField] private bool enableSettleWobble = true;

    [Tooltip("مدت زمان لرزش نهایی بر حسب ثانیه")]
    [SerializeField] private float settleDuration = 0.35f;

    [Tooltip("دامنه‌ی لرزش بر حسب درجه")]
    [SerializeField] private float settleWobbleAmplitude = 6f;

    [Tooltip("چند بار جلو-عقب بلرزه قبل از بی‌حرکت شدن کامل")]
    [SerializeField] private float settleWobbleCycles = 2.5f;

    [Header("دیباگ")]
    [Tooltip("وقتی چرخش تموم شد، توی Console بنویسه دقیقاً روی کدوم خونه ایستاده و چقدر جایزه داده")]
    [SerializeField] private bool enableDebugLog = true;

    [Tooltip("اگه فعال باشه، فرود روی خونه دقیقاً وسط خونه نیست و کمی رندوم داخل محدوده خونه‌ست (طبیعی‌تر به نظر میاد)")]
    [SerializeField] private bool randomOffsetInsideSegment = true;

    [Header("صدا")]
    [Tooltip("اگه خالی بذاری، خودش موقع اجرا یه AudioSource روی همین آبجکت پیدا/اضافه می‌کنه")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("صدایی که هر بار موقع چرخش، یه خونه‌ی جدید زیر نشانگر میاد پخش میشه (صدای تیک)")]
    [SerializeField] private AudioClip tickClip;
    [Range(0f, 1f)]
    [SerializeField] private float tickVolume = 1f;

    [Tooltip("صدایی که وقتی چرخ‌ونه کاملاً می‌ایسته (بعد از لرزش نهایی) یه‌بار پخش میشه")]
    [SerializeField] private AudioClip stopClip;
    [Range(0f, 1f)]
    [SerializeField] private float stopVolume = 1f;

    [Header("محدودیت روزانه و شارژ با تبلیغ")]
    [Tooltip("هر روز چند بار رایگان می‌تونه بچرخونه")]
    [SerializeField] private int freeSpinsPerDay = 3;

    [Tooltip("متنی که تعداد چرخش باقی‌مونده رو نشون میده (اختیاری)")]
    [SerializeField] private TMPro.TextMeshProUGUI remainingSpinsText;

    [Tooltip("آبجکت آیکون پلی که از قبل زیرمجموعه‌ی start-spinner-btn ساختیش - وقتی چرخش باقی‌مونده هست فعال میشه")]
    [SerializeField] private GameObject playIconObject;

    [Tooltip("آبجکت آیکون تبلیغ که از قبل زیرمجموعه‌ی start-spinner-btn ساختیش - وقتی چرخشی نمونده فعال میشه")]
    [SerializeField] private GameObject adIconObject;

    [Tooltip("وقتی چرخش تموم شده و کاربر روی دکمه (که الان آیکون تبلیغه) کلیک می‌کنه، این رویداد صدا زده میشه. اینجا رو به SDK تبلیغاتی خودت وصل کن تا رول‌درادورتایزینگ نشون بده")]
    public UnityEvent OnAdRequested;

    [Header("تست تبلیغ (قبل از وصل شدن SDK واقعی)")]
    [Tooltip("اگه فعال باشه، هر وقت درخواست تبلیغ بشه، به‌جای منتظر موندن برای تبلیغ واقعی، بعد از یه تاخیر کوتاه خودش موفقیت‌آمیز شبیه‌سازی میشه. برای تست سریع، قبل از وصل کردن SDK واقعی روشنش کن، بعداً خاموشش کن")]
    [SerializeField] private bool simulateAdWatch = false;

    [Tooltip("چند ثانیه بعد از کلیک، شبیه‌سازی تبلیغ موفق تموم بشه")]
    [SerializeField] private float simulateAdDelay = 1.5f;

    [Header("رویدادها")]
    [Tooltip("وقتی چرخش تموم شد و جایزه مشخص شد صدا زده میشه. عدد پاس داده شده تعداد سکه‌ست")]
    public UnityEvent<int> OnRewardGranted;

    [Tooltip("وقتی چرخش شروع میشه صدا زده میشه (برای غیرفعال کردن UI دیگه و ...)")]
    public UnityEvent OnSpinStarted;

    private bool isSpinning;
    private float segmentAngle;
    private int remainingSpins;

    private const string PrefKeyRemainingSpins = "SpinnerWheel_RemainingSpins";
    private const string PrefKeyLastResetDate = "SpinnerWheel_LastResetDate";

    /// <summary>
    /// دسترسی فقط-خواندنی به لیست خونه‌ها، برای ابزارهای دیگه مثل SpinnerDebugVisualizer
    /// که می‌خوان لیبل و مقدار سکه رو از همینجا بخونن، نه اینکه خودشون یه کپی جدا نگه دارن.
    /// </summary>
    public SpinnerSegment[] Segments => segments;

    private void Awake()
    {
        if (segments == null || segments.Length == 0)
        {
            Debug.LogError("[SpinnerWheelController] هیچ خونه‌ای تعریف نشده!");
            return;
        }

        segmentAngle = 360f / segments.Length;

        // زاویه‌ی مرکز هر خونه رو خودکار حساب می‌کنیم (خونه ۰ بالا، بقیه ساعت‌گرد)
        for (int i = 0; i < segments.Length; i++)
        {
            segments[i].centerAngle = i * segmentAngle;
        }

        if (spinButton != null)
        {
            spinButton.onClick.AddListener(OnSpinButtonClicked);
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;

        LoadOrResetDailySpins();
        UpdateSpinUI();
    }

    /// <summary>
    /// اگه از آخرین باری که بازی باز شده روز عوض شده باشه، تعداد چرخش‌ها رو ریست می‌کنه.
    /// فعلاً با PlayerPrefs ذخیره میشه که ساده و سریعه ولی روی گوشی روت‌شده قابل دستکاریه؛
    /// اگه امنیتش مهمه، همین دو تا خط ذخیره/خوندن رو با سیستم GameVars/SecureSave خودت جایگزین کن.
    /// </summary>
    private void LoadOrResetDailySpins()
    {
        string today = System.DateTime.Now.ToString("yyyy-MM-dd");
        string lastReset = PlayerPrefs.GetString(PrefKeyLastResetDate, "");

        if (lastReset != today)
        {
            remainingSpins = freeSpinsPerDay;
            PlayerPrefs.SetString(PrefKeyLastResetDate, today);
            PlayerPrefs.SetInt(PrefKeyRemainingSpins, remainingSpins);
            PlayerPrefs.Save();
        }
        else
        {
            remainingSpins = PlayerPrefs.GetInt(PrefKeyRemainingSpins, freeSpinsPerDay);
        }
    }

    private void SaveRemainingSpins()
    {
        PlayerPrefs.SetInt(PrefKeyRemainingSpins, remainingSpins);
        PlayerPrefs.Save();
    }

    private void UpdateSpinUI()
    {
        bool hasSpinsLeft = remainingSpins > 0;
        bool buttonEnabled = spinButton == null || spinButton.interactable;

        // آیکون‌ها فقط وقتی دکمه واقعاً فعاله نشون داده میشن؛
        // وقتی دکمه دیسیبله (مثلاً وسط چرخش)، هر دو آیکون غیرفعال میشن.
        if (playIconObject != null)
            playIconObject.SetActive(buttonEnabled && hasSpinsLeft);

        if (adIconObject != null)
            adIconObject.SetActive(buttonEnabled && !hasSpinsLeft);

        if (remainingSpinsText != null)
        {
            remainingSpinsText.text = remainingSpins.ToString();
            remainingSpinsText.gameObject.SetActive(hasSpinsLeft);
        }
    }

    /// <summary>
    /// جایگزین ست‌کردن مستقیم spinButton.interactable؛ علاوه بر فعال/غیرفعال کردن خودِ دکمه،
    /// آیکون‌های زیرمجموعه‌ش رو هم هماهنگ می‌کنه (وقتی دکمه دیسیبل میشه، آیکون‌ها هم غیرفعال میشن).
    /// </summary>
    private void SetButtonInteractable(bool value)
    {
        if (spinButton != null) spinButton.interactable = value;
        UpdateSpinUI();
    }

    private void OnSpinButtonClicked()
    {
        if (isSpinning) return;

        if (remainingSpins > 0)
        {
            Spin();
        }
        else
        {
            RequestAd();
        }
    }

    /// <summary>
    /// وقتی کاربر روی دکمه (که الان آیکون تبلیغه) کلیک می‌کنه صدا زده میشه.
    /// این فقط رویداد رو بیرون می‌فرسته؛ خودِ نمایش تبلیغ رو باید توی OnAdRequested
    /// به SDK تبلیغاتی‌ت (مثلاً Unity Ads / AdMob / IronSource) وصل کنی.
    /// </summary>
    private void RequestAd()
    {
        OnAdRequested?.Invoke();

        if (simulateAdWatch)
        {
            StartCoroutine(SimulateAdRoutine());
        }
    }

    /// <summary>
    /// فقط برای تست: بعد از یه تاخیر کوتاه، انگار تبلیغ با موفقیت دیده شده رفتار می‌کنه.
    /// وقتی SDK واقعی رو وصل کردی، تیک simulateAdWatch رو بردار تا این مسیر اجرا نشه.
    /// </summary>
    private IEnumerator SimulateAdRoutine()
    {
        yield return new WaitForSeconds(simulateAdDelay);
        OnAdWatchedSuccessfully();
    }

    /// <summary>
    /// از منوی راست‌کلیک روی کامپوننت هم می‌تونی همین لحظه، بدون تاخیر، یه چرخش شارژ کنی
    /// (برای تست دستی سریع، مستقل از تیک simulateAdWatch).
    /// </summary>
    [ContextMenu("شبیه‌سازی تماشای تبلیغ (+۱ چرخش)")]
    private void Debug_SimulateAdWatched()
    {
        OnAdWatchedSuccessfully();
    }

    /// <summary>
    /// این متد رو دقیقاً از callback موفقیت تبلیغ ریواردی SDK خودت صدا بزن
    /// (یعنی جایی که تبلیغ کامل دیده شده و باید جایزه داده بشه).
    /// هر بار صداش بزنی فقط یه چرخش شارژ می‌کنه.
    /// </summary>
    public void OnAdWatchedSuccessfully()
    {
        remainingSpins += 1;
        SaveRemainingSpins();
        UpdateSpinUI();
    }

    /// <summary>
    /// چرخش رو به صورت دستی هم میشه از کد صدا زد (مثلاً از یه سیستم دیگه).
    /// اگه چرخش رایگان نمونده باشه، به‌جای چرخوندن، فقط یه وارنینگ می‌ده -
    /// برای درخواست تبلیغ از کلیک روی دکمه استفاده کن (RequestAd از همونجا صدا زده میشه).
    /// </summary>
    public void Spin()
    {
        if (isSpinning) return;

        if (remainingSpins <= 0)
        {
            Debug.LogWarning("[SpinnerWheelController] چرخش رایگان تموم شده - باید از مسیر تبلیغ شارژ بشه.");
            return;
        }

        remainingSpins -= 1;
        SaveRemainingSpins();
        UpdateSpinUI();

        int winningIndex = PickWeightedRandomIndex();
        StartCoroutine(SpinRoutine(winningIndex));
    }

    private int PickWeightedRandomIndex()
    {
        float totalWeight = 0f;
        for (int i = 0; i < segments.Length; i++)
            totalWeight += segments[i].weight;

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < segments.Length; i++)
        {
            cumulative += segments[i].weight;
            if (roll <= cumulative)
                return i;
        }

        return segments.Length - 1; // fallback (به خاطر خطای شناور)
    }

    private IEnumerator SpinRoutine(int winningIndex)
    {
        isSpinning = true;
        SetButtonInteractable(false);
        OnSpinStarted?.Invoke();

        float startAngle = wheelVisual.localEulerAngles.z;

        float offset = randomOffsetInsideSegment
            ? Random.Range(-segmentAngle * 0.35f, segmentAngle * 0.35f)
            : 0f;

        // نشانگر (پوینتر) بالای چرخ‌ونه‌ست (زاویه صفر).
        // باید بچرخونیم تا خونه‌ی برنده دقیقاً زیر نشانگر بیفته.
        // (قبلاً اینجا علامت منفی بود که باعث می‌شد چرخ روی خونه‌ی آینه‌ایِ برنده بایسته،
        // نه خودِ برنده - چون زاویه‌ی نهاییِ لازم برابر خودِ centerAngle هست، نه منفیش)
        float desiredFinalAngle = segments[winningIndex].centerAngle + offset;
        desiredFinalAngle = ((desiredFinalAngle % 360f) + 360f) % 360f;

        float currentNormalized = ((startAngle % 360f) + 360f) % 360f;

        float deltaToDesired = desiredFinalAngle - currentNormalized;
        if (deltaToDesired < 0f) deltaToDesired += 360f;

        int fullRotations = Random.Range(minFullRotations, maxFullRotations + 1);
        float totalRotation = fullRotations * 360f + deltaToDesired;
        float finalAngle = startAngle + totalRotation;

        float elapsed = 0f;
        int prevBoundaryCount = Mathf.FloorToInt(startAngle / segmentAngle);

        while (elapsed < spinDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / spinDuration);
            float easedT = spinEase.Evaluate(t);
            float currentAngle = Mathf.Lerp(startAngle, finalAngle, easedT);

            wheelVisual.localEulerAngles = new Vector3(0f, 0f, currentAngle);

            // شمارش تعداد مرزهای خونه‌هایی که از فریم قبل تا الان رد شدیم
            // (نه فقط مقایسه‌ی ایندکس فعلی، چون توی سرعت زیاد ممکنه یه فریم چند خونه رو رد کنه)
            int currentBoundaryCount = Mathf.FloorToInt(currentAngle / segmentAngle);
            int crossedCount = currentBoundaryCount - prevBoundaryCount;
            for (int c = 0; c < crossedCount; c++)
                PlayTick();
            prevBoundaryCount = currentBoundaryCount;

            yield return null;
        }

        // اطمینان از فرود دقیق روی زاویه‌ی هدف (جلوگیری از خطای شناور)
        wheelVisual.localEulerAngles = new Vector3(0f, 0f, finalAngle);

        // لرزش نهایی: بعد از رسیدن به خونه‌ی برنده، به‌جای توقف خشک و ناگهانی،
        // چند بار کوچیک جلو-عقب می‌لرزه با دامنه‌ی کم‌شونده و بعد کاملاً بی‌حرکت می‌شه.
        if (enableSettleWobble)
        {
            float wobbleElapsed = 0f;
            while (wobbleElapsed < settleDuration)
            {
                wobbleElapsed += Time.deltaTime;
                float wt = Mathf.Clamp01(wobbleElapsed / settleDuration);
                float damping = 1f - wt; // هرچی جلوتر بریم دامنه کمتر میشه
                float wobble = Mathf.Sin(wt * Mathf.PI * settleWobbleCycles * 2f) * settleWobbleAmplitude * damping;

                wheelVisual.localEulerAngles = new Vector3(0f, 0f, finalAngle + wobble);
                yield return null;
            }

            wheelVisual.localEulerAngles = new Vector3(0f, 0f, finalAngle);
        }

        GrantReward(winningIndex, finalAngle);

        PlayStop();

        isSpinning = false;
        SetButtonInteractable(true);
    }

    private void PlayTick()
    {
        if (audioSource != null && tickClip != null)
            audioSource.PlayOneShot(tickClip, tickVolume);
    }

    private void PlayStop()
    {
        if (audioSource != null && stopClip != null)
            audioSource.PlayOneShot(stopClip, stopVolume);
    }

    private void GrantReward(int winningIndex, float finalAngle)
    {
        int reward = segments[winningIndex].coinReward;

        if (enableDebugLog)
        {
            string segLabel = string.IsNullOrEmpty(segments[winningIndex].label)
                ? $"#{winningIndex}"
                : segments[winningIndex].label;

            float normalizedAngle = ((finalAngle % 360f) + 360f) % 360f;

            Debug.Log(
                $"[SpinnerWheel] ایستاد روی خونه {winningIndex} ({segLabel}) | " +
                $"جایزه: {reward} سکه | زاویه‌ی نرمال‌شده‌ی چرخ: {normalizedAngle:F1}° | " +
                $"زاویه‌ی مرکز خونه: {segments[winningIndex].centerAngle:F1}°");
        }

        // اینجا نقطه‌ی اتصال به سیستم سکه‌ی خودت (GameVars) هست.
        // مثلاً اگه یه فیلد استاتیک به اسم Coins توی GameVars داری:
        //
        //     GameVars.Coins.Value += reward;
        //
        // یا اگه ObscuredInt مستقیم هست:
        //
        //     GameVars.Coins += reward;
        //
        // چون اسم دقیق فیلد رو نمی‌دونم، این خط رو خودت با ساختار GameVars جایگزین کن.

        OnRewardGranted?.Invoke(reward);
    }
}