using System.Text;
using UnityEngine;

namespace UIToolkit.Animation.Timeline
{
    // A large library of ready-made animation clips for common UI motions.
    // Build one for a named element and either play it or save it as an asset.
    public static class AnimationPresets
    {
        public enum Kind
        {
            // ---- Entrances ----
            FadeIn, FadeInUp, FadeInDown, FadeInLeft, FadeInRight,
            PopIn, ZoomIn, BounceIn, BackIn, RotateIn, RollIn,
            FlipInX, FlipInY,
            SlideInLeft, SlideInRight, SlideInTop, SlideInBottom,
            // ---- Exits ----
            FadeOut, FadeOutUp, FadeOutDown, FadeOutLeft, FadeOutRight,
            PopOut, ZoomOut, BounceOut, BackOut, RotateOut,
            FlipOutX, FlipOutY,
            SlideOutLeft, SlideOutRight, SlideOutTop, SlideOutBottom,
            // ---- Attention / idle ----
            Pulse, HeartBeat, Flash, Glow,
            Shake, Wiggle, Swing, HeadShake,
            Tada, RubberBand, Jello, Bounce,
            Spin, FloatUpDown
        }

        // "FadeInUp" -> "Fade In Up"
        public static string DisplayName(Kind k)
        {
            string s = k.ToString();
            var sb = new StringBuilder(s.Length + 4);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(s[i - 1])) sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }

        public static System.Array AllKinds => System.Enum.GetValues(typeof(Kind));

        public static UIAnimationClip Create(Kind kind, string elementName)
        {
            var clip = ScriptableObject.CreateInstance<UIAnimationClip>();
            clip.name = kind.ToString() + "_" + elementName;
            var el = clip.GetOrCreateElement(elementName);

            switch (kind)
            {
                // ---------------- Entrances ----------------
                case Kind.FadeIn: Fade(el, 0f, 1f); break;
                case Kind.FadeInUp: Fade(el, 0f, 1f); TY(el, 30f, 0f); break;
                case Kind.FadeInDown: Fade(el, 0f, 1f); TY(el, -30f, 0f); break;
                case Kind.FadeInLeft: Fade(el, 0f, 1f); TX(el, -40f, 0f); break;
                case Kind.FadeInRight: Fade(el, 0f, 1f); TX(el, 40f, 0f); break;
                case Kind.PopIn: Fade(el, 0f, 1f, 0.15f); Sc(el, 0.6f, 1f, 0.35f, Ease.OutBack); break;
                case Kind.ZoomIn: Fade(el, 0f, 1f); Sc(el, 0.2f, 1f, 0.35f, Ease.OutCubic); break;
                case Kind.BounceIn:
                    Fade(el, 0f, 1f, 0.2f);
                    K(el, AnimatableProperty.ScaleX, (0f, 0.3f, Ease.OutQuad), (0.5f, 1f, Ease.OutBounce));
                    K(el, AnimatableProperty.ScaleY, (0f, 0.3f, Ease.OutQuad), (0.5f, 1f, Ease.OutBounce));
                    break;
                case Kind.BackIn: Fade(el, 0f, 1f); TX(el, -200f, 0f, 0.5f, Ease.OutBack); break;
                case Kind.RotateIn: Fade(el, 0f, 1f); Rot(el, -180f, 0f, 0.5f, Ease.OutCubic); Sc(el, 0.5f, 1f, 0.5f, Ease.OutCubic); break;
                case Kind.RollIn: Fade(el, 0f, 1f); TX(el, -120f, 0f, 0.5f); Rot(el, -120f, 0f, 0.5f); break;
                case Kind.FlipInX: Fade(el, 0f, 1f); K(el, AnimatableProperty.ScaleY, (0f, 0f, Ease.OutBack), (0.4f, 1f, Ease.OutBack)); break;
                case Kind.FlipInY: Fade(el, 0f, 1f); K(el, AnimatableProperty.ScaleX, (0f, 0f, Ease.OutBack), (0.4f, 1f, Ease.OutBack)); break;
                case Kind.SlideInLeft: Fade(el, 0f, 1f); TX(el, -120f, 0f); break;
                case Kind.SlideInRight: Fade(el, 0f, 1f); TX(el, 120f, 0f); break;
                case Kind.SlideInTop: Fade(el, 0f, 1f); TY(el, -120f, 0f); break;
                case Kind.SlideInBottom: Fade(el, 0f, 1f); TY(el, 120f, 0f); break;

                // ---------------- Exits ----------------
                case Kind.FadeOut: Fade(el, 1f, 0f, 0.3f, Ease.InQuad); break;
                case Kind.FadeOutUp: Fade(el, 1f, 0f, 0.3f, Ease.InQuad); TY(el, 0f, -30f, 0.4f, Ease.InCubic); break;
                case Kind.FadeOutDown: Fade(el, 1f, 0f, 0.3f, Ease.InQuad); TY(el, 0f, 30f, 0.4f, Ease.InCubic); break;
                case Kind.FadeOutLeft: Fade(el, 1f, 0f, 0.3f, Ease.InQuad); TX(el, 0f, -40f, 0.4f, Ease.InCubic); break;
                case Kind.FadeOutRight: Fade(el, 1f, 0f, 0.3f, Ease.InQuad); TX(el, 0f, 40f, 0.4f, Ease.InCubic); break;
                case Kind.PopOut: Fade(el, 1f, 0f, 0.25f, Ease.InQuad); Sc(el, 1f, 0.6f, 0.25f, Ease.InBack); break;
                case Kind.ZoomOut: Fade(el, 1f, 0f); Sc(el, 1f, 0.2f, 0.3f, Ease.InCubic); break;
                case Kind.BounceOut:
                    Fade(el, 1f, 0f, 0.4f);
                    K(el, AnimatableProperty.ScaleX, (0f, 1f, Ease.OutQuad), (0.2f, 1.1f, Ease.OutQuad), (0.5f, 0.3f, Ease.InBack));
                    K(el, AnimatableProperty.ScaleY, (0f, 1f, Ease.OutQuad), (0.2f, 1.1f, Ease.OutQuad), (0.5f, 0.3f, Ease.InBack));
                    break;
                case Kind.BackOut: Fade(el, 1f, 0f, 0.4f); TX(el, 0f, 200f, 0.4f, Ease.InBack); break;
                case Kind.RotateOut: Fade(el, 1f, 0f, 0.4f); Rot(el, 0f, 180f, 0.5f, Ease.InCubic); Sc(el, 1f, 0.5f, 0.5f, Ease.InCubic); break;
                case Kind.FlipOutX: Fade(el, 1f, 0f); K(el, AnimatableProperty.ScaleY, (0f, 1f, Ease.InBack), (0.3f, 0f, Ease.InBack)); break;
                case Kind.FlipOutY: Fade(el, 1f, 0f); K(el, AnimatableProperty.ScaleX, (0f, 1f, Ease.InBack), (0.3f, 0f, Ease.InBack)); break;
                case Kind.SlideOutLeft: Fade(el, 1f, 0f, 0.3f, Ease.InQuad); TX(el, 0f, -120f, 0.4f, Ease.InCubic); break;
                case Kind.SlideOutRight: Fade(el, 1f, 0f, 0.3f, Ease.InQuad); TX(el, 0f, 120f, 0.4f, Ease.InCubic); break;
                case Kind.SlideOutTop: Fade(el, 1f, 0f, 0.3f, Ease.InQuad); TY(el, 0f, -120f, 0.4f, Ease.InCubic); break;
                case Kind.SlideOutBottom: Fade(el, 1f, 0f, 0.3f, Ease.InQuad); TY(el, 0f, 120f, 0.4f, Ease.InCubic); break;

                // ---------------- Attention / idle ----------------
                case Kind.Pulse:
                    K(el, AnimatableProperty.ScaleX, (0f, 1f, Ease.InOutSine), (0.4f, 1.08f, Ease.InOutSine), (0.8f, 1f, Ease.InOutSine));
                    K(el, AnimatableProperty.ScaleY, (0f, 1f, Ease.InOutSine), (0.4f, 1.08f, Ease.InOutSine), (0.8f, 1f, Ease.InOutSine));
                    SetLoop(clip); break;
                case Kind.HeartBeat:
                    K(el, AnimatableProperty.ScaleX, (0f, 1f, Ease.OutQuad), (0.14f, 1.2f, Ease.OutQuad), (0.28f, 1f, Ease.OutQuad), (0.42f, 1.2f, Ease.OutQuad), (0.7f, 1f, Ease.OutQuad), (1.3f, 1f, Ease.Linear));
                    K(el, AnimatableProperty.ScaleY, (0f, 1f, Ease.OutQuad), (0.14f, 1.2f, Ease.OutQuad), (0.28f, 1f, Ease.OutQuad), (0.42f, 1.2f, Ease.OutQuad), (0.7f, 1f, Ease.OutQuad), (1.3f, 1f, Ease.Linear));
                    SetLoop(clip); break;
                case Kind.Flash:
                    K(el, AnimatableProperty.Opacity, (0f, 1f, Ease.Linear), (0.25f, 0f, Ease.Linear), (0.5f, 1f, Ease.Linear), (0.75f, 0f, Ease.Linear), (1f, 1f, Ease.Linear));
                    SetLoop(clip); break;
                case Kind.Glow:
                    K(el, AnimatableProperty.Opacity, (0f, 1f, Ease.InOutSine), (0.7f, 0.55f, Ease.InOutSine), (1.4f, 1f, Ease.InOutSine));
                    SetLoop(clip); break;
                case Kind.Shake:
                    K(el, AnimatableProperty.TranslateX, (0f, 0f, Ease.Linear), (0.05f, 9f, Ease.Linear), (0.1f, -8f, Ease.Linear), (0.15f, 6f, Ease.Linear), (0.2f, -4f, Ease.Linear), (0.25f, 2f, Ease.Linear), (0.3f, 0f, Ease.Linear));
                    break;
                case Kind.Wiggle:
                    K(el, AnimatableProperty.Rotate, (0f, 0f, Ease.InOutSine), (0.2f, 8f, Ease.InOutSine), (0.6f, -8f, Ease.InOutSine), (0.8f, 0f, Ease.InOutSine));
                    SetLoop(clip); break;
                case Kind.Swing:
                    K(el, AnimatableProperty.Rotate, (0f, 0f, Ease.InOutSine), (0.15f, 15f, Ease.InOutSine), (0.3f, -10f, Ease.InOutSine), (0.45f, 5f, Ease.InOutSine), (0.6f, -3f, Ease.InOutSine), (0.7f, 0f, Ease.InOutSine));
                    break;
                case Kind.HeadShake:
                    K(el, AnimatableProperty.TranslateX, (0f, 0f, Ease.InOutSine), (0.1f, -8f, Ease.InOutSine), (0.25f, 7f, Ease.InOutSine), (0.4f, -5f, Ease.InOutSine), (0.55f, 3f, Ease.InOutSine), (0.7f, 0f, Ease.InOutSine));
                    break;
                case Kind.Tada:
                    K(el, AnimatableProperty.ScaleX, (0f, 1f, Ease.OutQuad), (0.1f, 0.9f, Ease.OutQuad), (0.3f, 1.1f, Ease.OutQuad), (0.7f, 1.1f, Ease.OutQuad), (1f, 1f, Ease.OutQuad));
                    K(el, AnimatableProperty.ScaleY, (0f, 1f, Ease.OutQuad), (0.1f, 0.9f, Ease.OutQuad), (0.3f, 1.1f, Ease.OutQuad), (0.7f, 1.1f, Ease.OutQuad), (1f, 1f, Ease.OutQuad));
                    K(el, AnimatableProperty.Rotate, (0f, 0f, Ease.InOutSine), (0.2f, -6f, Ease.InOutSine), (0.4f, 6f, Ease.InOutSine), (0.6f, -6f, Ease.InOutSine), (0.8f, 6f, Ease.InOutSine), (1f, 0f, Ease.InOutSine));
                    break;
                case Kind.RubberBand:
                    K(el, AnimatableProperty.ScaleX, (0f, 1f, Ease.OutQuad), (0.3f, 1.25f, Ease.OutQuad), (0.4f, 0.75f, Ease.OutQuad), (0.5f, 1.15f, Ease.OutQuad), (0.65f, 0.95f, Ease.OutQuad), (0.75f, 1.05f, Ease.OutQuad), (1f, 1f, Ease.OutQuad));
                    K(el, AnimatableProperty.ScaleY, (0f, 1f, Ease.OutQuad), (0.3f, 0.75f, Ease.OutQuad), (0.4f, 1.25f, Ease.OutQuad), (0.5f, 0.85f, Ease.OutQuad), (0.65f, 1.05f, Ease.OutQuad), (0.75f, 0.95f, Ease.OutQuad), (1f, 1f, Ease.OutQuad));
                    break;
                case Kind.Jello:
                    K(el, AnimatableProperty.Rotate, (0f, 0f, Ease.InOutSine), (0.2f, -3f, Ease.InOutSine), (0.4f, 3f, Ease.InOutSine), (0.6f, -2f, Ease.InOutSine), (0.8f, 1f, Ease.InOutSine), (1f, 0f, Ease.InOutSine));
                    K(el, AnimatableProperty.ScaleX, (0f, 1f, Ease.InOutSine), (0.2f, 1.05f, Ease.InOutSine), (0.5f, 0.95f, Ease.InOutSine), (0.8f, 1.02f, Ease.InOutSine), (1f, 1f, Ease.InOutSine));
                    break;
                case Kind.Bounce:
                    K(el, AnimatableProperty.TranslateY, (0f, 0f, Ease.OutQuad), (0.2f, -30f, Ease.OutQuad), (0.4f, 0f, Ease.InQuad), (0.55f, -15f, Ease.OutQuad), (0.7f, 0f, Ease.InQuad), (0.8f, -6f, Ease.OutQuad), (0.9f, 0f, Ease.InQuad));
                    break;
                case Kind.Spin:
                    K(el, AnimatableProperty.Rotate, (0f, 0f, Ease.Linear), (1f, 360f, Ease.Linear));
                    SetLoop(clip); break;
                case Kind.FloatUpDown:
                    K(el, AnimatableProperty.TranslateY, (0f, 0f, Ease.InOutSine), (1f, -12f, Ease.InOutSine), (2f, 0f, Ease.InOutSine));
                    SetLoop(clip); break;
            }

            return clip;
        }

        // ---------------- helpers ----------------
        static void SetLoop(UIAnimationClip clip, LoopType type = LoopType.Restart)
        {
            clip.loop = true; clip.loops = 0; clip.loopType = type;
        }

        static void Fade(ElementTrack el, float a, float b, float dur = 0.3f, Ease e = Ease.OutQuad)
            => K(el, AnimatableProperty.Opacity, (0f, a, e), (dur, b, e));

        static void TX(ElementTrack el, float a, float b, float dur = 0.4f, Ease e = Ease.OutCubic)
            => K(el, AnimatableProperty.TranslateX, (0f, a, e), (dur, b, e));

        static void TY(ElementTrack el, float a, float b, float dur = 0.4f, Ease e = Ease.OutCubic)
            => K(el, AnimatableProperty.TranslateY, (0f, a, e), (dur, b, e));

        static void Rot(ElementTrack el, float a, float b, float dur = 0.4f, Ease e = Ease.OutCubic)
            => K(el, AnimatableProperty.Rotate, (0f, a, e), (dur, b, e));

        static void Sc(ElementTrack el, float a, float b, float dur = 0.4f, Ease e = Ease.OutBack)
        {
            K(el, AnimatableProperty.ScaleX, (0f, a, e), (dur, b, e));
            K(el, AnimatableProperty.ScaleY, (0f, a, e), (dur, b, e));
        }

        static void K(ElementTrack el, AnimatableProperty p, params (float t, float v, Ease e)[] keys)
        {
            var track = el.GetOrCreate(p);
            foreach (var k in keys)
                track.keys.Add(new UIKeyframe(k.t, k.v, k.e));
            track.SortKeys();
        }
    }
}
