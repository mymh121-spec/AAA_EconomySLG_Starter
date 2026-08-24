using System;

namespace Game.Application.Turn
{
    public enum RealtimeGameSpeed
    {
        Paused = 0,
        Normal = 1,
        Fast2 = 2,
        Fast3 = 3,
        Fast4 = 4
    }

    public readonly struct RealtimeAdvanceResult
    {
        public int FixedStepCount { get; }
        public int CompletedGameHours { get; }
        public int CompletedGameDays { get; }
        public double AdvancedGameHours { get; }
        public double DroppedRealSeconds { get; }

        public RealtimeAdvanceResult(
            int fixedStepCount,
            int completedGameHours,
            int completedGameDays,
            double advancedGameHours,
            double droppedRealSeconds)
        {
            FixedStepCount = fixedStepCount;
            CompletedGameHours = completedGameHours;
            CompletedGameDays = completedGameDays;
            AdvancedGameHours = advancedGameHours;
            DroppedRealSeconds = droppedRealSeconds;
        }
    }

    /// <summary>
    /// 렌더링 프레임과 독립적으로 게임 시간을 진행하는 고정 스텝 시계다.
    /// 경제 정산은 CompletedGameDays만큼 하루 단위 파이프라인을 실행한다.
    /// </summary>
    public sealed class RealtimeSimulationClock
    {
        public const int MaximumSpeedMultiplier = 4;

        private readonly double _realSecondsPerGameDay;
        private readonly double _fixedRealStepSeconds;
        private readonly int _maxStepsPerAdvance;
        private double _realAccumulator;
        private double _totalGameHours;
        private int _speedMultiplier;
        private int _resumeSpeedMultiplier;

        public double RealSecondsPerGameDay => _realSecondsPerGameDay;
        public double FixedRealStepSeconds => _fixedRealStepSeconds;
        public double TotalGameHours => _totalGameHours;
        public int CurrentDayNumber =>
            (int)Math.Floor(_totalGameHours / 24d) + 1;
        public int HourOfDay =>
            PositiveModulo((int)Math.Floor(_totalGameHours), 24);
        public int MinuteOfHour
        {
            get
            {
                double fractionalHour =
                    _totalGameHours - Math.Floor(_totalGameHours);
                return Math.Min(
                    59,
                    (int)Math.Floor(fractionalHour * 60d + 0.000001d));
            }
        }
        public int SpeedMultiplier => _speedMultiplier;
        public bool IsPaused => _speedMultiplier == 0;

        public RealtimeSimulationClock(
            double realSecondsPerGameDay = 60d,
            double fixedRealStepSeconds = 0.1d,
            int maxStepsPerAdvance = 16,
            int initialSpeedMultiplier = 1,
            double initialGameHours = 0d)
        {
            if (realSecondsPerGameDay <= 0d ||
                double.IsNaN(realSecondsPerGameDay) ||
                double.IsInfinity(realSecondsPerGameDay))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(realSecondsPerGameDay));
            }
            if (fixedRealStepSeconds <= 0d ||
                double.IsNaN(fixedRealStepSeconds) ||
                double.IsInfinity(fixedRealStepSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fixedRealStepSeconds));
            }
            if (maxStepsPerAdvance < 1)
                throw new ArgumentOutOfRangeException(nameof(maxStepsPerAdvance));
            if (initialGameHours < 0d ||
                double.IsNaN(initialGameHours) ||
                double.IsInfinity(initialGameHours))
            {
                throw new ArgumentOutOfRangeException(nameof(initialGameHours));
            }

            _realSecondsPerGameDay = realSecondsPerGameDay;
            _fixedRealStepSeconds = fixedRealStepSeconds;
            _maxStepsPerAdvance = maxStepsPerAdvance;
            _totalGameHours = initialGameHours;
            _resumeSpeedMultiplier = ClampSpeed(initialSpeedMultiplier, false);
            _speedMultiplier = _resumeSpeedMultiplier;
        }

        public bool SetSpeed(int speedMultiplier)
        {
            int clamped = ClampSpeed(speedMultiplier, true);
            if (_speedMultiplier == clamped)
                return false;

            _speedMultiplier = clamped;
            if (clamped > 0)
                _resumeSpeedMultiplier = clamped;
            return true;
        }

        public bool TogglePause()
        {
            return IsPaused
                ? SetSpeed(_resumeSpeedMultiplier)
                : SetSpeed(0);
        }

        public RealtimeAdvanceResult Advance(double unscaledRealSeconds)
        {
            if (unscaledRealSeconds < 0d ||
                double.IsNaN(unscaledRealSeconds) ||
                double.IsInfinity(unscaledRealSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(unscaledRealSeconds));
            }

            if (IsPaused || unscaledRealSeconds <= 0d)
                return default;

            double maximumAcceptedSeconds =
                _fixedRealStepSeconds * _maxStepsPerAdvance;
            double acceptedSeconds = Math.Min(
                unscaledRealSeconds,
                maximumAcceptedSeconds);
            double droppedSeconds = Math.Max(
                0d,
                unscaledRealSeconds - acceptedSeconds);
            _realAccumulator = Math.Min(
                _realAccumulator + acceptedSeconds,
                maximumAcceptedSeconds);

            int stepCount = Math.Min(
                _maxStepsPerAdvance,
                (int)Math.Floor(
                    _realAccumulator / _fixedRealStepSeconds));
            if (stepCount <= 0)
            {
                return new RealtimeAdvanceResult(
                    0,
                    0,
                    0,
                    0d,
                    droppedSeconds);
            }

            _realAccumulator -= stepCount * _fixedRealStepSeconds;
            int previousHour = (int)Math.Floor(_totalGameHours);
            int previousDay = (int)Math.Floor(_totalGameHours / 24d);
            double advancedHours =
                stepCount * _fixedRealStepSeconds *
                _speedMultiplier * 24d / _realSecondsPerGameDay;
            _totalGameHours += advancedHours;

            int currentHour = (int)Math.Floor(_totalGameHours);
            int currentDay = (int)Math.Floor(_totalGameHours / 24d);
            return new RealtimeAdvanceResult(
                stepCount,
                Math.Max(0, currentHour - previousHour),
                Math.Max(0, currentDay - previousDay),
                advancedHours,
                droppedSeconds);
        }

        private static int ClampSpeed(int value, bool allowPause)
        {
            int minimum = allowPause ? 0 : 1;
            return Math.Min(
                MaximumSpeedMultiplier,
                Math.Max(minimum, value));
        }

        private static int PositiveModulo(int value, int divisor)
        {
            int remainder = value % divisor;
            return remainder < 0 ? remainder + divisor : remainder;
        }
    }
}
