using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIToolkit.Animation
{
    /// <summary>
    /// Fluent animation API for UI Toolkit VisualElements.
    /// Example: myElement.FadeIn(0.3f).SetEase(Ease.OutBack);
    /// </summary>
    public static class UIAnim
    {
        // ====================================================================
        //  LERP helpers
        // ====================================================================
        static readonly Func<float, float, float, float> LerpFloat = (a, b, t) => a + (b - a) * t;
        static readonly Func<Vector2, Vector2, float, Vector2> LerpV2 = Vector2.LerpUnclamped;
        static readonly Func<Vector3, Vector3, float, Vector3> LerpV3 = Vector3.LerpUnclamped;
        static readonly Func<Color, Color, float, Color> LerpColor = Color.LerpUnclamped;

        static Tween<T> Run<T>(Tween<T> t) { TweenManager.Register(t); return t; }

        // Tag the tween with its owning element so VisualElement.KillTweens() can
        // target only that element's tweens.
        static Tween<T> Run<T>(VisualElement owner, Tween<T> t) { t.SetId(owner); TweenManager.Register(t); return t; }

        // ====================================================================
        //  Generic value tween
        // ====================================================================
        public static Tween<float> ToFloat(Func<float> getter, Action<float> setter, float end, float dur)
            => Run(new Tween<float>(getter, setter, end, dur, LerpFloat));

        // ====================================================================
        //  Opacity / Fade
        // ====================================================================
        public static Tween<float> Fade(this VisualElement ve, float to, float dur)
            => Run(ve, new Tween<float>(
                () => ve.resolvedStyle.opacity,
                v => ve.style.opacity = v,
                to, dur, LerpFloat).SetEase(Ease.OutQuad));

        public static Tween<float> FadeIn(this VisualElement ve, float dur = 0.3f)
        {
            ve.style.display = DisplayStyle.Flex;
            return ve.Fade(1f, dur);
        }

        public static Tween<float> FadeOut(this VisualElement ve, float dur = 0.3f, bool hideAtEnd = true)
        {
            var t = ve.Fade(0f, dur);
            if (hideAtEnd) t.OnComplete(() => ve.style.display = DisplayStyle.None);
            return t;
        }

        // ====================================================================
        //  Scale (transform)
        // ====================================================================
        public static Tween<Vector3> ScaleTo(this VisualElement ve, Vector3 to, float dur)
            => Run(ve, new Tween<Vector3>(
                () => ve.resolvedStyle.scale.value,
                v => ve.style.scale = new Scale(v),
                to, dur, LerpV3).SetEase(Ease.OutBack));

        public static Tween<Vector3> ScaleTo(this VisualElement ve, float uniform, float dur)
            => ve.ScaleTo(new Vector3(uniform, uniform, 1f), dur);

        public static Tween<Vector3> PunchScale(this VisualElement ve, float strength = 0.2f, float dur = 0.3f)
        {
            var baseScale = ve.resolvedStyle.scale.value;
            return Run(ve, new Tween<Vector3>(
                () => baseScale,
                v => ve.style.scale = new Scale(v),
                baseScale + Vector3.one * strength, dur, LerpV3)
                .SetEase(Ease.OutElastic)
                .SetLoops(2, LoopType.Yoyo));
        }

        // ====================================================================
        //  Position / Translate
        // ====================================================================
        public static Tween<Vector3> MoveTo(this VisualElement ve, Vector2 to, float dur)
            => Run(ve, new Tween<Vector3>(
                () => ve.resolvedStyle.translate,
                v => ve.style.translate = new Translate(v.x, v.y, 0f),
                new Vector3(to.x, to.y, 0f), dur, LerpV3).SetEase(Ease.OutCubic));

        public static Tween<Vector3> MoveBy(this VisualElement ve, Vector2 delta, float dur)
        {
            var cur = ve.resolvedStyle.translate;
            return ve.MoveTo(new Vector2(cur.x + delta.x, cur.y + delta.y), dur);
        }

        public static Tween<Vector3> SlideInFromLeft(this VisualElement ve, float distance, float dur = 0.4f)
        {
            ve.style.display = DisplayStyle.Flex;
            ve.style.translate = new Translate(-distance, 0f, 0f);
            return ve.MoveTo(Vector2.zero, dur).SetEase(Ease.OutCubic);
        }

        public static Tween<Vector3> SlideInFromRight(this VisualElement ve, float distance, float dur = 0.4f)
        {
            ve.style.display = DisplayStyle.Flex;
            ve.style.translate = new Translate(distance, 0f, 0f);
            return ve.MoveTo(Vector2.zero, dur).SetEase(Ease.OutCubic);
        }

        public static Tween<Vector3> SlideInFromBottom(this VisualElement ve, float distance, float dur = 0.4f)
        {
            ve.style.display = DisplayStyle.Flex;
            ve.style.translate = new Translate(0f, distance, 0f);
            return ve.MoveTo(Vector2.zero, dur).SetEase(Ease.OutCubic);
        }

        public static Tween<Vector3> Shake(this VisualElement ve, float strength = 10f, float dur = 0.5f, int vibrato = 10)
        {
            var origin = ve.resolvedStyle.translate;
            float seed = UnityEngine.Random.value * 100f;
            return Run(ve, new Tween<Vector3>(
                () => origin,
                v => ve.style.translate = new Translate(v.x, v.y, 0f),
                origin, dur, LerpV3)
                .OnUpdate(t =>
                {
                    float damper = 1f - t;
                    float x = (Mathf.PerlinNoise(seed, t * vibrato) - 0.5f) * 2f * strength * damper;
                    float y = (Mathf.PerlinNoise(seed + 50f, t * vibrato) - 0.5f) * 2f * strength * damper;
                    ve.style.translate = new Translate(origin.x + x, origin.y + y, 0f);
                })
                .OnComplete(() => ve.style.translate = new Translate(origin.x, origin.y, 0f)));
        }

        // ====================================================================
        //  Rotation
        // ====================================================================
        public static Tween<float> RotateTo(this VisualElement ve, float degrees, float dur)
            => Run(ve, new Tween<float>(
                () => ve.resolvedStyle.rotate.angle.value,
                v => ve.style.rotate = new Rotate(new Angle(v, AngleUnit.Degree)),
                degrees, dur, LerpFloat).SetEase(Ease.OutCubic));

        public static Tween<float> Spin(this VisualElement ve, float dur = 1f)
            => ve.RotateTo(360f, dur).SetEase(Ease.Linear).SetLoops(-1, LoopType.Incremental);

        // ====================================================================
        //  Color
        // ====================================================================
        public static Tween<Color> ColorTo(this VisualElement ve, Color to, float dur)
            => Run(ve, new Tween<Color>(
                () => ve.resolvedStyle.color,
                v => ve.style.color = v,
                to, dur, LerpColor));

        public static Tween<Color> BackgroundColorTo(this VisualElement ve, Color to, float dur)
            => Run(ve, new Tween<Color>(
                () => ve.resolvedStyle.backgroundColor,
                v => ve.style.backgroundColor = v,
                to, dur, LerpColor));

        // ====================================================================
        //  Size (width / height) - accordion expand/collapse
        // ====================================================================
        public static Tween<float> WidthTo(this VisualElement ve, float to, float dur)
            => Run(ve, new Tween<float>(
                () => ve.resolvedStyle.width,
                v => ve.style.width = v,
                to, dur, LerpFloat).SetEase(Ease.OutCubic));

        public static Tween<float> HeightTo(this VisualElement ve, float to, float dur)
            => Run(ve, new Tween<float>(
                () => ve.resolvedStyle.height,
                v => ve.style.height = v,
                to, dur, LerpFloat).SetEase(Ease.OutCubic));

        // ====================================================================
        //  Looping idle animations
        // ====================================================================
        public static Tween<Vector3> Pulse(this VisualElement ve, float scale = 1.08f, float dur = 0.5f)
        {
            var baseScale = ve.resolvedStyle.scale.value;
            return Run(ve, new Tween<Vector3>(
                () => baseScale,
                v => ve.style.scale = new Scale(v),
                new Vector3(baseScale.x * scale, baseScale.y * scale, 1f), dur, LerpV3)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo));
        }

        public static Tween<float> Wiggle(this VisualElement ve, float angle = 8f, float dur = 0.2f)
            => Run(ve, new Tween<float>(
                () => ve.resolvedStyle.rotate.angle.value,
                v => ve.style.rotate = new Rotate(new Angle(v, AngleUnit.Degree)),
                angle, dur, LerpFloat)
                .From(-angle)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo));

        public static Tween<Vector3> FloatLoop(this VisualElement ve, float distance = 12f, float dur = 1f)
        {
            var origin = ve.resolvedStyle.translate;
            return Run(ve, new Tween<Vector3>(
                () => origin,
                v => ve.style.translate = new Translate(v.x, v.y, 0f),
                new Vector3(origin.x, origin.y - distance, 0f), dur, LerpV3)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo));
        }

        // ====================================================================
        //  Path motion + pivot
        // ====================================================================
        // Move along a smooth Catmull-Rom spline through the given local points.
        public static Tween<float> MovePath(this VisualElement ve, Vector2[] points, float dur, Ease ease = Ease.Linear)
        {
            return Run(ve, new Tween<float>(
                () => 0f,
                p => { var pos = CatmullRom(points, p); ve.style.translate = new Translate(pos.x, pos.y, 0f); },
                1f, dur, LerpFloat).From(0f).SetEase(ease));
        }

        // Set the transform pivot (0..1) used by scale/rotate.
        public static void SetPivot(this VisualElement ve, float x01, float y01)
            => ve.style.transformOrigin = new TransformOrigin(Length.Percent(x01 * 100f), Length.Percent(y01 * 100f));

        static Vector2 CatmullRom(Vector2[] pts, float t)
        {
            if (pts == null || pts.Length == 0) return Vector2.zero;
            if (pts.Length == 1) return pts[0];
            int seg = pts.Length - 1;
            float scaled = Mathf.Clamp01(t) * seg;
            int i = Mathf.Min((int)scaled, seg - 1);
            float lt = scaled - i;
            Vector2 p0 = pts[Mathf.Max(i - 1, 0)];
            Vector2 p1 = pts[i];
            Vector2 p2 = pts[i + 1];
            Vector2 p3 = pts[Mathf.Min(i + 2, pts.Length - 1)];
            return 0.5f * ((2f * p1) + (-p0 + p2) * lt
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * (lt * lt)
                + (-p0 + 3f * p1 - 3f * p2 + p3) * (lt * lt * lt));
        }

        // ====================================================================
        //  Stagger (animate a list with an incremental delay)
        // ====================================================================
        public static void StaggerFadeIn(this IList<VisualElement> elements, float dur = 0.3f, float interval = 0.05f)
        {
            if (elements == null) return;
            for (int i = 0; i < elements.Count; i++)
                if (elements[i] != null) elements[i].FadeIn(dur).SetDelay(i * interval);
        }

        public static void StaggerScaleIn(this IList<VisualElement> elements, float dur = 0.35f, float interval = 0.05f, Ease ease = Ease.OutBack)
        {
            if (elements == null) return;
            for (int i = 0; i < elements.Count; i++)
            {
                var ve = elements[i];
                if (ve == null) continue;
                ve.style.scale = new Scale(new Vector3(0.6f, 0.6f, 1f));
                ve.style.opacity = 0f;
                ve.ScaleTo(1f, dur).SetEase(ease).SetDelay(i * interval);
                ve.Fade(1f, dur).SetDelay(i * interval);
            }
        }

        public static void StaggerSlideInFromBottom(this IList<VisualElement> elements, float distance = 40f, float dur = 0.4f, float interval = 0.05f)
        {
            if (elements == null) return;
            for (int i = 0; i < elements.Count; i++)
            {
                var ve = elements[i];
                if (ve == null) continue;
                ve.style.opacity = 0f;
                ve.SlideInFromBottom(distance, dur).SetDelay(i * interval);
                ve.Fade(1f, dur).SetDelay(i * interval);
            }
        }

        // ====================================================================
        //  General control
        // ====================================================================
        public static void KillTweens(this VisualElement ve, bool complete = false)
        {
            // Per-element kill: tweens created here use the element as their id.
            TweenManager.Instance?.KillAll(ve, complete);
        }
    }
}
