using System;
using UnityEngine;

namespace UITween
{
    /// <summary>افکت لرزش (Shake) روی مقادیر Vector3 — بر پایه‌ی نویز پرلین، دامنه‌اش با گذر زمان کم می‌شود</summary>
    internal sealed class ShakeTween3D : Tween
    {
        Func<Vector3> getter;
        Action<Vector3> setter;
        Vector3 basePos;
        bool baseCaptured;
        float strength;
        float freq;
        Vector3 seed;

        public ShakeTween3D(UnityEngine.Object target, Func<Vector3> getter, Action<Vector3> setter, float dur, float strength, int vibrato)
        {
            safetyTarget = target;
            this.getter = getter;
            this.setter = setter;
            duration = Mathf.Max(0.01f, dur);
            this.strength = strength;
            freq = Mathf.Max(1, vibrato) * 10f;
            seed = new Vector3(UnityEngine.Random.value * 100f, UnityEngine.Random.value * 100f, UnityEngine.Random.value * 100f);
            ease = Ease.Linear;
        }

        internal override void Evaluate(float t)
        {
            if (!baseCaptured) { basePos = getter(); baseCaptured = true; }

            if (t >= 1f) { setter(basePos); return; }

            float decay = 1f - t;
            float nx = (Mathf.PerlinNoise(seed.x, t * freq) * 2f - 1f);
            float ny = (Mathf.PerlinNoise(seed.y, t * freq) * 2f - 1f);
            float nz = (Mathf.PerlinNoise(seed.z, t * freq) * 2f - 1f);
            setter(basePos + new Vector3(nx, ny, nz) * strength * decay);
        }
    }

    /// <summary>افکت لرزش (Shake) روی مقادیر Vector2 — مخصوص RectTransform.anchoredPosition</summary>
    internal sealed class ShakeTween2D : Tween
    {
        Func<Vector2> getter;
        Action<Vector2> setter;
        Vector2 basePos;
        bool baseCaptured;
        float strength;
        float freq;
        Vector2 seed;

        public ShakeTween2D(UnityEngine.Object target, Func<Vector2> getter, Action<Vector2> setter, float dur, float strength, int vibrato)
        {
            safetyTarget = target;
            this.getter = getter;
            this.setter = setter;
            duration = Mathf.Max(0.01f, dur);
            this.strength = strength;
            freq = Mathf.Max(1, vibrato) * 10f;
            seed = new Vector2(UnityEngine.Random.value * 100f, UnityEngine.Random.value * 100f);
            ease = Ease.Linear;
        }

        internal override void Evaluate(float t)
        {
            if (!baseCaptured) { basePos = getter(); baseCaptured = true; }

            if (t >= 1f) { setter(basePos); return; }

            float decay = 1f - t;
            float nx = (Mathf.PerlinNoise(seed.x, t * freq) * 2f - 1f);
            float ny = (Mathf.PerlinNoise(seed.y, t * freq) * 2f - 1f);
            setter(basePos + new Vector2(nx, ny) * strength * decay);
        }
    }
}
