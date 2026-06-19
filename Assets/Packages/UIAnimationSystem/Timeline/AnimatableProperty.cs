using UnityEngine;

namespace UIToolkit.Animation.Timeline
{
    // Properties that can be animated on a VisualElement.
    // To add a new property: add an entry here and a matching case in PropertyBinder.
    // NOTE: append new entries at the end only - values are serialized by index.
    public enum AnimatableProperty
    {
        Opacity,
        TranslateX,
        TranslateY,
        ScaleX,
        ScaleY,
        Rotate,
        Width,
        Height,
        Color,
        BackgroundColor,
        MarginLeft,
        MarginTop,
        BorderRadius,
        // ---- added ----
        Left,
        Top,
        Right,
        Bottom,
        MarginRight,
        MarginBottom,
        PaddingLeft,
        PaddingTop,
        PaddingRight,
        PaddingBottom,
        BorderWidth,
        FontSize,
        Display,        // 0 = None, 1 = Flex (steps at 0.5)
        Visible,        // 0 = Hidden, 1 = Visible (steps at 0.5)
        BorderColor,
        TintColor       // unityBackgroundImageTintColor
    }

    public enum PropertyValueType { Float, Color }

    public static class PropertyMeta
    {
        public static PropertyValueType TypeOf(AnimatableProperty p)
        {
            switch (p)
            {
                case AnimatableProperty.Color:
                case AnimatableProperty.BackgroundColor:
                case AnimatableProperty.BorderColor:
                case AnimatableProperty.TintColor:
                    return PropertyValueType.Color;
                default:
                    return PropertyValueType.Float;
            }
        }

        public static string DisplayName(AnimatableProperty p)
        {
            switch (p)
            {
                case AnimatableProperty.Opacity: return "Opacity";
                case AnimatableProperty.TranslateX: return "Translate X";
                case AnimatableProperty.TranslateY: return "Translate Y";
                case AnimatableProperty.ScaleX: return "Scale X";
                case AnimatableProperty.ScaleY: return "Scale Y";
                case AnimatableProperty.Rotate: return "Rotation";
                case AnimatableProperty.Width: return "Width";
                case AnimatableProperty.Height: return "Height";
                case AnimatableProperty.Color: return "Color";
                case AnimatableProperty.BackgroundColor: return "Background Color";
                case AnimatableProperty.MarginLeft: return "Margin Left";
                case AnimatableProperty.MarginTop: return "Margin Top";
                case AnimatableProperty.BorderRadius: return "Border Radius";
                case AnimatableProperty.Left: return "Left";
                case AnimatableProperty.Top: return "Top";
                case AnimatableProperty.Right: return "Right";
                case AnimatableProperty.Bottom: return "Bottom";
                case AnimatableProperty.MarginRight: return "Margin Right";
                case AnimatableProperty.MarginBottom: return "Margin Bottom";
                case AnimatableProperty.PaddingLeft: return "Padding Left";
                case AnimatableProperty.PaddingTop: return "Padding Top";
                case AnimatableProperty.PaddingRight: return "Padding Right";
                case AnimatableProperty.PaddingBottom: return "Padding Bottom";
                case AnimatableProperty.BorderWidth: return "Border Width";
                case AnimatableProperty.FontSize: return "Font Size";
                case AnimatableProperty.Display: return "Display (show/hide)";
                case AnimatableProperty.Visible: return "Visible";
                case AnimatableProperty.BorderColor: return "Border Color";
                case AnimatableProperty.TintColor: return "Tint Color";
                default: return p.ToString();
            }
        }

        public static float DefaultFloat(AnimatableProperty p)
        {
            switch (p)
            {
                case AnimatableProperty.Opacity:
                case AnimatableProperty.ScaleX:
                case AnimatableProperty.ScaleY:
                case AnimatableProperty.Display:
                case AnimatableProperty.Visible:
                    return 1f;
                default:
                    return 0f;
            }
        }
    }
}
