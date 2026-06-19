using System.Collections.Generic;
using UnityEngine;

namespace UIToolkit.Animation.Timeline
{
    // A registry asset that lists animation clips by reference. Place one (or more)
    // under a Resources folder; UIAnimation auto-loads them at startup and registers
    // every clip by its asset name. This keeps clips callable by name from code
    // without forcing every clip into Resources (only referenced clips ship).
    //
    // Create -> UI Toolkit -> Animation Library
    [CreateAssetMenu(fileName = "UIAnimationLibrary", menuName = "UI Toolkit/Animation Library")]
    public class UIAnimationLibrary : ScriptableObject
    {
        public List<UIAnimationClip> clips = new List<UIAnimationClip>();

        public bool Contains(UIAnimationClip clip) => clip != null && clips.Contains(clip);

        public void AddUnique(UIAnimationClip clip)
        {
            if (clip != null && !clips.Contains(clip)) clips.Add(clip);
        }
    }
}
