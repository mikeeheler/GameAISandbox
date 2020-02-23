using System;
using Microsoft.Xna.Framework;

namespace SnakeGame
{
    public interface IPlayerController
    {
        bool IsHuman { get; }

        PlayerMovement GetMovement(SnakeGameSim gameState);
        void Initialize();
        void Shutdown();
    }
}
