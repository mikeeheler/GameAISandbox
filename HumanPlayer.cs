using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Snakexperiment
{
    public class HumanPlayer : IPlayerController
    {
        private PlayerMovement _desiredMove;

        public HumanPlayer()
        {
        }

        public void Initialize()
        {
            _desiredMove = PlayerMovement.Right;
        }

        public PlayerMovement GetMovement()
        {
            return _desiredMove;
        }

        public void Update(SnakeGame snakeGame, GameTime _)
        {
            var keyState = Keyboard.GetState();
            if (snakeGame.IsLegalMove(PlayerMovement.Up)
                && (keyState.IsKeyDown(Keys.W) || keyState.IsKeyDown(Keys.Up)))
            {
                _desiredMove = PlayerMovement.Up;
            }
            else if (snakeGame.IsLegalMove(PlayerMovement.Down)
                && (keyState.IsKeyDown(Keys.S) || keyState.IsKeyDown(Keys.Down)))
            {
                _desiredMove = PlayerMovement.Down;
            }
            else if (snakeGame.IsLegalMove(PlayerMovement.Left)
                && (keyState.IsKeyDown(Keys.A) || keyState.IsKeyDown(Keys.Left)))
            {
                _desiredMove = PlayerMovement.Left;
            }
            else if (snakeGame.IsLegalMove(PlayerMovement.Right)
                && (keyState.IsKeyDown(Keys.D) || keyState.IsKeyDown(Keys.Right)))
            {
                _desiredMove = PlayerMovement.Right;
            }
        }
    }
}
