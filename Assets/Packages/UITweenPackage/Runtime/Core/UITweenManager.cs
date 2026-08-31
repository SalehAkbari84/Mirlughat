using System.Collections.Generic;
using UnityEngine;

namespace UITween
{
    /// <summary>
    /// موتور اصلی پکیج. یک GameObject مخفی و پایدار (DontDestroyOnLoad) به‌صورت خودکار
    /// در اولین استفاده ساخته می‌شود و نیازی به قرار دادن دستی در صحنه نیست.
    ///
    /// سیستم خودتعمیر: اگر به هر دلیلی (Enter Play Mode بدون Domain Reload، یا تعویض صحنه)
    /// چند نمونه ساخته شود، نمونه‌های اضافه خودکار حذف می‌شوند. هم‌چنین در حین بسته‌شدنِ
    /// برنامه (OnApplicationQuit)، دیگر هیچ موتورِ جدیدی ساخته نمی‌شود — این دقیقاً همان
    /// اتفاقی است که پیام "objects were not cleaned up... spawned from OnDestroy" را ایجاد می‌کرد.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [AddComponentMenu("")]
    public sealed class UITweenManager : MonoBehaviour
    {
        static UITweenManager _instance;
        static bool isQuitting;

        /// <summary>
        /// ریست خودکارِ وضعیت‌های استاتیک هر بار که وارد Play Mode می‌شوید — حتی اگر گزینه‌ی
        /// "Reload Domain" در تنظیمات یونیتی خاموش باشد (که باعث می‌شود مقادیر استاتیک بین
        /// اجراهای مختلفِ Play Mode باقی بمانند). بدون این، بعد از اولین Stop، isQuitting برای
        /// همیشه true می‌ماند و موتور دیگر هیچ‌وقت دوباره ساخته نمی‌شود.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticStateOnPlayModeEnter()
        {
            isQuitting = false;
            _instance = null;
        }

        readonly List<Tween> tweens = new List<Tween>(128);

        public static UITweenManager Instance
        {
            get
            {
                if (isQuitting)
                {
                    UITweenDebug.LogWarning("درخواستِ Instance رد شد چون برنامه در حال بسته‌شدن است.");
                    return null;
                }

                if (_instance == null)
                {
                    _instance = FindObjectOfType<UITweenManager>();
                    if (_instance != null)
                        UITweenDebug.Log("یک نمونه‌ی قبلیِ موتور در صحنه پیدا شد و دوباره استفاده شد.");
                }

                if (_instance == null)
                {
                    var go = new GameObject("[UITween Manager]");
                    _instance = go.AddComponent<UITweenManager>();
                    if (Application.isPlaying)
                        DontDestroyOnLoad(go);
                    UITweenDebug.LogSuccess("موتورِ UITween برای اولین بار ساخته شد.");
                }

                return _instance;
            }
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                UITweenDebug.LogWarning($"یک نمونه‌ی تکراری از UITweenManager روی «{gameObject.name}» پیدا شد و حذف شد.");
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        void OnApplicationQuit() => isQuitting = true;

        /// <summary>ثبت و ردیابیِ یک تویین در موتور. استفاده‌ی داخلی توسط متدهای DOxxx.
        /// اگر برنامه در حال بسته‌شدن باشد، بی‌خطر نادیده گرفته می‌شود.</summary>
        public static T Track<T>(T tween) where T : Tween
        {
            var inst = Instance;
            if (inst == null)
            {
                UITweenDebug.LogWarning("یک تویین ثبت نشد چون موتور در دسترس نبود (احتمالاً وسط بسته‌شدن برنامه).");
                return tween;
            }
            inst.tweens.Add(tween);
            UITweenDebug.Log($"تویینِ جدید ثبت شد: {typeof(T).Name} روی «{(tween.safetyTarget != null ? tween.safetyTarget.name : "نامشخص")}» — تعداد فعال الان: {inst.tweens.Count}");
            return tween;
        }

        internal void Register(Tween tween) => tweens.Add(tween);

        /// <summary>خارج‌کردن یک تویین از کنترل مستقل موتور (وقتی قرار است فقط داخل یک Sequence دستی درایو شود)</summary>
        internal void Unregister(Tween tween) => tweens.Remove(tween);

        /// <summary>نسخه‌ی استاتیک Unregister — استفاده‌شده توسط Sequence</summary>
        public static void Untrack(Tween tween) => Instance?.Unregister(tween);

        /// <summary>تعداد تویین‌های فعال (برای دیباگ/پروفایل)</summary>
        public int ActiveTweenCount => tweens.Count;

        void Update()
        {
            for (int i = tweens.Count - 1; i >= 0; i--)
            {
                var tw = tweens[i];

                if (tw.killed || !tw.TargetAlive())
                {
                    tw.ReturnToPool();
                    tweens.RemoveAt(i);
                    continue;
                }

                if (!tw.isPlaying) continue;

                float dt = tw.useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

                if (tw.delayElapsed < tw.delay)
                {
                    tw.delayElapsed += dt;
                    continue;
                }

                if (!tw.started)
                {
                    tw.started = true;
                    tw.onStartCb?.Invoke();
                }

                tw.elapsed += dt;
                float t = tw.duration <= 0f ? 1f : Mathf.Clamp01(tw.elapsed / tw.duration);
                tw.Evaluate(tw.EvaluateEase(t));
                tw.onUpdateCb?.Invoke();

                if (t >= 1f)
                {
                    tw.completedLoops++;
                    bool infinite = tw.loops < 0;
                    bool moreLoops = infinite || tw.completedLoops < tw.loops;

                    if (moreLoops)
                    {
                        tw.elapsed = 0f;
                        if (tw.loopType == LoopType.Yoyo)
                            tw.FlipDirection();
                        tw.onLoopCb?.Invoke();
                    }
                    else
                    {
                        tw.onCompleteCb?.Invoke();
                        tw.ReturnToPool();
                        tweens.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>کشتن همه‌ی تویین‌های وابسته به یک شیء خاص (استفاده‌شده توسط DOKill).
        /// از لیست مستقیم حذف نمی‌کنیم چون ممکن است از داخل یک OnComplete Callback
        /// (یعنی وسط حلقه‌ی Update خودِ موتور) صدا زده شود — فقط علامت killed می‌زنیم
        /// و حذفِ واقعی را به خودِ Update می‌سپاریم.</summary>
        internal void KillAll(Object target, bool complete)
        {
            int matched = 0;
            for (int i = tweens.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(tweens[i].safetyTarget, target))
                {
                    tweens[i].Kill(complete);
                    matched++;
                }
            }

            if (matched == 0)
                UITweenDebug.Log($"DOKill روی «{(target != null ? target.name : "null")}» صدا زده شد ولی هیچ تویینِ فعالی برای آن پیدا نشد.");
            else
                UITweenDebug.Log($"{matched} تویین روی «{target.name}» کشته شد.");
        }

        /// <summary>کشتن مطلق همه‌ی تویین‌های فعال در کل بازی (مثلاً هنگام تعویض صحنه)</summary>
        public static void KillEverything(bool complete = false)
        {
            var inst = Instance;
            if (inst == null) return;
            for (int i = inst.tweens.Count - 1; i >= 0; i--)
                inst.tweens[i].Kill(complete);
        }
    }
}
