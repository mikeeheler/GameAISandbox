using System;
using System.Collections.Generic;

using MathNet.Numerics.LinearAlgebra;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Snakexperiment
{
    public class SnakeGame : Game
    {
        const double TICK_RATE = 0.125;

        const int FIELD_HEIGHT = 20;
        const int FIELD_WIDTH = 20;

        const string GAME_OVER_MESSAGE = "GAME OVER";
        const string QUIT_MESSAGE = "Q to quit";
        const string TRY_AGAIN_MESSAGE  = "SPACE to try again";

        private readonly Random _rng;

        private Vector2 _gameOverMessagePos;
        private Vector2 _quitMessagePos;
        private Vector2 _tryAgainMessagePos;

        private SpriteBatch _spriteBatch;
        private SpriteFont _uiFont;

        private Texture2D _appleTexture;
        private Texture2D _snakeAliveTexture;
        private Texture2D _snakeDeadTexture;
        private Texture2D _tileTexture;

        private Point _lastDirection;
        private Point _lastPosition;
        private TimeSpan _lastTick;
        private int _ticks;

        private bool _alive;
        private Point _applePosition;
        private Point _direction;
        private Point _fieldTopLeft;
        private Queue<Point> _snake;
        private int _snakeSize;
        private IPlayerController _player;

        public SnakeGame()
        {
            _ = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferHeight = 720,
                PreferredBackBufferWidth = 1280,
                PreferHalfPixelOffset = true,
                PreferMultiSampling = true,
                SynchronizeWithVerticalRetrace = true
            };
            _rng = new Random();
            _fieldTopLeft = new Point(0, 0);

            _ticks = 0;

            IsFixedTimeStep = false;
            IsMouseVisible = true;

            Content.RootDirectory = "Content";
            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += OnResize;
        }

        public int FieldHeight { get; } = FIELD_HEIGHT;
        public int FieldWidth { get; } = FIELD_WIDTH;

        public int Score => _snake.Count;
        public Point ApplePosition => _applePosition;
        public Point Direction => _lastDirection;

        public bool IsLegalMove(PlayerMovement move)
        {
            return move switch
            {
                PlayerMovement.Down => _lastDirection.Y != -1,
                PlayerMovement.Left => _lastDirection.X != 1,
                PlayerMovement.Right => _lastDirection.X != -1,
                PlayerMovement.Up => _lastDirection.Y != 1,
                _ => throw new ArgumentOutOfRangeException(nameof(move), move, ""),
            };
        }

        protected override void Initialize()
        {
            _lastTick = TimeSpan.Zero;
            Reset();

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

            OnResize(null, EventArgs.Empty);
        }

        protected override void Update(GameTime gameTime)
        {
            _ticks++;

            HandleKeyPress(Keyboard.GetState());

            TimeSpan diff = gameTime.TotalGameTime - _lastTick;
            if (diff.TotalSeconds >= TICK_RATE)
            {
                UpdateEntities(diff);

                if (!_alive && !_player.IsHuman)
                {

                }

                _lastTick = gameTime.TotalGameTime;
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            float fps = (float)(1000.0 / gameTime.ElapsedGameTime.TotalMilliseconds);

            DrawField();
            DrawEntities();
            DrawUI(fps, gameTime.TotalGameTime.TotalMilliseconds);

            base.Draw(gameTime);
        }

        protected override void OnExiting(object sender, EventArgs args)
        {
            _player.Shutdown();
            base.OnExiting(sender, args);
        }

        private void DrawField()
        {
            Point tilePosition = Point.Zero;

            _spriteBatch.Begin();
            for (tilePosition.Y = 0; tilePosition.Y < FIELD_HEIGHT / 2; ++tilePosition.Y)
            {
                for (tilePosition.X = 0; tilePosition.X < FIELD_WIDTH / 2; ++tilePosition.X)
                {
                    _spriteBatch.Draw(
                        _tileTexture,
                        (_fieldTopLeft + tilePosition * _tileTexture.Bounds.Size).ToVector2(),
                        Color.White);
                }
            }
            _spriteBatch.End();
        }

        private void DrawEntities()
        {
            _spriteBatch.Begin();
            _spriteBatch.Draw(
                _appleTexture,
                (_fieldTopLeft + _applePosition * _appleTexture.Bounds.Size).ToVector2(),
                Color.White);
            Texture2D square = _alive ? _snakeAliveTexture : _snakeDeadTexture;
            int pieceCount = 0;
            foreach (Point snakePiece in _snake)
            {
                float ratio = Convert.ToSingle((double)pieceCount / _snake.Count) * 0.5f + 0.5f;
                _spriteBatch.Draw(
                    square,
                    (_fieldTopLeft + snakePiece * square.Bounds.Size).ToVector2(),
                    new Color(ratio, ratio, ratio));
                ++pieceCount;
            }
            _spriteBatch.End();
        }

        private void DrawUI(double fps, double gameTime)
        {
            _spriteBatch.Begin();

            _spriteBatch.DrawString(_uiFont, QUIT_MESSAGE, _quitMessagePos, Color.LightGray);

            string scoreMessage = $"size: {_snakeSize:N0}; fps: {fps:N0}; tavg: {gameTime/_ticks:N2}";
            Point scoreMessageSize = _uiFont.MeasureString(scoreMessage).ToPoint();
            Point scoreMessagePosition = Window.ClientBounds.Size - scoreMessageSize;
            _spriteBatch.DrawString(_uiFont, scoreMessage, scoreMessagePosition.ToVector2(), Color.LightGray);

            if (!_alive)
            {
                _spriteBatch.DrawString(_uiFont, GAME_OVER_MESSAGE, _gameOverMessagePos, Color.LightGoldenrodYellow);
                _spriteBatch.DrawString(_uiFont, TRY_AGAIN_MESSAGE, _tryAgainMessagePos, Color.LightGoldenrodYellow);
            }

            _spriteBatch.End();
        }

        private void HandleKeyPress(KeyboardState keyboardState)
        {
            if (keyboardState.IsKeyDown(Keys.Escape) || keyboardState.IsKeyDown(Keys.Q))
            {
                Exit();
                return;
            }

            if (!_alive && keyboardState.IsKeyDown(Keys.Space))
            {
                Reset();
                return;
            }
        }

        private void OnResize(object sender, EventArgs e)
        {
            int windowHeight = Window.ClientBounds.Height;
            int windowWidth = Window.ClientBounds.Width;

            int fieldHeight = FIELD_HEIGHT * _tileTexture.Height / 2;
            int fieldWidth = FIELD_WIDTH * _tileTexture.Width / 2;

            _fieldTopLeft.X = (windowWidth - fieldWidth) / 2;
            _fieldTopLeft.Y = (windowHeight - fieldHeight) / 2;

            Vector2 gameOverMessageSize = _uiFont.MeasureString(GAME_OVER_MESSAGE);
            _gameOverMessagePos = new Vector2(
                (windowWidth - gameOverMessageSize.X) / 2,
                windowHeight / 2 - gameOverMessageSize.Y);

            Vector2 quitMessageSize = _uiFont.MeasureString(QUIT_MESSAGE);
            _quitMessagePos = new Vector2(0.0f, windowHeight - quitMessageSize.Y);

            Vector2 tryAgainMessageSize = _uiFont.MeasureString(TRY_AGAIN_MESSAGE);
            _tryAgainMessagePos = new Vector2(
                (windowWidth - tryAgainMessageSize.X) / 2,
                windowHeight / 2);
        }

        private void Reset()
        {
            do
            {
                _applePosition = new Point(_rng.Next(FIELD_WIDTH), _rng.Next(FIELD_HEIGHT));
            } while (_applePosition == Point.Zero);
            _alive = true;
            _direction = new Point(1, 0);
            _lastDirection = _direction;
            _lastPosition = Point.Zero;
            _snake = new Queue<Point>(FIELD_WIDTH * FIELD_HEIGHT);
            _snake.Enqueue(_lastPosition);
            _snakeSize = 10;

            _player?.Shutdown();
            _player = new AIPlayer();
            _player.Initialize(this);
        }

        private void UpdateEntities(TimeSpan _)
        {
            if (!_alive)
                return;

            switch (_player.GetMovement())
            {
                case PlayerMovement.Down: _direction.X = 0; _direction.Y = 1; break;
                case PlayerMovement.Left: _direction.X = -1; _direction.Y = 0; break;
                case PlayerMovement.Right: _direction.X = 1; _direction.Y = 0; break;
                case PlayerMovement.Up: _direction.X = 0; _direction.Y = -1; break;
            }

            Point newPosition = _lastPosition + _direction;
            if (newPosition.Y < 0
                || newPosition.Y >= FIELD_HEIGHT
                || newPosition.X < 0
                || newPosition.X >= FIELD_WIDTH
                || _snake.Contains(newPosition))
            {
                _alive = false;
                return;
            }

            _snake.Enqueue(newPosition);
            while (_snake.Count >= _snakeSize)
                _snake.Dequeue();

            if (newPosition == _applePosition)
            {
                _snakeSize += 5;
                do
                {
                    _applePosition.X = _rng.Next(FIELD_WIDTH);
                    _applePosition.Y = _rng.Next(FIELD_HEIGHT);
                } while (_snake.Contains(_applePosition));
            }

            _lastDirection = _direction;
            _lastPosition = newPosition;
        }
    }
}
