using UnityEngine;

namespace UIToolkit.Animation
{
    // Toggleable trace logger for the animation system. Turn it on with the
    // scene's Setup > Debug > Log toggle (which sets UILog.Enabled). When off it
    // costs nothing. Use it to see exactly where runtime playback goes/stops.
    public static class UILog
    {
        public static bool Enabled;

        public static void Log(string msg)
        {
            if (Enabled) Debug.Log("[UIAnim] " + msg);
        }

        public static void Warn(string msg)
        {
            if (Enabled) Debug.LogWarning("[UIAnim] " + msg);
        }
    }
}
