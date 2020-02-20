namespace SnakeGame
{
    public class SnakeGameController
    {
        public SnakeGameController(ISnakeGameState gameState, IPlayerController player)
        {
            GameState = gameState;
            Player = player;
        }

        public ISnakeGameState GameState { get; }
        public IPlayerController Player { get; }

        public void Update()
        {
            var move = Player.GetMovement();
            GameState.Move(move);
        }
    }
}
