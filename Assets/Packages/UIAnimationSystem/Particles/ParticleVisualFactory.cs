using UnityEngine;
using UnityEngine.UIElements;

namespace UIToolkit.Animation.Particles
{
    // Builds the VisualElement used for each pooled particle, based on the
    // config's visual kind. Extend here to support new particle appearances.
    public static class ParticleVisualFactory
    {
        public static VisualElement Create(ParticleSystemConfig config)
        {
            var ve = new VisualElement();
            ve.style.position = Position.Absolute;
            ve.pickingMode = PickingMode.Ignore;

            switch (config.visualKind)
            {
                case ParticleVisualKind.Circle:
                    ve.style.borderTopLeftRadius = 9999;
                    ve.style.borderTopRightRadius = 9999;
                    ve.style.borderBottomLeftRadius = 9999;
                    ve.style.borderBottomRightRadius = 9999;
                    ve.style.backgroundColor = Color.white;
                    break;

                case ParticleVisualKind.Square:
                    ve.style.backgroundColor = Color.white;
                    break;

                case ParticleVisualKind.Image:
                    if (config.sprite != null)
                        ve.style.backgroundImage = new StyleBackground(config.sprite);
                    break;

                case ParticleVisualKind.CustomClass:
                    if (!string.IsNullOrEmpty(config.customUssClass))
                        ve.AddToClassList(config.customUssClass);
                    break;
            }
            return ve;
        }
    }
}
