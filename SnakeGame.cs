using System;
using System.Collections.Generic;
using System.Linq;

using MathNet.Numerics.LinearAlgebra;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Snakexperiment
{
    public class SnakeGame : Game
    {

        const int FIELD_HEIGHT = 20;
        const int FIELD_WIDTH = 20;

        const double MUTATION_RATE = 0.6;
        const int POPULATION_SIZE = 500;

        const string GAME_OVER_MESSAGE = "GAME OVER";
        const string QUIT_MESSAGE = "Q to quit";
        const string TRY_AGAIN_MESSAGE  = "SPACE to try again";

        private readonly GraphicsDeviceManager _graphicsDeviceManager;
        private Random _rng;

        private Vector2 _gameOverMessagePos;
        private Vector2 _quitMessagePos;
        private Vector2 _tryAgainMessagePos;

        private SpriteBatch _spriteBatch;
        private SpriteFont _uiFont;

        private Texture2D _appleTexture;
        private Texture2D _circleTexture;
        private Texture2D _smallSquareTexture;
        private Texture2D _snakeAliveTexture;
        private Texture2D _snakeDeadTexture;
        private Texture2D _tileTexture;

        private Point _lastDirection;
        private Point _lastPosition;
        private TimeSpan _lastTick;
        private int _ticks;
        private bool _tickEnabled;
        private double _tickRate = 0.125;

        private bool _alive;
        private Point _applePosition;
        private Point _direction;
        private Point _fieldTopLeft;
        private Queue<Point> _snake;
        private int _snakeSize;
        private IPlayerController _player;

        private List<AIPlayerScore> _aiPlayers;
        private int _aiPlayerIndex;
        private int _generation;
        private int _turns;
        private int _turnsSinceApple;

        private bool _tabDown;
        private bool _plusDown;
        private bool _minusDown;

        public SnakeGame()
        {
            _graphicsDeviceManager = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferHeight = 720,
                PreferredBackBufferWidth = 1280,
                PreferHalfPixelOffset = true,
                PreferMultiSampling = true,
                SynchronizeWithVerticalRetrace = true
            };
            _aiPlayers = GenerateAIPlayers(POPULATION_SIZE).ToList();
            _aiPlayerIndex = 0;
            _generation = 0;
            _fieldTopLeft = new Point(0, 0);

            _tickEnabled = true;
            _ticks = 0;
            _tabDown = false;
            _minusDown = false;
            _plusDown = false;

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
        public Point SnakePosition => _snake.Peek();
        public int SnakeSize => _snakeSize;

        public double[] GetBoardValues()
        {
            var result = Matrix<Double>.Build.Dense(FIELD_HEIGHT, FIELD_WIDTH, 0.0);
            result[_applePosition.Y, _applePosition.X] = 1.0;
            foreach (Point snakePiece in _snake)
            {
                result[snakePiece.Y, snakePiece.X] = -1.0;
            }
            return result.AsColumnMajorArray();
        }

        public bool IsCollision(Point point)
            => point.Y < 0
                || point.Y >= FIELD_HEIGHT
                || point.X < 0
                || point.X >= FIELD_WIDTH
                || _snake.Contains(point);

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

            _uiFont = Content.Load<SpriteFont>("CascadiaMono");
            _appleTexture = Content.Load<Texture2D>("apple");
            _circleTexture = Content.Load<Texture2D>("circle");
            _smallSquareTexture = Content.Load<Texture2D>("square8x8");
            _snakeDeadTexture = Content.Load<Texture2D>("dead");
            _snakeAliveTexture = Content.Load<Texture2D>("square");
            _tileTexture = Content.Load<Texture2D>("tile");

            OnResize(null, EventArgs.Empty);
        }

        protected override void Update(GameTime gameTime)
        {
            _ticks++;

            HandleKeyPress(Keyboard.GetState());

            TimeSpan diff = gameTime.TotalGameTime - _lastTick;
            if (!_tickEnabled || diff.TotalSeconds >= _tickRate)
            {
                UpdateEntities(diff);

                if (!_alive && !_player.IsHuman)
                {
                    HandleAI();
                    Reset();
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
            DrawDebug();

            base.Draw(gameTime);
        }

        protected override void OnExiting(object sender, EventArgs args)
        {
            _player.Shutdown();
            base.OnExiting(sender, args);
        }

        private void DrawSmallSquare(Vector2 pos, float shade)
        {
            _spriteBatch.Draw(_smallSquareTexture, pos, GetDebugColor(shade));
        }

        private Color GetDebugColor(float shade)
        {
            shade = Math.Clamp(shade, -1.0f, 1.0f);
            return shade >= 0.0f ? new Color(0.5f, 1.0f, 0.5f) * shade : new Color(-shade, 0.0f, 0.0f);
        }

        private void DrawDebug()
        {
            DrawDebugOutputs();

            var brain = _aiPlayers[_aiPlayerIndex].Player.CloneBrain();
            var values = brain.GetValues();

            Vector2 topLeft = new Vector2(336, 310);
            Vector2 offset = new Vector2(16, 0);
            _spriteBatch.Begin();
            for (int layerIndex = 0; layerIndex < values.Length; ++layerIndex)
            {
                if (values[layerIndex] == null) continue;

                double[] layer = values[layerIndex].Enumerate().ToArray();
                Vector2 pos = new Vector2(layerIndex * offset.X, offset.Y);
                for (int y = 0; y < layer.Length; ++y)
                {
                    pos.Y = y * 8;;
                    DrawSmallSquare(topLeft + pos, (float)layer[y]);
                }
            }

            _spriteBatch.End();
        }

        private void DrawDebugOutputs()
        {
            Vector2 topLeft = new Vector2(400, 350);
            var aiDecision = _aiPlayers[_aiPlayerIndex].Player.Decision;
            float down = aiDecision[0];
            float left = aiDecision[1];
            float right = aiDecision[2];
            float up = aiDecision[3];
            _spriteBatch.Begin();
            _spriteBatch.Draw(_circleTexture, topLeft, GetDebugColor(up));
            _spriteBatch.Draw(_circleTexture, topLeft + new Vector2(-16, 32), GetDebugColor(left));
            _spriteBatch.Draw(_circleTexture, topLeft + new Vector2(16, 32), GetDebugColor(right));
            _spriteBatch.Draw(_circleTexture, topLeft + new Vector2(0, 64), GetDebugColor(down));
            _spriteBatch.End();
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

            string aiMessage = $"gen: {_generation:N0}; best: {_aiPlayers[0].Score:N0}; idx: {_aiPlayerIndex:N0}";
            _spriteBatch.DrawString(_uiFont, aiMessage, Vector2.Zero, Color.LightGray);

            _spriteBatch.End();
        }

        private IEnumerable<AIPlayerScore> Breed(AIPlayerScore firstParent, AIPlayerScore secondParent, int count)
        {
            return Enumerable.Range(0, count).Select(_ =>
            {
                var baby = firstParent.Player.BreedWith(secondParent.Player);
                return new AIPlayerScore { Player = baby, Score = 0};
            });
        }

        private IEnumerable<AIPlayerScore> Evolve(AIPlayerScore input, int count)
        {
            return Enumerable.Range(0, count).Select(_ =>
            {
                var newPlayer = input.Player.Clone();
                newPlayer.Mutate(MUTATION_RATE);
                return new AIPlayerScore { Player = newPlayer, Score = 0 };
            });
        }

        private IEnumerable<AIPlayerScore> GenerateAIPlayers(int size)
        {
            return Enumerable.Range(0, size).Select(_ =>
            {
                var player = new AIPlayer();
                return new AIPlayerScore { Player = player, Score = 0 };
            });
        }

        private void HandleAI()
        {
            int applesEaten = (_snakeSize - 10) / 5;
            int penalty = applesEaten == 0 ? -1000 : 0;
            var currentAi = _aiPlayers[_aiPlayerIndex].Score = applesEaten * 1000 + penalty + _turns / 2;
            _aiPlayerIndex++;

            if (_aiPlayerIndex == POPULATION_SIZE)
            {
                var topTen = _aiPlayers.OrderByDescending(x => x.Score).Take(5).ToList();
                _aiPlayers = new List<AIPlayerScore>() { Capacity = POPULATION_SIZE };
                _aiPlayers.AddRange(topTen);
                // Add 20% offspring of the top 2
                _aiPlayers.AddRange(Breed(_aiPlayers[0], _aiPlayers[1], POPULATION_SIZE / 5));
                // Add 20% mutations of best score
                _aiPlayers.AddRange(Evolve(_aiPlayers[0], POPULATION_SIZE / 5));
                // Add 20% mutations of second best
                _aiPlayers.AddRange(Evolve(_aiPlayers[1], POPULATION_SIZE / 5));
                // Add 15% mutations of third
                _aiPlayers.AddRange(Evolve(_aiPlayers[2], POPULATION_SIZE / 20 * 3));
                // Add 10% mutations of fourth and fifth
                _aiPlayers.AddRange(Evolve(_aiPlayers[3], POPULATION_SIZE / 10));
                _aiPlayers.AddRange(Evolve(_aiPlayers[4], POPULATION_SIZE / 10));
                // Add 5% pure random
                _aiPlayers.AddRange(GenerateAIPlayers(POPULATION_SIZE - _aiPlayers.Count));
                _generation++;
                _aiPlayerIndex = 0;
            }
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

            if (keyboardState.IsKeyDown(Keys.Tab))
                _tabDown = true;
            if (keyboardState.IsKeyDown(Keys.Subtract))
                _minusDown = true;
            if (keyboardState.IsKeyDown(Keys.Add))
                _plusDown = true;

            if (_tabDown && keyboardState.IsKeyUp(Keys.Tab))
            {
                _graphicsDeviceManager.SynchronizeWithVerticalRetrace = !_graphicsDeviceManager.SynchronizeWithVerticalRetrace;
                _graphicsDeviceManager.ApplyChanges();
                _tickEnabled = _graphicsDeviceManager.SynchronizeWithVerticalRetrace;
                _tabDown = false;
            }

            if (_minusDown && keyboardState.IsKeyUp(Keys.Subtract))
            {
                _tickRate *= 2.0;
                _minusDown = false;
            }

            if (_plusDown && keyboardState.IsKeyUp(Keys.Add))
            {
                _tickRate *= 0.5;
                _plusDown = false;
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
            _rng = new Random(1024);

            do
            {
                _applePosition = new Point(_rng.Next(FIELD_WIDTH), _rng.Next(FIELD_HEIGHT));
            } while (_applePosition == Point.Zero);
            _alive = true;
            _direction = new Point(1, 0);
            _lastDirection = _direction;
            _lastPosition = new Point(10, 10);
            _snake = new Queue<Point>(FIELD_WIDTH * FIELD_HEIGHT);
            _snake.Enqueue(_lastPosition);
            _snakeSize = 10;
            _turns = 0;
            _turnsSinceApple = 0;

            _player?.Shutdown();
            _player = _aiPlayers[_aiPlayerIndex].Player;
            _player.Initialize(this);
        }

        private void UpdateEntities(TimeSpan _)
        {
            if (!_alive)
                return;

            ++_turnsSinceApple;
            ++_turns;

            switch (_player.GetMovement())
            {
                case PlayerMovement.Down: _direction.X = 0; _direction.Y = 1; break;
                case PlayerMovement.Left: _direction.X = -1; _direction.Y = 0; break;
                case PlayerMovement.Right: _direction.X = 1; _direction.Y = 0; break;
                case PlayerMovement.Up: _direction.X = 0; _direction.Y = -1; break;
            }

            Point newPosition = _lastPosition + _direction;
            if (IsCollision(newPosition) || _turnsSinceApple == 1000)
            {
                _alive = false;
                return;
            }

            _snake.Enqueue(newPosition);
            while (_snake.Count >= _snakeSize)
                _snake.Dequeue();

            if (newPosition == _applePosition)
            {
                _turnsSinceApple = 0;
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

        private class AIPlayerScore
        {
            public AIPlayer Player { get; set; }
            public int Score { get; set; }
        }
    }
}
