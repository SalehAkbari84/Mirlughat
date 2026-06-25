using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIToolkit.Animation.Timeline
{
    // When an animation should fire.
    public enum UITrigger
    {
        PointerEnter,   // hover in (desktop only - touch has no hover)
        PointerLeave,   // hover out (desktop only)
        Click,          // single click / tap
        DoubleClick,    // two clicks / double tap
        PointerDown,    // press / touch down
        PointerUp,      // release / touch up
        Hold            // press and hold for holdSeconds (long press)
    }

    // Which platform an interaction trigger applies to.
    public enum TriggerPlatform { Both, DesktopOnly, MobileOnly }

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
            => BindClip(ve, trigger, () => UIAnimation.Get(animationName), 1, LoopType.Restart, true, 0f, holdSeconds);

        public static void PlayOn(this VisualElement ve, UITrigger trigger, UIAnimationClip clip, float holdSeconds = 0.5f)
            => BindClip(ve, trigger, () => clip, 1, LoopType.Restart, true, 0f, holdSeconds);

        // Full clip trigger with spam control (used by the scene director).
        //   ignoreWhilePlaying = true  -> re-firing while the clip still plays is ignored.
        //   ignoreWhilePlaying = false -> re-firing restarts it (still a single instance).
        //   cooldown                   -> minimum seconds between fires (0 = none).
        public static void PlayOnClip(this VisualElement ve, UITrigger trigger, UIAnimationClip clip,
            int loops, LoopType loopType, bool ignoreWhilePlaying, float cooldown, float holdSeconds)
            => BindClip(ve, trigger, () => clip, loops, loopType, ignoreWhilePlaying, cooldown, holdSeconds);

        // Wires a clip to an event with anti-spam: never stacks players, and
        // optionally ignores re-fires while playing or within a cooldown window.
        static void BindClip(VisualElement ve, UITrigger trigger, Func<UIAnimationClip> clipGetter,
            int loops, LoopType loopType, bool ignoreWhilePlaying, float cooldown, float holdSeconds)
        {
            if (ve == null) return;
            ClipPlayer current = null;
            float lastFire = -9999f;
            PlayOn(ve, trigger, () =>
            {
                if (cooldown > 0f && Time.unscaledTime - lastFire < cooldown) return;

                var clip = clipGetter();
                if (clip == null) return;

                if (current != null && current.State == TweenState.Running)
                {
                    if (ignoreWhilePlaying) return;   // spam-proof: let it finish
                    current.Kill();                   // otherwise restart (single instance)
                }

                current = clip.Play(ve).SetLoopOverride(loops != 1, loops, loopType);
                lastFire = Time.unscaledTime;
            }, holdSeconds);
        }

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
