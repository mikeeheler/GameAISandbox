using System;

namespace SnakeGame
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            using var engine = new SnakeEngine();
            engine.Run();
        }
    }
}
