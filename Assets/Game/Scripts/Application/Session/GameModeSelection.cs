using System;

namespace Game.Application.Session
{
    public enum GamePlayMode
    {
        None = 0,
        SinglePlayer = 1,
        Multiplayer = 2
    }

    public sealed class GameModeSelection
    {
        public GamePlayMode CurrentMode { get; private set; }
        public bool HasSelection => CurrentMode != GamePlayMode.None;
        public bool IsSinglePlayer => CurrentMode == GamePlayMode.SinglePlayer;
        public bool IsMultiplayer => CurrentMode == GamePlayMode.Multiplayer;

        public bool TrySelect(GamePlayMode mode, out string reason)
        {
            if (mode != GamePlayMode.SinglePlayer &&
                mode != GamePlayMode.Multiplayer)
            {
                reason = "지원하지 않는 게임 모드입니다.";
                return false;
            }

            if (HasSelection && CurrentMode != mode)
            {
                reason = "다른 게임 모드가 이미 실행 중입니다.";
                return false;
            }

            CurrentMode = mode;
            reason = string.Empty;
            return true;
        }

        public void Clear()
        {
            CurrentMode = GamePlayMode.None;
        }
    }
}
