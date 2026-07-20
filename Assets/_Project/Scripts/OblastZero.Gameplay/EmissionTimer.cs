// Assets/_Project/Scripts/Gameplay/EmissionTimer.cs
using UnityEngine;
using OblastZero.Core;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// The 60-second emission countdown that drives the 3D Blowout. Pure C# so it's testable and the state
    /// owns it; the state ticks it each frame via Tick(deltaTime). Raises <see cref="ScavengeTimerTickEvent"/>
    /// once per whole second (for UI/audio) and <see cref="ScavengeTimerExpiredEvent"/> exactly once at zero.
    /// The owning state also polls <see cref="IsExpired"/> to drive the phase transition.
    /// </summary>
    public class EmissionTimer
    {
        public float Duration { get; }
        public float Remaining { get; private set; }
        public bool IsExpired { get; private set; }

        private int _lastWholeSecond;

        public EmissionTimer(float duration)
        {
            Duration = Mathf.Max(0f, duration);
            Remaining = Duration;
            _lastWholeSecond = Mathf.CeilToInt(Remaining);
        }

        public void Tick(float deltaTime)
        {
            if (IsExpired) return;

            Remaining -= deltaTime;
            if (Remaining < 0f) Remaining = 0f;

            int whole = Mathf.CeilToInt(Remaining);
            if (whole != _lastWholeSecond)
            {
                _lastWholeSecond = whole;
                EventBus.Raise(new ScavengeTimerTickEvent { SecondsRemaining = Remaining });
            }

            if (Remaining <= 0f)
            {
                IsExpired = true;
                EventBus.Raise(new ScavengeTimerExpiredEvent());
            }
        }
    }
}
