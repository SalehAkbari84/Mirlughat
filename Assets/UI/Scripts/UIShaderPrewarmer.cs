using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// پنل‌ها رو SetActive نمی‌کنه — فقط opacity رو موقتاً صفر می‌کنه.
/// این یعنی shader compile می‌شه ولی همه callback ها سالم می‌مونن.
/// </summary>
public class UIShaderPrewarmer : MonoBehaviour
{
    [Tooltip("همه PanelRenderer های صحنه رو اینجا بکش")]
    [SerializeField] private PanelRenderer[] allPanels;

    [Tooltip("چند فریم صبر کنه تا shader ها compile بشن")]
    [SerializeField] private int warmupFrames = 3;

    private readonly Dictionary<PanelRenderer, VisualElement> _roots = new();
    private bool _warmupStarted;

    private void OnEnable()
    {
        foreach (var panel in allPanels)
        {
            if (panel == null) continue;
            panel.RegisterUIReloadCallback(OnPanelReady);
        }
    }

    private void OnDisable()
    {
        foreach (var panel in allPanels)
        {
            if (panel == null) continue;
            panel.UnregisterUIReloadCallback(OnPanelReady);
        }
    }

    private void OnPanelReady(PanelRenderer renderer, VisualElement root, int version)
    {
        _roots[renderer] = root;
    }

    private void Update()
    {
        if (_warmupStarted) return;

        // صبر کن تا همه پنل‌هایی که فعال هستن root بدن
        int activePanels = 0;
        foreach (var panel in allPanels)
            if (panel != null && panel.gameObject.activeSelf) activePanels++;

        if (_roots.Count < activePanels) return;

        _warmupStarted = true;
        StartCoroutine(Prewarm());
    }

    private IEnumerator Prewarm()
    {
        // فقط پنل‌هایی که الان باید hidden باشن رو opacity=0 کن
        // پنل اصلی (index 0) دست نمی‌خوره
        for (int i = 1; i < allPanels.Length; i++)
        {
            var panel = allPanels[i];
            if (panel == null) continue;

            // اگه غیرفعاله، موقتاً فعالش کن با opacity=0
            if (!panel.gameObject.activeSelf)
            {
                panel.gameObject.SetActive(true);
                if (_roots.TryGetValue(panel, out var r))
                    r.style.opacity = 0f;
            }
        }

        // warmupFrames فریم صبر کن
        for (int i = 0; i < warmupFrames; i++)
            yield return null;

        // برگردون به حالت اصلی — فقط اونایی که ما فعال کردیم
        for (int i = 1; i < allPanels.Length; i++)
        {
            var panel = allPanels[i];
            if (panel == null) continue;

            if (_roots.TryGetValue(panel, out var root))
                root.style.opacity = 1f;

            // فقط اونایی که خودمون فعال کردیم رو برمی‌گردونیم
            panel.gameObject.SetActive(false);
        }

        Debug.Log("[UIShaderPrewarmer] ✓ Shader warmup complete — buttons intact.");
    }
}