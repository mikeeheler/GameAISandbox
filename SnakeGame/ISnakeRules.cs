namespace SnakeGame
{
    public interface ISnakeGameRules
    {
        int FieldHeight { get; }
        int FieldWidth { get; }
        int MaxAITurns { get; }
        int SnakeGrowLength { get; }
        int SnakeStartLength { get; }
    }
}
