using UnityEngine;
using UnityEngine.UIElements;
using UIToolkit.Animation;
using UIToolkit.Animation.Timeline;

// Example usage. Attach to the GameObject that hosts the UI Toolkit panel
// (Panel Renderer on Unity 6.5+, or UIDocument on older versions).
public class AnimationDemo : MonoBehaviour
{
    [Tooltip("Optional clip to play via the timeline system.")]
    public UIAnimationClip clip;

    VisualElement _root;
    VisualElement _panel;
    Button _button;
    Label _title;

    void OnEnable()
    {
        UIPanel.WhenReady(gameObject, root =>
        {
            _root = root;
            _panel = _root.Q<VisualElement>("panel");
            _button = _root.Q<Button>("playButton");
            _title = _root.Q<Label>("title");

            if (_button != null)
                _button.clicked += PlayShowcase;
        });
    }

    // Example 1: simple one-liners using the fluent API.
    void ExampleSimple()
    {
        _panel.FadeIn(0.3f).SetEase(Ease.OutBack);
        _button.ScaleTo(1.1f, 0.2f).SetEase(Ease.OutElastic);
    }

    // Example 2: a multi-step sequence.
    void PlayShowcase()
    {
        var seq = Tweening.Sequence();

        seq.Append(_panel.FadeIn(0.4f).SetEase(Ease.OutQuad))
           .Join(_panel.ScaleTo(1f, 0.4f).From(new Vector3(0.7f, 0.7f, 1f)).SetEase(Ease.OutBack))
           .AppendInterval(0.1f)
           .Append(_title.SlideInFromLeft(200f, 0.35f))
           .AppendCallback(() => Debug.Log("Title shown"))
           .Append(_button.PunchScale(0.25f, 0.5f))
           .OnComplete(() => Debug.Log("Sequence complete"));
    }

    // Example 3: play a timeline clip (multi-element) via code.
    void PlayClipExample()
    {
        if (clip != null)
            clip.Play(_root); // named element tracks are resolved under _root
    }

    // Example 4: stagger a list of children.
    void ExampleStaggerList(VisualElement list)
    {
        var seq = Tweening.Sequence();
        int i = 0;
        foreach (var child in list.Children())
        {
            child.style.opacity = 0f;
            seq.Insert(i * 0.07f, child.FadeIn(0.3f).SetEase(Ease.OutQuad));
            seq.Insert(i * 0.07f, child.MoveTo(Vector2.zero, 0.3f).From(new Vector3(0, 20, 0)).SetEase(Ease.OutCubic));
            i++;
        }
    }
}
