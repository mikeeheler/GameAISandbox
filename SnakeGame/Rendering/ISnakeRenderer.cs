using Microsoft.Xna.Framework;

namespace SnakeGame
{
    public interface ISnakeRenderer
    {
        void Initialize();
        void OnWindowResize(Rectangle clientBounds);
        void Render(GameTime gameTime);
    }
}
