using System;
using UnityEngine.UIElements;
using UIToolkit.Animation.Particles;

namespace UIToolkit.Animation.Timeline
{
    // Schedule clips, tweens, particles and callbacks on a single timeline from
    // code, then play them together:
    //
    //   UITimeline.New()
    //       .Clip(0f, introClip, root)
    //       .Spawn(0.3f, host, confetti, burst: 80)
    //       .Call(0.3f, () => audio.Play(sfx))
    //       .Clip(0.5f, titleClip, root)
    //       .Play();
    //
    // Built on Sequence, so it ticks in sync with everything else.
    public sealed class UITimeline
    {
        readonly Sequence _seq = new Sequence();

        public static UITimeline New() => new UITimeline();

        // Play a clip at an absolute time, resolved under root.
        public UITimeline Clip(float at, UIAnimationClip clip, VisualElement root)
        {
            if (clip != null && root != null) _seq.Insert(at, new ClipPlayer(clip, root));
            return this;
        }

        // Schedule any tweenable (a Tween, Sequence, etc.) at an absolute time.
        public UITimeline Add(float at, ITweenable tweenable)
        {
            if (tweenable != null) _seq.Insert(at, tweenable);
            return this;
        }

        // Fire a callback at an absolute time.
        public UITimeline Call(float at, Action action)
        {
            _seq.InsertCallback(at, action);
            return this;
        }

        // Spawn particles at an absolute time (burst > 0 for a one-shot).
        public UITimeline Spawn(float at, VisualElement host, ParticleSystemConfig config, int burst = 0)
        {
            _seq.InsertCallback(at, () =>
            {
                if (host == null || config == null) return;
                if (burst > 0) host.Burst(config, burst);
                else host.SpawnParticles(config);
            });
            return this;
        }

        public UITimeline Loop(int loops = 0)
        {
            _seq.SetLoops(loops <= 0 ? -1 : loops);
            return this;
        }

        // Register with the manager and start playing. Returns the underlying
        // Sequence so you can chain OnComplete, Kill, etc.
        public Sequence Play()
        {
            TweenManager.Register(_seq);
            return _seq;
        }

        public Sequence AsSequence => _seq;
    }
}
