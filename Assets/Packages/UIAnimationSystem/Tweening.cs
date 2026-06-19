using System;

namespace UIToolkit.Animation
{
    /// <summary>
    /// Static entry point for building sequences and killing tweens.
    /// Example: var seq = Tweening.Sequence(); seq.Append(...).Join(...).AppendCallback(...);
    /// </summary>
    public static class Tweening
    {
        /// <summary>Create a sequence already registered with the manager.</summary>
        public static Sequence Sequence()
        {
            var s = new Sequence();
            TweenManager.Register(s);
            return s;
        }

        /// <summary>Kill all active tweens.</summary>
        public static void KillAll(bool complete = false) => TweenManager.Instance?.KillAll(null, complete);

        /// <summary>Kill all active tweens with the given id.</summary>
        public static void Kill(object id, bool complete = false) => TweenManager.Instance?.KillAll(id, complete);
    }
}
