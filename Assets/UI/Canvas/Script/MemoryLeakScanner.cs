using System.Collections.Generic;
using UnityEngine;
using UITween;

/// <summary>
/// اسکنرِ عمومیِ نشتِ حافظه — نیازی نیست حدس بزنید مشکل از کجاست. این اسکریپت هر چند
/// ثانیه یک‌بار، تمامِ GameObjectهای فعالِ صحنه رو می‌شماره (گروه‌بندی‌شده بر اساسِ اسمِ
/// پایه، طوری که "Particle(Clone)", "Particle(Clone)(1)", "Particle(Clone)(2)" همه یک
/// گروه حساب بشن) و اگه یه گروه بینِ دو اسکن به‌طرزِ مشکوکی رشد کرد، دقیقاً می‌گه کدومه.
///
/// این رو روی همون آبجکتِ PerformanceHUD (یا هر آبجکتِ دیگه‌ای) بذارید و اجرا کنید.
/// </summary>
public class MemoryLeakScanner : MonoBehaviour
{
    [Header("فاصله‌ی زمانیِ هر اسکن (ثانیه)")]
    [SerializeField] private float scanInterval = 5f;

    [Header("اگه یه گروه بینِ دو اسکن بیشتر از این‌همه رشد کرد، هشدار بده")]
    [SerializeField] private int growthWarningThreshold = 5;

    Dictionary<string, int> lastCounts = new Dictionary<string, int>();
    float timer;
    bool firstScanDone;

    void Update()
    {
        timer += Time.unscaledDeltaTime;
        if (timer < scanInterval) return;
        timer = 0f;
        Scan();
    }

    void Scan()
    {
        var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        var counts = new Dictionary<string, int>();

        foreach (var go in allObjects)
        {
            string key = NormalizeName(go.name);
            counts.TryGetValue(key, out int c);
            counts[key] = c + 1;
        }

        int activeTweens = UITweenManager.Instance != null ? UITweenManager.Instance.ActiveTweenCount : 0;
        int particleCount = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Length;
        long gcMem = System.GC.GetTotalMemory(false);

        UITweenDebug.Log(
            $"[MemoryLeakScanner] اسکن — کلِ GameObject: {allObjects.Length} | " +
            $"تویینِ فعال: {activeTweens} | پارتیکلِ فعال: {particleCount} | " +
            $"GC Mem: {gcMem / 1024f / 1024f:F1}MB");

        if (firstScanDone)
        {
            bool foundAny = false;
            foreach (var kv in counts)
            {
                lastCounts.TryGetValue(kv.Key, out int prev);
                int growth = kv.Value - prev;
                if (growth >= growthWarningThreshold)
                {
                    foundAny = true;
                    UITweenDebug.LogError(
                        $"[MemoryLeakScanner] 🔴 مظنونِ نشت: گروهِ «{kv.Key}» از {prev} به {kv.Value} رسید " +
                        $"(+{growth} در {scanInterval} ثانیه). این یعنی یه‌جا داره از این آبجکت مدام Instantiate " +
                        "می‌شه بدون این‌که Destroy بشه.");
                }
            }

            if (!foundAny)
                UITweenDebug.Log("[MemoryLeakScanner] ✓ هیچ گروهی رشدِ مشکوکی نداشت این دور.");
        }

        lastCounts = counts;
        firstScanDone = true;
    }

    /// <summary>«Something(Clone)», «Something(Clone)(1)», «Something (5)» و... همه یک گروه حساب می‌شوند</summary>
    string NormalizeName(string rawName)
    {
        int idx = rawName.IndexOf('(');
        return idx > 0 ? rawName.Substring(0, idx).Trim() : rawName;
    }
}
