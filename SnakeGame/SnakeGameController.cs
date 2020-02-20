namespace SnakeGame
{
    public class SnakeGameController
    {
        public SnakeGameController(SnakeGameState gameState, IPlayerController player)
        {
            GameState = gameState;
            Player = player;
        }

        public SnakeGameState GameState { get; }
        public IPlayerController Player { get; }

        public void Update()
        {
            var move = Player.GetMovement(GameState);
            GameState.Move(move);
        }
    }
}
