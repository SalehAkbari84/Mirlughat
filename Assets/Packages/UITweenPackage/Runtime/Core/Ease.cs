using UnityEngine;

namespace UITween
{
    /// <summary>انواع منحنی‌های شتاب‌دهی (Easing) قابل استفاده در تویین‌ها</summary>
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

    /// <summary>محاسبه‌ی مقدار Easing بر اساس فرمول‌های استاندارد Robert Penner</summary>
    internal static class Easing
    {
        const float PI = Mathf.PI;

        public static float Evaluate(Ease ease, float t)
        {
            switch (ease)
            {
                case Ease.Linear: return t;

                case Ease.InSine: return 1f - Mathf.Cos(t * PI / 2f);
                case Ease.OutSine: return Mathf.Sin(t * PI / 2f);
                case Ease.InOutSine: return -(Mathf.Cos(PI * t) - 1f) / 2f;

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

                case Ease.InExpo: return t <= 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f);
                case Ease.OutExpo: return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
                case Ease.InOutExpo:
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return t < 0.5f ? Mathf.Pow(2f, 20f * t - 10f) / 2f : (2f - Mathf.Pow(2f, -20f * t + 10f)) / 2f;

                case Ease.InCirc: return 1f - Mathf.Sqrt(1f - Mathf.Pow(t, 2f));
                case Ease.OutCirc: return Mathf.Sqrt(1f - Mathf.Pow(t - 1f, 2f));
                case Ease.InOutCirc:
                    return t < 0.5f
                        ? (1f - Mathf.Sqrt(1f - Mathf.Pow(2f * t, 2f))) / 2f
                        : (Mathf.Sqrt(1f - Mathf.Pow(-2f * t + 2f, 2f)) + 1f) / 2f;

                case Ease.InBack:
                {
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    return c3 * t * t * t - c1 * t * t;
                }
                case Ease.OutBack:
                {
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
                }
                case Ease.InOutBack:
                {
                    const float c1 = 1.70158f;
                    const float c2 = c1 * 1.525f;
                    return t < 0.5f
                        ? (Mathf.Pow(2f * t, 2f) * ((c2 + 1f) * 2f * t - c2)) / 2f
                        : (Mathf.Pow(2f * t - 2f, 2f) * ((c2 + 1f) * (t * 2f - 2f) + c2) + 2f) / 2f;
                }

                case Ease.InElastic:
                {
                    const float c4 = (2f * PI) / 3f;
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return -Mathf.Pow(2f, 10f * t - 10f) * Mathf.Sin((t * 10f - 10.75f) * c4);
                }
                case Ease.OutElastic:
                {
                    const float c4 = (2f * PI) / 3f;
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
                }
                case Ease.InOutElastic:
                {
                    const float c5 = (2f * PI) / 4.5f;
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return t < 0.5f
                        ? -(Mathf.Pow(2f, 20f * t - 10f) * Mathf.Sin((20f * t - 11.125f) * c5)) / 2f
                        : (Mathf.Pow(2f, -20f * t + 10f) * Mathf.Sin((20f * t - 11.125f) * c5)) / 2f + 1f;
                }

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
            t -= 2.625f / d1; return n1 * t * t + 0.984375f;
        }
    }
}
