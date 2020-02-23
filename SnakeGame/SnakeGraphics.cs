using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SnakeGame
{
    public class SnakeGraphics : ISnakeGraphics
    {
        private readonly SnakeGame _game;

        public SnakeGraphics(SnakeGame game)
        {
            _game = game;
        }

        public Texture2D CreateBorderSquare(int width, int height, Color color, int thickness, Color border)
        {
            Color[] image = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (x < thickness || y < thickness || x >= (width - thickness) || y >= (height - thickness))
                        image[(y * width) + x] = border;
                    else
                        image[(y * width) + x] = color;
                }
            }
            var result = new Texture2D(_game.GraphicsDevice, width, height);
            result.SetData(image);
            return result;
        }

        public Texture2D CreateFlatTexture(int width, int height, Color color)
        {
            var result = new Texture2D(_game.GraphicsDevice, width, height);
            result.SetData(Enumerable.Repeat(color, height*width).ToArray());
            return result;
        }

    }
}
