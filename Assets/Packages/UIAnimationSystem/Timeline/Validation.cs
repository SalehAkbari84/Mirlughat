using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UIToolkit.Animation.Particles;

namespace UIToolkit.Animation.Timeline
{
    public enum ValidationSeverity { Info, Warning, Error }

    public struct ValidationIssue
    {
        public ValidationSeverity severity;
        public string message;
        public Object target;   // asset/object to ping in the editor

        public ValidationIssue(ValidationSeverity s, string m, Object t = null)
        {
            severity = s; message = m; target = t;
        }
    }

    // Data/configuration validator. Runs the same checks the editor surfaces, but
    // lives in the runtime assembly so you can also validate from code or CI.
    public static class UIAnimationValidator
    {
        // ---------------- public entry points ----------------
        public static List<ValidationIssue> ValidateClip(UIAnimationClip clip)
        {
            var issues = new List<ValidationIssue>();
            CheckClip(clip, issues, null);
            return issues;
        }

        public static List<ValidationIssue> ValidateParticles(ParticleSystemConfig cfg)
        {
            var issues = new List<ValidationIssue>();
            CheckParticles(cfg, issues);
            return issues;
        }

        // root: a cloned or live tree used to verify element names exist. May be null
        // (name checks are then skipped with a note).
        public static List<ValidationIssue> ValidateScene(UISceneAnimation scene, VisualElement root)
        {
            var issues = new List<ValidationIssue>();
            if (scene == null)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Error, "Scene asset is null."));
                return issues;
            }

            if (scene.uxml == null)
                issues.Add(new ValidationIssue(ValidationSeverity.Error, "Scene has no UXML assigned.", scene));

            HashSet<string> names = CollectNames(root);
            bool canCheckNames = root != null;
            if (!canCheckNames)
                issues.Add(new ValidationIssue(ValidationSeverity.Info, "Element-name checks skipped (preview not loaded)."));

            // clip bindings
            var clipIds = new HashSet<string>();
            foreach (var c in scene.clips)
            {
                string label = string.IsNullOrEmpty(c.id) ? "(unnamed clip binding)" : $"clip '{c.id}'";
                if (string.IsNullOrEmpty(c.id))
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, "A clip binding has an empty id.", scene));
                else if (!clipIds.Add(c.id))
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"Duplicate clip binding id '{c.id}'.", scene));

                if (c.clip == null)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"{label} has no clip assigned.", scene));
                    continue;
                }

                if (canCheckNames && !string.IsNullOrEmpty(c.rootElementName) && !names.Contains(c.rootElementName))
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"{label}: root element '{c.rootElementName}' not found in UXML.", c.clip));

                // element targets of the clip must exist under the (resolve) root
                if (canCheckNames)
                    foreach (var et in c.clip.elements)
                        if (!string.IsNullOrEmpty(et.elementName) && !names.Contains(et.elementName))
                            issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"{label}: target element '{et.elementName}' not found in UXML.", c.clip));

                CheckClip(c.clip, issues, null);
            }

            // particle bindings
            var partIds = new HashSet<string>();
            foreach (var p in scene.particles)
            {
                string label = string.IsNullOrEmpty(p.id) ? "(unnamed particle binding)" : $"particles '{p.id}'";
                if (string.IsNullOrEmpty(p.id))
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, "A particle binding has an empty id.", scene));
                else if (!partIds.Add(p.id))
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"Duplicate particle binding id '{p.id}'.", scene));

                if (p.particleConfig == null)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"{label} has no preset assigned.", scene));
                    continue;
                }

                if (canCheckNames && !string.IsNullOrEmpty(p.hostElementName) && !names.Contains(p.hostElementName))
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"{label}: host '{p.hostElementName}' not found in UXML.", p.particleConfig));

                CheckParticles(p.particleConfig, issues);
            }

            return issues;
        }

        // ---------------- checks ----------------
        static void CheckClip(UIAnimationClip clip, List<ValidationIssue> issues, System.Func<string, bool> nameExists)
        {
            if (clip == null)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Warning, "Clip is null."));
                return;
            }

            string cn = clip.name;
            if (clip.elements == null || clip.elements.Count == 0)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"Clip '{cn}' has no element tracks.", clip));
                return;
            }

            if (clip.playbackSpeed <= 0f)
                issues.Add(new ValidationIssue(ValidationSeverity.Error, $"Clip '{cn}': playbackSpeed must be > 0.", clip));

            var seenElements = new HashSet<string>();
            float maxTime = 0f;
            foreach (var el in clip.elements)
            {
                if (string.IsNullOrEmpty(el.elementName))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, $"Clip '{cn}': an element track has an empty name.", clip));
                    continue;
                }
                if (!seenElements.Add(el.elementName))
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"Clip '{cn}': duplicate element track '{el.elementName}'.", clip));

                if (nameExists != null && !nameExists(el.elementName))
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"Clip '{cn}': element '{el.elementName}' not found.", clip));

                foreach (var pt in el.properties)
                {
                    if (pt.keys == null || pt.keys.Count == 0)
                    {
                        issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"Clip '{cn}' / {el.elementName} / {PropertyMeta.DisplayName(pt.property)}: track has no keyframes.", clip));
                        continue;
                    }

                    float prev = float.NegativeInfinity;
                    bool unsorted = false, dupTime = false, negTime = false, nullCurve = false;
                    foreach (var k in pt.keys)
                    {
                        if (k.time < 0f) negTime = true;
                        if (k.time < prev) unsorted = true;
                        if (Mathf.Approximately(k.time, prev)) dupTime = true;
                        if (k.useCurve && k.curve == null) nullCurve = true;
                        prev = k.time;
                        if (k.time > maxTime) maxTime = k.time;
                    }

                    string where = $"Clip '{cn}' / {el.elementName} / {PropertyMeta.DisplayName(pt.property)}";
                    if (negTime) issues.Add(new ValidationIssue(ValidationSeverity.Error, $"{where}: has a key with negative time.", clip));
                    if (unsorted) issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"{where}: keys are not sorted by time (auto-fixable).", clip));
                    if (dupTime) issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"{where}: two keys share the same time.", clip));
                    if (nullCurve) issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"{where}: 'use curve' is on but the curve is empty.", clip));

                    if (clip.relative && pt.ValueType == PropertyValueType.Color)
                        issues.Add(new ValidationIssue(ValidationSeverity.Info, $"{where}: relative mode does not affect color tracks.", clip));
                }
            }

            if (maxTime <= 0f)
                issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"Clip '{cn}': total duration is 0 (no keys past t=0).", clip));
        }

        static void CheckParticles(ParticleSystemConfig cfg, List<ValidationIssue> issues)
        {
            if (cfg == null)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Warning, "Particle config is null."));
                return;
            }

            string pn = cfg.name;
            if (cfg.maxParticles <= 0)
                issues.Add(new ValidationIssue(ValidationSeverity.Error, $"Particles '{pn}': maxParticles must be > 0.", cfg));

            bool hasBursts = cfg.bursts != null && cfg.bursts.Count > 0;
            if (cfg.emissionRate <= 0f && !hasBursts && cfg.warmup <= 0f)
                issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"Particles '{pn}': emissionRate is 0 and there are no bursts (nothing will spawn).", cfg));

            if (!cfg.loop && cfg.duration <= 0f)
                issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"Particles '{pn}': non-looping with duration 0.", cfg));

            if (cfg.modules == null || cfg.modules.Count == 0)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"Particles '{pn}': no modules (particles will be static).", cfg));
            }
            else
            {
                bool hasLifetime = false, hasNull = false;
                foreach (var m in cfg.modules)
                {
                    if (m == null) { hasNull = true; continue; }
                    if (m is LifetimeModule) hasLifetime = true;
                }
                if (hasNull) issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"Particles '{pn}': module list contains an empty (null) entry (auto-fixable).", cfg));
                if (!hasLifetime) issues.Add(new ValidationIssue(ValidationSeverity.Info, $"Particles '{pn}': no Lifetime module; particles use the default 1s lifetime.", cfg));
            }

            if (cfg.visualKind == ParticleVisualKind.Image && cfg.sprite == null)
                issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"Particles '{pn}': Image kind but no sprite assigned.", cfg));
            if (cfg.visualKind == ParticleVisualKind.CustomClass && string.IsNullOrEmpty(cfg.customUssClass))
                issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"Particles '{pn}': CustomClass kind but no USS class set.", cfg));
            if (cfg.meshRenderer && (cfg.visualKind == ParticleVisualKind.Image || cfg.visualKind == ParticleVisualKind.CustomClass))
                issues.Add(new ValidationIssue(ValidationSeverity.Info, $"Particles '{pn}': mesh renderer ignores Image/CustomClass and falls back to per-element.", cfg));
        }

        // ---------------- auto-fixes (safe, data-only) ----------------
        // Returns the number of changes made. Sorts keys, removes empty tracks/
        // elements and null particle modules. Caller is responsible for Undo/SetDirty.
        public static int AutoFixClip(UIAnimationClip clip)
        {
            if (clip == null || clip.elements == null) return 0;
            int changes = 0;
            for (int e = clip.elements.Count - 1; e >= 0; e--)
            {
                var el = clip.elements[e];
                for (int p = el.properties.Count - 1; p >= 0; p--)
                {
                    var pt = el.properties[p];
                    if (pt.keys == null || pt.keys.Count == 0) { el.properties.RemoveAt(p); changes++; continue; }
                    if (!IsSorted(pt)) { pt.SortKeys(); changes++; }
                }
                if (el.properties.Count == 0) { clip.elements.RemoveAt(e); changes++; }
            }
            return changes;
        }

        public static int AutoFixParticles(ParticleSystemConfig cfg)
        {
            if (cfg == null || cfg.modules == null) return 0;
            int before = cfg.modules.Count;
            cfg.modules.RemoveAll(m => m == null);
            return before - cfg.modules.Count;
        }

        // ---------------- helpers ----------------
        static bool IsSorted(PropertyTrack pt)
        {
            for (int i = 1; i < pt.keys.Count; i++)
                if (pt.keys[i].time < pt.keys[i - 1].time) return false;
            return true;
        }

        static HashSet<string> CollectNames(VisualElement root)
        {
            var set = new HashSet<string>();
            if (root == null) return set;
            if (!string.IsNullOrEmpty(root.name)) set.Add(root.name);
            root.Query<VisualElement>().ForEach(ve =>
            {
                if (!string.IsNullOrEmpty(ve.name)) set.Add(ve.name);
            });
            return set;
        }
    }
}
