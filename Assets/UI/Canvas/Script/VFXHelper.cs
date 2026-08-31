using UnityEngine;

/// <summary>
/// ابزار مشترکِ پخشِ پارتیکل — استفاده‌شده توسط UIAnimatedElement, AnimatedPanelController
/// و GameEventEffect. به‌صورت خودکار تشخیص می‌دهد که آیا رفرنسِ داده‌شده یک پارتیکلِ
/// از قبل چیده‌شده در صحنه است یا یک پریفب از پوشه‌ی Project.
///
/// برای پریفب‌ها دو کارِ اضافه هم انجام می‌شود:
/// ۱) به‌جای حدس‌زدنِ زمانِ پایان، واقعاً صبر می‌کند تا کاملاً تمام شود و بعد نابودش می‌کند
///    (پس با هر نوع Curve/Sub-emitter/Delay درست کار می‌کند).
/// ۲) اگر یک RectTransform هدف بدهید، اندازه‌ی پارتیکل را خودکار با اندازه‌ی همان ناحیه
///    تنظیم می‌کند تا از آن بیرون نزند.
/// </summary>
public static class VFXHelper
{
    static bool warnedOnce;

    public static void PlayParticles(ParticleSystem[] particles, Vector3 spawnPosition, RectTransform fitToRect = null)
    {
        if (particles == null) return;

        Vector2? fitSize = fitToRect != null ? (Vector2?)GetWorldSize(fitToRect) : null;

        foreach (var p in particles)
        {
            if (p == null) continue;

            bool isPrefabAsset = !p.gameObject.scene.IsValid();

            if (isPrefabAsset)
            {
                // Rotation اصلیِ پریفب دست‌نخورده می‌ماند (خیلی از پارتیکل‌ها جهتِ فورانشان
                // وابسته به همون Rotationِ طراحی‌شده است)، فقط موقعیت را عوض می‌کنیم.
                var instance = Object.Instantiate(p);
                instance.transform.position = spawnPosition;
                instance.gameObject.SetActive(true);

                if (fitSize.HasValue)
                    FitToSize(instance, fitSize.Value);

                instance.Play(true);

                // به‌جای Destroy با تایمرِ تخمینی، یک کمکِ کوچک اضافه می‌کنیم که واقعاً صبر
                // می‌کند تا سیستم (همراه با فرزندانش) کاملاً تمام شود.
                var autoDestroy = instance.gameObject.AddComponent<ParticleAutoDestroy>();
                autoDestroy.Init(instance);

                if (!warnedOnce)
                {
                    warnedOnce = true;
                    UITween.UITweenDebug.Log(
                        "[VFXHelper] یادآوری یک‌باره: اگر پریفبِ پارتیکل با Canvas از نوع " +
                        "Screen Space - Camera دیده نمی‌شود، Culling Mask دوربین و Sorting Layer " +
                        "رندررِ پارتیکل را چک کنید.");
                }
            }
            else
            {
                p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                p.Play(true);
            }
        }
    }

    /// <summary>مقیاسِ پارتیکل را طوری تنظیم می‌کند که اندازه‌ی طبیعیِ افکت با اندازه‌ی هدف جور دربیاید</summary>
    static void FitToSize(ParticleSystem ps, Vector2 targetSize)
    {
        var main = ps.main;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        float desiredSize = Mathf.Max(targetSize.x, targetSize.y);
        if (desiredSize <= 0f) return;

        // شبیه‌سازیِ کوتاه (بدون این‌که واقعاً روی صفحه دیده بشه) برای گرفتنِ اندازه‌ی طبیعیِ افکت
        float simTime = Mathf.Max(main.duration * 0.5f, main.startLifetime.constant, 0.3f);
        ps.Simulate(simTime, true, true);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        Bounds bounds = renderer != null ? renderer.bounds : new Bounds(ps.transform.position, Vector3.one);

        ps.Clear(true);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        float currentSize = Mathf.Max(bounds.size.x, bounds.size.y, 0.01f);
        float scaleFactor = Mathf.Clamp(desiredSize / currentSize, 0.05f, 20f);
        ps.transform.localScale *= scaleFactor;
    }

    /// <summary>اندازه‌ی واقعیِ یک RectTransform در واحدِ دنیای واقعی (نه پیکسل UI)</summary>
    public static Vector2 GetWorldSize(RectTransform rect)
    {
        if (rect == null) return Vector2.zero;
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        float width = Vector3.Distance(corners[0], corners[3]);
        float height = Vector3.Distance(corners[0], corners[1]);
        return new Vector2(width, height);
    }
}

/// <summary>
/// به‌جای Destroy با تایمرِ حدسی، واقعاً صبر می‌کند تا پارتیکل (و فرزندانش) کاملاً تمام شود.
/// </summary>
internal class ParticleAutoDestroy : MonoBehaviour
{
    ParticleSystem ps;
    public void Init(ParticleSystem system) => ps = system;

    void Update()
    {
        if (ps == null || !ps.IsAlive(true))
            Destroy(gameObject);
    }
}