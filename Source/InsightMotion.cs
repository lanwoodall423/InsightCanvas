using System;

namespace InsightCanvas
{
    /// <summary>Delta-time based, interruptible motion helpers for custom components.</summary>
    public static class InsightMotion
    {
        public static float Approach(float current, float target, float deltaTime, float speed, bool reducedMotion)
        {
            if (reducedMotion || deltaTime <= 0f) return target;
            float amount = 1f - (float)Math.Exp(-Math.Max(0.01f, speed) * deltaTime);
            return current + (target - current) * amount;
        }

        public static InsightPoint Approach(InsightPoint current, InsightPoint target, float deltaTime, float speed, bool reducedMotion)
        {
            return new InsightPoint(Approach(current.X, target.X, deltaTime, speed, reducedMotion),
                Approach(current.Y, target.Y, deltaTime, speed, reducedMotion));
        }

        public static float Smooth(float start, float end, float progress, bool reducedMotion)
        {
            if (reducedMotion) return end;
            progress = progress < 0f ? 0f : progress > 1f ? 1f : progress;
            progress = progress * progress * (3f - 2f * progress);
            return start + (end - start) * progress;
        }
    }
}
