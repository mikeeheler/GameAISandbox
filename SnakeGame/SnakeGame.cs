using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Random;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SnakeGame
{
    public class SnakeGame : Game
    {
        const int FIELD_HEIGHT = 20;
        const int FIELD_WIDTH = 20;

        const double MUTATION_RATE = 0.6;
        const int POPULATION_SIZE = 500;
        const int MAX_AI_TURNS = FIELD_HEIGHT * FIELD_WIDTH;

        const double RAD90 = Math.PI * 0.5;
        const double RAD180 = Math.PI;

        const string GAME_OVER_MESSAGE = "GAME OVER";
        const string QUIT_MESSAGE = "Q to quit";
        const string TRY_AGAIN_MESSAGE  = "SPACE to try again";

        private readonly GraphicsDeviceManager _graphicsDeviceManager;
        private readonly RandomSource _rng;

        private readonly Color _backgroundColor;
        private readonly Color _activeNeuronColor;
        private readonly Color _inactiveNeuronColor;

        private Vector2 _gameOverMessagePos;
        private Vector2 _quitMessagePos;
        private Vector2 _tryAgainMessagePos;

        private SpriteBatch _spriteBatch;
        private SpriteFont _uiFont;

        private Texture2D _appleTexture;
        private Texture2D _arrowTexture;
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
        private IPlayerController _player;

        private List<AIPlayerScore> _aiPlayers;
        private int _aiPlayerIndex;
        private int _generation;
        private int _lastGenBestScore;
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
            _rng = MersenneTwister.Default;
            _aiPlayers = GenerateAIPlayers(POPULATION_SIZE).ToList();
            _aiPlayerIndex = 0;
            _generation = 0;
            _lastGenBestScore = 0;
            _fieldTopLeft = new Point(0, 0);

            _backgroundColor = new Color(0.1f, 0.1f, 0.1f);
            _activeNeuronColor = new Color(0.5f, 1.0f, 0.5f);
            _inactiveNeuronColor = new Color(1.0f, 0.0f, 0.0f);

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
            Window.Title = "Snake Game AI Sandbox";
        }

        public int FieldHeight { get; } = FIELD_HEIGHT;
        public int FieldWidth { get; } = FIELD_WIDTH;

        public int Score => _snake.Count;

        public Point ApplePosition => _applePosition;
        public Point Direction => _lastDirection;
        public Point SnakePosition => _lastPosition;
        public int SnakeSize { get; private set; }

        public double[] GetBoardValues()
        {
            var result = Matrix<double>.Build.Dense(FIELD_HEIGHT, FIELD_WIDTH, 0.0);
            result[_applePosition.Y, _applePosition.X] = 1.0;
            foreach (Point snakePiece in _snake)
            {
                result[snakePiece.Y, snakePiece.X] = -1.0;
            }
            return result.AsColumnMajorArray();
        }

        public double[] GetSnakeVision()
        {
            Point forward = _lastDirection;
            Point left = new Point(forward.Y, -forward.X);
            Point right = new Point(-forward.Y, forward.X);
            Point behind = new Point(-forward.X, -forward.Y);
            // MAX_AI_TURNS is like a shot clock, and danger computes how much stress the snake feels to get a shot off
            // The intention is that the snake will evolve to give more weight to the apple as this increases
            // In reality, the snake will do what it wants.
            double danger = (double)_turnsSinceApple / MAX_AI_TURNS;
            return new double[] { danger*danger } // danger squared
                .Concat(Look(forward))
                .Concat(Look(forward + left))
                .Concat(Look(left))
                .Concat(Look(behind + left))
                //.Concat(Look(behind)) // Should the snake be able to see behind its head?
                .Concat(Look(behind + right))
                .Concat(Look(right))
                .Concat(Look(forward + right))
                .ToArray();
        }

        public bool IsCollision(Point point)
            => IsWallCollision(point) || _snake.Contains(point);

        public bool IsWallCollision(Point point)
            => point.Y < 0
                || point.Y >= FIELD_HEIGHT
                || point.X < 0
                || point.X >= FIELD_WIDTH;

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

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(_backgroundColor);

            float fps = (float)(1000.0 / gameTime.ElapsedGameTime.TotalMilliseconds);

            DrawField();
            DrawEntities();
            DrawUI(fps, gameTime.TotalGameTime.TotalMilliseconds);
            DrawDebug();

            base.Draw(gameTime);
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
            _appleTexture = Content.Load<Texture2D>("Textures/apple");
            _arrowTexture = Content.Load<Texture2D>("Textures/arrow");
            _circleTexture = Content.Load<Texture2D>("Textures/circle");
            _smallSquareTexture = Content.Load<Texture2D>("Textures/square8x8");
            _snakeDeadTexture = Content.Load<Texture2D>("Textures/dead");
            _snakeAliveTexture = Content.Load<Texture2D>("Textures/square");
            _tileTexture = Content.Load<Texture2D>("Textures/tile");

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

        protected override void OnExiting(object sender, EventArgs args)
        {
            _player.Shutdown();
            base.OnExiting(sender, args);
        }

        private IEnumerable<AIPlayerScore> Breed(
            AIPlayerScore firstParent,
            AIPlayerScore secondParent,
            int count,
            AIBreedingMode breedingMode)
        {
            return Enumerable.Range(0, count).Select(_ =>
            {
                var baby = firstParent.Player.BreedWith(secondParent.Player, breedingMode);
                return new AIPlayerScore { Player = baby, Score = 0 };
            });
        }

        private void DrawSmallSquare(Vector2 pos, float shade)
        {
            _spriteBatch.Draw(_smallSquareTexture, pos, GetDebugColor(shade));
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

        private void DrawDebugArrow(Vector2 position, Vector2 offset, Color color, float rotation)
        {
            _spriteBatch.Draw(
                _arrowTexture,
                position + offset,
                null,
                color,
                rotation,
                offset,
                1.0f,
                SpriteEffects.None,
                0.0f);
        }

        private void DrawDebugOutputs()
        {
            Vector2 topLeft = new Vector2(400, 350);
            Vector2 offset = new Vector2(16, 16);
            var aiDecision = _aiPlayers[_aiPlayerIndex].Player.Decision;
            float up = aiDecision[0];
            float left = aiDecision[1];
            float right = aiDecision[2];
            // float down = aiDecision[0];
            _spriteBatch.Begin();
            DrawDebugArrow(topLeft, offset, GetDebugColor(up), (float)RAD90);
            DrawDebugArrow(topLeft + new Vector2(-16, 32), offset, GetDebugColor(left), 0.0f);
            DrawDebugArrow(topLeft + new Vector2(16, 32), offset, GetDebugColor(right), (float)RAD180);
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

            string scoreMessage = $"size: {SnakeSize:N0}; fps: {fps:N0}; tavg: {gameTime/_ticks:N2}";
            Point scoreMessageSize = _uiFont.MeasureString(scoreMessage).ToPoint();
            Point scoreMessagePosition = Window.ClientBounds.Size - scoreMessageSize;
            _spriteBatch.DrawString(_uiFont, scoreMessage, scoreMessagePosition.ToVector2(), Color.LightGray);

            if (!_alive)
            {
                _spriteBatch.DrawString(_uiFont, GAME_OVER_MESSAGE, _gameOverMessagePos, Color.LightGoldenrodYellow);
                _spriteBatch.DrawString(_uiFont, TRY_AGAIN_MESSAGE, _tryAgainMessagePos, Color.LightGoldenrodYellow);
            }

            static string GetBrainTypeName(AIBrainType brainType)
                => brainType switch
                {
                    AIBrainType.DescendentCoalesced => "coalesced",
                    AIBrainType.DescendentMixed => "mixed",
                    AIBrainType.MutatedClone => "mutant",
                    AIBrainType.OneOfGodsOwnPrototypes => "prototype",
                    _ => throw new ArgumentOutOfRangeException(nameof(brainType))
                };

            var stringBuilder = new StringBuilder()
                .Append($"gen: {_generation:N0}; ")
                .Append($"best: {_lastGenBestScore:N0}; ")
                .Append($"idx: {_aiPlayerIndex:N0}; ")
                .Append($"id: {_aiPlayers[_aiPlayerIndex].Player.Id:N0}; ")
                .AppendLine($"species: {_aiPlayers[_aiPlayerIndex].Player.SpeciesId:N0}")
                .Append($"brain: {GetBrainTypeName(_aiPlayers[_aiPlayerIndex].Player.BrainType)}");

            _spriteBatch.DrawString(_uiFont, stringBuilder, Vector2.Zero, Color.LightGray);

            _spriteBatch.End();
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

        private Color GetDebugColor(float shade)
        {
            shade = Math.Clamp(shade, -1.0f, 1.0f);

            Color shadeColor = new Color(
                (shade >= 0 ? _activeNeuronColor : _inactiveNeuronColor) * Math.Abs(shade),
                1.0f);
            return shadeColor;
        }

        private void HandleAI()
        {
            int applesEaten = (SnakeSize - 10) / 5;
            var currentAi = _aiPlayers[_aiPlayerIndex];
            double distanceToApple = 1.0 / (_applePosition - _lastPosition).ToVector2().Length() * 100.0;
            currentAi.Score = applesEaten;
            _aiPlayerIndex++;

            if (_aiPlayerIndex == POPULATION_SIZE)
            {
                var topTen = _aiPlayers.OrderByDescending(x => x.Score).Take(5).ToList();
                _aiPlayers = new List<AIPlayerScore>() { Capacity = POPULATION_SIZE };
                _aiPlayers.AddRange(topTen);
                // Top player breeds with the other top 10 for first 50% of population
                for (int i = 1; i < 10; ++i)
                {
                    // child = (parentA + parentB) / 2 -- selected genes blended
                    _aiPlayers.AddRange(Breed(_aiPlayers[0], _aiPlayers[i], 15, AIBreedingMode.Coalesce));
                    // child = (50% of parentA + 50% of parent B) -- selected genes unaltered
                    _aiPlayers.AddRange(Breed(_aiPlayers[0], _aiPlayers[i], 15, AIBreedingMode.Mix));
                }
                // Add 30% mutations of best score
                _aiPlayers.AddRange(Evolve(_aiPlayers[0], POPULATION_SIZE / 5));
                // Remainder pure random
                _aiPlayers.AddRange(GenerateAIPlayers(POPULATION_SIZE - _aiPlayers.Count));
                _generation++;
                _lastGenBestScore = _aiPlayers[0].Score;
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

        private double[] Look(Point direction)
        {
            // 0 = distance to apple, if in this direction
            // 1 = distance to tail, if in this direction
            // 2 = distance to wall in this direction
            double[] result = { 0.0, 0.0, 0.0 };
            Point lookPos = _lastPosition;
            int distance = 0;
            bool snakeFound = false;
            do
            {
                lookPos += direction;
                ++distance;

                if (IsWallCollision(lookPos))
                {
                    result[2] = 1.0 / distance;
                    return result;
                }

                if (lookPos == _applePosition)
                    result[0] = 1.0 / distance;
                if (!snakeFound && _snake.Contains(lookPos))
                {
                    result[1] = 1.0 / distance;
                    snakeFound = true;
                }
            } while (true);
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
            _lastPosition = new Point(9, 9);
            _snake = new Queue<Point>(FIELD_HEIGHT * FIELD_WIDTH);
            _snake.Enqueue(_lastPosition);
            SnakeSize = 10;
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
            if (IsCollision(newPosition) || _turnsSinceApple == MAX_AI_TURNS)
            {
                _alive = false;
                return;
            }

            _snake.Enqueue(newPosition);
            while (_snake.Count >= SnakeSize)
                _snake.Dequeue();

            if (newPosition == _applePosition)
            {
                _turnsSinceApple = 0;
                SnakeSize += 5;
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
