namespace SnakeGame
{
    public interface IPlayerController
    {
        bool IsHuman { get; }

        PlayerMovement GetMovement(SnakeGameSim instance);
        void Initialize();
    }
}
