using UnityEngine;
using UnityEngine.UIElements;

namespace UIToolkit.Animation.Timeline
{
    // Fluent, code-first builder for multi-element keyframe clips. Build a clip
    // entirely in code, then Play it (or Build it into an asset).
    //
    //   UIClip.New("intro")
    //       .Element("panel")
    //           .Opacity(0f, 0f).Opacity(0.3f, 1f)
    //           .Move(0f, new Vector2(0, 40)).Move(0.3f, Vector2.zero, Ease.OutCubic)
    //       .Element("title")
    //           .ScaleXY(0f, 0.6f).ScaleXY(0.35f, 1f, Ease.OutBack)
    //       .Event(0.3f, "intro_done")
    //       .Play(root);
    public sealed class UIClip
    {
        readonly UIAnimationClip _clip;
        ElementTrack _cur;

        UIClip(UIAnimationClip clip) { _clip = clip; }

        // Start a new in-memory clip.
        public static UIClip New(string name = "clip")
        {
            var c = ScriptableObject.CreateInstance<UIAnimationClip>();
            c.name = name;
            return new UIClip(c);
        }

        // Keep building on an existing clip asset.
        public static UIClip Edit(UIAnimationClip clip) => new UIClip(clip);

        // ----- target -----
        public UIClip Element(string elementName)
        {
            _cur = _clip.GetOrCreateElement(elementName);
            return this;
        }

        // ----- generic keys -----
        public UIClip Key(AnimatableProperty p, float time, float value, Ease ease = Ease.OutQuad)
        {
            EnsureElement();
            var t = _cur.GetOrCreate(p);
            t.keys.Add(new UIKeyframe(time, value, ease));
            t.SortKeys();
            return this;
        }

        public UIClip Key(AnimatableProperty p, float time, Color value, Ease ease = Ease.OutQuad)
        {
            EnsureElement();
            var t = _cur.GetOrCreate(p);
            t.keys.Add(new UIKeyframe(time, value, ease));
            t.SortKeys();
            return this;
        }

        // ----- convenience keys -----
        public UIClip Opacity(float time, float v, Ease e = Ease.OutQuad) => Key(AnimatableProperty.Opacity, time, v, e);
        public UIClip MoveX(float time, float x, Ease e = Ease.OutCubic) => Key(AnimatableProperty.TranslateX, time, x, e);
        public UIClip MoveY(float time, float y, Ease e = Ease.OutCubic) => Key(AnimatableProperty.TranslateY, time, y, e);
        public UIClip Move(float time, Vector2 pos, Ease e = Ease.OutCubic)
        {
            Key(AnimatableProperty.TranslateX, time, pos.x, e);
            return Key(AnimatableProperty.TranslateY, time, pos.y, e);
        }
        public UIClip ScaleX(float time, float s, Ease e = Ease.OutBack) => Key(AnimatableProperty.ScaleX, time, s, e);
        public UIClip ScaleY(float time, float s, Ease e = Ease.OutBack) => Key(AnimatableProperty.ScaleY, time, s, e);
        public UIClip ScaleXY(float time, float s, Ease e = Ease.OutBack)
        {
            Key(AnimatableProperty.ScaleX, time, s, e);
            return Key(AnimatableProperty.ScaleY, time, s, e);
        }
        public UIClip Rotation(float time, float degrees, Ease e = Ease.OutCubic) => Key(AnimatableProperty.Rotate, time, degrees, e);
        public UIClip TextColor(float time, Color c, Ease e = Ease.OutQuad) => Key(AnimatableProperty.Color, time, c, e);
        public UIClip BgColor(float time, Color c, Ease e = Ease.OutQuad) => Key(AnimatableProperty.BackgroundColor, time, c, e);
        public UIClip Width(float time, float w, Ease e = Ease.OutCubic) => Key(AnimatableProperty.Width, time, w, e);
        public UIClip Height(float time, float h, Ease e = Ease.OutCubic) => Key(AnimatableProperty.Height, time, h, e);

        // ----- clip-level options -----
        public UIClip Loop(LoopType type = LoopType.Restart, int loops = 0) { _clip.loop = true; _clip.loopType = type; _clip.loops = loops; return this; }
        public UIClip Speed(float s) { _clip.playbackSpeed = Mathf.Max(0.01f, s); return this; }
        public UIClip Relative(bool on = true) { _clip.relative = on; return this; }
        public UIClip IgnoreTimeScale(bool on = true) { _clip.ignoreTimeScale = on; return this; }
        public UIClip Event(float time, string name) { _clip.events.Add(new ClipEvent { time = time, name = name }); return this; }

        // ----- output -----
        public UIAnimationClip Build() => _clip;
        public ClipPlayer Play(VisualElement root) => _clip.Play(root);

        void EnsureElement()
        {
            if (_cur == null) _cur = _clip.GetOrCreateElement("element");
        }
    }
}
