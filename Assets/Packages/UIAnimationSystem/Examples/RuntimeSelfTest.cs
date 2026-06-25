using UnityEngine;
using UnityEngine.UIElements;
using UIToolkit.Animation;            // FadeIn / Pulse extensions
using UIToolkit.Animation.Timeline;   // UIPanel

// TEMPORARY DIAGNOSTIC.
// Put this on the SAME GameObject that hosts your UI (the one with a
// Panel Renderer on Unity 6.5+, or a UIDocument on older versions).
// Press Play and read the Console. The logs tell us exactly where it breaks.
public class RuntimeSelfTest : MonoBehaviour
{
    void OnEnable()
    {
        Debug.Log($"[SelfTest] OnEnable on '{name}'. HasHost(this GameObject) = {UIPanel.HasHost(gameObject)}");

        UIPanel.WhenReady(gameObject, root =>
        {
            if (root == null)
            {
                Debug.LogError("[SelfTest] Host found but root is NULL.");
                return;
            }
            Debug.Log($"[SelfTest] ROOT READY: name='{root.name}', childCount={root.childCount}. " +
                      "Running an obvious FadeIn + Pulse on the whole UI now.");

            // Unmistakable: the whole UI fades in and gently pulses.
            root.FadeIn(0.6f);
            root.Pulse(1.06f, 0.7f);
        });
    }
}
