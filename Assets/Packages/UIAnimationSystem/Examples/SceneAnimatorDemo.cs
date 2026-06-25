using UnityEngine;
using UnityEngine.UIElements;
using UIToolkit.Animation;
using UIToolkit.Animation.Timeline;
using UIToolkit.Animation.Particles;

namespace UIToolkit.Animation.Examples
{
    // Drop this on the GameObject that hosts the UI Toolkit panel using
    // DemoMenu.uxml (Panel Renderer on Unity 6.5+, or UIDocument on older).
    // It shows the most common code-API patterns end to end.
    public class SceneAnimatorDemo : MonoBehaviour
    {
        void OnEnable()
        {
            // UIPanel resolves the root for whichever host this Unity version uses.
            UIPanel.WhenReady(gameObject, Run);
        }

        void Run(VisualElement root)
        {
            var panel = root.Q<VisualElement>("panel");
            var title = root.Q<Label>("title");
            var playBtn = root.Q<Button>("playButton");
            var settingsBtn = root.Q<Button>("settingsButton");
            var quitBtn = root.Q<Button>("quitButton");
            var fxHost = root.Q<VisualElement>("fxHost");

            // 1. Intro: build a clip in code and play it on the whole panel + title.
            UIClip.New("intro")
                .Element("panel").Opacity(0f, 0f).Opacity(0.3f, 1f)
                                 .MoveY(0f, 30f).MoveY(0.3f, 0f, Ease.OutCubic)
                .Element("title").ScaleXY(0f, 0.6f).ScaleXY(0.4f, 1f, Ease.OutBack)
                .Play(root);

            // 2. Stagger the buttons in, one after another.
            var buttons = new System.Collections.Generic.List<VisualElement> { playBtn, settingsBtn, quitBtn };
            buttons.StaggerFadeIn(0.3f, 0.06f);

            // 3. Hover/click feedback via triggers.
            if (playBtn != null)
            {
                playBtn.PlayOn(UITrigger.PointerEnter, () => playBtn.PunchScale(0.12f, 0.25f));
                playBtn.PlayOn(UITrigger.Click, () =>
                {
                    if (fxHost != null) fxHost.BurstPreset(ParticlePresets.Kind.Confetti, 60);
                });
            }

            // 4. An idle looping accent.
            title?.Pulse(1.04f, 0.8f);
        }
    }
}
