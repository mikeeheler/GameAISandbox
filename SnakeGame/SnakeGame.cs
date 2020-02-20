﻿using System;
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
        const int GAMES_PER_GENERATION = 100;
        const double MUTATION_RATE = 0.40;
        const int POPULATION_SIZE = 100;

        const double RAD90 = Math.PI * 0.5;
        const double RAD180 = Math.PI;

        const string GAME_OVER_MESSAGE = "GAME OVER";
        const string QUIT_MESSAGE = "Q to quit";
        const string TRY_AGAIN_MESSAGE  = "SPACE to try again";

        private readonly SnakeGameState _gameState;
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
        private Texture2D _tileDarkTexture;
        private Texture2D _tileLightTexture;

        private TimeSpan _lastTick;
        private int _ticks;
        private bool _tickEnabled;
        private double _tickRate = 0.125;

        private Point _fieldTopLeft;
        private IPlayerController _player;

        private List<AIPlayerScore> _aiPlayers;
        private int _aiPlayerIndex;
        private int _gamesPlayed;
        private int _generation;
        private int _allTimeBestScore;
        private int _allTimeBestSpecies;
        private int _allTimeBestUnit;
        private int _thisGenBestScore;
        private int _thisGenBestSpecies;
        private int _thisGenBestUnit;

        private bool _tabDown;
        private bool _plusDown;
        private bool _minusDown;

        public SnakeGame()
        {
            _gameState = new SnakeGameState();
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
            _gamesPlayed = 0;
            _generation = 0;
            _allTimeBestScore = 0;
            _allTimeBestSpecies = 0;
            _allTimeBestUnit = 0;
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
            _tileLightTexture = Content.Load<Texture2D>("Textures/tile");

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

                if (!_player.IsHuman
                    && (!_gameState.Alive || _gameState.TotalTurns == SnakeRules.MAX_AI_TURNS))
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
            for (tilePosition.Y = 0; tilePosition.Y < SnakeRules.FIELD_HEIGHT / 2; ++tilePosition.Y)
            {
                for (tilePosition.X = 0; tilePosition.X < SnakeRules.FIELD_WIDTH / 2; ++tilePosition.X)
                {
                    _spriteBatch.Draw(
                        _tileLightTexture,
                        (_fieldTopLeft + tilePosition * _tileLightTexture.Bounds.Size).ToVector2(),
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
                (_fieldTopLeft + _gameState.ApplePosition * _appleTexture.Bounds.Size).ToVector2(),
                Color.White);
            Texture2D square = _gameState.Alive ? _snakeAliveTexture : _snakeDeadTexture;
            int pieceCount = 0;
            foreach (Point snakePiece in _gameState.Snake)
            {
                float ratio = Convert.ToSingle((double)pieceCount / _gameState.Snake.Count) * 0.5f + 0.5f;
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

            string scoreMessage = $"size: {_gameState.SnakeSize:N0}; fps: {fps:N0}; tavg: {gameTime/_ticks:N2}";
            Point scoreMessageSize = _uiFont.MeasureString(scoreMessage).ToPoint();
            Point scoreMessagePosition = Window.ClientBounds.Size - scoreMessageSize;
            _spriteBatch.DrawString(_uiFont, scoreMessage, scoreMessagePosition.ToVector2(), Color.LightGray);

            if (!_gameState.Alive)
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
                .Append($"idx: {_aiPlayerIndex:N0}; ")
                .Append($"id: {_aiPlayers[_aiPlayerIndex].Player.Id:N0}; ")
                .Append($"species: {_aiPlayers[_aiPlayerIndex].Player.SpeciesId:N0}; ")
                .AppendLine($"games: {_gamesPlayed:N0}")
                .Append($"score: {_aiPlayers[_aiPlayerIndex].Score:N0}; ")
                .Append($"this-gen: {_thisGenBestScore:N0} ({_thisGenBestUnit}/{_thisGenBestSpecies}); ")
                .AppendLine($"all-time: {_allTimeBestScore:N0} ({_allTimeBestUnit}/{_allTimeBestSpecies}) ")
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
                var player = new AIPlayerController();
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
            var activePlayer = _aiPlayers[_aiPlayerIndex];

            activePlayer.Score += _gameState.ApplesEaten;
            _gamesPlayed++;

            if (activePlayer.Score > _thisGenBestScore)
            {
                _thisGenBestScore = activePlayer.Score;
                _thisGenBestSpecies = (int)activePlayer.Player.SpeciesId;
                _thisGenBestUnit = (int)activePlayer.Player.Id;
            }

            if (_thisGenBestScore > _allTimeBestScore)
            {
                _allTimeBestScore = _thisGenBestScore;
                _allTimeBestSpecies = _thisGenBestSpecies;
                _allTimeBestUnit = _thisGenBestUnit;
            }

            if (_gamesPlayed < GAMES_PER_GENERATION)
                return;

            _aiPlayerIndex++;
            _gamesPlayed = 0;

            if (_aiPlayerIndex == POPULATION_SIZE)
            {
                _aiPlayerIndex = 0;
                _generation++;
                _thisGenBestScore = 0;
                _thisGenBestSpecies = 0;
                _thisGenBestUnit = 0;

                var sorted = _aiPlayers.OrderByDescending(x => x.Score).ToList();
                _aiPlayers = new List<AIPlayerScore>() { Capacity = POPULATION_SIZE };
                var parents = new List<AIPlayerScore>(sorted.Count);
                long oddsSum = 0;
                for (int i = 0; i < sorted.Count; ++i)
                {
                    // This should prune about half, with increasing likelihood of dying towards the bottom
                    // of the pile
                    double deathChance = (double)i / sorted.Count;
                    if (_rng.NextDouble() < deathChance)
                        continue;

                    _aiPlayers.Add(sorted[i]);
                    parents.Add(sorted[i]);
                    oddsSum += sorted[i].Score;
                }

                // Now select parents to breed from. The top entities have the best chance of breeding.
                while (_aiPlayers.Count < POPULATION_SIZE)
                {
                    int index = 0;
                    double sum = 0.0;
                    double roll = _rng.NextDouble() * oddsSum;
                    while (sum < roll && index <= parents.Count)
                        sum += parents[index++].Score;
                    var child = new AIPlayerScore
                    {
                        Player = parents[--index].Player.Clone(),
                        Score = 0
                    };
                    child.Player.Mutate(MUTATION_RATE);
                    _aiPlayers.Add(child);
                }

                foreach (var aiPlayer in _aiPlayers)
                    aiPlayer.Score = 0;
            }
        }

        private void HandleKeyPress(KeyboardState keyboardState)
        {
            if (keyboardState.IsKeyDown(Keys.Escape) || keyboardState.IsKeyDown(Keys.Q))
            {
                Exit();
                return;
            }

            if (!_gameState.Alive && _player.IsHuman && keyboardState.IsKeyDown(Keys.Space))
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

            int fieldHeight = SnakeRules.FIELD_HEIGHT * _tileLightTexture.Height / 2;
            int fieldWidth = SnakeRules.FIELD_WIDTH * _tileLightTexture.Width / 2;

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
            _gameState.Reset();
            _player?.Shutdown();
            _player = _aiPlayers[_aiPlayerIndex].Player;
            _player.Initialize();
        }

        private void UpdateEntities(TimeSpan _)
        {
            _gameState.Move(_player.GetMovement(_gameState));
        }

        private class AIPlayerScore
        {
            public AIPlayerController Player { get; set; }
            public int Score { get; set; }
        }
    }
}
