# سیستم انیمیشن و پارتیکل UI Toolkit

یک ابزار کامل برای ساخت انیمیشن و افکت روی رابط‌های **UI Toolkit** یونیتی (`VisualElement` / UXML / USS).
شامل موتور انیمیشن، ویرایشگر بصری تک‌پنجره‌ای، سیستم پارتیکل، و یک API کامل برای کار با کد.

> این راهنما «صفر تا صد» است. اگر تازه‌کاری، از بخش **۱ تا ۵** را به‌ترتیب بخوان. اگر برنامه‌نویسی، بخش **۶ به بعد** مرجع کامل کد است.

---

## فهرست

1. [نصب](#۱-نصب)
2. [مفاهیم پایه](#۲-مفاهیم-پایه)
3. [شروع سریع با کد (۲ دقیقه)](#۳-شروع-سریع-با-کد)
4. [آموزش بصری کامل: پنجره‌ی Scene Animator](#۴-آموزش-بصری-کامل)
5. [اجرا در بازی (runtime)](#۵-اجرا-در-بازی)
6. [مرجع کامل API کد](#۶-مرجع-کامل-api-کد)
7. [Easing‌ها](#۷-easingها)
8. [خصوصیات قابل انیمیت (AnimatableProperty)](#۸-خصوصیات-قابل-انیمیت)
9. [پریست‌های آماده](#۹-پریستهای-آماده)
10. [سیستم اعتبارسنجی (Validation)](#۱۰-سیستم-اعتبارسنجی)
11. [عیب‌یابی و سؤالات متداول](#۱۱-عیبیابی)
12. [محدودیت‌ها](#۱۲-محدودیتها)

---

## ۱. نصب

1. پوشه‌ی `UIAnimationSystem` را داخل `Assets` پروژه‌ات کپی کن.
2. تمام. هیچ setup دیگری لازم نیست؛ `TweenManager` در زمان اجرا خودش را خودکار می‌سازد.
3. نیازمندی: **Unity 6** (6000.x) با پکیج UI Toolkit (پیش‌فرض نصب است). برای اجرای تست‌ها پکیج **Test Framework** لازم است.

---

## ۲. مفاهیم پایه

چهار مفهوم را که بفهمی، همه‌چیز ساده می‌شود:

- **Tween**: یک تغییر نرمِ یک مقدار در طول زمان (مثلاً opacity از ۰ به ۱ در ۰.۳ ثانیه).
- **Clip (`UIAnimationClip`)**: یک انیمیشنِ کامل و قابل‌ذخیره که می‌تواند **چند المان را هم‌زمان** انیمیت کند. هر کلیپ از «تِرَک‌های المان» تشکیل شده و هر تِرَک، چند «تِرَک خاصیت» با کی‌فریم دارد.
- **bind by name (اتصال با نام)**: کلیپ‌ها به المان‌ها **با نام** وصل می‌شوند، نه با ارجاع مستقیم. یعنی هر المانی که می‌خواهی انیمیت شود، باید در UXML **نام (name)** داشته باشد. هنگام پخش، سیستم با `root.Q<VisualElement>("نام")` المان را پیدا می‌کند.
- **Scene (`UISceneAnimation`)**: یک asset که یک فایل UXML را به مجموعه‌ای از کلیپ‌ها و پارتیکل‌ها وصل می‌کند — بدون دست‌زدن به خود UXML.

نکته‌ی کلیدی: **برای انیمیت‌شدن، المان باید در UXML نام داشته باشد.** (در UI Builder، فیلد Name را پر کن.)

---

## ۳. شروع سریع با کد

```csharp
using UIToolkit.Animation;            // افکت‌های آماده روی VisualElement

myElement.FadeIn(0.3f);                                   // محو ورود
myButton.ScaleTo(1.2f, 0.25f).SetEase(Ease.OutBack);     // بزرگ‌شدن فنری
panel.SlideInFromLeft(300f, 0.5f);                       // ورود از چپ
label.ColorTo(Color.red, 0.3f);                          // تغییر رنگ
spinner.Spin(1f);                                        // چرخش بی‌نهایت
icon.Pulse();                                            // نبض بی‌نهایت
```

هر متد یک `Tween` برمی‌گرداند که قابل زنجیره‌کردن است (بخش ۶).

---

## ۴. آموزش بصری کامل

روش بدون کد. کل کار در **یک پنجره** انجام می‌شود.

### گام ۱ — ساخت asset‌ها
از پنجره‌ی Project راست‌کلیک کن:
- **Create > UI Toolkit > Scene Animation** → یک «صحنه» بساز (مثلاً `MainMenuScene`).
- (در صورت نیاز) **Create > UI Toolkit > Animation Clip** و **Create > UI Toolkit > Particle System**. اما معمولاً لازم نیست؛ از داخل پنجره می‌سازی.

### گام ۲ — باز کردن پنجره
**Window > UI Toolkit > Scene Animator**. (یا روی asset صحنه دابل‌کلیک کن.)

در نوار بالا (Toolbar) فیلد **Scene** را به asset صحنه‌ات وصل کن، و در تب **Setup** فایل **UXML** را بده. حالا preview زنده‌ی رابطت را می‌بینی.

### گام ۳ — بخش‌های پنجره

```
┌─ Toolbar: Scene | Reload | New Scene | AutoFit | Zoom | Frame | Reset | Preview Scene | ▸ Setup & Play
├───────────────┬───────────────────────────┬──────────────────────────┐
│  HIERARCHY    │        PREVIEW            │   تب‌ها: Setup/Animate/   │
│  (درختِ       │  (رندر واقعیِ UXML)        │   Effects/Manage         │
│   المان‌ها)    │                           │                          │
├───────────────┴───────────────────────────┴──────────────────────────┤
│  TIMELINE  (داکِ تمام‌عرض پایین — کی‌فریم‌ها اینجا)                       │
└────────────────────────────────────────────────────────────────────────┘
```

**Hierarchy (چپ):**
- درختِ تاشو از همه‌ی المان‌های UXML. روی فلش بزن تا باز/بسته شود.
- بالای آن یک کادر **جستجو** هست.
- نشانِ آبی «A» = این المان در کلیپ فعال انیمیشن دارد. نشانِ نارنجی «P» = پارتیکل دارد.
- نقطه‌ی آبی = نام‌دار، نقطه‌ی خاکستری = بی‌نام (قابل انیمیت نیست).
- **کلیک** = انتخاب. **دابل‌کلیک** = focus/قاب‌گرفتن آن المان در preview.

**Preview (وسط) و ناوبری:**
- **چرخ موس** = زوم به‌سمت نشانگر.
- **درگ با دکمه‌ی وسط موس** = جابه‌جایی (pan).
- **Auto Fit** (روشن به‌صورت پیش‌فرض) = خودکار کل صفحه را جا می‌دهد و وسط می‌چیند.
- **Frame** (یا کلید `F`) = قاب‌گرفتن المان انتخاب‌شده. **Reset View** = برگشت به نمای کامل.
- کلیک روی هر المان در preview = انتخابش.

### گام ۴ — ساخت اولین انیمیشن (قدم‌به‌قدم)

1. در preview یا hierarchy یک المان **نام‌دار** را انتخاب کن.
2. برو تب **Animate**. اگر کلیپ نداری، دکمه‌ی **New** را بزن (خودکار در `Assets/Resources/UIAnimations` ذخیره می‌شود).
3. **ساده‌ترین راه — Preset**: بخش **Presets** را باز کن، یکی مثل `Pop In` را انتخاب و **Apply** بزن. تمام شد؛ یک کلیپِ آماده ساخته و به المان bind می‌شود.
4. **راه دستی — Element Inspector + Timeline**:
   - در کارت **ELEMENT INSPECTOR** دکمه‌ی **+ Property** را بزن و خاصیت موردنظر (مثلاً Opacity) را اضافه کن.
   - در **تایم‌لاینِ پایین**، playhead (خط قرمز) را روی زمان دلخواه ببر، روی تِرَک دابل‌کلیک کن تا کی‌فریم بسازی (یا دکمه‌ی + کنار تِرَک).
   - روی الماسِ کی‌فریم کلیک کن؛ در پنل پایینِ تایم‌لاین مقدار، زمان و **ease** آن را تنظیم کن (نمودار کوچک ease هم نمایش داده می‌شود).
   - playhead را بکش تا انیمیشن را روی preview واقعی ببینی.

### گام ۵ — Record (ضبط حرکت)
در تب Animate بخش **Record & shortcuts** را باز کن و **Record** را روشن کن. حالا المان انتخاب‌شده را در preview **بکش**؛ کلیدهای موقعیت (TranslateX/Y) خودکار در playhead ساخته می‌شوند.

### میان‌برهای کیبورد (وقتی پنجره فوکوس دارد)
`Space` پخش/مکث · `K` ثبت مقدار فعلی · `Del` حذف کی‌فریم · `←/→` حرکت playhead · `Shift+←/→` جابه‌جایی کی‌فریم · `F` قاب‌گرفتن · `Ctrl+C/V/D` کپی/پیست/تکثیر کی‌فریم.

### ویرایش پیشرفته‌ی کی‌فریم (در تایم‌لاین)
- **چند-انتخاب**: `Ctrl/Shift + کلیک`.
- **انتخاب کادری (box-select)**: روی فضای خالیِ تِرَک درگ کن.
- **درگ گروهی**: یک کی‌فریمِ انتخاب‌شده را بکش، بقیه هم با همان دلتا جابه‌جا می‌شوند.
- **ease کل تِرَک**: دکمه‌ی `E` کنار تِرَک.

### گام ۶ — پارتیکل (تب Effects)
1. المان میزبان (host) را انتخاب کن.
2. تب **Effects**: یک preset پارتیکل (Confetti/Sparkle/Smoke/Trail/…) انتخاب و **Apply** بزن؛ یا یک `ParticleSystemConfig` آماده را bind کن.

### گام ۷ — مدیریت (تب Manage)
سه بخش جمع‌شونده:
- **Library**: همه‌ی کلیپ‌ها و پارتیکل‌های پروژه؛ جستجو، Edit، **Copy Call** (کپیِ کد صدا زدن)، Ping، تغییر نام درجا، حذف (به سطل بازیافت).
- **Scene Bindings**: مدیریت همه‌ی bindingهای صحنه (id/trigger/root/host) با هشدارِ نام‌های نامعتبر.
- **Validation**: بررسی کامل مشکلات + دکمه‌ی **Auto-fix**.

### گام ۸ — اجرا
- **پیش‌نمایش کل صحنه داخل ادیتور**: دکمه‌ی **Preview Scene** (بدون Play Mode).
- **اجرای واقعی با یک کلیک**: دکمه‌ی سبزِ **`> Setup & Play`** — خودکار یک `UISceneDirector` به GameObjectِ UIDocument در صحنه وصل می‌کند و Play Mode را روشن می‌کند.
  - پیش‌نیاز: باید یک GameObject با **UIDocument** (و یک **PanelSettings**) در صحنه داشته باشی که همین UXML را نمایش دهد.

---

## ۵. اجرا در بازی

سه روش:

### الف) با دایرکتور (بدون کد)
کامپوننت **UI Toolkit > UI Scene Director** را به GameObjectِ UIDocument اضافه کن، asset صحنه را بده. binding‌هایی که trigger‌شان `OnEnable`/`OnStart` است خودکار اجرا می‌شوند. (دکمه‌ی Setup & Play همین را خودکار انجام می‌دهد.)
- اجرای دستی از کد: `GetComponent<UISceneDirector>().PlayClip("intro");`

### ب) صدا زدن با نام
کلیپ‌هایی که در ادیتور می‌سازی، در `Resources/UIAnimations` ذخیره و در `Resources/UIAnimationLibrary.asset` ثبت می‌شوند، پس با **نام** قابل اجرا هستند:
```csharp
using UIToolkit.Animation.Timeline;
UIAnimation.Play("PopIn_loginButton", rootVisualElement);
```

### ج) با trigger (هاور/کلیک/…)
انیمیشنِ از-قبل-ساخته را با شرط اجرا به المان بده:
```csharp
button.PlayOn(UITrigger.PointerEnter, "ButtonHover");
button.PlayOnClick("ButtonPress");
icon.PlayOn(UITrigger.Hold, "ChargeUp", holdSeconds: 0.6f);
```

---

## ۶. مرجع کامل API کد

### ۶.۱ افکت‌های آماده (روی هر `VisualElement`)
`using UIToolkit.Animation;`

| دسته | متدها |
|------|-------|
| Fade | `Fade(to,dur)` · `FadeIn(dur)` · `FadeOut(dur, hideAtEnd)` |
| Scale | `ScaleTo(uniform یا Vector3, dur)` · `PunchScale(strength,dur)` |
| Move | `MoveTo(Vector2,dur)` · `MoveBy(delta,dur)` · `SlideInFromLeft/Right/Bottom` · `Shake(strength,dur,vibrato)` · `MovePath(points,dur)` |
| Rotate | `RotateTo(deg,dur)` · `Spin(dur)` |
| Color | `ColorTo(c,dur)` · `BackgroundColorTo(c,dur)` |
| Size | `WidthTo(v,dur)` · `HeightTo(v,dur)` |
| لوپ | `Pulse(scale,dur)` · `Wiggle(angle,dur)` · `FloatLoop(distance,dur)` |
| ابزار | `SetPivot(x,y)` · `KillTweens()` |
| Stagger (روی `IList<VisualElement>`) | `StaggerFadeIn` · `StaggerScaleIn` · `StaggerSlideInFromBottom` |

### ۶.۲ زنجیره‌ی Tween (Fluent)
```csharp
myElement.MoveTo(target, 0.5f)
    .From(startPos)
    .SetEase(Ease.InOutCubic)          // یا .SetEase(myAnimationCurve)
    .SetDelay(0.2f)
    .SetLoops(3, LoopType.Yoyo)        // -1 = بی‌نهایت
    .SetId("intro")
    .SetIgnoreTimeScale()
    .OnStart(()=>{}).OnUpdate(t=>{}).OnStepComplete(()=>{}).OnComplete(()=>{});
```
کنترل: `t.Pause()` · `t.Play()` · `t.Restart()` · `t.Kill()` · `t.Kill(complete:true)`.

### ۶.۳ Sequence (انیمیشن چندمرحله‌ای)
```csharp
Tweening.Sequence()
    .Append(panel.FadeIn(0.4f))
    .Join(panel.ScaleTo(1f,0.4f).From(Vector3.zero))   // هم‌زمان با قبلی
    .AppendInterval(0.1f)                              // مکث
    .Append(title.SlideInFromLeft(200f,0.35f))
    .Insert(0.5f, icon.Spin(1f))                       // در زمان دقیق
    .AppendCallback(()=>Debug.Log("mid"))
    .OnComplete(()=>Debug.Log("done"));
```
سراسری: `Tweening.Kill(id)` · `Tweening.KillAll()`.

### ۶.۴ ساخت کلیپ با کد (`UIClip`)
```csharp
using UIToolkit.Animation.Timeline;
UIClip.New("intro")
    .Element("panel").Opacity(0,0).Opacity(0.3f,1).Move(0,new Vector2(0,40)).Move(0.3f,Vector2.zero)
    .Element("title").ScaleXY(0,0.6f).ScaleXY(0.35f,1, Ease.OutBack)
    .Event(0.3f,"intro_done")
    .Loop(LoopType.Restart).Relative(false)
    .Play(root);     // یا .Build() برای گرفتنِ خودِ UIAnimationClip
```
متدهای میان‌بر: `Opacity/MoveX/MoveY/Move/ScaleX/ScaleY/ScaleXY/Rotation/TextColor/BgColor/Width/Height` و `Key(property,time,value,ease)` برای هر خاصیت دلخواه.

### ۶.۵ صدا زدن با نام (`UIAnimation`)
```csharp
UIAnimation.Play("name", root);            // اجرا
UIAnimation.Register("intro", clip);       // ثبت دستی با نام دلخواه
UIAnimation.Get("name");  UIAnimation.Has("name");  UIAnimation.Unregister("name");
```

### ۶.۶ تایم‌لاین صحنه با کد (`UITimeline`)
```csharp
UITimeline.New()
    .Clip(0f, introClip, root)
    .Spawn(0.3f, host, confetti, burst:80)
    .Call(0.3f, ()=>PlaySfx())
    .Add(0.5f, title.FadeIn(0.3f))
    .Loop()                                  // اختیاری
    .Play();
```

### ۶.۷ تریگرها (`UITrigger`)
```csharp
ve.PlayOn(UITrigger.PointerEnter, "Hover");   // یا کلیپ / Action
ve.PlayOnHover("Hover");  ve.PlayOnClick("Click");
ve.PlayOnDoubleClick("Open");  ve.PlayOnHold("Charge", 0.6f);
```
تریگرها: `PointerEnter, PointerLeave, Click, DoubleClick, PointerDown, PointerUp, Hold`.

### ۶.۸ async / coroutine
```csharp
// async/await
await panel.FadeIn();
await title.ScaleTo(1f,0.3f);

// coroutine
yield return panel.FadeIn().WaitForCompletion();
```

### ۶.۹ رویدادها و scrub کلیپ
```csharp
var player = clip.Play(root);
player.OnEvent += name => Debug.Log("event: " + name);   // رویدادهای زمان‌بندی‌شده
player.Seek(0.5f, pause:true);                            // پرش به زمان دلخواه
```

### ۶.۱۰ پارتیکل با کد
`using UIToolkit.Animation.Particles;`
```csharp
host.SpawnParticles(myConfig);                              // پخش پیوسته
button.Burst(myConfig, 50);                                 // یک‌باره
host.SpawnPreset(ParticlePresets.Kind.Sparkle);            // پریست آماده
button.BurstPreset(ParticlePresets.Kind.Confetti, 80);
```
کامپوننت آماده: **UI Toolkit > UI Particle System** (config + نام host).

---

## ۷. Easing‌ها

۳۱ تابع: `Linear` و خانواده‌های `Sine, Quad, Cubic, Quart, Quint, Expo, Circ, Back, Elastic, Bounce` — هرکدام در سه نسخه‌ی `In`, `Out`, `InOut`.
برای منحنی سفارشی: `tween.SetEase(myAnimationCurve)` یا در کی‌فریم گزینه‌ی «Use Custom Curve».

انواع لوپ: `Restart` (از اول)، `Yoyo` (رفت‌وبرگشت)، `Incremental` (تجمعی — مناسب چرخش بی‌پایان).

---

## ۸. خصوصیات قابل انیمیت

| نوع float | نوع رنگ |
|-----------|---------|
| Opacity, TranslateX, TranslateY, ScaleX, ScaleY, Rotate | Color (متن/تینت) |
| Width, Height, Left, Top, Right, Bottom | BackgroundColor |
| MarginLeft/Top/Right/Bottom | BorderColor |
| PaddingLeft/Top/Right/Bottom | TintColor (تینت تصویر پس‌زمینه) |
| BorderRadius, BorderWidth, FontSize | |
| Display (مخفی/نمایش)، Visible | |

`Display` و `Visible` به‌صورت سوییچ کار می‌کنند: مقدار ≥ ۰.۵ یعنی روشن.

برای افزودن خاصیت جدید: یک مقدار به enum `AnimatableProperty` اضافه کن + یک case در `PropertyBinder` (هم Apply هم Read). خودکار در ادیتور ظاهر می‌شود.

---

## ۹. پریست‌های آماده

**انیمیشن (`AnimationPresets.Kind`)**: FadeIn, FadeOut, PopIn, PopOut, SlideInLeft/Right/Top/Bottom, Pulse, Shake, Spin, Wiggle, FloatUpDown.

**پارتیکل (`ParticlePresets.Kind`)**: Confetti, Sparkle, Smoke, Burst, Fireworks, Trail.

از ادیتور با دکمه‌ی Apply، یا از کد:
```csharp
var clip = AnimationPresets.Create(AnimationPresets.Kind.PopIn, "myElement");
var cfg  = ParticlePresets.Create(ParticlePresets.Kind.Confetti);
```

---

## ۱۰. سیستم اعتبارسنجی

قبل از اجرا، مشکلات داده/پیکربندی را بگیر تا به باگ رانتایم نخوری:
- در پنجره: تب **Manage > Validation** → لیست خطاها/هشدارها + **Auto-fix**.
- از منو: **Window > UI Toolkit > Validate Selected Scene Animation** (گزارش در Console).
- از کد: `UIAnimationValidator.ValidateScene(scene, root)` / `ValidateClip(clip)` / `ValidateParticles(cfg)`.

نمونه بررسی‌ها: نام المانِ ناموجود در UXML، تِرَک بدون کی‌فریم، کلیدهای نامرتب، `maxParticles<=0`، نام کلیپ‌های تکراری (که `UIAnimation.Play` را مبهم می‌کند) و…

---

## ۱۱. عیب‌یابی

- **انیمیشن اجرا نمی‌شود / المان پیدا نمی‌شود**: المان باید در UXML **نام** داشته باشد و نامِ تِرَک کلیپ با آن یکی باشد. تب Validation را چک کن.
- **`Setup & Play` کاری نمی‌کند**: باید یک UIDocument با PanelSettings در صحنه باشد که همان UXML را نمایش دهد.
- **مقادیر در فریم اول صفر می‌شوند**: `resolvedStyle` تا بعد از اولین layout آماده نیست. اگر در `OnEnable` انیمیت می‌کنی یا `From()` صریح بده یا یک فریم صبر کن (`root.schedule.Execute(...)`). دایرکتورها این کار را خودکار انجام می‌دهند.
- **`UIAnimation.Play` پیدا نمی‌کند**: کلیپ باید زیر `Resources/UIAnimations` یا در library باشد، یا با `UIAnimation.Register` ثبت شده باشد. نامِ تکراری نداشته باش.
- **پارتیکل دیده نمی‌شود**: host باید اندازه داشته باشد؛ `overflow` خودکار Hidden می‌شود. برای هزاران ذره `meshRenderer` را روشن کن.

---

## ۱۲. محدودیت‌ها

- فقط **UI Toolkit** (نه uGUI/Canvas و نه انیمیشن GameObject سه‌بعدی).
- خصوصیات قابل انیمیت همان لیست بخش ۸ هستند (افزودنی).
- spline سه‌بعدی، sub-emitter پارتیکل، و curve-editor کامل per-track فعلاً نیست.

---

## فایل‌های مرتبط

- `PRO_FEATURES.md` — مرجع قابلیت‌های حرفه‌ای و کل API کد.
- `TIMELINE_GUIDE.md` — جزئیات تایم‌لاین و دایرکتور.
- `PARTICLES_GUIDE.md` — جزئیات پارتیکل.
- `SCENE_ANIMATOR_GUIDE.md` — جزئیات پنجره‌ی واحد.
</content>
# UIAnimatioSystem
