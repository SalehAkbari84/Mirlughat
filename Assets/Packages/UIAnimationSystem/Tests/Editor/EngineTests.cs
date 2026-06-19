using NUnit.Framework;
using UnityEngine;
using UIToolkit.Animation;
using UIToolkit.Animation.Timeline;

namespace UIToolkit.Animation.Tests
{
    // Edit-mode tests for the pure parts of the engine (no scene objects needed).
    public class EngineTests
    {
        const float Eps = 0.001f;

        static System.Func<float, float, float, float> LerpF => (a, b, t) => a + (b - a) * t;

        // ---------------- Easing ----------------
        [Test]
        public void Easing_Linear_IsIdentity()
        {
            Assert.AreEqual(0f, Easing.Evaluate(Ease.Linear, 0f), Eps);
            Assert.AreEqual(0.3f, Easing.Evaluate(Ease.Linear, 0.3f), Eps);
            Assert.AreEqual(1f, Easing.Evaluate(Ease.Linear, 1f), Eps);
        }

        [Test]
        public void Easing_ClampsInput()
        {
            Assert.AreEqual(0f, Easing.Evaluate(Ease.Linear, -5f), Eps);
            Assert.AreEqual(1f, Easing.Evaluate(Ease.Linear, 5f), Eps);
        }

        [Test]
        public void Easing_Endpoints_AreZeroAndOne()
        {
            foreach (Ease e in System.Enum.GetValues(typeof(Ease)))
            {
                Assert.AreEqual(0f, Easing.Evaluate(e, 0f), Eps, $"{e} at 0");
                Assert.AreEqual(1f, Easing.Evaluate(e, 1f), Eps, $"{e} at 1");
            }
        }

        // ---------------- Tween ----------------
        [Test]
        public void Tween_Linear_InterpolatesMidpoint()
        {
            float v = 0f;
            var t = new Tween<float>(() => v, x => v = x, 10f, 1f, LerpF).From(0f).SetEase(Ease.Linear);
            t.Update(0.5f);
            Assert.AreEqual(5f, v, Eps);
        }

        [Test]
        public void Tween_Completes_AndReachesEnd()
        {
            float v = 0f;
            var t = new Tween<float>(() => v, x => v = x, 10f, 1f, LerpF).From(0f).SetEase(Ease.Linear);
            bool done = t.Update(1f);
            Assert.IsTrue(done);
            Assert.AreEqual(10f, v, Eps);
            Assert.AreEqual(TweenState.Completed, t.State);
        }

        [Test]
        public void Tween_Incremental_ExtendsRangeEachLoop()
        {
            // Regression test: Incremental loop must keep advancing the end value.
            float v = 0f;
            var t = new Tween<float>(() => v, x => v = x, 1f, 1f, LerpF)
                .From(0f).SetEase(Ease.Linear).SetLoops(-1, LoopType.Incremental);
            t.Update(1f);        // finish loop 1: v == 1, range becomes [1, 2]
            Assert.AreEqual(1f, v, Eps);
            t.Update(0.5f);      // half of loop 2: lerp(1, 2, 0.5) == 1.5
            Assert.AreEqual(1.5f, v, Eps);
        }

        [Test]
        public void Tween_Yoyo_ReversesOnSecondLoop()
        {
            float v = 0f;
            var t = new Tween<float>(() => v, x => v = x, 10f, 1f, LerpF)
                .From(0f).SetEase(Ease.Linear).SetLoops(2, LoopType.Yoyo);
            t.Update(1f);        // forward complete: v == 10
            Assert.AreEqual(10f, v, Eps);
            t.Update(0.5f);      // reverse half: v == 5
            Assert.AreEqual(5f, v, Eps);
        }

        // ---------------- Sequence ----------------
        [Test]
        public void Sequence_TotalDuration_IsSumOfAppended()
        {
            float a = 0f, b = 0f;
            var t1 = new Tween<float>(() => a, x => a = x, 1f, 1f, LerpF);
            var t2 = new Tween<float>(() => b, x => b = x, 1f, 2f, LerpF);
            var seq = new Sequence().Append(t1).Append(t2);
            Assert.AreEqual(3f, seq.TotalDuration, Eps);
        }

        // ---------------- PropertyTrack sampling ----------------
        [Test]
        public void PropertyTrack_SampleFloat_LinearInterpolates()
        {
            var pt = new PropertyTrack { property = AnimatableProperty.TranslateX };
            pt.keys.Add(new UIKeyframe(0f, 0f, Ease.Linear));
            pt.keys.Add(new UIKeyframe(1f, 10f, Ease.Linear));
            Assert.AreEqual(0f, pt.SampleFloat(0f), Eps);
            Assert.AreEqual(5f, pt.SampleFloat(0.5f), Eps);
            Assert.AreEqual(10f, pt.SampleFloat(1f), Eps);
        }

        [Test]
        public void PropertyTrack_SampleFloat_ClampsOutsideRange()
        {
            var pt = new PropertyTrack { property = AnimatableProperty.Opacity };
            pt.keys.Add(new UIKeyframe(1f, 2f, Ease.Linear));
            pt.keys.Add(new UIKeyframe(2f, 4f, Ease.Linear));
            Assert.AreEqual(2f, pt.SampleFloat(0f), Eps);   // before first key
            Assert.AreEqual(4f, pt.SampleFloat(5f), Eps);   // after last key
        }

        [Test]
        public void Clip_Duration_IsLatestKeyTime()
        {
            var clip = ScriptableObject.CreateInstance<UIAnimationClip>();
            var el = clip.GetOrCreateElement("box");
            var pt = el.GetOrCreate(AnimatableProperty.TranslateX);
            pt.keys.Add(new UIKeyframe(0f, 0f));
            pt.keys.Add(new UIKeyframe(1.5f, 100f));
            Assert.AreEqual(1.5f, clip.Duration, Eps);
            Object.DestroyImmediate(clip);
        }
    }
}
