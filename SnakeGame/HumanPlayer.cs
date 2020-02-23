using Microsoft.Xna.Framework.Input;

namespace SnakeGame
{
    public class HumanPlayer : IPlayerController
    {
        private PlayerMovement _desiredMove;

        public HumanPlayer(SnakeEngine engine)
        {
            engine.KeyDown += OnKeyDown;
        }

        public bool IsHuman { get; } = true;

        public void Initialize()
        {
            _desiredMove = PlayerMovement.Right;
        }

        public PlayerMovement GetMovement(SnakeGameSim instance)
        {
            return _desiredMove;
        }

        private void OnKeyDown(object sender, KeyDownEventArgs e)
        {
            if (e.Key == Keys.Up || e.Key == Keys.W)
                _desiredMove = PlayerMovement.Up;
            else if (e.Key == Keys.Down || e.Key == Keys.S)
                _desiredMove = PlayerMovement.Down;
            else if (e.Key == Keys.Left || e.Key == Keys.A)
                _desiredMove = PlayerMovement.Left;
            else if (e.Key == Keys.Right || e.Key == Keys.D)
                _desiredMove = PlayerMovement.Right;
        }
    }
}
