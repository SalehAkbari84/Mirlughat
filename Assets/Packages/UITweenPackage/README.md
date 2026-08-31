# UITween — موتور انیمیشن همه‌کاره برای یونیتی

یک موتور تویین سبک، بدون وابستگی خارجی، با پوشش گسترده روی UGUI و اکثر کامپوننت‌های رایج یونیتی (دوربین، نور، صدا، متریال، اسپرایت). API روان و زنجیره‌ای، شبیه DOTween.

> **یک نکته‌ی صادقانه:** DOTween یک ابزار چندین‌ساله، بهینه‌شده در سطح پایین و تست‌شده روی هزاران پروژه‌ی واقعی است. این پکیج را نمی‌توان به‌طور واقعی "۱۰ برابر سریع‌تر" از آن دانست. آنچه این نسخه ارائه می‌دهد: **دامنه‌ی کاربرد بسیار گسترده‌تر** (نه‌فقط UI، بلکه دوربین/نور/صدا/متریال/اسپرایت)، **کد کاملاً باز و قابل‌ویرایش** (چون مالکیت کامل کدش با شماست)، Pooling برای کاهش GC، و امکاناتی مثل Shake، Sequence، DOVirtual، منحنی سفارشی و تایپ‌رایتر متن که در نسخه‌ی اول نبود.

## نصب

پوشه‌ی `UITweenPackage` را کامل داخل `Assets` کپی کنید. اگر پروژه‌تان TextMeshPro ندارد، پوشه‌ی `Runtime/Optional_TMP` را حذف کنید (در غیر این‌صورت خطای کامپایل می‌گیرید چون به اسمبلی TMPro رفرنس دارد).

```csharp
using UITween;
```

## مثال‌های پایه

```csharp
// جابجایی یک پنل
rectTransform.DOAnchorPos(new Vector2(0, 300), 0.5f).SetEase(Ease.OutBack);

// ورود از یک نقطه‌ی مشخص به موقعیت فعلی
rectTransform.DOAnchorPosFrom(new Vector2(-800, 0), 0.5f).SetEase(Ease.OutCubic);

// محو شدن با تاخیر
canvasGroup.DOFade(0f, 0.4f).SetDelay(0.3f);

// ضربان بی‌نهایت
transform.DOScale(1.1f, 0.6f).SetLoops(-1, LoopType.Yoyo);

// فیدبک لمس دکمه
button.transform.DOPunchScale(0.15f, 0.3f);

// لرزش هنگام خطا
errorPanel.GetComponent<RectTransform>().DOShakeAnchorPos(0.4f, strength: 15f);

// تایپ‌رایتر
myText.DOText("سلام، خوش آمدید!", 1.2f);

// منحنی حرکتی سفارشی
image.rectTransform.DOAnchorPosY(400, 1f).SetEase(myCustomAnimationCurve);

// تویین روی چیزی غیر از UI
mainCamera.DOFieldOfView(75f, 0.5f);
spotLight.DOIntensity(0f, 1.5f);
musicSource.DOFadeOutAndStop(2f);

// تویین مقدار دلخواه بدون آبجکت صحنه (مثلاً شمارنده‌ی امتیاز)
int score = 0;
DOVirtual.Int(0, 1000, 1.5f, v => { score = v; scoreText.text = v.ToString(); });

// اجرای تابع بعد از تاخیر، بدون Coroutine
DOVirtual.DelayedCall(2f, () => Debug.Log("۲ ثانیه گذشت"));

// اجرای دوره‌ای — مثلا هر ۳ ثانیه یک دشمن اسپاون شود، تا وقتی خودِ اسپاونر نابود شود
this.DOSchedule(3f, () => SpawnEnemy());

// همون کار ولی محدود به ۵ بار تکرار و شروع فوری (بدون صبر برای ۳ ثانیه‌ی اول)
this.DOSchedule(3f, () => SpawnEnemy(), loops: 5, callImmediately: true);

// نسخه‌ی مستقل از هر Component، تا وقتی دستی متوقفش کنید (بی‌نهایت)
Tween heartbeat = DOVirtual.RepeatingCall(1f, () => Debug.Log("تیک"));
// بعداً هر زمان خواستید:
heartbeat.Kill();

// کشتن تویین‌های یک شیء
panelTransform.DOKill();

// کشتن مطلق همه‌چیز (مثلاً موقع تعویض صحنه)
UITweenManager.KillEverything();
```

## Sequence

```csharp
var seq = panel.DOSequence();
seq.Append(rect.DOAnchorPos(Vector2.zero, 0.4f).SetEase(Ease.OutCubic))
   .Join(canvasGroup.DOFade(1f, 0.4f))
   .AppendInterval(0.5f)
   .AppendCallback(() => Debug.Log("نمایش کامل شد"))
   .Append(rect.DOAnchorPos(new Vector2(0, 400), 0.4f).SetEase(Ease.InCubic))
   .OnComplete(() => panel.gameObject.SetActive(false));
```

## فهرست کامل امکانات

| دسته | متدها |
|---|---|
| `RectTransform` | `DOAnchorPos`, `DOAnchorPosFrom`, `DOAnchorPosX/Y`, `DOSizeDelta`, `DOAnchorMin/Max`, `DOPivot`, `DOShakeAnchorPos` |
| `Transform` | `DOMove`, `DOLocalMove`, `DOScale`, `DOScaleFrom`, `DORotate`, `DOPunchScale`, `DOShakePosition/LocalPosition/Scale/Rotation` |
| `CanvasGroup` | `DOFade`, `DOFadeFrom` |
| `Graphic` (Image/RawImage/Text) | `DOColor`, `DOColorFrom`, `DOFade` |
| `Image` | `DOFillAmount` |
| `Text` | `DOText` (تایپ‌رایتر) |
| `Slider` | `DOValue` |
| `ScrollRect` | `DONormalizedPos` |
| `LayoutElement` | `DOPreferredHeight/Width` |
| `Camera` | `DOFieldOfView`, `DOOrthoSize`, `DOBackgroundColor` |
| `Light` | `DOIntensity`, `DOColor`, `DORange` |
| `AudioSource` | `DOVolume`, `DOPitch`, `DOFadeOutAndStop` |
| `Material` | `DOColor`, `DOFloat`, `DOOffset` |
| `SpriteRenderer` | `DOColor`, `DOFade` |
| `TMP_Text` (اختیاری) | `DOColor`, `DOFade`, `DOText`, `DOFontSize` |
| هر `Component` | `DOSequence`, `DOKill` |
| بدون آبجکت صحنه | `DOVirtual.Float/Vector2/Vector3/Color/Int`, `DOVirtual.DelayedCall`, `DOVirtual.RepeatingCall` |
| زمان‌بندی روی یک Component | `component.DOSchedule(interval, callback, loops, callImmediately)` — معادل تمیز و ایمنِ `InvokeRepeating` |

## متدهای زنجیره‌ای روی هر تویین

```
.SetEase(Ease.OutBack)              // یکی از ۳۰ منحنی آماده
.SetEase(myAnimationCurve)          // یا یک منحنی کاملاً سفارشی
.SetDelay(0.5f)
.SetLoops(3, LoopType.Yoyo)         // -1 = بی‌نهایت
.SetUpdate(true)                    // مستقل از Time.timeScale
.OnStart(() => ...)
.OnUpdate(() => ...)
.OnComplete(() => ...)
.OnLoop(() => ...)                  // بعد از پایان هر دور تکرار — پایه‌ی تایمرهای زمان‌بندی‌شده
.OnKill(() => ...)
.Play() / .Pause() / .TogglePause()
.Kill(complete: false)
.Restart()
```

## معماری و بهینه‌سازی

- **بدون نیاز به MonoBehaviour اضافه:** موتور Singleton و lazy است.
- **ایمنی خودکار:** اگر GameObject هدف حین انیمیشن نابود شود، تویین بی‌خطا حذف می‌شود.
- **Pooling:** تویین‌های `ValueTween<T>` که مستقیم به موتور ثبت می‌شوند (نه داخل Sequence) بعد از پایان بازیافت می‌شوند تا فشار روی Garbage Collector کم شود. تویین‌های داخل یک Sequence فعلاً از این بازیافت مستثنا هستند (محدودیت شناخته‌شده، تاثیر عملی‌اش برای اکثر بازی‌ها ناچیز است).
- **توسعه‌پذیر:** برای افزودن هر کامپوننت یا نوع مقدار جدید، فقط یک متد اکستنشن با همان الگوی نمونه‌های موجود بنویسید — یک `ValueTween<T>` با Setter و Lerp مناسب بسازید و با `UITweenManager.Track(...)` ثبت کنید.

## ساختار پوشه‌ها

```
UITweenPackage/
├── Runtime/
│   ├── UITween.asmdef
│   ├── Core/
│   │   ├── Ease.cs              منحنی‌های حرکتی
│   │   ├── Tween.cs             کلاس پایه + ValueTween Pool‑شده + تنظیمات سراسری
│   │   ├── UITweenManager.cs    موتور اجرا
│   │   ├── Sequence.cs          زنجیره‌سازی
│   │   ├── ShakeTween.cs        موتور افکت لرزش
│   │   └── DOVirtual.cs         تویین بدون شیء صحنه
│   ├── Extensions/
│   │   ├── UGUIExtensions.cs    RectTransform, Transform, CanvasGroup, Graphic, Image, Text, Slider, ScrollRect, LayoutElement
│   │   └── ComponentExtensions.cs   Camera, Light, AudioSource, Material, SpriteRenderer
│   └── Optional_TMP/            (در صورت نبودِ TextMeshPro در پروژه، حذف شود)
│       ├── UITween.TMP.asmdef
│       └── TMPExtensions.cs
└── README.md
```
