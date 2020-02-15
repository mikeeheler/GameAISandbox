using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Snakexperiment
{
    public class SnakeGame : Game
    {
        const float MOVE_SPEED = 64.0f;

        private TimeSpan _lastTick;

        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private SpriteFont _uiFont;

        private Texture2D _square;
        private Texture2D _tile;

        private Vector2 _direction;
        private Vector2 _position;

        public SnakeGame()
        {
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferHeight = 608,
                PreferredBackBufferWidth = 800,
                PreferHalfPixelOffset = true,
                PreferMultiSampling = true,
                SynchronizeWithVerticalRetrace = false
            };

            IsFixedTimeStep = false;
            IsMouseVisible = true;

            Content.RootDirectory = "Content";
        }

        protected override void Initialize()
        {
            _direction = new Vector2(1, 0);
            _position = Vector2.Zero;

            _lastTick = TimeSpan.Zero;

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _square = Content.Load<Texture2D>("square");
            _tile = Content.Load<Texture2D>("tile");
            _uiFont = Content.Load<SpriteFont>("CascadiaMono");
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed
                || Keyboard.GetState().IsKeyDown(Keys.Escape))
            {
                Exit();
            }

            TimeSpan diff = gameTime.TotalGameTime - _lastTick;
            if (diff.TotalSeconds >= 0.5)
            {
                _position += _direction;
                _lastTick = gameTime.TotalGameTime;
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.DimGray);

            float fps = (float)(1000.0 / gameTime.ElapsedGameTime.TotalMilliseconds);

            _spriteBatch.Begin();
            for (int y = 0; y < 20; ++y) {
                for (int x = 0; x < 25; ++x) {
                    _spriteBatch.Draw(_tile, new Vector2(x * 32, y * 32), Color.White);
                }
            }
            _spriteBatch.End();

            _spriteBatch.Begin();
            _spriteBatch.Draw(_square, _position * 16, Color.White);
            _spriteBatch.End();

            _spriteBatch.Begin();
            _spriteBatch.DrawString(_uiFont, $"fps: {fps:N2}", new Vector2(640, 576), Color.LightGray);
            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
