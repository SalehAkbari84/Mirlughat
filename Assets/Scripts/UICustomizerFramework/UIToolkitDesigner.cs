// ================================================================================================
//  UIToolkitDesigner.cs
//  A single-file UI Toolkit Visual Designer Framework for Unity 6.5+
//
//  Workflow targeted   : GameObject -> PanelRenderer -> UIToolkitDesigner (MonoBehaviour)
//  UI Toolkit workflow : PanelRenderer (NOT UIDocument)
//  Supported (full)    : Slider
//  Framework-ready for : Button, Toggle, ProgressBar, ScrollView, Dropdown, TextField, Custom
//
//  Notes on design choices (see chat for full rationale):
//   - Polymorphic per-element data uses [SerializeReference] (no ScriptableObjects required).
//   - Render-order is abstracted via sibling reordering (BringToFront/PlaceBehind) since UI
//     Toolkit (6.5) has no native z-index/layer property — this is the closest faithful analog.
//   - Animations use VisualElement.experimental.animation (the supported, non-deprecated
//     UI Toolkit animation API), not coroutula tweening.
//   - States (Hover/Pressed/Focused/Disabled/Selected/ReadOnly) are driven by real pointer/focus
//     callbacks and re-resolve + re-apply styles, not pseudo-class CSS (UI Toolkit's built-in
//     pseudo-classes are USS-only; this framework needs Inspector-driven control, so it manages
//     state resolution itself).
//   - Presets use JsonUtility snapshot/restore — fully functional, no placeholders.
//   - Editor UI is built with UI Toolkit (CreateInspectorGUI / CreatePropertyGUI), not IMGUI.
// ================================================================================================

#region Usings

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
#endif

#endregion

namespace UIToolkitDesignerFramework
{
    #region Core Enums

    /// <summary>The kind of UI Toolkit control a <see cref="UIElementEntry"/> targets.</summary>
    public enum UIElementType
    {
        Slider,
        Button,
        Toggle,
        ProgressBar,
        ScrollView,
        Dropdown,
        TextField,
        Custom
    }

    /// <summary>Strategy used to locate the target VisualElement inside the panel.</summary>
    public enum QueryMode
    {
        /// <summary>Find the first element matching the declared UIElementType anywhere in the panel.</summary>
        AutoFindFirst,
        /// <summary>Find an element whose <c>name</c> attribute matches <see cref="UIElementEntry.targetQuery"/>.</summary>
        ByName,
        /// <summary>Find an element whose USS class list contains <see cref="UIElementEntry.targetQuery"/>.</summary>
        ByClass
    }

    /// <summary>How a numeric slider value is rendered as text.</summary>
    public enum ValueFormatMode
    {
        Integer,
        Float,
        Percentage,
        Custom
    }

    /// <summary>Visual state used by the state-override system.</summary>
    public enum ElementVisualState
    {
        Normal,
        Hover,
        Pressed,
        Focused,
        Disabled,
        Selected,
        ReadOnly
    }

    /// <summary>Built-in theme identifiers. Custom themes use <see cref="ThemeKind.Custom"/>.</summary>
    public enum ThemeKind
    {
        Dark,
        Light,
        Modern,
        Minimal,
        Glass,
        Neon,
        Retro,
        Custom
    }

    /// <summary>Background rendering mode shared by every style block that paints a surface.</summary>
    public enum BackgroundMode
    {
        Color,
        Image
    }

    /// <summary>How an image fills its element's bounds.</summary>
    public enum ImageScaleMode
    {
        Stretch,
        Cover,
        Contain
    }

    /// <summary>Anchor used to position the floating percentage/value label.</summary>
    public enum LabelPosition
    {
        Above,
        Below,
        Left,
        Right,
        Center
    }

    /// <summary>Animation easing curves, mapped to <see cref="Easing"/> at apply time.</summary>
    public enum EasingMode
    {
        Linear,
        EaseIn,
        EaseOut,
        EaseInOut,
        Spring,
        Elastic
    }

    /// <summary>Which transition triggers should animate.</summary>
    [Flags]
    public enum AnimationTriggers
    {
        None       = 0,
        Hover      = 1 << 0,
        Pressed    = 1 << 1,
        FillChange = 1 << 2,
        Fade       = 1 << 3,
        Scale      = 1 << 4
    }

    #endregion

    #region Primitive Style Data Classes (reusable by every future element designer)

    /// <summary>Solid colour wrapper, kept as its own class so it can be swapped/overridden independently.</summary>
    [Serializable]
    public class ColorStyle
    {
        public Color color = Color.white;
    }

    /// <summary>Background paint: solid colour or image (Texture2D / Sprite), with tint and scale mode.</summary>
    [Serializable]
    public class BackgroundStyle
    {
        public bool enabled = true;
        public BackgroundMode mode = BackgroundMode.Color;

        [Header("Color Mode")]
        public Color color = new Color(0.18f, 0.18f, 0.18f, 1f);

        [Header("Image Mode")]
        public Texture2D texture;
        public Sprite sprite;
        public ImageScaleMode scaleMode = ImageScaleMode.Stretch;
        public Color tintColor = Color.white;
        [Range(0f, 1f)] public float opacity = 1f;

        public bool HasImage => sprite != null || texture != null;
    }

    /// <summary>Per-side border width/colour plus per-corner radius.</summary>
    [Serializable]
    public class BorderStyle
    {
        public bool enabled = false;

        [Header("Width")]
        public bool uniformWidth = true;
        [Min(0f)] public float width = 1f;
        [Min(0f)] public float widthTop = 1f, widthRight = 1f, widthBottom = 1f, widthLeft = 1f;

        [Header("Color")]
        public bool uniformColor = true;
        public Color color = Color.black;
        public Color colorTop = Color.black, colorRight = Color.black, colorBottom = Color.black, colorLeft = Color.black;

        public float ResolvedWidthTop    => uniformWidth ? width : widthTop;
        public float ResolvedWidthRight  => uniformWidth ? width : widthRight;
        public float ResolvedWidthBottom => uniformWidth ? width : widthBottom;
        public float ResolvedWidthLeft   => uniformWidth ? width : widthLeft;

        public Color ResolvedColorTop    => uniformColor ? color : colorTop;
        public Color ResolvedColorRight  => uniformColor ? color : colorRight;
        public Color ResolvedColorBottom => uniformColor ? color : colorBottom;
        public Color ResolvedColorLeft   => uniformColor ? color : colorLeft;
    }

    /// <summary>Per-corner radius, kept separate from <see cref="BorderStyle"/> so a borderless element can still be rounded.</summary>
    [Serializable]
    public class CornerStyle
    {
        public bool uniform = true;
        [Min(0f)] public float radius = 0f;
        [Min(0f)] public float topLeft = 0f, topRight = 0f, bottomRight = 0f, bottomLeft = 0f;

        public float ResolvedTL => uniform ? radius : topLeft;
        public float ResolvedTR => uniform ? radius : topRight;
        public float ResolvedBR => uniform ? radius : bottomRight;
        public float ResolvedBL => uniform ? radius : bottomLeft;
    }

    /// <summary>Font, size, colour, alignment and wrap/overflow behaviour for any text-bearing element.</summary>
    [Serializable]
    public class TextStyle
    {
        public Color color = Color.white;
        public Font font;
        public FontStyle fontStyle = FontStyle.Normal;
        [Min(1f)] public float fontSize = 14f;
        public TextAnchor alignment = TextAnchor.MiddleLeft;
        public bool wordWrap = false;
        public bool useEllipsis = false;
    }

    /// <summary>Standalone image style for elements whose entire surface is an icon/graphic (distinct from a background).</summary>
    [Serializable]
    public class ImageStyle
    {
        public bool enabled = false;
        public Texture2D texture;
        public Sprite sprite;
        public ImageScaleMode scaleMode = ImageScaleMode.Stretch;
        public Color tintColor = Color.white;
        public bool HasImage => sprite != null || texture != null;
    }

    /// <summary>Margin and padding, uniform or per-side.</summary>
    [Serializable]
    public class SpacingStyle
    {
        [Header("Margin")]
        public bool uniformMargin = true;
        public float margin = 0f;
        public float marginTop = 0f, marginRight = 0f, marginBottom = 0f, marginLeft = 0f;

        [Header("Padding")]
        public bool uniformPadding = true;
        public float padding = 0f;
        public float paddingTop = 0f, paddingRight = 0f, paddingBottom = 0f, paddingLeft = 0f;

        public float ResolvedMarginTop    => uniformMargin ? margin : marginTop;
        public float ResolvedMarginRight  => uniformMargin ? margin : marginRight;
        public float ResolvedMarginBottom => uniformMargin ? margin : marginBottom;
        public float ResolvedMarginLeft   => uniformMargin ? margin : marginLeft;

        public float ResolvedPaddingTop    => uniformPadding ? padding : paddingTop;
        public float ResolvedPaddingRight  => uniformPadding ? padding : paddingRight;
        public float ResolvedPaddingBottom => uniformPadding ? padding : paddingBottom;
        public float ResolvedPaddingLeft   => uniformPadding ? padding : paddingLeft;
    }

    /// <summary>Sizing, positioning and flex behaviour for an element.</summary>
    [Serializable]
    public class LayoutStyle
    {
        [Header("Size (0 = stylesheet default)")]
        [Min(0f)] public float width = 0f;
        [Min(0f)] public float height = 0f;
        [Min(0f)] public float minWidth = 0f;
        [Min(0f)] public float maxWidth = 0f;
        [Min(0f)] public float minHeight = 0f;
        [Min(0f)] public float maxHeight = 0f;

        [Header("Position")]
        public Position positionType = Position.Relative;
        public Vector2 offset = Vector2.zero;

        [Header("Transform")]
        public Vector2 pivot = new Vector2(0.5f, 0.5f);
        public float rotation = 0f;
        public Vector2 scale = Vector2.one;

        [Header("Flex")]
        public float flexGrow = 0f;
        public float flexShrink = 1f;
        [Min(0f)] public float flexBasis = -1f; // -1 => auto
    }

    /// <summary>Display + opacity visibility toggle, decoupled from the rest of the style so it composes everywhere.</summary>
    [Serializable]
    public class VisibilityStyle
    {
        public bool visible = true;
        [Range(0f, 1f)] public float opacity = 1f;
    }

    /// <summary>Render-order abstraction (UI Toolkit has no native z-index; this re-orders siblings).</summary>
    [Serializable]
    public class RenderStyle
    {
        [Tooltip("Higher values are brought toward the front (closer to last-drawn = visually on top).")]
        public int priority = 0;
    }

    /// <summary>Animation intent for a single visual part. Honoured by the runtime animation dispatcher.</summary>
    [Serializable]
    public class AnimationStyle
    {
        public AnimationTriggers triggers = AnimationTriggers.None;
        public EasingMode easing = EasingMode.EaseOut;
        [Min(0f)] public float durationSeconds = 0.15f;
        [Min(0f)] public float delaySeconds = 0f;
        [Tooltip("Scale multiplier applied on Hover/Pressed when the Scale trigger is enabled.")]
        public float hoverScale = 1.05f;
        public float pressedScale = 0.95f;
        [Tooltip("Optional custom curve, used only when Easing = Spring is not expressive enough.")]
        public AnimationCurve customCurve = AnimationCurve.Linear(0, 0, 1, 1);
    }

    #endregion

    #region State System

    /// <summary>
    /// Optional style overrides for one <see cref="ElementVisualState"/>.
    /// Disabled fields fall back to the part's Normal-state style.
    /// </summary>
    [Serializable]
    public class StateOverride
    {
        public bool overrideBackground = false;
        public BackgroundStyle background = new BackgroundStyle();

        public bool overrideBorder = false;
        public BorderStyle border = new BorderStyle();

        public bool overrideText = false;
        public TextStyle text = new TextStyle();

        public bool overrideCorner = false;
        public CornerStyle corner = new CornerStyle();
    }

    /// <summary>
    /// A full set of per-state overrides for one visual part (Track / Fill / Handle / Label).
    /// Normal is implicit — it is simply the part's base style.
    /// </summary>
    [Serializable]
    public class PartStateSet
    {
        public StateOverride hover    = new StateOverride();
        public StateOverride pressed  = new StateOverride();
        public StateOverride focused  = new StateOverride();
        public StateOverride disabled = new StateOverride();
        public StateOverride selected = new StateOverride();
        public StateOverride readOnly = new StateOverride();

        /// <summary>Returns the override block for <paramref name="state"/>, or null for Normal.</summary>
        public StateOverride Get(ElementVisualState state) => state switch
        {
            ElementVisualState.Hover    => hover,
            ElementVisualState.Pressed  => pressed,
            ElementVisualState.Focused  => focused,
            ElementVisualState.Disabled => disabled,
            ElementVisualState.Selected => selected,
            ElementVisualState.ReadOnly => readOnly,
            _ => null
        };
    }

    #endregion

    #region Theme System

    /// <summary>
    /// A reusable colour/shape palette that can be applied to any element's base style.
    /// Per-field "use theme" toggles on the consuming style (see <see cref="SliderDesignerData"/>)
    /// decide whether a given property is theme-driven or manually authored.
    /// </summary>
    [Serializable]
    public class ThemeDefinition
    {
        public string themeName = "New Theme";
        public ThemeKind kind = ThemeKind.Custom;

        [Header("Palette")]
        public Color primaryColor   = new Color(0.27f, 0.55f, 1f, 1f);
        public Color secondaryColor = new Color(0.18f, 0.18f, 0.18f, 1f);
        public Color accentColor    = Color.white;
        public Color backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        public Color textColor      = Color.white;

        [Header("Shape Defaults")]
        [Min(0f)] public float defaultCornerRadius = 4f;
        [Min(0f)] public float defaultBorderWidth = 0f;
        public Color defaultBorderColor = Color.black;
    }

    /// <summary>Static factory for the framework's built-in themes.</summary>
    public static class BuiltInThemes
    {
        public static ThemeDefinition Dark() => new ThemeDefinition
        {
            themeName = "Dark", kind = ThemeKind.Dark,
            primaryColor = new Color(0.30f, 0.55f, 0.95f), secondaryColor = new Color(0.15f, 0.15f, 0.15f),
            accentColor = Color.white, backgroundColor = new Color(0.10f, 0.10f, 0.10f), textColor = Color.white,
            defaultCornerRadius = 4f, defaultBorderWidth = 0f
        };

        public static ThemeDefinition Light() => new ThemeDefinition
        {
            themeName = "Light", kind = ThemeKind.Light,
            primaryColor = new Color(0.20f, 0.45f, 0.95f), secondaryColor = new Color(0.92f, 0.92f, 0.92f),
            accentColor = new Color(0.1f, 0.1f, 0.1f), backgroundColor = Color.white, textColor = new Color(0.1f, 0.1f, 0.1f),
            defaultCornerRadius = 4f, defaultBorderWidth = 1f, defaultBorderColor = new Color(0.8f, 0.8f, 0.8f)
        };

        public static ThemeDefinition Modern() => new ThemeDefinition
        {
            themeName = "Modern", kind = ThemeKind.Modern,
            primaryColor = new Color(0.36f, 0.36f, 0.92f), secondaryColor = new Color(0.20f, 0.20f, 0.24f),
            accentColor = Color.white, backgroundColor = new Color(0.13f, 0.13f, 0.16f), textColor = Color.white,
            defaultCornerRadius = 8f, defaultBorderWidth = 0f
        };

        public static ThemeDefinition Minimal() => new ThemeDefinition
        {
            themeName = "Minimal", kind = ThemeKind.Minimal,
            primaryColor = new Color(0.05f, 0.05f, 0.05f), secondaryColor = new Color(0.85f, 0.85f, 0.85f),
            accentColor = Color.black, backgroundColor = Color.white, textColor = Color.black,
            defaultCornerRadius = 0f, defaultBorderWidth = 1f, defaultBorderColor = Color.black
        };

        public static ThemeDefinition Glass() => new ThemeDefinition
        {
            themeName = "Glass", kind = ThemeKind.Glass,
            primaryColor = new Color(1f, 1f, 1f, 0.35f), secondaryColor = new Color(1f, 1f, 1f, 0.12f),
            accentColor = Color.white, backgroundColor = new Color(1f, 1f, 1f, 0.08f), textColor = Color.white,
            defaultCornerRadius = 12f, defaultBorderWidth = 1f, defaultBorderColor = new Color(1f, 1f, 1f, 0.4f)
        };

        public static ThemeDefinition Neon() => new ThemeDefinition
        {
            themeName = "Neon", kind = ThemeKind.Neon,
            primaryColor = new Color(0f, 1f, 0.85f), secondaryColor = new Color(0.05f, 0.05f, 0.08f),
            accentColor = new Color(1f, 0f, 0.85f), backgroundColor = new Color(0.02f, 0.02f, 0.04f), textColor = new Color(0f, 1f, 0.85f),
            defaultCornerRadius = 2f, defaultBorderWidth = 1f, defaultBorderColor = new Color(0f, 1f, 0.85f)
        };

        public static ThemeDefinition Retro() => new ThemeDefinition
        {
            themeName = "Retro", kind = ThemeKind.Retro,
            primaryColor = new Color(0.85f, 0.55f, 0.13f), secondaryColor = new Color(0.36f, 0.25f, 0.16f),
            accentColor = new Color(0.95f, 0.85f, 0.6f), backgroundColor = new Color(0.18f, 0.12f, 0.08f), textColor = new Color(0.95f, 0.85f, 0.6f),
            defaultCornerRadius = 0f, defaultBorderWidth = 2f, defaultBorderColor = new Color(0.95f, 0.85f, 0.6f)
        };

        public static List<ThemeDefinition> All() => new List<ThemeDefinition>
        {
            Dark(), Light(), Modern(), Minimal(), Glass(), Neon(), Retro()
        };
    }

    #endregion

    #region Style Preset System

    /// <summary>
    /// A saved snapshot of a <see cref="SliderDesignerData"/> (or any future element data),
    /// stored as JSON so it can be applied, duplicated, or used to reset a live element.
    /// </summary>
    [Serializable]
    public class StylePreset
    {
        public string presetName = "New Preset";
        [TextArea(2, 6)] public string serializedJson = "{}";

        /// <summary>Captures <paramref name="data"/> into this preset's JSON payload.</summary>
        public void CaptureFrom(SliderDesignerData data) => serializedJson = JsonUtility.ToJson(data);

        /// <summary>Overwrites the fields of <paramref name="target"/> with this preset's JSON payload.</summary>
        public void ApplyTo(SliderDesignerData target)
        {
            if (target == null || string.IsNullOrEmpty(serializedJson)) return;
            JsonUtility.FromJsonOverwrite(serializedJson, target);
        }

        /// <summary>Returns a copy of this preset with a new name.</summary>
        public StylePreset Duplicate(string newName) => new StylePreset
        {
            presetName = newName,
            serializedJson = serializedJson
        };
    }

    #endregion
    #region Utilities

    /// <summary>Static helpers shared across the entire framework (queries, formatting, safe ops).</summary>
    public static class UIToolkitDesignerUtils
    {
        public static VisualElement SafeQueryByClass(VisualElement root, string className)
        {
            if (root == null || string.IsNullOrEmpty(className)) return null;
            return root.Q(className: className);
        }

        public static T SafeQuery<T>(VisualElement root, string name = null, string className = null) where T : VisualElement
        {
            if (root == null) return null;
            return root.Q<T>(name, className);
        }

        public static void SetVisible(VisualElement element, bool visible)
        {
            if (element == null) return;
            element.style.display = visible ? new StyleEnum<DisplayStyle>(DisplayStyle.Flex)
                                             : new StyleEnum<DisplayStyle>(DisplayStyle.None);
        }

        public static string FormatValue(float value, float low, float high, ValueFormatMode mode,
                                         int decimals, string prefix, string suffix, string customFormat)
        {
            string body = mode switch
            {
                ValueFormatMode.Integer    => Mathf.RoundToInt(value).ToString(),
                ValueFormatMode.Float      => value.ToString($"F{Mathf.Max(0, decimals)}", CultureInfo.InvariantCulture),
                ValueFormatMode.Percentage => FormatPercent(value, low, high, decimals),
                ValueFormatMode.Custom     => SafeCustomFormat(customFormat, value),
                _ => value.ToString(CultureInfo.InvariantCulture)
            };
            return $"{prefix}{body}{suffix}";
        }

        private static string FormatPercent(float value, float low, float high, int decimals)
        {
            float range = Mathf.Approximately(high - low, 0f) ? 1f : high - low;
            float pct = Mathf.Clamp01((value - low) / range) * 100f;
            return pct.ToString($"F{Mathf.Max(0, decimals)}", CultureInfo.InvariantCulture);
        }

        private static string SafeCustomFormat(string format, float value)
        {
            try { return string.Format(CultureInfo.InvariantCulture, format, value); }
            catch { return value.ToString(CultureInfo.InvariantCulture); }
        }

        /// <summary>
        /// Re-orders <paramref name="parts"/> (each sharing the same parent) according to ascending
        /// priority using BringToFront, which is the closest available analog to CSS z-index in
        /// UI Toolkit 6.5 (stacking is governed purely by hierarchy/document order).
        /// </summary>
        public static void ApplyRenderOrder(List<(VisualElement element, int priority)> parts)
        {
            parts.Sort((a, b) => a.priority.CompareTo(b.priority));
            foreach (var (element, _) in parts)
            {
                if (element != null) element.BringToFront();
            }
        }
    }

    #endregion

    #region Core Interfaces

    /// <summary>Contract for a style applier that pushes a style POCO onto a VisualElement.</summary>
    public interface IStyleApplier<in TStyle>
    {
        void Apply(VisualElement target, TStyle style);
        void Clear(VisualElement target);
    }

    /// <summary>
    /// Contract every element designer (Slider, Button, Toggle, …) must implement.
    /// New element types only need a class implementing this interface — the core
    /// <see cref="UIToolkitDesigner"/> component dispatches to designers without modification.
    /// </summary>
    public interface IUIElementDesigner
    {
        UIElementType ElementType { get; }
        bool Bind(VisualElement panelRoot, UIElementEntry entry);
        void Apply(UIElementEntry entry, List<ThemeDefinition> themes);
        void Tick(UIElementEntry entry);
        void Unbind(UIElementEntry entry);
        string GetDebugSummary(UIElementEntry entry);
    }

    #endregion

    #region Style Appliers (shared, reusable by every future element designer)

    /// <summary>
    /// Stateless static dispatcher that writes primitive style POCOs onto a VisualElement
    /// via inline styles, using only non-deprecated Unity 6.5 UI Toolkit APIs.
    /// </summary>
    public static class StyleAppliers
    {
        // ── Background ───────────────────────────────────────────────────

        public static void ApplyBackground(VisualElement target, BackgroundStyle style)
        {
            if (target == null || style == null) return;

            if (!style.enabled)
            {
                ClearBackground(target);
                return;
            }

            if (style.mode == BackgroundMode.Color)
            {
                target.style.backgroundColor = new StyleColor(style.color);
                target.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                target.style.unityBackgroundImageTintColor = StyleKeyword.Null;
                target.style.backgroundSize = StyleKeyword.Null;
                target.style.backgroundRepeat = StyleKeyword.Null;
                target.style.backgroundPositionX = StyleKeyword.Null;
                target.style.backgroundPositionY = StyleKeyword.Null;
            }
            else
            {
                target.style.backgroundColor = new StyleColor(Color.clear);
                target.style.backgroundImage = style.sprite != null ? new StyleBackground(style.sprite)
                                              : style.texture != null ? new StyleBackground(style.texture)
                                              : new StyleBackground(StyleKeyword.None);
                target.style.unityBackgroundImageTintColor =
                    new StyleColor(new Color(style.tintColor.r, style.tintColor.g, style.tintColor.b, style.tintColor.a * style.opacity));

                target.style.backgroundRepeat = new StyleBackgroundRepeat(new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat));

                switch (style.scaleMode)
                {
                    case ImageScaleMode.Stretch:
                        target.style.backgroundSize = new StyleBackgroundSize(
                            new BackgroundSize(new Length(100, LengthUnit.Percent), new Length(100, LengthUnit.Percent)));
                        break;
                    case ImageScaleMode.Cover:
                        target.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Cover));
                        break;
                    case ImageScaleMode.Contain:
                        target.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
                        break;
                }
                target.style.backgroundPositionX = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Center));
                target.style.backgroundPositionY = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Center));
            }
        }

        public static void ClearBackground(VisualElement target)
        {
            if (target == null) return;
            target.style.backgroundColor = StyleKeyword.Null;
            target.style.backgroundImage = StyleKeyword.Null;
            target.style.backgroundSize = StyleKeyword.Null;
            target.style.backgroundRepeat = StyleKeyword.Null;
            target.style.backgroundPositionX = StyleKeyword.Null;
            target.style.backgroundPositionY = StyleKeyword.Null;
            target.style.unityBackgroundImageTintColor = StyleKeyword.Null;
        }

        // ── Border ───────────────────────────────────────────────────────

        public static void ApplyBorder(VisualElement target, BorderStyle style)
        {
            if (target == null || style == null) return;
            if (!style.enabled) { ClearBorder(target); return; }

            target.style.borderTopWidth = style.ResolvedWidthTop;
            target.style.borderRightWidth = style.ResolvedWidthRight;
            target.style.borderBottomWidth = style.ResolvedWidthBottom;
            target.style.borderLeftWidth = style.ResolvedWidthLeft;

            target.style.borderTopColor = new StyleColor(style.ResolvedColorTop);
            target.style.borderRightColor = new StyleColor(style.ResolvedColorRight);
            target.style.borderBottomColor = new StyleColor(style.ResolvedColorBottom);
            target.style.borderLeftColor = new StyleColor(style.ResolvedColorLeft);
        }

        public static void ClearBorder(VisualElement target)
        {
            if (target == null) return;
            target.style.borderTopWidth = StyleKeyword.Null;
            target.style.borderRightWidth = StyleKeyword.Null;
            target.style.borderBottomWidth = StyleKeyword.Null;
            target.style.borderLeftWidth = StyleKeyword.Null;
            target.style.borderTopColor = StyleKeyword.Null;
            target.style.borderRightColor = StyleKeyword.Null;
            target.style.borderBottomColor = StyleKeyword.Null;
            target.style.borderLeftColor = StyleKeyword.Null;
        }

        // ── Corner radius ────────────────────────────────────────────────

        public static void ApplyCorner(VisualElement target, CornerStyle style)
        {
            if (target == null || style == null) return;
            target.style.borderTopLeftRadius = style.ResolvedTL;
            target.style.borderTopRightRadius = style.ResolvedTR;
            target.style.borderBottomRightRadius = style.ResolvedBR;
            target.style.borderBottomLeftRadius = style.ResolvedBL;
        }

        public static void ClearCorner(VisualElement target)
        {
            if (target == null) return;
            target.style.borderTopLeftRadius = StyleKeyword.Null;
            target.style.borderTopRightRadius = StyleKeyword.Null;
            target.style.borderBottomRightRadius = StyleKeyword.Null;
            target.style.borderBottomLeftRadius = StyleKeyword.Null;
        }

        // ── Text ─────────────────────────────────────────────────────────

        public static void ApplyText(VisualElement target, TextStyle style)
        {
            if (target == null || style == null) return;
            target.style.color = new StyleColor(style.color);
            target.style.unityFont = style.font != null ? new StyleFont(style.font) : StyleKeyword.Null;
            target.style.unityFontStyleAndWeight = new StyleEnum<FontStyle>(style.fontStyle);
            target.style.fontSize = style.fontSize;
            target.style.unityTextAlign = new StyleEnum<TextAnchor>(style.alignment);
            target.style.whiteSpace = style.wordWrap ? new StyleEnum<WhiteSpace>(WhiteSpace.Normal) : new StyleEnum<WhiteSpace>(WhiteSpace.NoWrap);
            target.style.textOverflow = style.useEllipsis
                ? new StyleEnum<UnityEngine.UIElements.TextOverflow>(UnityEngine.UIElements.TextOverflow.Ellipsis)
                : new StyleEnum<UnityEngine.UIElements.TextOverflow>(UnityEngine.UIElements.TextOverflow.Clip);
        }

        // ── Spacing ──────────────────────────────────────────────────────

        public static void ApplySpacing(VisualElement target, SpacingStyle style)
        {
            if (target == null || style == null) return;
            target.style.marginTop = style.ResolvedMarginTop;
            target.style.marginRight = style.ResolvedMarginRight;
            target.style.marginBottom = style.ResolvedMarginBottom;
            target.style.marginLeft = style.ResolvedMarginLeft;
            target.style.paddingTop = style.ResolvedPaddingTop;
            target.style.paddingRight = style.ResolvedPaddingRight;
            target.style.paddingBottom = style.ResolvedPaddingBottom;
            target.style.paddingLeft = style.ResolvedPaddingLeft;
        }

        // ── Layout ───────────────────────────────────────────────────────

        public static void ApplyLayout(VisualElement target, LayoutStyle style)
        {
            if (target == null || style == null) return;

            target.style.width = style.width > 0f ? new StyleLength(style.width) : new StyleLength(StyleKeyword.Null);
            target.style.height = style.height > 0f ? new StyleLength(style.height) : new StyleLength(StyleKeyword.Null);
            target.style.minWidth = style.minWidth > 0f ? new StyleLength(style.minWidth) : new StyleLength(StyleKeyword.Null);
            target.style.maxWidth = style.maxWidth > 0f ? new StyleLength(style.maxWidth) : new StyleLength(StyleKeyword.Null);
            target.style.minHeight = style.minHeight > 0f ? new StyleLength(style.minHeight) : new StyleLength(StyleKeyword.Null);
            target.style.maxHeight = style.maxHeight > 0f ? new StyleLength(style.maxHeight) : new StyleLength(StyleKeyword.Null);

            target.style.position = new StyleEnum<Position>(style.positionType);
            if (style.positionType == Position.Absolute)
            {
                target.style.left = style.offset.x;
                target.style.top = style.offset.y;
            }

            target.style.transformOrigin = new StyleTransformOrigin(
                new TransformOrigin(new Length(style.pivot.x * 100, LengthUnit.Percent), new Length(style.pivot.y * 100, LengthUnit.Percent)));
            target.style.rotate = new StyleRotate(new Rotate(style.rotation));
            target.style.scale = new StyleScale(new Scale(new Vector2(style.scale.x, style.scale.y)));

            target.style.flexGrow = style.flexGrow;
            target.style.flexShrink = style.flexShrink;
            target.style.flexBasis = style.flexBasis >= 0f ? new StyleLength(style.flexBasis) : new StyleLength(StyleKeyword.Auto);
        }

        // ── Visibility ───────────────────────────────────────────────────

        public static void ApplyVisibility(VisualElement target, VisibilityStyle style)
        {
            if (target == null || style == null) return;
            target.style.display = style.visible ? new StyleEnum<DisplayStyle>(DisplayStyle.Flex) : new StyleEnum<DisplayStyle>(DisplayStyle.None);
            target.style.opacity = style.opacity;
        }

        // ── Animation (experimental.animation — supported Unity 6 API) ─────

        /// <summary>
        /// Animates a uniform scale on <paramref name="target"/> using the confirmed
        /// ITransitionAnimations.Start(float,float,int,Action&lt;VisualElement,float&gt;) overload
        /// (the higher-level convenience Scale() helper is intentionally avoided here since its
        /// signature is not part of the stable, documented surface).
        /// </summary>
        public static void AnimateScale(VisualElement target, float toScale, AnimationStyle anim)
        {
            if (target == null || anim == null) return;
            float fromScale = target.resolvedStyle.scale.value.x;
            target.experimental.animation
                .Start(fromScale, toScale, Mathf.RoundToInt(anim.durationSeconds * 1000f),
                       (el, val) => el.style.scale = new StyleScale(new Scale(new Vector2(val, val))))
                .Ease(ToEasingFunction(anim.easing));
        }

        public static void AnimateOpacity(VisualElement target, float toOpacity, AnimationStyle anim)
        {
            if (target == null || anim == null) return;
            target.experimental.animation
                .Start(target.resolvedStyle.opacity, toOpacity, Mathf.RoundToInt(anim.durationSeconds * 1000f),
                       (el, val) => el.style.opacity = val)
                .Ease(ToEasingFunction(anim.easing));
        }

        /// <summary>
        /// Maps our Inspector-facing <see cref="EasingMode"/> onto confirmed members of
        /// <see cref="Easing"/>. Linear uses a local identity function since Easing.Linear
        /// is not part of the confirmed stable member set.
        /// </summary>
        private static Func<float, float> ToEasingFunction(EasingMode mode) => mode switch
        {
            EasingMode.Linear    => (t => t),
            EasingMode.EaseIn    => Easing.InCubic,
            EasingMode.EaseOut   => Easing.OutCubic,
            EasingMode.EaseInOut => Easing.InOutCubic,
            EasingMode.Spring    => Easing.OutElastic,
            EasingMode.Elastic   => Easing.OutBounce,
            _ => (t => t)
        };
    }


    #endregion
    #region Slider Designer Data

    /// <summary>Track (background bar) settings.</summary>
    [Serializable]
    public class SliderTrackSettings
    {
        public BackgroundStyle background = new BackgroundStyle();
        public BorderStyle border = new BorderStyle();
        public CornerStyle corner = new CornerStyle();
        public LayoutStyle layout = new LayoutStyle();
        public VisibilityStyle visibility = new VisibilityStyle();
        public RenderStyle render = new RenderStyle { priority = 0 };
        public bool useThemeColor = false;
    }

    /// <summary>Fill (filled portion) settings, including the render-order / "z-index" controls.</summary>
    [Serializable]
    public class SliderFillSettings
    {
        public bool enabled = true;
        public BackgroundStyle background = new BackgroundStyle { color = new Color(0.27f, 0.55f, 1f, 1f) };
        public BorderStyle border = new BorderStyle();
        public CornerStyle corner = new CornerStyle();
        public LayoutStyle layout = new LayoutStyle();
        public VisibilityStyle visibility = new VisibilityStyle();
        [Tooltip("Render order relative to Track and Handle. Higher = closer to front.")]
        public RenderStyle render = new RenderStyle { priority = 1 };
        public bool useThemeColor = true;
    }

    /// <summary>Handle (thumb / dragger) settings.</summary>
    [Serializable]
    public class SliderHandleSettings
    {
        public BackgroundStyle background = new BackgroundStyle { color = Color.white };
        public BorderStyle border = new BorderStyle();
        public CornerStyle corner = new CornerStyle();
        public LayoutStyle layout = new LayoutStyle();
        public VisibilityStyle visibility = new VisibilityStyle();
        public RenderStyle render = new RenderStyle { priority = 2 };
        public bool useThemeColor = false;
    }

    /// <summary>Built-in Slider label settings.</summary>
    [Serializable]
    public class SliderLabelSettings
    {
        public bool enabled = true;
        public string text = "Slider";
        public TextStyle textStyle = new TextStyle();
        public SpacingStyle spacing = new SpacingStyle();
        public VisibilityStyle visibility = new VisibilityStyle();
    }

    /// <summary>Floating value/percentage label settings.</summary>
    [Serializable]
    public class SliderPercentageSettings
    {
        public bool enabled = false;
        public bool liveUpdate = true;

        [Header("Formatting")]
        public ValueFormatMode formatMode = ValueFormatMode.Percentage;
        [Range(0, 6)] public int decimalPlaces = 0;
        public string prefix = "";
        public string suffix = "%";
        public string customFormat = "{0:F1}";

        [Header("Appearance")]
        public TextStyle textStyle = new TextStyle { fontSize = 12f, alignment = TextAnchor.MiddleCenter };

        [Header("Position")]
        public LabelPosition position = LabelPosition.Above;
        public Vector2 offset = Vector2.zero;
    }

    /// <summary>Container for the per-part state-override sets.</summary>
    [Serializable]
    public class SliderStateSettings
    {
        public PartStateSet track = new PartStateSet();
        public PartStateSet fill = new PartStateSet();
        public PartStateSet handle = new PartStateSet();
    }

    /// <summary>Container for the per-part animation intents.</summary>
    [Serializable]
    public class SliderAnimationSettings
    {
        public AnimationStyle handleAnimation = new AnimationStyle { triggers = AnimationTriggers.Hover | AnimationTriggers.Scale };
        public AnimationStyle fillAnimation = new AnimationStyle { triggers = AnimationTriggers.FillChange | AnimationTriggers.Fade };
        public AnimationStyle trackAnimation = new AnimationStyle();
    }

    /// <summary>Theme participation flags for a Slider entry.</summary>
    [Serializable]
    public class SliderThemeSettings
    {
        public bool useTheme = false;
        public string themeName = "Dark";
    }

    /// <summary>Debug foldout settings + cached read-only info populated at runtime.</summary>
    [Serializable]
    public class SliderDebugSettings
    {
        public bool enabled = false;
        [TextArea(3, 10), Tooltip("Read-only. Populated automatically when Debug is enabled.")]
        public string runtimeReport = "(not bound yet)";
    }

    /// <summary>
    /// Full, polymorphic settings payload for one Slider entry in the designer's element list.
    /// Stored via [SerializeReference] on <see cref="UIElementEntry.data"/>.
    /// </summary>
    [Serializable]
    public class SliderDesignerData
    {
        public SliderTrackSettings track = new SliderTrackSettings();
        public SliderFillSettings fill = new SliderFillSettings();
        public SliderHandleSettings handle = new SliderHandleSettings();
        public SliderLabelSettings label = new SliderLabelSettings();
        public SliderPercentageSettings percentage = new SliderPercentageSettings();
        public SliderStateSettings states = new SliderStateSettings();
        public SliderAnimationSettings animation = new SliderAnimationSettings();
        public SliderThemeSettings theme = new SliderThemeSettings();
        public SliderDebugSettings debug = new SliderDebugSettings();
        public List<StylePreset> presets = new List<StylePreset>();
    }

    #endregion

    #region Element Entry (list item)

    /// <summary>
    /// One row in the designer's "UI Elements" list. Identifies which control to bind to and
    /// holds a polymorphic settings payload appropriate for <see cref="elementType"/>.
    /// </summary>
    [Serializable]
    public class UIElementEntry
    {
        [Tooltip("Which UI Toolkit control this entry targets.")]
        public UIElementType elementType = UIElementType.Slider;

        [Tooltip("Friendly name shown in the Inspector list.")]
        public string elementName = "New Element";

        [Tooltip("How to locate the element inside the panel.")]
        public QueryMode queryMode = QueryMode.AutoFindFirst;

        [Tooltip("Element name or USS class to match, depending on Query Mode.")]
        public string targetQuery = "";

        [Tooltip("When off, this entry is skipped entirely (no binding, no styling).")]
        public bool enabled = true;

        [Tooltip("Show the Debug foldout for this entry.")]
        public bool debug = false;

        /// <summary>
        /// Polymorphic settings payload. Currently only <see cref="SliderDesignerData"/> is
        /// fully implemented; other element types resolve this to null until a designer for
        /// that type is added (see <see cref="UIToolkitDesigner.CreateDataForType"/>).
        /// </summary>
        [SerializeReference]
        public object data;

        /// <summary>Strongly-typed accessor used by the Slider designer.</summary>
        public SliderDesignerData SliderData => data as SliderDesignerData;
    }

    #endregion
    #region Slider Element Designer

    /// <summary>Cached runtime references and callback delegates for one bound Slider entry.</summary>
    internal class SliderRuntimeContext
    {
        public Slider slider;
        public VisualElement track;
        public VisualElement fill;
        public VisualElement fillContainer;
        public VisualElement handle;
        public Label builtInLabel;
        public Label percentageLabel;

        public ElementVisualState currentState = ElementVisualState.Normal;

        public EventCallback<PointerEnterEvent> onPointerEnter;
        public EventCallback<PointerLeaveEvent> onPointerLeave;
        public EventCallback<PointerDownEvent> onPointerDown;
        public EventCallback<PointerUpEvent> onPointerUp;
        public EventCallback<FocusInEvent> onFocusIn;
        public EventCallback<FocusOutEvent> onFocusOut;
        public EventCallback<ChangeEvent<float>> onValueChanged;

        public bool isBound;
    }

    /// <summary>
    /// Full implementation of <see cref="IUIElementDesigner"/> for UI Toolkit's <see cref="Slider"/>.
    /// Handles track/fill/handle styling, label + floating percentage label, per-state overrides,
    /// theme participation, animation triggers, and render-order (z-index analog).
    /// </summary>
    public class SliderElementDesigner : IUIElementDesigner
    {
        public UIElementType ElementType => UIElementType.Slider;

        private const string TrackClass = "unity-base-slider__tracker";
        private const string FillClass = "unity-base-slider__fill";
        private const string FillContainerClass = "unity-base-slider__fill-container";
        private const string DraggerClass = "unity-base-slider__dragger";
        private const string LabelClass = "unity-base-field__label";
        private const string PercentLabelName = "uitc-percentage-label";

        private readonly Dictionary<UIElementEntry, SliderRuntimeContext> _contexts = new();

        // ── Bind ─────────────────────────────────────────────────────────

        public bool Bind(VisualElement panelRoot, UIElementEntry entry)
        {
            if (panelRoot == null || entry == null) return false;

            Slider slider = entry.queryMode switch
            {
                QueryMode.ByName  => UIToolkitDesignerUtils.SafeQuery<Slider>(panelRoot, name: entry.targetQuery),
                QueryMode.ByClass => UIToolkitDesignerUtils.SafeQuery<Slider>(panelRoot, className: entry.targetQuery),
                _                 => panelRoot.Q<Slider>()
            };

            if (slider == null)
            {
                Debug.LogWarning($"[SliderElementDesigner] Could not locate a Slider for entry '{entry.elementName}' " +
                                 $"(QueryMode={entry.queryMode}, Query='{entry.targetQuery}').");
                return false;
            }

            if (entry.data is not SliderDesignerData)
                entry.data = new SliderDesignerData();

            var ctx = new SliderRuntimeContext
            {
                slider = slider,
                track = slider.Q(className: TrackClass),
                fill = slider.Q(className: FillClass),
                fillContainer = slider.Q(className: FillContainerClass),
                handle = slider.Q(className: DraggerClass),
                builtInLabel = slider.Q<Label>(className: LabelClass)
            };

            ctx.percentageLabel = slider.Q<Label>(PercentLabelName);
            if (ctx.percentageLabel == null)
            {
                ctx.percentageLabel = new Label { name = PercentLabelName, pickingMode = PickingMode.Ignore };
                ctx.percentageLabel.style.position = Position.Absolute;
                slider.Add(ctx.percentageLabel);
            }

            RegisterStateCallbacks(ctx, entry);

            ctx.onValueChanged = evt => OnValueChanged(entry, ctx, evt.newValue);
            slider.RegisterValueChangedCallback(ctx.onValueChanged);

            ctx.isBound = true;
            _contexts[entry] = ctx;
            return true;
        }

        // ── Apply ────────────────────────────────────────────────────────

        public void Apply(UIElementEntry entry, List<ThemeDefinition> themes)
        {
            if (!_contexts.TryGetValue(entry, out var ctx) || !ctx.isBound) return;
            var data = entry.SliderData;
            if (data == null) return;

            ThemeDefinition theme = ResolveTheme(data, themes);

            ApplyPart(ctx.track, data.track.background, data.track.border, data.track.corner,
                      data.states.track.Get(ctx.currentState),
                      data.track.useThemeColor, theme?.secondaryColor);
            StyleAppliers.ApplyLayout(ctx.track, data.track.layout);
            StyleAppliers.ApplyVisibility(ctx.track, data.track.visibility);

            UIToolkitDesignerUtils.SetVisible(ctx.fill, data.fill.enabled);
            if (ctx.fillContainer != null) UIToolkitDesignerUtils.SetVisible(ctx.fillContainer, data.fill.enabled);
            if (data.fill.enabled)
            {
                ApplyPart(ctx.fill, data.fill.background, data.fill.border, data.fill.corner,
                          data.states.fill.Get(ctx.currentState),
                          data.fill.useThemeColor, theme?.primaryColor);
                StyleAppliers.ApplyLayout(ctx.fill, data.fill.layout);
                StyleAppliers.ApplyVisibility(ctx.fill, data.fill.visibility);
            }

            ApplyPart(ctx.handle, data.handle.background, data.handle.border, data.handle.corner,
                      data.states.handle.Get(ctx.currentState),
                      data.handle.useThemeColor, theme?.accentColor);
            StyleAppliers.ApplyLayout(ctx.handle, data.handle.layout);
            StyleAppliers.ApplyVisibility(ctx.handle, data.handle.visibility);

            ApplyRenderOrder(ctx, data);

            ApplyLabel(ctx, data, theme);
            ApplyPercentage(ctx, data, theme);

            if (data.debug.enabled)
                data.debug.runtimeReport = GetDebugSummary(entry);
        }

        // ── Tick (reserved for designers needing per-frame polling; Slider is fully event-driven) ──

        public void Tick(UIElementEntry entry) { /* No per-frame work required for Slider. */ }

        // ── Unbind ───────────────────────────────────────────────────────

        public void Unbind(UIElementEntry entry)
        {
            if (!_contexts.TryGetValue(entry, out var ctx)) return;

            if (ctx.slider != null)
            {
                if (ctx.onValueChanged != null) ctx.slider.UnregisterValueChangedCallback(ctx.onValueChanged);
                UnregisterStateCallbacks(ctx);
            }

            StyleAppliers.ClearBackground(ctx.track);
            StyleAppliers.ClearBorder(ctx.track);
            StyleAppliers.ClearCorner(ctx.track);

            StyleAppliers.ClearBackground(ctx.fill);
            StyleAppliers.ClearBorder(ctx.fill);
            StyleAppliers.ClearCorner(ctx.fill);

            StyleAppliers.ClearBackground(ctx.handle);
            StyleAppliers.ClearBorder(ctx.handle);
            StyleAppliers.ClearCorner(ctx.handle);

            ctx.percentageLabel?.RemoveFromHierarchy();

            _contexts.Remove(entry);
        }

        // ── Debug ────────────────────────────────────────────────────────

        public string GetDebugSummary(UIElementEntry entry)
        {
            if (!_contexts.TryGetValue(entry, out var ctx) || !ctx.isBound)
                return "(not bound)";

            return
                $"Bound: yes\n" +
                $"Value: {ctx.slider.value:F3}  Range: [{ctx.slider.lowValue:F2}, {ctx.slider.highValue:F2}]\n" +
                $"State: {ctx.currentState}\n" +
                $"Track found: {ctx.track != null}\n" +
                $"Fill found: {ctx.fill != null}\n" +
                $"Handle found: {ctx.handle != null}\n" +
                $"Built-in label found: {ctx.builtInLabel != null}\n" +
                $"Panel: {ctx.slider.panel?.GetType().Name ?? "null"}";
        }

        // ── Private: state resolution & application ─────────────────────

        private static void ApplyPart(VisualElement target, BackgroundStyle bg, BorderStyle border, CornerStyle corner,
                                       StateOverride stateOverride, bool useTheme, Color? themeColor)
        {
            if (target == null) return;

            BackgroundStyle effectiveBg = (stateOverride is { overrideBackground: true }) ? stateOverride.background : bg;
            if (useTheme && themeColor.HasValue)
                effectiveBg = CloneWithColor(effectiveBg, themeColor.Value);

            BorderStyle effectiveBorder = (stateOverride is { overrideBorder: true }) ? stateOverride.border : border;
            CornerStyle effectiveCorner = (stateOverride is { overrideCorner: true }) ? stateOverride.corner : corner;

            StyleAppliers.ApplyBackground(target, effectiveBg);
            StyleAppliers.ApplyBorder(target, effectiveBorder);
            StyleAppliers.ApplyCorner(target, effectiveCorner);
        }

        private static BackgroundStyle CloneWithColor(BackgroundStyle src, Color color)
        {
            return new BackgroundStyle
            {
                enabled = src?.enabled ?? true,
                mode = BackgroundMode.Color,
                color = color,
                texture = src?.texture,
                sprite = src?.sprite,
                scaleMode = src?.scaleMode ?? ImageScaleMode.Stretch,
                tintColor = src?.tintColor ?? Color.white,
                opacity = src?.opacity ?? 1f
            };
        }

        private static ThemeDefinition ResolveTheme(SliderDesignerData data, List<ThemeDefinition> themes)
        {
            if (!data.theme.useTheme || themes == null) return null;
            return themes.Find(t => t.themeName == data.theme.themeName);
        }

        private static void ApplyRenderOrder(SliderRuntimeContext ctx, SliderDesignerData data)
        {
            var parts = new List<(VisualElement, int)>();
            if (ctx.track != null) parts.Add((ctx.track, data.track.render.priority));
            if (ctx.fill != null && data.fill.enabled) parts.Add((ctx.fill, data.fill.render.priority));
            if (ctx.handle != null) parts.Add((ctx.handle, data.handle.render.priority));
            UIToolkitDesignerUtils.ApplyRenderOrder(parts);
        }

        private static void ApplyLabel(SliderRuntimeContext ctx, SliderDesignerData data, ThemeDefinition theme)
        {
            if (ctx.slider == null) return;

            ctx.slider.label = data.label.enabled ? data.label.text : string.Empty;
            UIToolkitDesignerUtils.SetVisible(ctx.builtInLabel, data.label.enabled);
            if (ctx.builtInLabel == null || !data.label.enabled) return;

            TextStyle textStyle = data.label.textStyle;
            if (theme != null) textStyle = CloneTextWithColor(textStyle, theme.textColor);

            StyleAppliers.ApplyText(ctx.builtInLabel, textStyle);
            StyleAppliers.ApplySpacing(ctx.builtInLabel, data.label.spacing);
            StyleAppliers.ApplyVisibility(ctx.builtInLabel, data.label.visibility);
        }

        private static void ApplyPercentage(SliderRuntimeContext ctx, SliderDesignerData data, ThemeDefinition theme)
        {
            if (ctx.percentageLabel == null) return;

            UIToolkitDesignerUtils.SetVisible(ctx.percentageLabel, data.percentage.enabled);
            if (!data.percentage.enabled) return;

            TextStyle textStyle = data.percentage.textStyle;
            if (theme != null) textStyle = CloneTextWithColor(textStyle, theme.textColor);
            StyleAppliers.ApplyText(ctx.percentageLabel, textStyle);

            ApplyPercentagePosition(ctx.percentageLabel, data.percentage);

            float low = ctx.slider.lowValue, high = ctx.slider.highValue;
            ctx.percentageLabel.text = UIToolkitDesignerUtils.FormatValue(
                ctx.slider.value, low, high, data.percentage.formatMode, data.percentage.decimalPlaces,
                data.percentage.prefix, data.percentage.suffix, data.percentage.customFormat);
        }

        private static TextStyle CloneTextWithColor(TextStyle src, Color color) => new TextStyle
        {
            color = color,
            font = src?.font,
            fontStyle = src?.fontStyle ?? FontStyle.Normal,
            fontSize = src?.fontSize ?? 14f,
            alignment = src?.alignment ?? TextAnchor.MiddleLeft,
            wordWrap = src?.wordWrap ?? false,
            useEllipsis = src?.useEllipsis ?? false
        };

        private static void ApplyPercentagePosition(Label label, SliderPercentageSettings settings)
        {
            label.style.top = StyleKeyword.Null;
            label.style.bottom = StyleKeyword.Null;
            label.style.left = StyleKeyword.Null;
            label.style.right = StyleKeyword.Null;

            float ox = settings.offset.x, oy = settings.offset.y;

            switch (settings.position)
            {
                case LabelPosition.Above:
                    label.style.top = -20f + oy; label.style.left = ox; label.style.right = 0f;
                    label.style.unityTextAlign = TextAnchor.MiddleCenter;
                    break;
                case LabelPosition.Below:
                    label.style.bottom = -20f - oy; label.style.left = ox; label.style.right = 0f;
                    label.style.unityTextAlign = TextAnchor.MiddleCenter;
                    break;
                case LabelPosition.Left:
                    label.style.top = oy; label.style.bottom = 0f; label.style.left = ox;
                    label.style.unityTextAlign = TextAnchor.MiddleLeft;
                    break;
                case LabelPosition.Right:
                    label.style.top = oy; label.style.bottom = 0f; label.style.right = ox;
                    label.style.unityTextAlign = TextAnchor.MiddleRight;
                    break;
                case LabelPosition.Center:
                    label.style.top = oy; label.style.bottom = 0f; label.style.left = ox; label.style.right = 0f;
                    label.style.unityTextAlign = TextAnchor.MiddleCenter;
                    break;
            }
        }

        // ── Private: state callback wiring ──────────────────────────────

        private void RegisterStateCallbacks(SliderRuntimeContext ctx, UIElementEntry entry)
        {
            ctx.onPointerEnter = _ => SetState(entry, ctx, ElementVisualState.Hover);
            ctx.onPointerLeave = _ => SetState(entry, ctx, ElementVisualState.Normal);
            ctx.onPointerDown  = _ => SetState(entry, ctx, ElementVisualState.Pressed);
            ctx.onPointerUp    = _ => SetState(entry, ctx, ElementVisualState.Hover);
            ctx.onFocusIn      = _ => SetState(entry, ctx, ElementVisualState.Focused);
            ctx.onFocusOut     = _ => SetState(entry, ctx, ElementVisualState.Normal);

            ctx.slider.RegisterCallback(ctx.onPointerEnter);
            ctx.slider.RegisterCallback(ctx.onPointerLeave);
            ctx.slider.RegisterCallback(ctx.onPointerDown);
            ctx.slider.RegisterCallback(ctx.onPointerUp);
            ctx.slider.RegisterCallback(ctx.onFocusIn);
            ctx.slider.RegisterCallback(ctx.onFocusOut);
        }

        private void UnregisterStateCallbacks(SliderRuntimeContext ctx)
        {
            if (ctx.slider == null) return;
            if (ctx.onPointerEnter != null) ctx.slider.UnregisterCallback(ctx.onPointerEnter);
            if (ctx.onPointerLeave != null) ctx.slider.UnregisterCallback(ctx.onPointerLeave);
            if (ctx.onPointerDown != null) ctx.slider.UnregisterCallback(ctx.onPointerDown);
            if (ctx.onPointerUp != null) ctx.slider.UnregisterCallback(ctx.onPointerUp);
            if (ctx.onFocusIn != null) ctx.slider.UnregisterCallback(ctx.onFocusIn);
            if (ctx.onFocusOut != null) ctx.slider.UnregisterCallback(ctx.onFocusOut);
        }

        private void SetState(UIElementEntry entry, SliderRuntimeContext ctx, ElementVisualState newState)
        {
            if (entry.SliderData == null) return;
            if (!entry.SliderData.handle.visibility.visible && newState != ElementVisualState.Normal) return;
            if (ctx.currentState == newState) return;

            ctx.currentState = newState;
            Apply(entry, null);
            TriggerStateAnimation(ctx, entry.SliderData, newState);
        }

        private void TriggerStateAnimation(SliderRuntimeContext ctx, SliderDesignerData data, ElementVisualState state)
        {
            var anim = data.animation.handleAnimation;
            if ((anim.triggers & AnimationTriggers.Scale) == 0 || ctx.handle == null) return;

            float targetScale = state switch
            {
                ElementVisualState.Hover when (anim.triggers & AnimationTriggers.Hover) != 0 => anim.hoverScale,
                ElementVisualState.Pressed when (anim.triggers & AnimationTriggers.Pressed) != 0 => anim.pressedScale,
                _ => 1f
            };
            StyleAppliers.AnimateScale(ctx.handle, targetScale, anim);
        }

        // ── Private: value-change handling ──────────────────────────────

        private void OnValueChanged(UIElementEntry entry, SliderRuntimeContext ctx, float newValue)
        {
            var data = entry.SliderData;
            if (data == null) return;

            if (data.percentage.enabled && data.percentage.liveUpdate)
            {
                float low = ctx.slider.lowValue, high = ctx.slider.highValue;
                ctx.percentageLabel.text = UIToolkitDesignerUtils.FormatValue(
                    newValue, low, high, data.percentage.formatMode, data.percentage.decimalPlaces,
                    data.percentage.prefix, data.percentage.suffix, data.percentage.customFormat);
            }

            var fillAnim = data.animation.fillAnimation;
            if ((fillAnim.triggers & AnimationTriggers.FillChange) != 0 && ctx.fill != null &&
                (fillAnim.triggers & AnimationTriggers.Fade) != 0)
            {
                StyleAppliers.AnimateOpacity(ctx.fill, data.fill.visibility.opacity, fillAnim);
            }
        }
    }

    #endregion
    #region Main Component

    /// <summary>
    /// Attach this to a GameObject alongside a <see cref="PanelRenderer"/>.
    /// Maintains a list of <see cref="UIElementEntry"/> rows (each targeting one UI Toolkit
    /// control) and drives the Bind → Apply → Unbind lifecycle for every entry via the
    /// registered <see cref="IUIElementDesigner"/> for its <see cref="UIElementType"/>.
    /// <para>
    /// New element types become supported by implementing <see cref="IUIElementDesigner"/> and
    /// registering it in <see cref="BuildDesignerRegistry"/> — no other part of this class changes.
    /// </para>
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("UI Toolkit/UI Toolkit Designer")]
    public class UIToolkitDesigner : MonoBehaviour
    {
        // ── Inspector: Panel ─────────────────────────────────────────────

        [Header("Panel Renderer")]
        [Tooltip("PanelRenderer hosting the UI Toolkit panel. Auto-assigned from this GameObject if left empty.")]
        [SerializeField] private PanelRenderer panelRenderer;

        // ── Inspector: Elements ──────────────────────────────────────────

        [Header("UI Elements")]
        [Tooltip("One entry per UI Toolkit control you want to customize from the Inspector.")]
        [SerializeField] private List<UIElementEntry> elements = new();

        // ── Inspector: Themes ────────────────────────────────────────────

        [Header("Themes")]
        [Tooltip("Available themes. Use BuiltIn Themes / Reset to repopulate the standard set.")]
        [SerializeField] private List<ThemeDefinition> themes = new(BuiltInThemes.All());

        // ── Runtime State ────────────────────────────────────────────────

        private readonly Dictionary<UIElementType, IUIElementDesigner> _designerRegistry = new();
        private readonly HashSet<UIElementEntry> _boundEntries = new();
        private VisualElement _cachedRoot;
        private bool _isPanelReady;
        private bool _callbackRegistered;

        // ── Public Accessors (used by the custom editor) ────────────────

        public List<UIElementEntry> Elements => elements;
        public List<ThemeDefinition> Themes => themes;
        public PanelRenderer PanelRendererRef => panelRenderer;

        // ── Unity Lifecycle ──────────────────────────────────────────────
        //
        // PanelRenderer (Unity 6.5+) has no public "panel" / "rootVisualElement" property.
        // UI access is exclusively callback-based via RegisterUIReloadCallback, which fires
        // immediately if the UI is already loaded, and again on every reload (asset change,
        // live-reload, re-enable). Unlike UIDocument, disabling a PanelRenderer does NOT
        // destroy its visual tree, so all (re)binding must happen inside that callback rather
        // than by polling for a root element in OnEnable/Update.

        private void Awake() => BuildDesignerRegistry();

        private void OnEnable()
        {
            BuildDesignerRegistry();
            AutoAssignPanelRenderer();

            if (panelRenderer == null)
            {
                Debug.LogWarning("[UIToolkitDesigner] No PanelRenderer found or assigned. " +
                                 "Add a PanelRenderer component to this GameObject or assign one in the Inspector.");
                return;
            }

            if (!_callbackRegistered)
            {
                panelRenderer.RegisterUIReloadCallback(OnUIReload);
                _callbackRegistered = true;
            }
        }

        private void OnDisable()
        {
            if (panelRenderer != null && _callbackRegistered)
            {
                panelRenderer.UnregisterUIReloadCallback(OnUIReload);
                _callbackRegistered = false;
            }
            UnbindAll();
        }

        private void OnDestroy() => UnbindAll();

        /// <summary>
        /// Fired by PanelRenderer whenever its visual tree is (re)built — immediately upon
        /// registration if already loaded, and again on every subsequent reload. Always
        /// re-binds from scratch since cached element references may be stale after a reload.
        /// </summary>
        private void OnUIReload(PanelRenderer renderer, VisualElement root)
        {
            UnbindAll();
            _cachedRoot = root;
            BindAll();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (this == null || !gameObject.activeInHierarchy) return;
            UnityEditor.EditorApplication.delayCall += EditorReapply;
        }

        private void EditorReapply()
        {
            if (this == null) return;
            if (_isPanelReady) ApplyAll();
            // If not yet bound, OnUIReload will bind automatically once the panel is ready —
            // there is no synchronous way to force this, by design of the PanelRenderer API.
        }
#endif

        // ── Designer Registry ─────────────────────────────────────────────

        /// <summary>
        /// Registers one <see cref="IUIElementDesigner"/> per supported <see cref="UIElementType"/>.
        /// Adding support for a new control type requires only a new designer class and one line here.
        /// </summary>
        private void BuildDesignerRegistry()
        {
            if (_designerRegistry.Count > 0) return;
            _designerRegistry[UIElementType.Slider] = new SliderElementDesigner();
            // Future: _designerRegistry[UIElementType.Button] = new ButtonElementDesigner(); etc.
        }

        private bool TryGetDesigner(UIElementType type, out IUIElementDesigner designer) =>
            _designerRegistry.TryGetValue(type, out designer);

        /// <summary>Creates a fresh, type-appropriate data payload for a newly added entry.</summary>
        public static object CreateDataForType(UIElementType type) => type switch
        {
            UIElementType.Slider => new SliderDesignerData(),
            _ => null // No designer registered yet for this type; entry will be skipped gracefully.
        };

        // ── Bind / Apply / Unbind ─────────────────────────────────────────

        private void BindAll()
        {
            if (_cachedRoot == null) return;

            _isPanelReady = true;

            foreach (var entry in elements)
            {
                if (entry == null || !entry.enabled) continue;
                if (_boundEntries.Contains(entry)) continue;

                if (!TryGetDesigner(entry.elementType, out var designer))
                {
                    Debug.LogWarning($"[UIToolkitDesigner] No designer registered for element type " +
                                     $"'{entry.elementType}' (entry '{entry.elementName}'). It will be skipped.");
                    continue;
                }

                if (entry.data == null) entry.data = CreateDataForType(entry.elementType);

                if (designer.Bind(_cachedRoot, entry))
                {
                    _boundEntries.Add(entry);
                    designer.Apply(entry, themes);
                }
            }
        }

        private void ApplyAll()
        {
            foreach (var entry in elements)
            {
                if (entry == null || !entry.enabled || !_boundEntries.Contains(entry)) continue;
                if (TryGetDesigner(entry.elementType, out var designer))
                    designer.Apply(entry, themes);
            }
        }

        private void UnbindAll()
        {
            foreach (var entry in elements)
            {
                if (entry == null || !_boundEntries.Contains(entry)) continue;
                if (TryGetDesigner(entry.elementType, out var designer))
                    designer.Unbind(entry);
            }
            _boundEntries.Clear();
            _isPanelReady = false;
        }

        /// <summary>Re-applies styling for every bound entry. Safe to call from the custom editor.</summary>
        public void RefreshAll()
        {
            if (!_isPanelReady) { BindAll(); return; }
            ApplyAll();
        }

        // ── Panel Helpers ─────────────────────────────────────────────────

        private void AutoAssignPanelRenderer()
        {
            if (panelRenderer == null) panelRenderer = GetComponent<PanelRenderer>();
        }


        // ── Preset Convenience API (operates on a Slider entry's data) ────

        public void SavePreset(UIElementEntry entry, string presetName)
        {
            var data = entry?.SliderData;
            if (data == null) return;
            var preset = new StylePreset { presetName = presetName };
            preset.CaptureFrom(data);
            data.presets.Add(preset);
        }

        public void ApplyPreset(UIElementEntry entry, StylePreset preset)
        {
            var data = entry?.SliderData;
            if (data == null || preset == null) return;
            preset.ApplyTo(data);
            RefreshAll();
        }

        public void DuplicatePreset(UIElementEntry entry, StylePreset preset)
        {
            var data = entry?.SliderData;
            if (data == null || preset == null) return;
            data.presets.Add(preset.Duplicate(preset.presetName + " Copy"));
        }

        public void ResetToDefault(UIElementEntry entry)
        {
            if (entry == null) return;
            entry.data = CreateDataForType(entry.elementType);
            RefreshAll();
        }

        // ── Theme Convenience API ─────────────────────────────────────────

        public void ResetThemesToBuiltIn()
        {
            themes.Clear();
            themes.AddRange(BuiltInThemes.All());
        }
    }

    #endregion
#if UNITY_EDITOR
    #region Editor: Property Drawers

    /// <summary>
    /// Renders one <see cref="UIElementEntry"/> as an expandable, component-like block:
    /// header identification fields followed by grouped foldouts for the element's settings.
    /// </summary>
    [CustomPropertyDrawer(typeof(UIElementEntry))]
    public class UIElementEntryDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            root.style.marginBottom = 6;
            root.style.paddingLeft = 4;
            root.style.paddingRight = 4;
            root.style.paddingTop = 4;
            root.style.paddingBottom = 4;
            root.style.backgroundColor = new Color(0, 0, 0, 0.08f);

            var nameProp = property.FindPropertyRelative("elementName");
            var typeProp = property.FindPropertyRelative("elementType");

            var outerFoldout = new Foldout
            {
                text = string.IsNullOrEmpty(nameProp.stringValue) ? "UI Element" : $"{nameProp.stringValue}  ({(UIElementType)typeProp.enumValueIndex})",
                value = false
            };
            root.Add(outerFoldout);

            outerFoldout.Add(new PropertyField(typeProp, "Element Type"));
            outerFoldout.Add(new PropertyField(nameProp, "Element Name"));
            outerFoldout.Add(new PropertyField(property.FindPropertyRelative("queryMode"), "Query Mode"));
            outerFoldout.Add(new PropertyField(property.FindPropertyRelative("targetQuery"), "Target Query"));
            outerFoldout.Add(new PropertyField(property.FindPropertyRelative("enabled"), "Enabled"));
            outerFoldout.Add(new PropertyField(property.FindPropertyRelative("debug"), "Debug"));

            var separator = new VisualElement();
            separator.style.height = 1;
            separator.style.marginTop = 4;
            separator.style.marginBottom = 4;
            separator.style.backgroundColor = new Color(1, 1, 1, 0.1f);
            outerFoldout.Add(separator);

            var dataProp = property.FindPropertyRelative("data");
            BuildDataSection(outerFoldout, dataProp, (UIElementType)typeProp.enumValueIndex);

            // Update the foldout title when the name changes.
            outerFoldout.RegisterCallback<ChangeEvent<string>>(_ =>
            {
                outerFoldout.text = string.IsNullOrEmpty(nameProp.stringValue)
                    ? "UI Element" : $"{nameProp.stringValue}  ({(UIElementType)typeProp.enumValueIndex})";
            });

            return root;
        }

        private static void BuildDataSection(VisualElement parent, SerializedProperty dataProp, UIElementType elementType)
        {
            bool isSlider = dataProp != null
                && dataProp.propertyType == SerializedPropertyType.ManagedReference
                && !string.IsNullOrEmpty(dataProp.managedReferenceFullTypename)
                && dataProp.managedReferenceFullTypename.Contains(nameof(SliderDesignerData));

            if (!isSlider)
            {
                parent.Add(new HelpBox(
                    $"No designer is implemented yet for '{elementType}'. This entry will be skipped at bind time " +
                    "until a corresponding IUIElementDesigner is registered.", HelpBoxMessageType.Info));
                return;
            }

            AddGroup(parent, "Track",      dataProp.FindPropertyRelative("track"));
            AddGroup(parent, "Fill",       dataProp.FindPropertyRelative("fill"));
            AddGroup(parent, "Handle",     dataProp.FindPropertyRelative("handle"));
            AddGroup(parent, "Label",      dataProp.FindPropertyRelative("label"));
            AddGroup(parent, "Percentage", dataProp.FindPropertyRelative("percentage"));
            AddGroup(parent, "States",     dataProp.FindPropertyRelative("states"));
            AddGroup(parent, "Animation",  dataProp.FindPropertyRelative("animation"));
            AddGroup(parent, "Theme",      dataProp.FindPropertyRelative("theme"));
            AddGroup(parent, "Presets",    dataProp.FindPropertyRelative("presets"));
            AddGroup(parent, "Debug",      dataProp.FindPropertyRelative("debug"));
        }

        private static void AddGroup(VisualElement parent, string label, SerializedProperty prop)
        {
            if (prop == null) return;
            var foldout = new Foldout { text = label, value = false };
            foldout.style.marginLeft = 8;
            foldout.Add(new PropertyField(prop, string.Empty));
            parent.Add(foldout);
        }
    }

    #endregion

    #region Editor: Custom Inspector

    /// <summary>
    /// UI Toolkit–based custom inspector for <see cref="UIToolkitDesigner"/>.
    /// Renders the Panel Renderer field, the expandable UI Elements list (with the
    /// standard Unity +/− list controls), the Themes list, and quick-action buttons.
    /// </summary>
    [CustomEditor(typeof(UIToolkitDesigner))]
    public class UIToolkitDesignerEditor : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            root.Add(new PropertyField(serializedObject.FindProperty("panelRenderer"), "Panel Renderer"));

            var elementsHeader = new Label("UI Elements") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 8 } };
            root.Add(elementsHeader);
            root.Add(new PropertyField(serializedObject.FindProperty("elements"), string.Empty));

            var themesHeader = new Label("Themes") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 8 } };
            root.Add(themesHeader);
            root.Add(new PropertyField(serializedObject.FindProperty("themes"), string.Empty));

            var resetThemesButton = new Button(() =>
            {
                foreach (var t in targets)
                {
                    if (t is UIToolkitDesigner designer)
                    {
                        Undo.RecordObject(designer, "Reset Themes To Built-In");
                        designer.ResetThemesToBuiltIn();
                        EditorUtility.SetDirty(designer);
                    }
                }
                serializedObject.Update();
            })
            { text = "Reset Themes to Built-In" };
            resetThemesButton.style.marginTop = 4;
            root.Add(resetThemesButton);

            var refreshButton = new Button(() =>
            {
                foreach (var t in targets)
                {
                    if (t is UIToolkitDesigner designer)
                        designer.RefreshAll();
                }
            })
            { text = "Refresh / Apply Now" };
            refreshButton.style.marginTop = 8;
            root.Add(refreshButton);

            root.Bind(serializedObject);
            return root;
        }
    }

    #endregion
#endif
}
