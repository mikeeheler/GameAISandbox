using System;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SnakeGame
{
    public class SnakeGraphics : ISnakeGraphics
    {
        private readonly SnakeEngine _engine;

        private readonly Lazy<Texture2D> _appleTexture;
        private readonly Lazy<Texture2D> _arrowTexture;
        private readonly Lazy<SpriteFont> _defaultUIFont;
        private readonly Lazy<SpriteFont> _smallUIFont;

        public SnakeGraphics(SnakeEngine engine)
        {
            _engine = engine;
            _appleTexture = new Lazy<Texture2D>(() => LoadTexture("Textures/apple"), false);
            _arrowTexture = new Lazy<Texture2D>(() => LoadTexture("Textures/arrow"), false);
            _defaultUIFont = new Lazy<SpriteFont>(() => LoadFont("UIFont"), false);
            _smallUIFont = new Lazy<SpriteFont>(() => LoadFont("UIFont-Small"), false);
        }

        public Texture2D AppleTexture => _appleTexture.Value;
        public Texture2D ArrowTexture => _arrowTexture.Value;
        public SpriteFont DefaultUIFont => _defaultUIFont.Value;
        public SpriteFont SmallUIFont => _smallUIFont.Value;

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
            var result = new Texture2D(_engine.GraphicsDevice, width, height);
            result.SetData(image);
            return result;
        }

        public Texture2D CreateFlatTexture(int width, int height, Color color)
        {
            var result = new Texture2D(_engine.GraphicsDevice, width, height);
            result.SetData(Enumerable.Repeat(color, height*width).ToArray());
            return result;
        }

        public SpriteFont LoadFont(string resourceName)
            => _engine.Content.Load<SpriteFont>(resourceName);

        public Texture2D LoadTexture(string resourceName)
            => _engine.Content.Load<Texture2D>(resourceName);
    }
}
