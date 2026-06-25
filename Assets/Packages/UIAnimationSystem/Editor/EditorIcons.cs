#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIToolkit.Animation.Editor
{
    // Loads Unity's built-in editor icons (no external assets/libraries) and
    // applies them to buttons. Results are cached so missing names log at most
    // once, and any button gracefully falls back to text if an icon is absent.
    static class EditorIcons
    {
        static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

        public static Texture2D Tex(params string[] names)
        {
            if (names == null || names.Length == 0) return null;
            string key = string.Join("|", names);
            if (_cache.TryGetValue(key, out var cached)) return cached;

            Texture2D found = null;
            foreach (var n in names)
            {
                if (string.IsNullOrEmpty(n)) continue;
                GUIContent c = null;
                try { c = EditorGUIUtility.IconContent(n); } catch { }
                if (c != null && c.image is Texture2D t) { found = t; break; }
            }
            _cache[key] = found;
            return found;
        }

        // Turn a button into an icon button (keeps text as fallback if no icon).
        // Always gives the button a definite size so it never collapses to a
        // thin line when the text is cleared (resolvedStyle isn't ready yet here).
        public static Button SetIcon(this Button b, string tooltip, params string[] names)
        {
            b.tooltip = tooltip;
            var tex = Tex(names);
            if (tex != null)
            {
                b.text = string.Empty;
                b.style.backgroundImage = new StyleBackground(tex);
                // "Contain" = scale-to-fit (replaces the deprecated unityBackgroundScaleMode)
                b.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                b.style.minWidth = 26;
                b.style.height = 20;
                b.style.paddingLeft = 3; b.style.paddingRight = 3;
                b.style.paddingTop = 2; b.style.paddingBottom = 2;
            }
            return b;
        }

        // Create a fresh icon button.
        public static Button Button(Action onClick, string tooltip, string fallbackText, params string[] names)
        {
            var b = new Button(onClick) { text = fallbackText };
            return b.SetIcon(tooltip, names);
        }
    }
}
#endif
