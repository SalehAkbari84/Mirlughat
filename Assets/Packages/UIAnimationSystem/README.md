# سیستم انیمیشن و پارتیکل UI Toolkit — راهنمای کامل

یک ابزار کامل برای ساخت انیمیشن و افکت روی رابط‌های **UI Toolkit** یونیتی (`VisualElement` / UXML / USS):
موتور انیمیشن + ویرایشگر بصریِ تک‌پنجره‌ای + سیستم پارتیکل + یک API کامل برای کار با کد. بدون هیچ کتابخانه‌ی خارجی.

> این تنها فایلِ مستندات است. همه‌چیز این‌جاست. اگر تازه‌کاری، بخش ۱ تا ۶ را به‌ترتیب بخوان؛ اگر برنامه‌نویسی، بخش ۷ مرجع کد است.

---

## فهرست

1. [نصب و نیازمندی](#۱-نصب-و-نیازمندی)
2. [مفاهیم پایه](#۲-مفاهیم-پایه)
3. [شروع سریع](#۳-شروع-سریع)
4. [پنجره‌ی Scene Animator — خط‌به‌خط](#۴-پنجرهی-scene-animator)
5. [Play Order (ترتیب پخش)](#۵-play-order)
6. [Interaction Triggers (رویدادها)](#۶-interaction-triggers)
7. [مرجع کامل API کد](#۷-مرجع-کامل-api-کد)
8. [پارتیکل](#۸-پارتیکل)
9. [Easing و لوپ](#۹-easing-و-لوپ)
10. [خصوصیات قابل انیمیت](#۱۰-خصوصیات-قابل-انیمیت)
11. [پریست‌های آماده](#۱۱-پریستهای-آماده)
12. [اعتبارسنجی (Validation)](#۱۲-اعتبارسنجی)
13. [عیب‌یابی و سؤالات متداول](#۱۳-عیبیابی)
14. [محدودیت‌ها](#۱۴-محدودیتها)
15. [تاریخچه‌ی نسخه](#۱۵-تاریخچهی-نسخه)

---

## ۱. نصب و نیازمندی

1. پوشه‌ی `UIAnimationSystem` را داخل `Assets` پروژه‌ات کپی کن.
2. تمام. `TweenManager` در زمان اجرا خودش را خودکار می‌سازد؛ هیچ setup دیگری لازم نیست.
3. **Unity 6** (6000.x) با پکیج UI Toolkit (پیش‌فرض نصب است). برای اجرای تست‌ها پکیج **Test Framework** لازم است.
4. دو اسمبلی: `UIToolkit.Animation` (runtime) و `UIToolkit.Animation.Editor` (editor).
5. **سازگاریِ خودکارِ نسخه:** میزبانِ پنلِ زمان اجرا خودکار انتخاب می‌شود — روی **Unity 6.5+** از **Panel Renderer** (چون UIDocument منسوخ شده) و روی نسخه‌های قدیمی‌تر از **UIDocument**. این کار با لایه‌ی `UIPanel` و سوییچِ کامپایلِ `UNITY_6000_5_OR_NEWER` انجام می‌شود؛ تو کاری لازم نیست بکنی. (بخشِ طراحی/ادیتور اصلاً به این وابسته نیست.)

---

## ۲. مفاهیم پایه

- **Tween**: تغییر نرمِ یک مقدار در طول زمان (مثلاً opacity از ۰ به ۱ در ۰.۳ ثانیه).
- **Clip (`UIAnimationClip`)**: یک انیمیشنِ کامل و قابل‌ذخیره که **چند المان را هم‌زمان** انیمیت می‌کند. هر کلیپ = لیست `ElementTrack` (هر المان با نام)؛ هر ElementTrack = لیست `PropertyTrack` (هر خاصیت با کی‌فریم‌ها).
- **اتصال با نام (bind by name)**: کلیپ‌ها به المان‌ها **با نام** وصل می‌شوند، نه ارجاع مستقیم. هر المانی که می‌خواهی انیمیت شود **باید در UXML نام داشته باشد**. هنگام پخش با `root.Q<VisualElement>("نام")` پیدا می‌شود.
- **Scene (`UISceneAnimation`)**: asset مرکزی که یک UXML را به Play Order، Interaction Triggers و پارتیکل‌ها وصل می‌کند — بدون دست‌زدن به UXML.
- **Play Order**: تنها لیستِ ترتیب‌دارِ پخشِ کلیپ‌ها (اجرای خودکار موقع شروع).
- **Interaction Triggers**: پخشِ کلیپ هنگام رویداد (کلیک/هاور/نگه‌داشتن/…) — رویدادمحور، جدا از Play Order.

> نکته‌ی طلایی: **برای انیمیت‌شدن، المان باید در UXML نام داشته باشد.** (در UI Builder فیلد Name را پر کن.)

---

## ۳. شروع سریع

**با کد:**
```csharp
using UIToolkit.Animation;

myElement.FadeIn(0.3f);
myButton.ScaleTo(1.2f, 0.25f).SetEase(Ease.OutBack);
panel.SlideInFromLeft(300f, 0.5f);
spinner.Spin(1f);
icon.Pulse();
```

**بصری (بدون کد):** بخش ۴ را دنبال کن.

---

## ۴. پنجره‌ی Scene Animator

روش بدون کد. همه‌چیز در **یک پنجره**.

### گام ۱ — ساخت asset صحنه
Project → راست‌کلیک → **Create > UI Toolkit > Scene Animation** (مثلاً `MainMenuScene`).

### گام ۲ — باز کردن پنجره
**Window > UI Toolkit > Scene Animator** (یا دابل‌کلیک روی asset صحنه/کلیپ).
در toolbar فیلد **Scene** را بده، و در تب **Setup** فایل **UXML** را وصل کن. حالا preview زنده را می‌بینی.

### نقشه‌ی پنجره
```
Toolbar (wrap-شونده): Scene | Reload | New | AutoFit | Zoom | Frame | Reset | Preview Scene | > Setup & Play
بالا:  Hierarchy | Preview | تب‌ها (Setup / Animate / Effects / Manage)
پایین: TIMELINE  (داکِ تمام‌عرض)
```

### Toolbar (نوار بالا) — دکمه‌به‌دکمه
- **Scene**: انتخاب asset صحنه.
- **Reload** (آیکون refresh): بازسازی preview از UXML.
- **New** (آیکون +): ساخت صحنه‌ی جدید.
- **Auto Fit**: خودکار کل layout را در پنل جا و وسط می‌چیند (پیش‌فرض روشن).
- **Zoom**: اسلایدر بزرگ‌نمایی (با چرخ موس هم کنترل می‌شود).
- **Frame**: قاب‌گرفتن المان انتخاب‌شده (کلید `F`).
- **Reset View**: برگشت به نمای کامل.
- **Preview Scene**: پخش زنده‌ی کل صحنه (Play Order + پارتیکل) داخل ادیتور، بدون Play Mode.
- **> Setup & Play** (سبز): خودکار `UISceneDirector` را به host پنلِ صحنه وصل و Play Mode را روشن می‌کند. **host به‌صورت خودکار تشخیص داده می‌شود:** Panel Renderer روی Unity 6.5+ و UIDocument روی نسخه‌های قدیمی‌تر.

### Hierarchy (چپ)
- درختِ تاشو از **همه‌ی** المان‌های UXML + کادر **جستجو**.
- نقطه‌ی آبی = نام‌دار، خاکستری = بی‌نام (قابل انیمیت نیست).
- نشانِ **A** = در کلیپ فعال انیمیشن دارد، نشانِ **P** = پارتیکل دارد.
- **کلیک** = انتخاب، **دابل‌کلیک** = Frame روی preview.

### Preview (وسط) و ناوبری
- **چرخ موس** = زوم به‌سمت نشانگر.
- **درگ دکمه‌ی وسط موس** = جابه‌جایی (pan).
- کلیک روی هر المان = انتخابش (کادر زرد دورش).

### تب Setup
- **UXML Layout**، **Preview Size**، **Preview Background**، و **Additional Style Sheets** (افزودن/حذف).

### تب Animate — خط‌به‌خط
1. **CLIP**: انتخاب کلیپ یا **New** (خودکار در `Assets/Resources/UIAnimations` ذخیره می‌شود).
2. **Add to Play Order**: کلیپ فعال را به‌عنوان یک step به Play Order اضافه می‌کند (تنها جای زمان‌بندیِ پخش).
3. **ELEMENT INSPECTOR** (وقتی المانِ نام‌دار انتخاب است): تِرَک‌های آن المان با **مقدار زنده** + دکمه‌ی **Key** (ثبت مقدار فعلی در playhead) + حذف tِرَک + **+ Property**.
4. **Presets** (فولد): انتخاب از ۴۷ انیمیشن آماده + **Apply** (می‌سازد، ذخیره و به Play Order اضافه می‌کند).
5. **Record & shortcuts** (فولد): تاگل **Record** — المان انتخاب‌شده را در preview بکش تا کلیدهای موقعیت خودکار ساخته شوند.
6. **Clip options & events** (فولد): **Relative** (افزودن روی pose فعلی)، **Playback Speed**، و **Events** (رویدادهای زمان‌بندی‌شده). توجه: تکرار این‌جا نیست؛ در Manage تنظیم می‌شود.

### تایم‌لاین (داکِ پایین) — خط‌به‌خط
- transport: **|<** شروع، **Play/Pause**، **>|** پایان، فیلد زمان، snap، zoom، **+ Element**.
- روی تِرَک **دابل‌کلیک** = ساخت کی‌فریم. الماسِ کی‌فریم را بکش تا جابه‌جا شود؛ راست‌کلیک = حذف.
- **چند-انتخاب**: `Ctrl/Shift + کلیک`؛ **box-select**: درگ روی فضای خالی؛ **درگ گروهی**.
- **copy/paste/duplicate**: `Ctrl+C / V / D`.
- دکمه‌ی **E** کنار هر تِرَک = ease کل آن تِرَک.
- اینسپکتورِ کی‌فریم: زمان، مقدار، ease/curve + **نمودار کوچک ease**.
- در حالت **چند-انتخاب**، یک کنترلِ **Ease/Curve برای همه‌ی انتخاب‌شده‌ها** ظاهر می‌شود.

### میان‌برهای کیبورد
`Space` پخش · `K` snapshot · `Del` حذف · `←/→` scrub · `Shift+←/→` جابه‌جایی کی‌فریم · `F` frame · `Ctrl+C/V/D` کپی/پیست/تکثیر.

### تب Effects (طراح پارتیکل)
بخش ۸ را ببین.

### تب Manage
چهار فولد: **Library** · **Play Order** (بخش ۵) · **Interaction Triggers** (بخش ۶) · **Validation** (بخش ۱۲).
- **Library**: همه‌ی کلیپ‌ها و پارتیکل‌های پروژه؛ جستجو، **Edit**، **Copy Call** (کپیِ کدِ صدا زدن)، **Ping**، تغییر نام درجا، حذف (به سطل بازیافت).

---

## ۵. Play Order

**تنها لیستِ ترتیب‌دارِ پخشِ کلیپ‌ها.** هر مرحله (step):
- **Clip**، **Repeat** (`1`=یک‌بار، `0`=بی‌نهایت، `N`=N بار)، **Repeat Type** (Restart/Yoyo/Incremental)، **Delay before**، **Play together with previous (parallel)** (روشن = هم‌زمان با مرحله‌ی قبلی اجرا می‌شود؛ خاموش = بعد از آن)، **Root element** (اختیاری).
- مراحلِ موازی با تورفتگی و خطِ کناریِ آبی زیر مرحله‌ی قبل **گروه‌بندیِ بصری** می‌شوند و با نشانِ `+` مشخص‌اند. می‌توانی چند مرحله را پشت‌سرهم «together» کنی تا همه با هم اجرا شوند.
- **پارتیکل هم در همین ترتیب پشتیبانی می‌شود**: هر step می‌تواند یک **Particle** (config + Host + Burst) داشته باشد که در زمانِ شروعِ همان step شلیک می‌شود — کنار کلیپ یا به‌تنهایی. دکمه‌های **+ Clip Step** و **+ Particle Step**. (`Burst = 0` یعنی پیوسته، `N>0` یعنی یک‌باره.)
- دکمه‌های جابه‌جایی (^ ▾) و حذف.
- **`Play sequence on start`**: اگر روشن باشد، موقع شروعِ صحنه خودکار اجرا می‌شود (پیش‌فرض روشن).

نکات:
- یک مرحله با **Repeat = 0 (بی‌نهایت) پایانی است**؛ مراحلِ بعد از آن خودکار **غیرفعال/خاکستری** می‌شوند (چون هرگز پخش نمی‌شوند).
- برای پخش on-demand (هر وقت خواستی از کد) از `UIAnimation.Play` استفاده کن (بخش ۷)، نه Play Order.

---

## ۶. Interaction Triggers

پخشِ یک کلیپ هنگام **رویداد روی یک المان**. جدا از Play Order (رویدادمحور). تنظیم در **Manage → Interaction Triggers**.

هر trigger:
- **Element**: کدام المان.
- **On Event**: `Click`، `DoubleClick`، `Hold` (نگه‌داشتن/long-press)، `PointerDown`، `PointerUp`، `PointerEnter` (هاور)، `PointerLeave`.
- **Play Clip**: کدام کلیپ.
- **Platform**: `Both` / `DesktopOnly` / `MobileOnly` — برای جداسازیِ موبایل/دسکتاپ (هاور را Desktop بگذار).
- **Repeat** + **Repeat Type**.
- **Hold (s)**: فقط برای ایونت Hold.
- **ضد-spam**:
  - **Ignore while playing** (پیش‌فرض روشن): تا انیمیشن تمام نشده، رویدادِ دوباره **نادیده** گرفته می‌شود.
  - اگر خاموش: رویدادِ دوباره انیمیشن را **restart** می‌کند (باز هم تک‌instance، بدون انباشت).
  - **Cooldown (s)**: حداقل فاصله بین دو اجرا.

موبایل: رویدادهای Pointer روی موس و لمس کار می‌کنند؛ در زمان اجرا با `Application.isMobilePlatform` فیلتر پلتفرم اعمال می‌شود. `Hold` همان long-press موبایل است.

از کد:
```csharp
button.PlayOnClick("ButtonPress");                 // ضد-spam خودکار
icon.PlayOn(UITrigger.Hold, "Charge", holdSeconds: 0.6f);
card.PlayOnHover("CardPop");
```

---

## ۷. مرجع کامل API کد

### ۷.۱ افکت‌های آماده (روی هر `VisualElement`) — `using UIToolkit.Animation;`
| دسته | متدها |
|------|-------|
| Fade | `Fade(to,dur)` · `FadeIn(dur)` · `FadeOut(dur)` |
| Scale | `ScaleTo(uniform یا Vector3, dur)` · `PunchScale(strength,dur)` |
| Move | `MoveTo` · `MoveBy` · `SlideInFromLeft/Right/Bottom` · `Shake` · `MovePath(points,dur)` |
| Rotate | `RotateTo(deg,dur)` · `Spin(dur)` |
| Color | `ColorTo` · `BackgroundColorTo` |
| Size | `WidthTo` · `HeightTo` |
| لوپ | `Pulse(scale,dur)` · `Wiggle(angle,dur)` · `FloatLoop(distance,dur)` |
| ابزار | `SetPivot(x,y)` · `KillTweens()` |
| Stagger (روی `IList<VisualElement>`) | `StaggerFadeIn` · `StaggerScaleIn` · `StaggerSlideInFromBottom` |

### ۷.۲ زنجیره‌ی Tween
```csharp
myElement.MoveTo(target, 0.5f)
    .From(startPos).SetEase(Ease.InOutCubic).SetDelay(0.2f)
    .SetLoops(3, LoopType.Yoyo)          // -1 = بی‌نهایت
    .SetId("intro").SetIgnoreTimeScale()
    .OnStart(()=>{}).OnUpdate(t=>{}).OnStepComplete(()=>{}).OnComplete(()=>{});
```
کنترل: `Pause()` · `Play()` · `Restart()` · `Kill()` · `Kill(complete:true)`.

### ۷.۳ Sequence
```csharp
Tweening.Sequence()
    .Append(panel.FadeIn(0.4f))
    .Join(panel.ScaleTo(1f,0.4f).From(Vector3.zero))
    .AppendInterval(0.1f)
    .Append(title.SlideInFromLeft(200f,0.35f))
    .Insert(0.5f, icon.Spin(1f))
    .AppendCallback(()=>Debug.Log("mid"))
    .OnComplete(()=>Debug.Log("done"));
```
سراسری: `Tweening.Kill(id)` · `Tweening.KillAll()`.

### ۷.۴ ساخت کلیپ با کد (`UIClip`)
```csharp
using UIToolkit.Animation.Timeline;
UIClip.New("intro")
    .Element("panel").Opacity(0,0).Opacity(0.3f,1).Move(0,new Vector2(0,40)).Move(0.3f,Vector2.zero)
    .Element("title").ScaleXY(0,0.6f).ScaleXY(0.35f,1, Ease.OutBack)
    .Event(0.3f,"done").Speed(1f).Relative(false)
    .Play(root);     // یا .Build()
```
متدها: `Opacity/MoveX/MoveY/Move/ScaleX/ScaleY/ScaleXY/Rotation/TextColor/BgColor/Width/Height` و `Key(property,time,value,ease)`.

### ۷.۵ صدا زدن با نام (`UIAnimation`)
```csharp
UIAnimation.Play("WinPanel", root);     // پخش هر وقت خواستی (on-demand)
UIAnimation.Register("intro", clip);    // ثبت دستی
UIAnimation.Get("name"); UIAnimation.Has("name"); UIAnimation.Unregister("name");
```
کلیپ‌ها زیر `Resources/UIAnimations` و در `UIAnimationLibrary.asset` ثبت می‌شوند → با نام قابل اجرا.

### ۷.۶ تایم‌لاین صحنه با کد (`UITimeline`)
```csharp
UITimeline.New()
    .Clip(0f, introClip, root)
    .Spawn(0.3f, host, confetti, burst:80)
    .Call(0.3f, ()=>PlaySfx())
    .Add(0.5f, title.FadeIn(0.3f))
    .Play();
```

### ۷.۷ تریگرها (`UITrigger`) — با ضد-spam
```csharp
ve.PlayOn(UITrigger.PointerEnter, "Hover");
ve.PlayOnClick("Click");  ve.PlayOnDoubleClick("Open");  ve.PlayOnHold("Charge", 0.6f);
// کنترل کامل:
ve.PlayOnClip(UITrigger.Click, clip, loops:1, LoopType.Restart,
              ignoreWhilePlaying:true, cooldown:0.2f, holdSeconds:0.5f);
```
overloadِ `PlayOn(..., Action, ...)` هر کدِ دلخواه را اجرا می‌کند (بدون ضد-spam؛ خودت کنترل کن).

### ۷.۸ async / coroutine
```csharp
await panel.FadeIn();                                   // async/await
yield return panel.FadeIn().WaitForCompletion();        // coroutine
```

### ۷.۹ رویداد و scrub
```csharp
var player = clip.Play(root);
player.OnEvent += name => Debug.Log("event: " + name);
player.Seek(0.5f, pause:true);
```

### ۷.۱۰ پارتیکل با کد — `using UIToolkit.Animation.Particles;`
```csharp
host.SpawnParticles(myConfig);
button.Burst(myConfig, 50);
host.SpawnPreset(ParticlePresets.Kind.Sparkle);
button.BurstPreset(ParticlePresets.Kind.Confetti, 80);
```

### ۷.۱۱ اجرا با دایرکتور
```csharp
var dir = GetComponent<UISceneDirector>();
dir.PlaySequence();              // پخش Play Order
dir.PlayParticles("id");         // پخش یک پارتیکلِ bindشده
```

---

## ۸. پارتیکل

موتورِ pooled و ماژولار. ذرات یا VisualElement واقعی‌اند یا برای تعداد بالا با **یک mesh (Painter2D)** رسم می‌شوند.

### طراح پارتیکل (تب Effects)
- **New / انتخاب config / Create from preset**.
- **پیش‌نمایش زنده** با Play/Restart/Stop (ذرات از مرکزِ بوم پخش می‌شوند).
- ویرایش کاملِ خواص با Inspector تعبیه‌شده.
- **Bind to host**: اتصال به المان انتخاب‌شده.

### ParticleSystemConfig
- **Emission**: `emissionRate`, `maxParticles`, `loop`, `duration`, `warmup`, `bursts`.
- **Visual**: `visualKind` (Circle/Square/Image/CustomClass), `sprite`, `customUssClass`, `additiveHint`.
- **Mesh/Trail**: `meshRenderer` (هزاران ذره)، `trail` + `trailLength` + `trailWidthScale`.
- **Modules** (افزودن/حذف/ترتیب): Shape, Velocity, Lifetime, Size, Color, Gravity, Rotation, **Attractor**, **Noise/Turbulence**.

### ماژول جدید
```csharp
[System.Serializable]
public class MyModule : ParticleModule
{
    public override void OnUpdate(Particle p, in ParticleContext ctx) { /* ... */ }
    public override string DisplayName => "My Module";
}
```
خودکار در منوی Add Module ظاهر می‌شود.

---

## ۹. Easing و لوپ

۳۱ تابع: `Linear` و خانواده‌های `Sine, Quad, Cubic, Quart, Quint, Expo, Circ, Back, Elastic, Bounce` — هرکدام `In/Out/InOut`. منحنی سفارشی: `SetEase(AnimationCurve)` یا «Use Custom Curve» در کی‌فریم.

لوپ: `Restart` (از اول)، `Yoyo` (رفت‌وبرگشت)، `Incremental` (تجمعی). تمام مسیرهای پخش از **carry-overshoot** استفاده می‌کنند → لوپِ صاف بدون تیک/drift.

---

## ۱۰. خصوصیات قابل انیمیت

| float | رنگ |
|-------|-----|
| Opacity, TranslateX/Y, ScaleX/Y, Rotate | Color |
| Width, Height, Left, Top, Right, Bottom | BackgroundColor |
| MarginLeft/Top/Right/Bottom | BorderColor |
| PaddingLeft/Top/Right/Bottom | TintColor |
| BorderRadius, BorderWidth, FontSize | |
| Display (مخفی/نمایش), Visible | |

`Display`/`Visible` سوییچی‌اند (مقدار ≥ ۰.۵ = روشن). افزودن خاصیت جدید: یک مقدار به `AnimatableProperty` + یک case در `PropertyBinder` (Apply و Read).

---

## ۱۱. پریست‌های آماده

**انیمیشن (`AnimationPresets.Kind`) — ۴۷ مورد:**
- ورود: FadeIn, FadeInUp/Down/Left/Right, PopIn, ZoomIn, BounceIn, BackIn, RotateIn, RollIn, FlipInX/Y, SlideInLeft/Right/Top/Bottom.
- خروج: FadeOut(+۴ جهت), PopOut, ZoomOut, BounceOut, BackOut, RotateOut, FlipOutX/Y, SlideOutLeft/Right/Top/Bottom.
- جلب‌توجه: Pulse, HeartBeat, Flash, Glow, Shake, Wiggle, Swing, HeadShake, Tada, RubberBand, Jello, Bounce, Spin, FloatUpDown.

**پارتیکل (`ParticlePresets.Kind`):** Confetti, Sparkle, Smoke, Burst, Fireworks, Trail.

```csharp
var clip = AnimationPresets.Create(AnimationPresets.Kind.PopIn, "myButton");
var cfg  = ParticlePresets.Create(ParticlePresets.Kind.Confetti);
```

---

## ۱۲. اعتبارسنجی

قبل از اجرا مشکلات را بگیر: تب **Manage → Validation** (با **Auto-fix**)، یا منوی **Window > UI Toolkit > Validate Selected Scene Animation**، یا از کد `UIAnimationValidator.ValidateScene/ValidateClip/ValidateParticles`.
نمونه: المانِ ناموجود در UXML، تِرَک بدون کی‌فریم، کلیدهای نامرتب، `maxParticles<=0`، نام کلیپ‌های تکراری، trigger بدون کلیپ.

---

## ۱۳. عیب‌یابی

- **اجرا نمی‌شود / المان پیدا نمی‌شود**: المان باید در UXML نام داشته باشد و نامِ track کلیپ با آن یکی باشد. Validation را چک کن.
- **`Setup & Play` کاری نمی‌کند**: باید یک host پنل با PanelSettings در صحنه باشد که UXML را نمایش دهد — **Panel Renderer** در Unity 6.5+ یا **UIDocument** در نسخه‌های قدیمی‌تر. سیستم خودکار هرکدام موجود باشد را پیدا می‌کند.
- **مقادیر در فریم اول صفر**: `resolvedStyle` تا بعد از اولین layout آماده نیست؛ یا `From()` بده یا یک فریم صبر کن. دایرکتورها خودکار صبر می‌کنند.
- **`UIAnimation.Play` پیدا نمی‌کند**: کلیپ باید زیر `Resources/UIAnimations`/library باشد یا با `Register` ثبت شود؛ نام تکراری نداشته باش.
- **پارتیکل دیده نمی‌شود**: host باید اندازه داشته باشد؛ برای تعداد زیاد `meshRenderer` را روشن کن.
- **کلیک‌های پشت‌سرهم انیمیشن را spam می‌کنند**: در trigger گزینه‌ی **Ignore while playing** را روشن نگه‌دار یا **Cooldown** بگذار.

---

## ۱۴. محدودیت‌ها

- فقط **UI Toolkit** (نه uGUI/Canvas، نه انیمیشن GameObject سه‌بعدی).
- خصوصیات قابل انیمیت همان لیست بخش ۱۰ (افزودنی).
- spline سه‌بعدی، sub-emitter پارتیکل، و curve-editor کاملِ per-track فعلاً نیست.

---

## ۱۵. تاریخچه‌ی نسخه

### 1.0.0
- موتور tween/sequence با ۳۱ easing، لوپ Restart/Yoyo/Incremental، async/await و coroutine.
- کلیپ‌های چند-المانی، حالت Relative، رویدادهای زمان‌بندی‌شده، `Seek`، `UIClip` builder، `UITimeline`.
- صدا زدن با نام (`UIAnimation.Play`) از طریق library/Resources.
- ۴۷ پریست انیمیشن، ۶ پریست پارتیکل.
- موتور پارتیکلِ pooled + رندر mesh + trail + ماژول‌های Attractor/Noise.
- پنجره‌ی واحد (Setup/Animate/Effects/Manage)، preview با زوم/پن/auto-fit، hierarchy تاشو با badge، اینسپکتورِ المان، تایم‌لاینِ داک با multi/box-select و copy/paste و ease graph، Record و میان‌برها، طراح پارتیکل، Play Order، Interaction Triggers با ضد-spam و تفکیک موبایل، Validation با auto-fix، Setup & Play و Preview Scene، UI آیکونی و toolbar ریسپانسیو.
- تست‌های edit-mode برای موتور و validator.

---

لایسنس: MIT — فایل `LICENSE`.
