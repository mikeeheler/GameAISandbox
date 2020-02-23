using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SnakeGame
{
    public interface ISnakeGraphics
    {
        Texture2D CreateBorderSquare(int width, int height, Color color, int thickness, Color border);
        Texture2D CreateFlatTexture(int width, int height, Color color);
    }
}
