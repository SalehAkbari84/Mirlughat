using UnityEngine;
using UnityEngine.UIElements;

namespace UIToolkit.Animation.Timeline
{
    // Bridge between AnimatableProperty and the actual VisualElement style.
    // Read current value (for the Record button) and write value (during playback).
    public static class PropertyBinder
    {
        public static void ApplyFloat(VisualElement ve, AnimatableProperty p, float v)
        {
            switch (p)
            {
                case AnimatableProperty.Opacity:
                    ve.style.opacity = v;
                    break;
                case AnimatableProperty.TranslateX:
                {
                    var cur = ve.resolvedStyle.translate;
                    ve.style.translate = new Translate(v, cur.y, 0f);
                    break;
                }
                case AnimatableProperty.TranslateY:
                {
                    var cur = ve.resolvedStyle.translate;
                    ve.style.translate = new Translate(cur.x, v, 0f);
                    break;
                }
                case AnimatableProperty.ScaleX:
                {
                    var cur = ve.resolvedStyle.scale.value;
                    ve.style.scale = new Scale(new Vector3(v, cur.y, 1f));
                    break;
                }
                case AnimatableProperty.ScaleY:
                {
                    var cur = ve.resolvedStyle.scale.value;
                    ve.style.scale = new Scale(new Vector3(cur.x, v, 1f));
                    break;
                }
                case AnimatableProperty.Rotate:
                    ve.style.rotate = new Rotate(new Angle(v, AngleUnit.Degree));
                    break;
                case AnimatableProperty.Width:
                    ve.style.width = v;
                    break;
                case AnimatableProperty.Height:
                    ve.style.height = v;
                    break;
                case AnimatableProperty.MarginLeft:
                    ve.style.marginLeft = v;
                    break;
                case AnimatableProperty.MarginTop:
                    ve.style.marginTop = v;
                    break;
                case AnimatableProperty.BorderRadius:
                    ve.style.borderTopLeftRadius = v;
                    ve.style.borderTopRightRadius = v;
                    ve.style.borderBottomLeftRadius = v;
                    ve.style.borderBottomRightRadius = v;
                    break;
                case AnimatableProperty.Left: ve.style.left = v; break;
                case AnimatableProperty.Top: ve.style.top = v; break;
                case AnimatableProperty.Right: ve.style.right = v; break;
                case AnimatableProperty.Bottom: ve.style.bottom = v; break;
                case AnimatableProperty.MarginRight: ve.style.marginRight = v; break;
                case AnimatableProperty.MarginBottom: ve.style.marginBottom = v; break;
                case AnimatableProperty.PaddingLeft: ve.style.paddingLeft = v; break;
                case AnimatableProperty.PaddingTop: ve.style.paddingTop = v; break;
                case AnimatableProperty.PaddingRight: ve.style.paddingRight = v; break;
                case AnimatableProperty.PaddingBottom: ve.style.paddingBottom = v; break;
                case AnimatableProperty.BorderWidth:
                    ve.style.borderTopWidth = v;
                    ve.style.borderRightWidth = v;
                    ve.style.borderBottomWidth = v;
                    ve.style.borderLeftWidth = v;
                    break;
                case AnimatableProperty.FontSize: ve.style.fontSize = v; break;
                case AnimatableProperty.Display:
                    ve.style.display = v >= 0.5f ? DisplayStyle.Flex : DisplayStyle.None;
                    break;
                case AnimatableProperty.Visible:
                    ve.style.visibility = v >= 0.5f ? Visibility.Visible : Visibility.Hidden;
                    break;
            }
        }

        public static void ApplyColor(VisualElement ve, AnimatableProperty p, Color c)
        {
            switch (p)
            {
                case AnimatableProperty.Color:
                    ve.style.color = c;
                    break;
                case AnimatableProperty.BackgroundColor:
                    ve.style.backgroundColor = c;
                    break;
                case AnimatableProperty.BorderColor:
                    ve.style.borderTopColor = c;
                    ve.style.borderRightColor = c;
                    ve.style.borderBottomColor = c;
                    ve.style.borderLeftColor = c;
                    break;
                case AnimatableProperty.TintColor:
                    ve.style.unityBackgroundImageTintColor = c;
                    break;
            }
        }

        public static float ReadFloat(VisualElement ve, AnimatableProperty p)
        {
            switch (p)
            {
                case AnimatableProperty.Opacity:      return ve.resolvedStyle.opacity;
                case AnimatableProperty.TranslateX:   return ve.resolvedStyle.translate.x;
                case AnimatableProperty.TranslateY:   return ve.resolvedStyle.translate.y;
                case AnimatableProperty.ScaleX:       return ve.resolvedStyle.scale.value.x;
                case AnimatableProperty.ScaleY:       return ve.resolvedStyle.scale.value.y;
                case AnimatableProperty.Rotate:       return ve.resolvedStyle.rotate.angle.value;
                case AnimatableProperty.Width:        return ve.resolvedStyle.width;
                case AnimatableProperty.Height:       return ve.resolvedStyle.height;
                case AnimatableProperty.MarginLeft:   return ve.resolvedStyle.marginLeft;
                case AnimatableProperty.MarginTop:    return ve.resolvedStyle.marginTop;
                case AnimatableProperty.BorderRadius: return ve.resolvedStyle.borderTopLeftRadius;
                case AnimatableProperty.Left:         return ve.resolvedStyle.left;
                case AnimatableProperty.Top:          return ve.resolvedStyle.top;
                case AnimatableProperty.Right:        return ve.resolvedStyle.right;
                case AnimatableProperty.Bottom:       return ve.resolvedStyle.bottom;
                case AnimatableProperty.MarginRight:  return ve.resolvedStyle.marginRight;
                case AnimatableProperty.MarginBottom: return ve.resolvedStyle.marginBottom;
                case AnimatableProperty.PaddingLeft:  return ve.resolvedStyle.paddingLeft;
                case AnimatableProperty.PaddingTop:   return ve.resolvedStyle.paddingTop;
                case AnimatableProperty.PaddingRight: return ve.resolvedStyle.paddingRight;
                case AnimatableProperty.PaddingBottom:return ve.resolvedStyle.paddingBottom;
                case AnimatableProperty.BorderWidth:  return ve.resolvedStyle.borderTopWidth;
                case AnimatableProperty.FontSize:     return ve.resolvedStyle.fontSize;
                case AnimatableProperty.Display:      return ve.resolvedStyle.display == DisplayStyle.Flex ? 1f : 0f;
                case AnimatableProperty.Visible:      return ve.resolvedStyle.visibility == Visibility.Visible ? 1f : 0f;
                default: return PropertyMeta.DefaultFloat(p);
            }
        }

        public static Color ReadColor(VisualElement ve, AnimatableProperty p)
        {
            switch (p)
            {
                case AnimatableProperty.Color:           return ve.resolvedStyle.color;
                case AnimatableProperty.BackgroundColor: return ve.resolvedStyle.backgroundColor;
                case AnimatableProperty.BorderColor:     return ve.resolvedStyle.borderTopColor;
                case AnimatableProperty.TintColor:       return ve.resolvedStyle.unityBackgroundImageTintColor;
                default: return Color.white;
            }
        }
    }
}
