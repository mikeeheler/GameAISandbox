using System;

namespace SnakeGame
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            using var app = new SnakeApp();
            app.Run();
        }
    }
}
