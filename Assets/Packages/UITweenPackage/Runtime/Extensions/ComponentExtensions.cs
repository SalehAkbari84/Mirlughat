using UnityEngine;

namespace UITween
{
    /// <summary>
    /// اکستنشن‌متودها برای اجزای رایج خارج از UGUI (دوربین، نور، صدا، متریال، اسپرایت)
    /// تا این پکیج فقط مخصوص UI نباشد و روی هر چیزی در پروژه قابل استفاده باشد.
    /// </summary>
    public static class ComponentExtensions
    {
        // ==================== Camera ====================

        public static Tween DOFieldOfView(this Camera cam, float to, float duration)
        {
            float from = cam.fieldOfView;
            return UITweenManager.Track(ValueTween<float>.Get(cam, from, to, duration, v => cam.fieldOfView = v, Mathf.LerpUnclamped));
        }

        public static Tween DOOrthoSize(this Camera cam, float to, float duration)
        {
            float from = cam.orthographicSize;
            return UITweenManager.Track(ValueTween<float>.Get(cam, from, to, duration, v => cam.orthographicSize = v, Mathf.LerpUnclamped));
        }

        public static Tween DOBackgroundColor(this Camera cam, Color to, float duration)
        {
            Color from = cam.backgroundColor;
            return UITweenManager.Track(ValueTween<Color>.Get(cam, from, to, duration, v => cam.backgroundColor = v, Color.LerpUnclamped));
        }

        // ==================== Light ====================

        public static Tween DOIntensity(this Light light, float to, float duration)
        {
            float from = light.intensity;
            return UITweenManager.Track(ValueTween<float>.Get(light, from, to, duration, v => light.intensity = v, Mathf.LerpUnclamped));
        }

        public static Tween DOColor(this Light light, Color to, float duration)
        {
            Color from = light.color;
            return UITweenManager.Track(ValueTween<Color>.Get(light, from, to, duration, v => light.color = v, Color.LerpUnclamped));
        }

        public static Tween DORange(this Light light, float to, float duration)
        {
            float from = light.range;
            return UITweenManager.Track(ValueTween<float>.Get(light, from, to, duration, v => light.range = v, Mathf.LerpUnclamped));
        }

        // ==================== AudioSource ====================

        public static Tween DOVolume(this AudioSource src, float to, float duration)
        {
            float from = src.volume;
            return UITweenManager.Track(ValueTween<float>.Get(src, from, to, duration, v => src.volume = v, Mathf.LerpUnclamped));
        }

        public static Tween DOPitch(this AudioSource src, float to, float duration)
        {
            float from = src.pitch;
            return UITweenManager.Track(ValueTween<float>.Get(src, from, to, duration, v => src.pitch = v, Mathf.LerpUnclamped));
        }

        /// <summary>کاهش تدریجی صدا تا صفر و سپس توقف پخش (برای موسیقی پس‌زمینه)</summary>
        public static Tween DOFadeOutAndStop(this AudioSource src, float duration)
        {
            var t = src.DOVolume(0f, duration);
            t.OnComplete(() => src.Stop());
            return t;
        }

        // ==================== Material (نمونه‌ی material، نه sharedMaterial) ====================

        public static Tween DOColor(this Material mat, Color to, float duration, string property = "_Color")
        {
            Color from = mat.GetColor(property);
            return UITweenManager.Track(ValueTween<Color>.Get(mat, from, to, duration, v => mat.SetColor(property, v), Color.LerpUnclamped));
        }

        public static Tween DOFloat(this Material mat, float to, float duration, string property)
        {
            float from = mat.GetFloat(property);
            return UITweenManager.Track(ValueTween<float>.Get(mat, from, to, duration, v => mat.SetFloat(property, v), Mathf.LerpUnclamped));
        }

        public static Tween DOOffset(this Material mat, Vector2 to, float duration, string property = "_MainTex")
        {
            Vector2 from = mat.GetTextureOffset(property);
            return UITweenManager.Track(ValueTween<Vector2>.Get(mat, from, to, duration, v => mat.SetTextureOffset(property, v), Vector2.LerpUnclamped));
        }

        // ==================== SpriteRenderer ====================

        public static Tween DOColor(this SpriteRenderer sr, Color to, float duration)
        {
            Color from = sr.color;
            return UITweenManager.Track(ValueTween<Color>.Get(sr, from, to, duration, v => sr.color = v, Color.LerpUnclamped));
        }

        public static Tween DOFade(this SpriteRenderer sr, float to, float duration)
        {
            var from = sr.color;
            return sr.DOColor(new Color(from.r, from.g, from.b, to), duration);
        }
    }
}
