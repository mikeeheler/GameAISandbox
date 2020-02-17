using System;
using Microsoft.Xna.Framework;

namespace Snakexperiment
{
    public interface IPlayerController
    {
        bool IsHuman { get; }

        PlayerMovement GetMovement();
        void Initialize(SnakeGame snakeGame);
        void Shutdown();
    }
}
