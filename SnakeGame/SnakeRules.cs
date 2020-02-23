namespace SnakeGame
{
    public class SnakeGameRules : ISnakeGameRules
    {
        public int FieldHeight { get; set; }
        public int FieldWidth { get; set; }
        public int MaxAITurns { get; set; }
        public int SnakeGrowLength { get; set; }
        public int SnakeStartLength { get; set; }
    }
}
