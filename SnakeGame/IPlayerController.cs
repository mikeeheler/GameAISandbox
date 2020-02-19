using System;
using Microsoft.Xna.Framework;

namespace SnakeGame
{
    public interface IPlayerController
    {
        bool IsHuman { get; }

        PlayerMovement GetMovement();
        void Initialize(SnakeGame snakeGame);
        void Shutdown();
    }
}
