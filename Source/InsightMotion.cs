using System;
using System.Collections.Generic;

namespace InsightCanvas
{
    /// <summary>Small deterministic easing set used by document-owned effects.</summary>
    public enum InsightMotionEasing
    {
        Linear,
        Smooth,
        EaseOut,
        Approach
    }

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

        /// <summary>Evaluates one of the compact, deterministic easing modes.</summary>
        public static float Eased(float progress, InsightMotionEasing easing)
        {
            progress = progress < 0f ? 0f : progress > 1f ? 1f : progress;
            switch (easing)
            {
                case InsightMotionEasing.Smooth:
                    return progress * progress * (3f - 2f * progress);
                case InsightMotionEasing.EaseOut:
                    return 1f - (1f - progress) * (1f - progress);
                default:
                    return progress;
            }
        }
    }

    /// <summary>Document-owned keyed transitions and short-lived feedback flashes.</summary>
    public sealed class InsightUiEffects
    {
        private readonly Dictionary<string, float> transitions = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, InsightUiFlashState> flashes =
            new Dictionary<string, InsightUiFlashState>(StringComparer.Ordinal);

        /// <summary>Gets the number of currently active transitions or flashes.</summary>
        public int ActiveCount => transitions.Count + flashes.Count;

        /// <summary>Moves a keyed value toward a target; changing the target naturally interrupts the prior motion.</summary>
        public float Transition(string id, float target, float deltaTime, float duration, bool reducedMotion,
            InsightMotionEasing easing = InsightMotionEasing.Smooth)
        {
            if (string.IsNullOrEmpty(id)) return target;
            float current;
            if (!transitions.TryGetValue(id, out current))
            {
                transitions[id] = target;
                return target;
            }
            if (reducedMotion || duration <= 0f || deltaTime <= 0f)
            {
                transitions[id] = target;
                return target;
            }

            float progress = Math.Min(1f, Math.Max(0f, deltaTime / duration));
            float amount;
            if (easing == InsightMotionEasing.Approach)
                return transitions[id] = Approach(current, target, deltaTime, Math.Max(0.01f, 4.6f / duration));
            amount = InsightMotion.Eased(progress, easing);
            current += (target - current) * amount;
            if (Math.Abs(target - current) < 0.001f) current = target;
            transitions[id] = current;
            return current;
        }

        /// <summary>Starts or refreshes a keyed highlight flash.</summary>
        public void Flash(string id, float duration = 0.18f)
        {
            if (string.IsNullOrEmpty(id)) return;
            InsightUiFlashState flash;
            if (!flashes.TryGetValue(id, out flash))
            {
                flash = new InsightUiFlashState();
                flashes[id] = flash;
            }
            flash.Duration = Math.Max(0.01f, duration);
            flash.Remaining = flash.Duration;
        }

        /// <summary>Advances a flash and returns its remaining normalized intensity.</summary>
        public float FlashProgress(string id, float deltaTime, bool reducedMotion)
        {
            InsightUiFlashState flash;
            if (string.IsNullOrEmpty(id) || !flashes.TryGetValue(id, out flash)) return 0f;
            if (reducedMotion)
            {
                flashes.Remove(id);
                return 1f;
            }
            flash.Remaining -= Math.Max(0f, deltaTime);
            if (flash.Remaining <= 0f)
            {
                flashes.Remove(id);
                return 0f;
            }
            return Math.Min(1f, flash.Remaining / flash.Duration);
        }

        /// <summary>Stops all document-owned transitions and feedback.</summary>
        public void Clear()
        {
            transitions.Clear();
            flashes.Clear();
        }

        private static float Approach(float current, float target, float deltaTime, float speed)
        {
            float amount = 1f - (float)Math.Exp(-Math.Max(0.01f, speed) * Math.Max(0f, deltaTime));
            return current + (target - current) * amount;
        }

        private sealed class InsightUiFlashState
        {
            public float Remaining;
            public float Duration;
        }
    }

    /// <summary>Severity for a document/window-local toast.</summary>
    public enum InsightToastSeverity
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>Small replacement-style toast service owned by one document.</summary>
    public sealed class InsightUiToastService
    {
        public string Message { get; private set; }
        public InsightToastSeverity Severity { get; private set; }
        public float Remaining { get; private set; }
        public bool IsVisible => !string.IsNullOrEmpty(Message) && Remaining > 0f;

        /// <summary>Shows a new toast, replacing any previous toast in this document.</summary>
        public void Show(string message, InsightToastSeverity severity = InsightToastSeverity.Info, float duration = 2.5f)
        {
            Message = message ?? string.Empty;
            Severity = severity;
            Remaining = Math.Max(0.01f, duration);
        }

        /// <summary>Clears the current toast immediately.</summary>
        public void Clear()
        {
            Message = null;
            Remaining = 0f;
        }

        internal void Advance(float deltaTime, bool reducedMotion)
        {
            if (!IsVisible) return;
            Remaining -= Math.Max(0f, deltaTime);
            if (Remaining <= 0f) Clear();
        }
    }
}
