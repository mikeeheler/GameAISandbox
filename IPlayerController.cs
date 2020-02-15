using System;
using Microsoft.Xna.Framework;

namespace Snakexperiment
{
    public interface IPlayerController
    {
        PlayerMovement GetMovement();
        void Initialize(SnakeGame snakeGame);
        void Shutdown();
        void Update(GameTime gameTime);
    }
}
