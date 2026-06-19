using System;
using System.Runtime.CompilerServices;

namespace UIToolkit.Animation
{
    // Lets you await any tween directly:
    //
    //   async void Intro() {
    //       await panel.FadeIn();
    //       await title.ScaleTo(1f, 0.3f);
    //   }
    //
    // The continuation resumes on the main thread when the tween completes/dies.
    public static class TweenAwaitExtensions
    {
        public static TweenAwaiter GetAwaiter(this ITweenable t) => new TweenAwaiter(t);
    }

    public readonly struct TweenAwaiter : INotifyCompletion
    {
        readonly ITweenable _t;
        public TweenAwaiter(ITweenable t) { _t = t; }

        public bool IsCompleted => _t == null || _t.State == TweenState.Completed || _t.State == TweenState.Killed;

        public void OnCompleted(Action continuation)
        {
            if (IsCompleted) { continuation(); return; }
            var mgr = TweenManager.Instance;
            if (mgr != null) mgr.WhenComplete(_t, continuation);
            else continuation();
        }

        public void GetResult() { }
    }
}
