using System;
using Microsoft.Xna.Framework;

namespace Snakexperiment
{
    public interface IPlayerController
    {
        PlayerMovement GetMovement();
        void Initialize();
        void Update(SnakeGame snakeGame, GameTime gameTime);
    }
}
