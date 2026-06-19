using System;
using UnityEngine.UIElements;

namespace UIToolkit.Animation.Timeline
{
    // When an animation should fire.
    public enum UITrigger
    {
        PointerEnter,   // hover in
        PointerLeave,   // hover out
        Click,          // single click
        DoubleClick,    // two clicks
        PointerDown,    // press
        PointerUp,      // release
        Hold            // press and hold for holdSeconds
    }

    // Bind a pre-made animation (authored in the tool, called by name) to an
    // element and choose WHEN it plays:
    //
    //   button.PlayOn(UITrigger.PointerEnter, "ButtonHover");
    //   button.PlayOn(UITrigger.Click, "ButtonPress");
    //   icon.PlayOn(UITrigger.Hold, "ChargeUp", holdSeconds: 0.6f);
    //   card.PlayOnHover("CardPop");
    //
    // The named clip is resolved on the element itself, so its element-track name
    // should match the element's name (the default when you author per element).
    public static class UITriggerExtensions
    {
        public static void PlayOn(this VisualElement ve, UITrigger trigger, string animationName, float holdSeconds = 0.5f)
            => PlayOn(ve, trigger, () => UIAnimation.Play(animationName, ve), holdSeconds);

        public static void PlayOn(this VisualElement ve, UITrigger trigger, UIAnimationClip clip, float holdSeconds = 0.5f)
            => PlayOn(ve, trigger, () => { if (clip != null) clip.Play(ve); }, holdSeconds);

        // Most general form: run any action on the chosen trigger.
        public static void PlayOn(this VisualElement ve, UITrigger trigger, Action action, float holdSeconds = 0.5f)
        {
            if (ve == null || action == null) return;
            switch (trigger)
            {
                case UITrigger.PointerEnter: ve.RegisterCallback<PointerEnterEvent>(_ => action()); break;
                case UITrigger.PointerLeave: ve.RegisterCallback<PointerLeaveEvent>(_ => action()); break;
                case UITrigger.PointerDown:  ve.RegisterCallback<PointerDownEvent>(_ => action()); break;
                case UITrigger.PointerUp:    ve.RegisterCallback<PointerUpEvent>(_ => action()); break;
                case UITrigger.Click:        ve.RegisterCallback<ClickEvent>(_ => action()); break;
                case UITrigger.DoubleClick:  ve.RegisterCallback<ClickEvent>(e => { if (e.clickCount == 2) action(); }); break;
                case UITrigger.Hold:         RegisterHold(ve, action, holdSeconds); break;
            }
        }

        // ----- convenience shortcuts -----
        public static void PlayOnHover(this VisualElement ve, string animationName) => ve.PlayOn(UITrigger.PointerEnter, animationName);
        public static void PlayOnClick(this VisualElement ve, string animationName) => ve.PlayOn(UITrigger.Click, animationName);
        public static void PlayOnDoubleClick(this VisualElement ve, string animationName) => ve.PlayOn(UITrigger.DoubleClick, animationName);
        public static void PlayOnHold(this VisualElement ve, string animationName, float holdSeconds = 0.5f) => ve.PlayOn(UITrigger.Hold, animationName, holdSeconds);

        static void RegisterHold(VisualElement ve, Action action, float holdSeconds)
        {
            IVisualElementScheduledItem pending = null;
            ve.RegisterCallback<PointerDownEvent>(_ =>
            {
                pending?.Pause();
                pending = ve.schedule.Execute(() => action()).StartingIn((long)(holdSeconds * 1000f));
            });
            ve.RegisterCallback<PointerUpEvent>(_ => pending?.Pause());
            ve.RegisterCallback<PointerLeaveEvent>(_ => pending?.Pause());
        }
    }
}
