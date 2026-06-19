using NUnit.Framework;
using UnityEngine;
using UIToolkit.Animation.Timeline;
using UIToolkit.Animation.Particles;

namespace UIToolkit.Animation.Tests
{
    public class ValidationTests
    {
        static bool Has(System.Collections.Generic.List<ValidationIssue> issues, ValidationSeverity sev, string contains)
        {
            return issues.Exists(i => i.severity == sev && i.message.Contains(contains));
        }

        [Test]
        public void Clip_EmptyElementName_IsError()
        {
            var clip = ScriptableObject.CreateInstance<UIAnimationClip>();
            var el = clip.GetOrCreateElement("");           // empty name
            el.GetOrCreate(AnimatableProperty.TranslateX).keys.Add(new UIKeyframe(0f, 0f));
            var issues = UIAnimationValidator.ValidateClip(clip);
            Assert.IsTrue(Has(issues, ValidationSeverity.Error, "empty name"));
            Object.DestroyImmediate(clip);
        }

        [Test]
        public void Clip_UnsortedKeys_WarnsAndAutoFixSorts()
        {
            var clip = ScriptableObject.CreateInstance<UIAnimationClip>();
            var pt = clip.GetOrCreateElement("box").GetOrCreate(AnimatableProperty.TranslateX);
            pt.keys.Add(new UIKeyframe(1f, 10f));
            pt.keys.Add(new UIKeyframe(0f, 0f));            // out of order

            var issues = UIAnimationValidator.ValidateClip(clip);
            Assert.IsTrue(Has(issues, ValidationSeverity.Warning, "not sorted"));

            int changes = UIAnimationValidator.AutoFixClip(clip);
            Assert.Greater(changes, 0);
            Assert.AreEqual(0f, pt.keys[0].time, 0.0001f);  // now sorted
            Object.DestroyImmediate(clip);
        }

        [Test]
        public void Clip_AutoFix_RemovesEmptyTrack()
        {
            var clip = ScriptableObject.CreateInstance<UIAnimationClip>();
            var el = clip.GetOrCreateElement("box");
            el.GetOrCreate(AnimatableProperty.Opacity);     // no keys -> should be removed
            UIAnimationValidator.AutoFixClip(clip);
            Assert.AreEqual(0, clip.elements.Count);        // empty track removed, then empty element removed
            Object.DestroyImmediate(clip);
        }

        [Test]
        public void Particles_ZeroMax_IsError()
        {
            var cfg = ScriptableObject.CreateInstance<ParticleSystemConfig>();
            cfg.maxParticles = 0;
            var issues = UIAnimationValidator.ValidateParticles(cfg);
            Assert.IsTrue(Has(issues, ValidationSeverity.Error, "maxParticles"));
            Object.DestroyImmediate(cfg);
        }
    }
}
