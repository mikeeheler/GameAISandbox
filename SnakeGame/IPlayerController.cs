using System;
using Microsoft.Xna.Framework;

namespace SnakeGame
{
    public interface IPlayerController
    {
        bool IsHuman { get; }

        PlayerMovement GetMovement(SnakeGameState gameState);
        void Initialize();
        void Shutdown();
    }
}
