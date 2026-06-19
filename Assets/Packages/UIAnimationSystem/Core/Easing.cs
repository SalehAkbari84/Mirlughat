using System;
using UnityEngine;

namespace UIToolkit.Animation
{
    /// <summary>
    /// Complete set of easing functions for animations.
    /// </summary>
    public enum Ease
    {
        Linear,
        InSine, OutSine, InOutSine,
        InQuad, OutQuad, InOutQuad,
        InCubic, OutCubic, InOutCubic,
        InQuart, OutQuart, InOutQuart,
        InQuint, OutQuint, InOutQuint,
        InExpo, OutExpo, InOutExpo,
        InCirc, OutCirc, InOutCirc,
        InBack, OutBack, InOutBack,
        InElastic, OutElastic, InOutElastic,
        InBounce, OutBounce, InOutBounce
    }

    public static class Easing
    {
        const float C1 = 1.70158f;
        const float C2 = C1 * 1.525f;
        const float C3 = C1 + 1f;
        const float C4 = (2f * Mathf.PI) / 3f;
        const float C5 = (2f * Mathf.PI) / 4.5f;

        /// <summary>Evaluate eased value for t in range [0,1].</summary>
        public static float Evaluate(Ease ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case Ease.Linear: return t;

                case Ease.InSine: return 1f - Mathf.Cos((t * Mathf.PI) / 2f);
                case Ease.OutSine: return Mathf.Sin((t * Mathf.PI) / 2f);
                case Ease.InOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;

                case Ease.InQuad: return t * t;
                case Ease.OutQuad: return 1f - (1f - t) * (1f - t);
                case Ease.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

                case Ease.InCubic: return t * t * t;
                case Ease.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);
                case Ease.InOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

                case Ease.InQuart: return t * t * t * t;
                case Ease.OutQuart: return 1f - Mathf.Pow(1f - t, 4f);
                case Ease.InOutQuart: return t < 0.5f ? 8f * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 4f) / 2f;

                case Ease.InQuint: return t * t * t * t * t;
                case Ease.OutQuint: return 1f - Mathf.Pow(1f - t, 5f);
                case Ease.InOutQuint: return t < 0.5f ? 16f * t * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 5f) / 2f;

                case Ease.InExpo: return t == 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f);
                case Ease.OutExpo: return t == 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
                case Ease.InOutExpo:
                    if (t == 0f) return 0f;
                    if (t == 1f) return 1f;
                    return t < 0.5f
                        ? Mathf.Pow(2f, 20f * t - 10f) / 2f
                        : (2f - Mathf.Pow(2f, -20f * t + 10f)) / 2f;

                case Ease.InCirc: return 1f - Mathf.Sqrt(1f - Mathf.Pow(t, 2f));
                case Ease.OutCirc: return Mathf.Sqrt(1f - Mathf.Pow(t - 1f, 2f));
                case Ease.InOutCirc:
                    return t < 0.5f
                        ? (1f - Mathf.Sqrt(1f - Mathf.Pow(2f * t, 2f))) / 2f
                        : (Mathf.Sqrt(1f - Mathf.Pow(-2f * t + 2f, 2f)) + 1f) / 2f;

                case Ease.InBack: return C3 * t * t * t - C1 * t * t;
                case Ease.OutBack: return 1f + C3 * Mathf.Pow(t - 1f, 3f) + C1 * Mathf.Pow(t - 1f, 2f);
                case Ease.InOutBack:
                    return t < 0.5f
                        ? (Mathf.Pow(2f * t, 2f) * ((C2 + 1f) * 2f * t - C2)) / 2f
                        : (Mathf.Pow(2f * t - 2f, 2f) * ((C2 + 1f) * (t * 2f - 2f) + C2) + 2f) / 2f;

                case Ease.InElastic:
                    if (t == 0f) return 0f;
                    if (t == 1f) return 1f;
                    return -Mathf.Pow(2f, 10f * t - 10f) * Mathf.Sin((t * 10f - 10.75f) * C4);
                case Ease.OutElastic:
                    if (t == 0f) return 0f;
                    if (t == 1f) return 1f;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * C4) + 1f;
                case Ease.InOutElastic:
                    if (t == 0f) return 0f;
                    if (t == 1f) return 1f;
                    return t < 0.5f
                        ? -(Mathf.Pow(2f, 20f * t - 10f) * Mathf.Sin((20f * t - 11.125f) * C5)) / 2f
                        : (Mathf.Pow(2f, -20f * t + 10f) * Mathf.Sin((20f * t - 11.125f) * C5)) / 2f + 1f;

                case Ease.InBounce: return 1f - OutBounce(1f - t);
                case Ease.OutBounce: return OutBounce(t);
                case Ease.InOutBounce:
                    return t < 0.5f
                        ? (1f - OutBounce(1f - 2f * t)) / 2f
                        : (1f + OutBounce(2f * t - 1f)) / 2f;

                default: return t;
            }
        }

        static float OutBounce(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
            if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }

        /// <summary>Custom AnimationCurve support.</summary>
        public static float Evaluate(AnimationCurve curve, float t) => curve?.Evaluate(t) ?? t;
    }
}
