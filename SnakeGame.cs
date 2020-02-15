using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Snakexperiment
{
    public class SnakeGame : Game
    {
        const double TICK_RATE = 0.125;
        const int FIELD_HEIGHT = 38;
        const int FIELD_WIDTH = 50;

        private readonly Random _rng;

        private TimeSpan _lastTick;

        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private SpriteFont _uiFont;

        private Texture2D _appleTexture;
        private Texture2D _snakeAliveTexture;
        private Texture2D _snakeDeadTexture;
        private Texture2D _tileTexture;

        private Vector2 _direction;
        private Vector2 _lastPosition;
        private Vector2 _applePosition;
        private Queue<Vector2> _snake;
        private int _snakeSize;
        private bool _alive;

        public SnakeGame()
        {
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferHeight = FIELD_HEIGHT * 16,
                PreferredBackBufferWidth = FIELD_WIDTH * 16,
                PreferHalfPixelOffset = true,
                PreferMultiSampling = true,
                SynchronizeWithVerticalRetrace = true
            };
            _rng = new Random();

            IsFixedTimeStep = false;
            IsMouseVisible = true;

            Content.RootDirectory = "Content";
        }

        protected override void Initialize()
        {
            _applePosition = new Vector2(_rng.Next(FIELD_WIDTH), _rng.Next(FIELD_HEIGHT));
            _alive = true;
            _direction = new Vector2(1, 0);
            _snake = new Queue<Vector2>(FIELD_WIDTH * FIELD_HEIGHT);
            _snake.Enqueue(_lastPosition);
            _snakeSize = 10;

            _lastPosition = Vector2.Zero;
            _lastTick = TimeSpan.Zero;

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _snakeAliveTexture = Content.Load<Texture2D>("square");
            _appleTexture = Content.Load<Texture2D>("apple");
            _snakeDeadTexture = Content.Load<Texture2D>("dead");
            _tileTexture = Content.Load<Texture2D>("tile");
            _uiFont = Content.Load<SpriteFont>("CascadiaMono");
        }

        protected override void Update(GameTime gameTime)
        {
            var keyState = Keyboard.GetState();

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed
                || keyState.IsKeyDown(Keys.Escape)
                || keyState.IsKeyDown(Keys.Q))
            {
                Exit();
            }

            if (_direction.X == 0)
            {
                if (keyState.IsKeyDown(Keys.A) || keyState.IsKeyDown(Keys.Left))
                {
                    _direction.X = -1;
                    _direction.Y = 0;
                }
                else if (keyState.IsKeyDown(Keys.D) || keyState.IsKeyDown(Keys.Right))
                {
                    _direction.X = 1;
                    _direction.Y = 0;
                }
            }
            else if (_direction.Y == 0)
            {
                if (keyState.IsKeyDown(Keys.W) || keyState.IsKeyDown(Keys.Up))
                {
                    _direction.X = 0;
                    _direction.Y = -1;
                }
                else if (keyState.IsKeyDown(Keys.S) || keyState.IsKeyDown(Keys.Down))
                {
                    _direction.X = 0;
                    _direction.Y = 1;
                }
            }

            if (_alive)
            {
                TimeSpan diff = gameTime.TotalGameTime - _lastTick;
                if (diff.TotalSeconds >= TICK_RATE)
                {
                    Vector2 newPosition = _lastPosition + _direction;
                    if (newPosition.Y < 0
                        || newPosition.Y >= FIELD_HEIGHT
                        || newPosition.X < 0
                        || newPosition.X >= FIELD_WIDTH
                        || _snake.Contains(newPosition))
                    {
                        _alive = false;
                    }
                    else
                    {
                        if (newPosition == _applePosition)
                        {
                            _snakeSize += 5;
                            do
                            {
                                _applePosition.X = _rng.Next(FIELD_WIDTH);
                                _applePosition.Y = _rng.Next(FIELD_HEIGHT);
                            } while (_snake.Contains(_applePosition));
                        }

                        while (_snake.Count >= _snakeSize)
                            _snake.Dequeue();
                        _snake.Enqueue(newPosition);
                        _lastPosition = newPosition;
                        _lastTick = gameTime.TotalGameTime;
                    }
                }
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.DimGray);

            float fps = (float)(1000.0 / gameTime.ElapsedGameTime.TotalMilliseconds);

            _spriteBatch.Begin();
            for (int y = 0; y < FIELD_HEIGHT / 2 + 1; ++y) {
                for (int x = 0; x < FIELD_WIDTH / 2 + 1; ++x) {
                    _spriteBatch.Draw(_tileTexture, new Vector2(x * _tileTexture.Width, y * _tileTexture.Height), Color.White);
                }
            }
            _spriteBatch.End();

            _spriteBatch.Begin();
            _spriteBatch.Draw(_appleTexture, _applePosition * _appleTexture.Width, Color.White);
            Texture2D square = _alive ? _snakeAliveTexture : _snakeDeadTexture;
            foreach (Vector2 snakePiece in _snake)
            {
                _spriteBatch.Draw(square, snakePiece * square.Width, Color.White);
            }
            _spriteBatch.End();

            string uiMessage = $"size: {_snakeSize:N0}; fps: {fps:N0}";
            var messageSize = _uiFont.MeasureString(uiMessage);

            _spriteBatch.Begin();
            _spriteBatch.DrawString(
                _uiFont,
                uiMessage,
                new Vector2(800 - messageSize.X, 608 - messageSize.Y),
                Color.LightGray);
            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
