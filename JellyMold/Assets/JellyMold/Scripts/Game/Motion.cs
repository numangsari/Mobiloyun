using UnityEngine;

namespace JellyMold.Game
{
    /// <summary>Animation timings in seconds. Tweak feel here, not in the views.</summary>
    public static class Timing
    {
        public const float Move = 0.24f;
        public const float StepStagger = 0.035f;
        public const float Bump = 0.2f;
        public const float EnterMold = 0.32f;
        public const float Settle = 0.28f;
        public const float BoardIntro = 0.45f;
        public const float WinDelay = 0.55f;
    }

    /// <summary>Easing curves (t in 0..1).</summary>
    public static class Ease
    {
        public static float OutCubic(float t)
        {
            t = 1f - t;
            return 1f - t * t * t;
        }

        public static float InOutCubic(float t)
        {
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }

        public static float OutBack(float t, float overshoot = 1.70158f)
        {
            float c3 = overshoot + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + overshoot * u * u;
        }

        /// <summary>Damped oscillation from 1 to 0, used for jelly wobble after an impact.</summary>
        public static float Wobble(float t, float cycles = 2.2f, float damping = 4.5f)
        {
            return Mathf.Exp(-damping * t) * Mathf.Cos(t * cycles * Mathf.PI * 2f);
        }

        /// <summary>0 → 1 → 0 bell curve.</summary>
        public static float Bell(float t)
        {
            return Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
        }
    }
}
