using System.Collections;

namespace UIToolkit.Animation
{
    // Coroutine helpers so animations can be sequenced from gameplay code:
    //
    //   yield return element.FadeIn().WaitForCompletion();
    //   yield return clip.Play(root).WaitForCompletion();
    //
    // Works for any ITweenable (Tween, Sequence, ClipPlayer, ParticleEmitter).
    public static class TweenCoroutineExtensions
    {
        // Yields until the tween reports Completed or Killed.
        public static IEnumerator WaitForCompletion(this ITweenable t)
        {
            if (t == null) yield break;
            while (t.State != TweenState.Completed && t.State != TweenState.Killed)
                yield return null;
        }

        // Yields until the tween finishes or the timeout (seconds) elapses.
        public static IEnumerator WaitForCompletion(this ITweenable t, float timeout)
        {
            if (t == null) yield break;
            float elapsed = 0f;
            while (t.State != TweenState.Completed && t.State != TweenState.Killed)
            {
                elapsed += UnityEngine.Time.unscaledDeltaTime;
                if (elapsed >= timeout) yield break;
                yield return null;
            }
        }
    }
}
