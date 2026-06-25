using UnityEngine;
using UnityEngine.UIElements;
using UIToolkit.Animation.Particles;

namespace UIToolkit.Animation.Timeline
{
    // Plays a UISceneAnimation's ordered playlist (Play Order). Each step plays in
    // order and can repeat (loops: 1 = once, 0 = forever, N). A step can play
    // together with the previous one (parallel), and can play a clip and/or fire
    // a particle effect at its start time.
    public static class UISequenceRunner
    {
        // Build the sequence without registering it (used for editor preview).
        public static Sequence Build(UISceneAnimation scene, VisualElement root)
        {
            if (scene == null || root == null || scene.sequence == null) return null;

            UILog.Log($"Sequence.Build: {scene.sequence.Count} step(s) under root '{root.name}'.");
            var seq = new Sequence();
            float endSoFar = 0f;    // where a sequential ("after previous") step begins
            float prevStart = 0f;   // start time of the previous step (for parallel)
            bool first = true;

            foreach (var step in scene.sequence)
            {
                if (step == null || (step.clip == null && step.particle == null)) continue;

                // "After previous" = sequential; "with previous" = parallel (same
                // start as the previous step). delay offsets either case.
                float start = (first || step.waitForPrevious)
                    ? endSoFar + step.delay
                    : prevStart + step.delay;

                // clip part
                if (step.clip != null)
                {
                    VisualElement r = string.IsNullOrEmpty(step.rootElementName)
                        ? root : root.Q<VisualElement>(step.rootElementName) ?? root;
                    var player = new ClipPlayer(step.clip, r)
                        .SetLoopOverride(step.loops != 1, step.loops, step.loopType);
                    seq.Insert(start, player);
                    endSoFar = Mathf.Max(endSoFar, start + player.TotalDuration);
                }

                // particle part: fire at the step's start time
                if (step.particle != null)
                {
                    var cfg = step.particle;
                    int burst = step.particleBurst;
                    VisualElement host = string.IsNullOrEmpty(step.particleHost)
                        ? root : root.Q<VisualElement>(step.particleHost) ?? root;
                    seq.InsertCallback(start, () =>
                    {
                        if (burst > 0) host.Burst(cfg, burst);
                        else host.SpawnParticles(cfg);
                    });
                }

                UILog.Log($"  step '{step.id}': clip={(step.clip ? step.clip.name : "-")}, particle={(step.particle ? step.particle.name : "-")}, start={start:0.00}s, {(step.waitForPrevious ? "after prev" : "with prev")}.");

                prevStart = start;
                endSoFar = Mathf.Max(endSoFar, start);
                first = false;

                // A clip that repeats forever is terminal: later steps never play.
                if (step.clip != null && step.loops == 0) break;
            }
            return seq;
        }

        // Build, register with the manager, and start playing.
        public static Sequence Play(UISceneAnimation scene, VisualElement root)
        {
            var seq = Build(scene, root);
            if (seq != null) TweenManager.Register(seq);
            return seq;
        }
    }
}
