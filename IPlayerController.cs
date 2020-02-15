using System;
using Microsoft.Xna.Framework;

namespace Snakexperiment
{
    public interface IPlayerController
    {
        PlayerMovement GetMovement();
        void Update(SnakeGame snakeGame, GameTime gameTime);
    }
}
