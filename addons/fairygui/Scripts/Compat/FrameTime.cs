// FairyGUI for Godot - compatibility layer (not ported; written for the Godot port).
// Replacement for UnityEngine.Time. Named FrameTime (not Time) to avoid shadowing Godot.Time.
// Advanced exactly once per frame by StageEngine._Process, before Timers/TweenManager/Stage update.
using Godot;

namespace FairyGUI
{
    public static class FrameTime
    {
        /// <summary>Scaled delta (Engine.TimeScale applied). Unity: Time.deltaTime</summary>
        public static float deltaTime;
        /// <summary>Real delta. Unity: Time.unscaledDeltaTime</summary>
        public static float unscaledDeltaTime;
        /// <summary>Scaled time since start. Unity: Time.time</summary>
        public static float time;
        /// <summary>Real time since start. Unity: Time.unscaledTime</summary>
        public static float unscaledTime;
        /// <summary>Starts at 1 on the first advanced frame. Unity: Time.frameCount</summary>
        public static int frameCount;

        /// <summary>Unity: Time.timeScale</summary>
        public static float timeScale
        {
            get => (float)Engine.TimeScale;
            set => Engine.TimeScale = value;
        }

        /// <summary>Unity: Time.realtimeSinceStartup. Valid at any time, not frame-quantized.</summary>
        public static float realtimeSinceStartup => Time.GetTicksUsec() / 1000000f;

        /// <summary>Unity: Application.targetFrameRate (0 = unlimited).</summary>
        public static int targetFrameRate => Engine.MaxFps;

        static ulong _lastTicksUsec;

        /// <summary>Called by StageEngine once per frame. delta is the scaled delta Godot passed to _Process.</summary>
        public static void Advance(double delta)
        {
            ulong now = Time.GetTicksUsec();
            float real = _lastTicksUsec == 0 ? (float)delta : (now - _lastTicksUsec) / 1000000f;
            _lastTicksUsec = now;

            deltaTime = (float)delta;
            unscaledDeltaTime = real;
            time += deltaTime;
            unscaledTime += unscaledDeltaTime;
            frameCount++;
        }
    }
}
