using UnityEngine.UIElements;

namespace UIToolkit.Animation.Timeline
{
    // Play API. Example: clip.Play(rootElement);
    public static class ClipExtensions
    {
        // root is the element under which the clip's named targets are resolved.
        public static ClipPlayer Play(this UIAnimationClip clip, VisualElement root)
        {
            var player = new ClipPlayer(clip, root);
            TweenManager.Register(player);
            return player;
        }

        public static ClipPlayer PlayClip(this VisualElement root, UIAnimationClip clip)
            => clip.Play(root);
    }
}
